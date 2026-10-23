using System;
using HarmonyLib;
using Netcode.Transports.Facepunch;
using Steamworks;
using Unity.Netcode;

namespace BetterNetcode;

/// <summary>
/// Sets Steam's global connection settings right before the host or client starts. Steam copies the global
/// settings into each connection when it's created, so this covers every player who joins.
/// </summary>
[HarmonyPatch]
internal static class SteamTuning
{
    // Steam's defaults (GameNetworkingSockets): 256 KB/s fixed rate, 512 KB buffer, 5000 us Nagle.
    private const int DefaultRate = 256 * 1024;

    [HarmonyPatch(typeof(FacepunchTransport), nameof(FacepunchTransport.StartServer))]
    [HarmonyPrefix]
    private static void StartServer_Prefix() => Apply("host");

    [HarmonyPatch(typeof(FacepunchTransport), nameof(FacepunchTransport.StartClient))]
    [HarmonyPrefix]
    private static void StartClient_Prefix() => Apply("client");

    internal static string Summary { get; private set; } = "";

    internal static void Apply(string role)
    {
        try
        {
            if (Plugin.Enabled.Value)
            {
                int rate = Plugin.SendRateKB.Value * 1024;
                // Min and max must match: with min < max Steam starts from a small ping-based guess and never
                // raises it (bandwidth estimation isn't implemented), so the rate would end up near the minimum.
                SteamNetworkingUtils.SendRateMax = rate;
                SteamNetworkingUtils.SendRateMin = rate;
                SteamNetworkingUtils.SendBufferSize = Plugin.SendBufferKB.Value * 1024;
                if (Plugin.DisableNagle.Value) SteamNetworkingUtils.NagleTime = 0;
            }
            else
            {
                // Put back Steam's defaults in case the mod was turned off mid-session.
                SteamNetworkingUtils.SendRateMax = DefaultRate;
                SteamNetworkingUtils.SendRateMin = DefaultRate;
                SteamNetworkingUtils.SendBufferSize = 512 * 1024;
                SteamNetworkingUtils.NagleTime = 5000;
            }

            // Read back what Steam actually has.
            int min = SteamNetworkingUtils.SendRateMin, max = SteamNetworkingUtils.SendRateMax;
            int buffer = SteamNetworkingUtils.SendBufferSize, nagle = SteamNetworkingUtils.NagleTime;
            Summary = $"Send limit {max / 1024} KB/s  -  buffer {buffer / 1024} KB  -  Nagle {(nagle == 0 ? "off" : $"{nagle / 1000f:0.#} ms")}";
            Plugin.Log.LogInfo($"Steam connection ({role}): {Summary}{(min != max ? $" (min {min / 1024} KB/s)" : "")}" +
                               $"{(Plugin.Enabled.Value ? "" : " [mod off: game defaults]")}");

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.NetworkConfig != null)
                Plugin.Log.LogInfo($"Netcode tick rate: {nm.NetworkConfig.TickRate}/s.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Couldn't apply Steam connection settings ({role}): {ex}");
        }
    }
}
