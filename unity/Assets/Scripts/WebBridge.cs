using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

// Page integrations: rewarded ads (Poki / CrazyGames only), share sheet, haptics, analytics.
public class WebBridge : MonoBehaviour
{
    public static WebBridge I;
    Action<bool> pending;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SD_Rewarded(string goName);
    [DllImport("__Internal")] static extern void SD_Gameplay(int on);
    [DllImport("__Internal")] static extern void SD_Event(string name, int value);
    [DllImport("__Internal")] static extern void SD_Ready();
    [DllImport("__Internal")] static extern int SD_AdsAvailable();
    [DllImport("__Internal")] static extern void GS_ArmShare(string text);
    [DllImport("__Internal")] static extern void GS_Vibrate(int ms);
#endif

    void Awake() { I = this; gameObject.name = "WebBridge"; }

    public static bool AdsAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SD_AdsAvailable() == 1;
#else
            return false;
#endif
        }
    }

    public void ShowRewarded(Action<bool> done)
    {
        pending = done;
        Sfx.I.SetMuted(true);
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Rewarded(gameObject.name);
#else
        OnRewarded("1");
#endif
    }

    public void OnRewarded(string ok)
    {
        Sfx.I.SetMuted(Game.I.Save.muted);
        var cb = pending; pending = null;
        cb?.Invoke(ok == "1");
    }

    public static void Gameplay(bool on)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Gameplay(on ? 1 : 0);
#endif
    }

    public static void Event(string name, int value = 0)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Event(name, value);
#endif
    }

    public static void Ready()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Ready();
#endif
    }

    public static void Vibrate(int ms)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GS_Vibrate(ms);
#endif
    }

    // Web Share needs a live gesture: arm on pointer-down, the page fires it on pointer-up.
    public static void ArmShare(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GS_ArmShare(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
}

public class ShareOnPress : MonoBehaviour, IPointerDownHandler
{
    public Func<string> Text;
    public void OnPointerDown(PointerEventData e) { if (Text != null) WebBridge.ArmShare(Text()); }
}
