using System;
using System.Collections.Generic;
using System.Text;
using Netcode.Transports.Facepunch;
using Steamworks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UObject = UnityEngine.Object;
using UColor = UnityEngine.Color;

namespace BetterNetcode;

/// <summary>
/// Network overlay (F9) and log warnings.
/// Twice a second it reads Steam's live status for each connection: ping, quality, KB/s in and out, and how much
/// data is waiting to be sent (and for how long). The host sees one line per player, a client sees its line to the host.
/// Warnings go to the log even while the overlay is hidden.
/// </summary>
public class NetOverlay : MonoBehaviour
{
    public NetOverlay(IntPtr ptr) : base(ptr) { }

    private GameObject canvasGo, panel;
    private TextMeshProUGUI text;
    private bool visible;
    private float nextSample, nextErrorLog;
    private int frames;
    private float fpsWindowStart, fps;

    private readonly Dictionary<string, float> nextWarn = new();
    private float peakOutKB, peakQueueMs;
    private string lastText = "";

    private void Awake() => visible = Plugin.ShowOnStart.Value;

    private void Update()
    {
        frames++;
        float now = Time.unscaledTime;
        if (now - fpsWindowStart >= 1f)
        {
            fps = frames / (now - fpsWindowStart);
            frames = 0;
            fpsWindowStart = now;
        }

        Step("keys", () => { if (Pressed(Plugin.ToggleKey.Value)) visible = !visible; });

        if (visible && panel == null) Step("build", Build);
        if (canvasGo != null && canvasGo.activeSelf != visible) Step("show/hide", () => canvasGo.SetActive(visible));

        if (now < nextSample) return;
        nextSample = now + 0.5f;
        Step("sample", () =>
        {
            string s = Sample();
            if (visible && text != null && s != lastText)
            {
                lastText = s;
                text.text = s;
                var rt = panel.GetComponent<RectTransform>();
                int lines = s.Split('\n').Length;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 20f + lines * 24f);
            }
        });
    }

    private void Step(string what, Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            if (Time.unscaledTime < nextErrorLog) return;
            nextErrorLog = Time.unscaledTime + 10f;
            Plugin.Log.LogError($"Overlay ({what}): {ex}");
        }
    }

    private static bool Pressed(string keyName)
    {
        var kb = Keyboard.current;
        if (kb == null || string.IsNullOrEmpty(keyName)) return false;
        if (!Enum.TryParse(keyName, true, out Key key) || key == Key.None) return false;
        var control = kb[key];
        return control != null && control.wasPressedThisFrame;
    }

    // ---------- reading the connections ----------

    private string Sample()
    {
        var sb = new StringBuilder();
        var nm = NetworkManager.Singleton;
        bool listening = nm != null && nm.IsListening;
        string role = !listening ? "not connected" : nm.IsHost ? "host" : nm.IsServer ? "server" : "client";

        var transport = listening ? nm.NetworkConfig?.NetworkTransport : null;
        var steam = transport?.TryCast<FacepunchTransport>();
        string kind = transport == null ? "" : steam != null ? "Steam" : transport.GetIl2CppType().Name.Contains("EOS") ? "Epic" : transport.GetIl2CppType().Name;

        sb.Append("<b>NETWORK</b>  <color=#b9b0e8>").Append(kind.Length > 0 ? $"{kind}, {role}" : role)
          .Append("</color>  <size=80%><color=#ffffff80>").Append(Plugin.ToggleKey.Value).Append(" to hide</color></size>\n");
        sb.Append("<size=85%>Your FPS ").Append(Colour(fps, 50f, 30f, true, "0")).Append("</size>\n");

        if (!listening)
        {
            peakOutKB = peakQueueMs = 0f;
            return sb.ToString().TrimEnd('\n');
        }

        if (steam == null)
        {
            sb.Append("<size=85%><color=#ffffff99>Per-player stats are only available on Steam networking.\nThis mod's settings only change Steam connections.</color></size>");
            return sb.ToString();
        }

        if (SteamTuning.Summary.Length > 0) sb.Append("<size=80%><color=#ffffff99>").Append(SteamTuning.Summary).Append("</color></size>\n");

        int rows = 0;
        if (nm.IsServer)
        {
            var clients = steam.connectedClients;
            if (clients != null)
            {
                foreach (var kv in clients)
                {
                    var c = kv.Value;
                    if (c == null) continue;
                    AddRow(sb, NameOf(c.steamId, kv.Key), c.connection);
                    rows++;
                }
            }
            if (rows == 0) sb.Append("<size=85%><color=#ffffff99>No other players connected.</color></size>\n");
        }
        else if (steam.connectionManager != null)
        {
            AddRow(sb, "Host", steam.connectionManager.Connection);
            rows++;
        }

        if (rows > 0)
            sb.Append("<size=80%><color=#ffffff99>Peak out ").Append(peakOutKB.ToString("0"))
              .Append(" KB/s  -  peak wait ").Append(peakQueueMs.ToString("0")).Append(" ms</color></size>");
        return sb.ToString().TrimEnd('\n');
    }

    private void AddRow(StringBuilder sb, string who, Steamworks.Data.Connection connection)
    {
        Steamworks.Data.ConnectionStatus st;
        try { st = connection.QuickStatus(); }
        catch { st = null; }
        if (st == null)
        {
            sb.Append("<size=85%>").Append(who).Append(": no status</size>\n");
            return;
        }

        int ping = st.ping;
        float quality = st.connectionQualityLocal;           // 0..1, share of packets arriving (-1 = unknown)
        float outKB = st.outBytesPerSec / 1024f, inKB = st.inBytesPerSec / 1024f;
        float limitKB = st.sendRateBytesPerSecond / 1024f;
        float waitingKB = (st.cbPendingReliable + st.cbPendingUnreliable) / 1024f;
        float waitMs = st.ecQueueTime / 1000f;                // Steam's estimate of how long new data waits to go out

        peakOutKB = Math.Max(peakOutKB, outKB);
        peakQueueMs = Math.Max(peakQueueMs, waitMs);

        sb.Append("<size=85%><b>").Append(Trim(who, 16)).Append("</b>  ping ").Append(Colour(ping, 120, 200, false, "0")).Append(" ms");
        if (quality >= 0f) sb.Append("  ").Append(Colour(quality * 100f, 98f, 90f, true, "0")).Append('%');
        sb.Append("\n   out ").Append(Colour(outKB, limitKB * 0.6f, limitKB * 0.9f, false, "0")).Append('/').Append(limitKB.ToString("0"))
          .Append(" KB/s  in ").Append(inKB.ToString("0")).Append(" KB/s  waiting ")
          .Append(Colour(waitingKB, 32f, 256f, false, "0")).Append(" KB (").Append(Colour(waitMs, 50f, Math.Max(100f, Plugin.WarnQueueMs.Value), false, "0")).Append(" ms)</size>\n");

        // Log warning (also while the overlay is hidden), at most every 10 s per connection.
        int warnMs = Plugin.WarnQueueMs.Value;
        if (warnMs > 0 && waitMs >= warnMs)
        {
            float now = Time.unscaledTime;
            if (!nextWarn.TryGetValue(who, out float next) || now >= next)
            {
                nextWarn[who] = now + 10f;
                Plugin.Log.LogWarning($"{who}: data waiting {waitMs:0} ms to be sent ({waitingKB:0} KB queued, sending {outKB:0} of {limitKB:0} KB/s, ping {ping} ms, FPS {fps:0}). " +
                                      "If this happens often, raise SendRateKB (host).");
            }
        }
    }

    private static string NameOf(SteamId id, ulong clientId)
    {
        try
        {
            string n = new Friend(id).Name;
            if (!string.IsNullOrWhiteSpace(n) && n != "[unknown]") return n;
        }
        catch { }
        return $"Player {clientId}";
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    // Green / yellow / red. higherIsBetter flips the thresholds (FPS, quality).
    private static string Colour(float v, float warn, float bad, bool higherIsBetter, string fmt)
    {
        string c = higherIsBetter
            ? (v >= warn ? "#7CFC7C" : v >= bad ? "#FFD84A" : "#FF6060")
            : (v < warn ? "#7CFC7C" : v < bad ? "#FFD84A" : "#FF6060");
        return $"<color={c}>{v.ToString(fmt)}</color>";
    }

    // ---------- UI ----------

    private void Build()
    {
        TMP_FontAsset font = null;
        var anyText = UObject.FindObjectOfType<TextMeshProUGUI>();
        if (anyText != null) font = anyText.font;
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font == null) return; // try again once the game's text has loaded

        canvasGo = new GameObject("BetterNetcode_Canvas");
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panel = new GameObject("Panel");
        panel.AddComponent<RectTransform>();
        panel.transform.SetParent(canvasGo.transform, false);
        var bg = panel.AddComponent<UnityEngine.UI.Image>();
        bg.color = new UColor(0.06f, 0.04f, 0.12f, 0.82f);
        bg.raycastTarget = false;
        var pr = panel.GetComponent<RectTransform>();
        pr.sizeDelta = new Vector2(560f, 120f);
        Anchor(pr, Plugin.OverlayPosition.Value);

        var tgo = new GameObject("Text");
        var trt = tgo.AddComponent<RectTransform>();
        tgo.transform.SetParent(panel.transform, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(14f, 8f);
        trt.offsetMax = new Vector2(-14f, -8f);
        text = tgo.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 20f;
        text.richText = true;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.color = UColor.white;
        lastText = "";
        Plugin.Log.LogInfo("Network overlay ready.");
    }

    private static void Anchor(RectTransform rt, string where)
    {
        bool top = !where.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase);
        bool right = where.EndsWith("Right", StringComparison.OrdinalIgnoreCase);
        var a = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        rt.anchorMin = rt.anchorMax = rt.pivot = a;
        rt.anchoredPosition = new Vector2(right ? -16f : 16f, top ? -16f : 16f);
    }
}
