using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace BetterNetcode;

/// <summary>
/// Less lag for players who aren't the host when a lot is going on.
///
/// The game talks over Steam's networking (Facepunch transport) and never changes Steam's defaults:
///   - 256 KB/s send limit per connection (a fixed rate: Steam doesn't estimate bandwidth),
///   - 512 KB send buffer; when it's full, sends fail and the transport ignores the failure (the message is lost),
///   - Nagle on: small messages wait up to 5 ms to be bundled.
/// In a busy fight the host can need more than 256 KB/s per player. Data then queues up (latency climbs, up to
/// ~2 s of backlog) and, once the buffer is full, messages are dropped.
///
/// This mod raises the limit and the buffer and turns Nagle off, right before the host or client starts.
/// It also has an overlay (F9) with each connection's ping, data rate and queued data, to see what's happening.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "girthy_mods.BetterNetcode";
    public const string Name = "BetterNetcode";
    public const string Version = "1.0.0";

    internal static new ManualLogSource Log;

    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<int> SendRateKB;
    internal static ConfigEntry<int> SendBufferKB;
    internal static ConfigEntry<bool> DisableNagle;

    internal static ConfigEntry<string> ToggleKey;
    internal static ConfigEntry<bool> ShowOnStart;
    internal static ConfigEntry<string> OverlayPosition;
    internal static ConfigEntry<int> WarnQueueMs;

    public override void Load()
    {
        Log = base.Log;

        Enabled = Config.Bind("Steam Connection", "Enabled", true,
            "Apply the settings below to Steam connections. Off = the game's normal settings.");
        SendRateKB = Config.Bind("Steam Connection", "SendRateKB", 1024,
            new ConfigDescription("Most data per second sent to each player, in KB/s (game default: 256). Matters most on the host. " +
                                  "It's a limit, not a constant rate: only what the game needs is sent. Lower it if the host's upload is slow.",
                new AcceptableValueRange<int>(256, 8192)));
        SendBufferKB = Config.Bind("Steam Connection", "SendBufferKB", 4096,
            new ConfigDescription("How much data can wait to be sent before Steam starts refusing (and the game loses) messages, in KB (game default: 512).",
                new AcceptableValueRange<int>(512, 65536)));
        DisableNagle = Config.Bind("Steam Connection", "DisableNagle", true,
            "Send right away instead of holding small messages up to 5 ms to bundle them (game default: hold).");

        ToggleKey = Config.Bind("Overlay", "ToggleKey", "F9",
            "Key to show or hide the network overlay (Unity Input System key name).");
        ShowOnStart = Config.Bind("Overlay", "ShowOnStart", false,
            "Show the network overlay when the game starts.");
        OverlayPosition = Config.Bind("Overlay", "ScreenPosition", "TopRight",
            new ConfigDescription("Where the overlay sits on screen.",
                new AcceptableValueList<string>("TopRight", "TopLeft", "BottomRight", "BottomLeft")));
        WarnQueueMs = Config.Bind("Overlay", "WarnQueueMs", 250,
            new ConfigDescription("Write a warning to the log when data waits this long to be sent to a player (0 = never).",
                new AcceptableValueRange<int>(0, 5000)));

        new Harmony(Guid).PatchAll(typeof(SteamTuning));
        AddComponent<NetOverlay>();
        Log.LogInfo($"{Name} {Version} ready.");
    }
}
