using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public static UI I;
    Canvas canvas; CanvasScaler scaler;
    RectTransform root, hud, screens, numbers;
    Font F => Kit.Font;
    static readonly Color Ink = Kit.Hex("#14111f"), Bone = Kit.Hex("#EDE6D6"), Toxic = Kit.Hex("#7CFFB2"), Blood = Kit.Hex("#FF4D6D"), Gold = Kit.Hex("#FFD166");

    // joystick
    public Vector2 Joy { get; private set; }
    RectTransform joyBase, joyKnob; int joyId = -99; Vector2 joyOrigin; bool joyMouse;
    const float JoyRadius = 110f;

    // hud
    Text timerText, killText, goldText, bossName, bannerText, candyText;
    Image vignette;
    float bannerT, vignetteT;

    // number pool
    class Num { public Text t; public Vector3 world; public float life; public bool active; }
    readonly List<Num> nums = new List<Num>();

    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string n) { if (!icons.TryGetValue(n, out var s)) icons[n] = s = Resources.Load<Sprite>("Icons/" + n); return s; }

    public void Init()
    {
        I = this;
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;

        numbers = Fill("numbers", root);
        vignette = Fill("vignette", root).gameObject.AddComponent<Image>();
        vignette.sprite = Sprite.Create(Vignette(), new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
        vignette.color = new Color(1, 0, 0, 0); vignette.raycastTarget = false;

        BuildHud();
        screens = Fill("screens", root);

        joyBase = (RectTransform)Img(root, Sprite.Create(Kit.Ring, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f)), Vector2.zero, Vector2.zero, new Vector2(JoyRadius * 2.2f, JoyRadius * 2.2f)).transform;
        joyBase.GetComponent<Image>().color = new Color(1, 1, 1, 0.45f);
        joyKnob = (RectTransform)Img(joyBase, Sprite.Create(Kit.Disc, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f)), new Vector2(.5f, .5f), Vector2.zero, new Vector2(110, 110)).transform;
        joyKnob.GetComponent<Image>().color = new Color(1, 1, 1, 0.8f);
        joyBase.gameObject.SetActive(false);

        bannerText = Txt(root, "", 76, new Vector2(.5f, .62f), Vector2.zero, Toxic, TextAnchor.MiddleCenter, 1400);
        Outline(bannerText, 4);
        bannerText.gameObject.SetActive(false);

        for (int i = 0; i < 48; i++)
        {
            var t = Txt(numbers, "", 36, Vector2.zero, Vector2.zero, Color.white, TextAnchor.MiddleCenter, 200);
            Outline(t, 2);
            t.gameObject.SetActive(false);
            nums.Add(new Num { t = t });
        }
    }

    static Texture2D Vignette()
    {
        int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n * 0.7f);
            px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01((d - 0.45f) * 2.2f));
        }
        t.SetPixels(px); t.Apply(); return t;
    }

    // ---------------------------------------------------------------- building blocks
    RectTransform Rect(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }
    RectTransform Fill(string n, Transform p)
    {
        var rt = Rect(n, p, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }
    RectTransform Box(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool ray = false)
    {
        var rt = Rect("box", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = Kit.RoundedSprite; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = ray;
        return rt;
    }
    Image Img(Transform p, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect("img", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = s; img.preserveAspect = true; img.raycastTarget = false; return img;
    }
    Text Txt(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Color c, TextAnchor align = TextAnchor.MiddleCenter, float w = 700)
    {
        size = Mathf.Max(size, 30);   // readable floor: Lilita below this turns to mush on phones and short desktop windows
        var rt = Rect("txt", p, anchor, pos, new Vector2(w, size * 1.4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = F; t.fontSize = size; t.fontStyle = FontStyle.Normal; t.alignment = align; t.color = c; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    static void Outline(Text t, float d) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.75f); o.effectDistance = new Vector2(d, -d); }
    Button Btn(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action onClick, int fs = 48)
    {
        var rt = Box(p, anchor, pos, size, bg, true);
        // raised lip so buttons read as tappable over the dark graveyard
        var lip = Box(rt, new Vector2(.5f, 0), new Vector2(0, -8), new Vector2(size.x, 20), Color.Lerp(bg.a < 0.5f ? new Color(0, 0, 0, 0.35f) : bg, Color.black, 0.45f));
        lip.pivot = new Vector2(.5f, 0); lip.SetAsFirstSibling();
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>();
        b.onClick.AddListener(() => { Sfx.I.Click(); onClick(); });
        rt.gameObject.AddComponent<Press>();
        Txt(rt, label, fs, new Vector2(.5f, .5f), Vector2.zero, fg, TextAnchor.MiddleCenter, size.x);
        return b;
    }

    // ---------------------------------------------------------------- HUD
    // HUD: XP bar + level badge across the top, vitals (heart, HP, lantern oil) top-left, counters top-right,
    // timer + time-to-dawn centre, boss bar, and a mini HP bar over the hero that only shows when it matters.
    HudBar xpBar, hpBar, oilBar, bossBar, miniHp;
    RectTransform xpRow, lvBadge, heartRt, killPill, goldPill;
    Image lvRing, lvFlash, heartGlow;
    Text lvNum, xpText, dawnText;
    CanvasGroup miniGroup;
    float miniShow, lvPop, killPunch, goldPunch, heartPhase, heartHit, lastHp = -1;
    int lastLevel = -1, lastKills = -1, lastGold = -1; float shownKills, shownGold;
    bool bossWas;
    static readonly Color Panel = new Color(0.04f, 0.03f, 0.07f, 0.88f), Oil = Kit.Hex("#FFB25A"), Dusk = Kit.Hex("#B98CFF");

    Image Sliced(Transform p, Sprite s, Color c, float inset = 0)
    {
        var rt = Fill("s", p); rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        var i = rt.gameObject.AddComponent<Image>(); i.sprite = s; i.type = Image.Type.Sliced; i.color = c; i.raycastTarget = false;
        return i;
    }

    RectTransform Pill(Vector2 anchor, Vector2 pos, Sprite icon, Color c, out Text t)
    {
        var rt = Rect("pill", hud, anchor, pos, new Vector2(236, 64));
        var sh = Sliced(rt, HudArt.Round, new Color(0, 0, 0, 0.45f), -5); sh.rectTransform.anchoredPosition = new Vector2(0, -4);
        Sliced(rt, HudArt.Round, Panel);
        Sliced(rt, HudArt.Rim, new Color(1, 1, 1, 0.14f));
        var ig = Img(rt, HudArt.Glow, new Vector2(0, .5f), new Vector2(38, 0), new Vector2(90, 90)); ig.color = Kit.A(c, 0.25f);
        Img(rt, icon, new Vector2(0, .5f), new Vector2(38, 0), new Vector2(56, 56));
        t = Txt(rt, "0", 42, new Vector2(1, .5f), new Vector2(-20, 0), c, TextAnchor.MiddleRight, 180);
        t.rectTransform.pivot = new Vector2(1, .5f); Outline(t, 2);
        return rt;
    }

    void BuildHud()
    {
        hud = Fill("hud", root);

        // ---- XP: level badge + segmented bar
        xpRow = Rect("xprow", hud, new Vector2(.5f, 1), new Vector2(0, -66), new Vector2(1000, 44));
        xpBar = new HudBar(xpRow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 34), Toxic, new Color(0.92f, 1f, 0.95f, 0.95f), 10, 0, F);
        xpBar.Root.anchorMin = new Vector2(0, .5f); xpBar.Root.anchorMax = new Vector2(1, .5f);
        xpBar.Root.sizeDelta = new Vector2(-84, 34); xpBar.Root.anchoredPosition = new Vector2(42, 0);
        xpBar.RimCol = Kit.A(Toxic, 0.3f);
        xpText = Txt(xpBar.Root, "", 30, new Vector2(1, .5f), new Vector2(-16, 0), new Color(1, 1, 1, 0.92f), TextAnchor.MiddleRight, 300);
        xpText.rectTransform.pivot = new Vector2(1, .5f); Outline(xpText, 2);

        lvBadge = Rect("lv", xpRow, new Vector2(0, .5f), new Vector2(18, -4), new Vector2(128, 128));
        var bsh = Img(lvBadge, HudArt.Disc, new Vector2(.5f, .5f), new Vector2(0, -6), new Vector2(136, 136)); bsh.color = new Color(0, 0, 0, 0.5f);
        lvFlash = Img(lvBadge, HudArt.Glow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(240, 240)); lvFlash.color = Kit.A(Toxic, 0f);
        Img(lvBadge, HudArt.Disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(128, 128)).color = Kit.Hex("#0b0912");
        Img(lvBadge, HudArt.RingS, new Vector2(.5f, .5f), Vector2.zero, new Vector2(124, 124)).color = new Color(1, 1, 1, 0.1f);
        lvRing = Img(lvBadge, HudArt.RingS, new Vector2(.5f, .5f), Vector2.zero, new Vector2(124, 124));
        lvRing.type = Image.Type.Filled; lvRing.fillMethod = Image.FillMethod.Radial360; lvRing.fillOrigin = 2; lvRing.fillClockwise = true; lvRing.color = Toxic;
        Img(lvBadge, HudArt.Disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(98, 98)).color = Kit.Hex("#1a1430");
        var lvl = Txt(lvBadge, "LEVEL", 30, new Vector2(.5f, .5f), new Vector2(0, 28), Kit.A(Toxic, 0.85f), TextAnchor.MiddleCenter, 120);
        lvl.rectTransform.localScale = Vector3.one * 0.62f;
        lvNum = Txt(lvBadge, "1", 58, new Vector2(.5f, .5f), new Vector2(0, -8), Color.white, TextAnchor.MiddleCenter, 120); Outline(lvNum, 3);

        // ---- timer + time to dawn
        timerText = Txt(hud, "00:00", 66, new Vector2(.5f, 1), new Vector2(0, -150), Bone);
        Outline(timerText, 3);
        dawnText = Txt(hud, "", 30, new Vector2(.5f, 1), new Vector2(0, -198), Kit.A(Bone, 0.6f));
        dawnText.rectTransform.localScale = Vector3.one * 0.8f;
        Outline(dawnText, 2);

        // ---- vitals: beating heart, HP with numbers, lantern oil
        heartRt = Rect("heart", hud, new Vector2(0, 1), new Vector2(86, -262), new Vector2(96, 96));
        heartGlow = Img(heartRt, HudArt.Glow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(190, 190)); heartGlow.color = Kit.A(Blood, 0.3f);
        Img(heartRt, HudArt.Heart, new Vector2(.5f, .5f), new Vector2(0, -4), new Vector2(96, 96)).color = new Color(0, 0, 0, 0.5f);
        Img(heartRt, HudArt.Heart, new Vector2(.5f, .5f), Vector2.zero, new Vector2(96, 96)).color = Kit.Hex("#FF3B5C");
        hpBar = new HudBar(hud, new Vector2(0, 1), new Vector2(370, -262), new Vector2(440, 46), Blood, Kit.Hex("#FFE3C2"), 4, 32, F);
        hpBar.RimCol = new Color(1f, 0.6f, 0.6f, 0.22f);
        heartRt.SetAsLastSibling();
        var lamp = Img(hud, Icon("lantern-candle"), new Vector2(0, 1), new Vector2(160, -318), new Vector2(70, 70));
        var lampGlow = Img(lamp.rectTransform, HudArt.Glow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(90, 90)); lampGlow.color = Kit.A(Oil, 0.3f); lampGlow.transform.SetAsFirstSibling();
        oilBar = new HudBar(hud, new Vector2(0, 1), new Vector2(370, -316), new Vector2(340, 24), Oil, Kit.Hex("#FFF0CF"), 0, 0, F);
        oilBar.Root.anchoredPosition = new Vector2(200 + 170, -316);
        oilBar.RimCol = Kit.A(Oil, 0.25f); oilBar.LowCol = Dusk; oilBar.ShineEvery = 4.5f;

        // ---- counters
        killPill = Pill(new Vector2(1, 1), new Vector2(-158, -262), Icon("gravestone-round"), Bone, out killText);
        goldPill = Pill(new Vector2(1, 1), new Vector2(-158, -336), Icon("coin"), Gold, out goldText);
        candyText = Txt(hud, "", 34, new Vector2(1, 1), new Vector2(-158, -398), Kit.Hex("#FF9A3C"), TextAnchor.MiddleCenter, 300);
        Outline(candyText, 2);
        Btn(hud, "II", new Vector2(1, 1), new Vector2(-70, -150), new Vector2(90, 90), new Color(0, 0, 0, 0.5f), Color.white, () => Game.I.Pause(), 40);

        // ---- boss
        bossBar = new HudBar(hud, new Vector2(.5f, 1), new Vector2(0, -470), new Vector2(760, 38), Blood, Kit.Hex("#FFE3C2"), 10, 0, F);
        bossBar.RimCol = Kit.A(Gold, 0.4f);
        bossName = Txt(bossBar.Root, "", 38, new Vector2(.5f, 1), new Vector2(0, 30), Kit.Hex("#FF7A8C"));
        Outline(bossName, 3);
        bossBar.Root.gameObject.SetActive(false);

        // ---- mini HP over the hero
        miniHp = new HudBar(hud, Vector2.zero, Vector2.zero, new Vector2(132, 18), Blood, Kit.Hex("#FFE3C2"), 0, 0, F);
        miniHp.ShineEvery = 999f;
        miniGroup = miniHp.Root.gameObject.AddComponent<CanvasGroup>(); miniGroup.alpha = 0;
    }

    public void ShowHud(bool on) => hud.gameObject.SetActive(on);

    public void Banner(string s, Color c)
    {
        bannerText.text = s; bannerText.color = c; bannerT = 2.2f;
        bannerText.gameObject.SetActive(true);
    }

    public void HurtVignette() => vignetteT = 0.45f;

    public void Number(Vector3 world, float dmg, bool crit)
    {
        Num n = null;
        foreach (var x in nums) if (!x.active) { n = x; break; }
        if (n == null) return;   // saturated: skip rather than steal
        n.active = true; n.life = 0.6f; n.world = world + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0, 0);
        n.t.text = Mathf.CeilToInt(dmg).ToString();
        n.t.fontSize = crit ? 50 : 34;
        n.t.color = crit ? Gold : Color.white;
        n.t.gameObject.SetActive(true);
    }

    public void Float(Vector3 world, string s, Color c) => StartCoroutine(FloatCo(world, s, c));

    IEnumerator FloatCo(Vector3 world, string s, Color c)
    {
        var t = Txt(root, s, 46, Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 600); Outline(t, 3);
        float k = 0;
        while (k < 1f)
        {
            k += Time.unscaledDeltaTime / 1.1f;
            t.rectTransform.position = Game.I.Cam.WorldToScreenPoint(world) + Vector3.up * k * 120f * canvas.scaleFactor;
            t.color = Kit.A(c, Mathf.Clamp01((1 - k) * 3));
            yield return null;
        }
        Destroy(t.gameObject);
    }

    // ---------------------------------------------------------------- screens
    RectTransform Screen(bool dim = true)
    {
        foreach (Transform c in screens) Destroy(c.gameObject);
        var s = Fill("screen", screens);
        if (dim) { var img = s.gameObject.AddComponent<Image>(); img.color = new Color(0.03f, 0.02f, 0.06f, 0.78f); }
        return s;
    }

    public void CloseScreens() { foreach (Transform c in screens) Destroy(c.gameObject); }

    IEnumerator Pop(RectTransform r, float delay = 0)
    {
        r.localScale = Vector3.zero;
        float k = -delay / 0.28f;
        while (k < 1f) { k += Time.unscaledDeltaTime / 0.28f; r.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k)); yield return null; }
        r.localScale = Vector3.one;
    }

    Text Title(Transform p, string s, float y, int size, Color c)
    {
        var shadow = Txt(p, s, size, new Vector2(.5f, 1), new Vector2(6, y - 6), new Color(0, 0, 0, 0.6f), TextAnchor.MiddleCenter, 1400);
        var t = Txt(p, s, size, new Vector2(.5f, 1), new Vector2(0, y), c, TextAnchor.MiddleCenter, 1400);
        return t;
    }

    public void ShowMenu()
    {
        var s = Screen(false);
        var g = Game.I;
        // title block up top, buttons in a stack at the bottom, the middle left clear for the keeper
        Kit.Scrim(s, true, 900, new Color(0.03f, 0.04f, 0.05f, 0.82f));
        Kit.Scrim(s, false, 1000, new Color(0.03f, 0.04f, 0.05f, 0.88f));
        var glow = Img(s, Fx.Glow, new Vector2(.5f, 1), new Vector2(0, -330), new Vector2(1100, 600));
        glow.color = Kit.A(Toxic, 0.16f);
        Title(s, "GRAVE", -210, 190, Bone);
        Title(s, "SHIFT", -390, 190, Toxic);
        var tag = Txt(s, "THE DARK IS HUNGRY. KEEP YOUR LANTERN LIT.", 40, new Vector2(.5f, 1), new Vector2(0, -525), Bone, TextAnchor.MiddleCenter, 1000);
        Outline(tag, 2);
        if (g.Save.bestTime > 0)
        {
            var best = Txt(s, "BEST  " + Clock(g.Save.bestTime) + "   ·   " + g.Save.bestKills + " KILLS" + (g.Save.wins > 0 ? "   ·   " + g.Save.wins + " DAWNS" : ""), 36, new Vector2(.5f, 1), new Vector2(0, -590), Gold, TextAnchor.MiddleCenter, 1000);
            Outline(best, 2);
        }

        float y = 860;
        if (Spooky.On)
        {
            // the event is one tappable button: what it is, and what you have to spend
            var ev = Btn(s, "", new Vector2(.5f, 0), new Vector2(0, y), new Vector2(720, 130), Kit.Hex("#FF7A1A"), Ink, ShowCandyShop, 40);
            Txt(ev.transform, "SPOOKTOBER EVENT", 30, new Vector2(.5f, .5f), new Vector2(0, 26), Kit.A(Ink, 0.75f), TextAnchor.MiddleCenter, 700);
            Txt(ev.transform, "CANDY SHOP  ·  " + g.Save.candy + " CANDY", 46, new Vector2(.5f, .5f), new Vector2(0, -16), Ink, TextAnchor.MiddleCenter, 700);
            StartCoroutine(Pulse(ev.transform));
            y -= 190;
        }
        var play = Btn(s, "PLAY", new Vector2(.5f, 0), new Vector2(0, y - 10), new Vector2(720, 190), Toxic, Ink, () => g.StartRun(), 96);
        if (!Spooky.On) StartCoroutine(Pulse(play.transform));
        y -= 205;
        var shop = Btn(s, "", new Vector2(.5f, 0), new Vector2(-185, y), new Vector2(350, 130), new Color(1, 1, 1, 0.16f), Gold, ShowShop, 46);
        Txt(shop.transform, "SHOP", 46, new Vector2(.5f, .5f), new Vector2(0, 16), Gold, TextAnchor.MiddleCenter, 340);
        Txt(shop.transform, g.Save.gold + " GOLD", 28, new Vector2(.5f, .5f), new Vector2(0, -30), Kit.A(Bone, 0.8f), TextAnchor.MiddleCenter, 340);
        Btn(s, g.Save.muted ? "SOUND OFF" : "SOUND ON", new Vector2(.5f, 0), new Vector2(185, y), new Vector2(350, 130), new Color(1, 1, 1, 0.16f), Bone, () => { g.ToggleMute(); ShowMenu(); }, 40);
        var hint = Txt(s, "Move with one thumb. Aiming is automatic.", 32, new Vector2(.5f, 0), new Vector2(0, y - 125), Kit.A(Bone, 0.75f), TextAnchor.MiddleCenter, 1000);
        Outline(hint, 2);
    }

    IEnumerator Pulse(Transform t)
    {
        while (t) { t.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.035f); yield return null; }
    }

    public void ShowCandyShop()
    {
        var s = Screen();
        var g = Game.I;
        Title(s, "CANDY SHOP", -200, 96, Kit.Hex("#FF9A3C"));
        Txt(s, g.Save.candy + " CANDY", 46, new Vector2(.5f, 1), new Vector2(0, -310), Kit.Hex("#FF9A3C"));
        Txt(s, "Undead drop candy all October. The Pumpkin King drops a sackful.", 30, new Vector2(.5f, 1), new Vector2(0, -380), new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, 1000);
        for (int i = 0; i < Spooky.Hats.Length; i++)
        {
            var h = Spooky.Hats[i];
            bool owned = g.Save.hatsOwned.Contains(h.id + ";"), worn = g.Save.hat == h.id;
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -540 - i * 210), new Vector2(900, 180), new Color(1, 1, 1, 0.08f));
            Txt(row, h.name, 48, new Vector2(0, .5f), new Vector2(260, 24), Bone, TextAnchor.MiddleLeft, 420);
            Txt(row, owned ? (worn ? "WEARING" : "OWNED") : h.cost + " CANDY", 30, new Vector2(0, .5f), new Vector2(260, -30), owned ? Toxic : Kit.Hex("#FF9A3C"), TextAnchor.MiddleLeft, 420);
            bool afford = g.Save.candy >= h.cost;
            string label = worn ? "TAKE OFF" : owned ? "WEAR" : "BUY";
            Color bg = worn ? new Color(1, 1, 1, 0.15f) : owned || afford ? Kit.Hex("#FF7A1A") : new Color(1, 1, 1, 0.1f);
            var hid = h.id; int cost = h.cost;
            Btn(row, label, new Vector2(1, .5f), new Vector2(-160, 0), new Vector2(260, 110), bg, worn || (!owned && !afford) ? Bone : Ink, () =>
            {
                if (g.Save.hat == hid) g.Save.hat = "";
                else if (g.Save.hatsOwned.Contains(hid + ";")) g.Save.hat = hid;
                else if (g.Save.candy >= cost) { g.Save.candy -= cost; g.Save.hatsOwned += hid + ";"; g.Save.hat = hid; Sfx.I.LevelUp(); }
                else return;
                g.Persist(); g.Hero.ApplyHat(g.Save.hat); ShowCandyShop();
            }, 40);
        }
        Btn(s, "BACK", new Vector2(.5f, 0), new Vector2(0, 200), new Vector2(420, 130), new Color(1, 1, 1, 0.15f), Bone, ShowMenu, 50);
    }

    public void ShowShop()
    {
        var s = Screen();
        var g = Game.I;
        Title(s, "CRYPT SHOP", -200, 90, Gold);
        var goldT = Txt(s, g.Save.gold + " GOLD", 44, new Vector2(.5f, 1), new Vector2(0, -300), Gold);
        for (int i = 0; i < Defs.ShopNames.Length; i++)
        {
            int idx = i;
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -440 - i * 190), new Vector2(900, 170), new Color(1, 1, 1, 0.08f));
            Txt(row, Defs.ShopNames[i], 44, new Vector2(0, .5f), new Vector2(40 + 170, 26), Bone, TextAnchor.MiddleLeft, 340);
            Txt(row, Defs.ShopDesc[i], 30, new Vector2(0, .5f), new Vector2(40 + 170, -26), new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, 340);
            int lv = g.Save.shop[i];
            var pips = new System.Text.StringBuilder();
            for (int k = 0; k < Defs.ShopMax; k++) pips.Append(k < lv ? "■" : "□");
            Txt(row, lv + "/" + Defs.ShopMax, 32, new Vector2(.5f, .5f), new Vector2(60, 0), Gold, TextAnchor.MiddleCenter, 160);
            bool max = lv >= Defs.ShopMax;
            int cost = Defs.ShopCost(lv);
            bool afford = !max && g.Save.gold >= cost;
            Btn(row, max ? "MAX" : cost + "", new Vector2(1, .5f), new Vector2(-150, 0), new Vector2(240, 110), max ? new Color(1, 1, 1, 0.1f) : afford ? Gold : new Color(1, 1, 1, 0.12f), afford ? Ink : new Color(1, 1, 1, 0.5f), () =>
            {
                if (max || g.Save.gold < cost) return;
                g.Save.gold -= cost; g.Save.shop[idx]++; g.Persist();
                Sfx.I.LevelUp(); ShowShop();
            }, 44);
        }
        Btn(s, "BACK", new Vector2(.5f, 0), new Vector2(0, 200), new Vector2(420, 130), new Color(1, 1, 1, 0.15f), Bone, ShowMenu, 50);
    }

    public void ShowLevelUp(List<Up> options, int level, Action<Up> pick)
    {
        var s = Screen();
        var t = Title(s, "LEVEL UP!", -300, 110, Toxic);
        Txt(s, "Choose one", 36, new Vector2(.5f, 1), new Vector2(0, -410), new Color(1, 1, 1, 0.7f));
        var ars = Arsenal.I;
        for (int i = 0; i < options.Count; i++)
        {
            var u = options[i]; var d = Defs.U[u];
            int cur = ars.Level(u);
            var card = Box(s, new Vector2(.5f, .5f), new Vector2(0, 250 - i * 300), new Vector2(900, 270), Kit.Hex("#221c33"), true);
            var edge = Box(card, new Vector2(0, .5f), new Vector2(10, 0), new Vector2(20, 270), d.color);
            var iconBg = Box(card, new Vector2(0, .5f), new Vector2(150, 0), new Vector2(190, 190), Kit.A(d.color, 0.18f));
            Img(iconBg, Icon(d.icon), new Vector2(.5f, .5f), Vector2.zero, new Vector2(160, 160));
            Txt(card, d.name.ToUpper(), 48, new Vector2(0, .5f), new Vector2(280 + 280, 62), Bone, TextAnchor.MiddleLeft, 560);
            Txt(card, cur == 0 ? (d.weapon ? "NEW WEAPON" : "NEW") : "LV " + cur + "  >  " + (cur + 1), 32, new Vector2(0, .5f), new Vector2(280 + 280, 12), d.color, TextAnchor.MiddleLeft, 560);
            var desc = Txt(card, d.desc, 32, new Vector2(0, .5f), new Vector2(280 + 280, -52), new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft, 560);
            var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card.GetComponent<Image>();
            var picked = u;
            b.onClick.AddListener(() => { Sfx.I.Click(); pick(picked); });
            StartCoroutine(Pop(card, 0.08f * i));
        }
    }

    public void ShowPause()
    {
        var s = Screen();
        Title(s, "PAUSED", -500, 120, Bone);
        Btn(s, "RESUME", new Vector2(.5f, .5f), new Vector2(0, 60), new Vector2(560, 160), Toxic, Ink, () => Game.I.Resume(), 64);
        Btn(s, "QUIT RUN", new Vector2(.5f, .5f), new Vector2(0, -150), new Vector2(560, 130), new Color(1, 1, 1, 0.14f), Bone, () => Game.I.EndRun(false), 46);
    }

    public void ShowResults(bool won, float time, int kills, int level, int gold, bool canRevive, string reviveLabel)
    {
        var s = Screen();
        var g = Game.I;
        Title(s, won ? "DAWN BREAKS!" : "YOU FELL", -260, 120, won ? Gold : Blood);
        Txt(s, won ? "You survived the night." : "The dead claim another keeper...", 36, new Vector2(.5f, 1), new Vector2(0, -370), new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, 1000);
        string[] labels = { "SURVIVED", "KILLS", "LEVEL", "GOLD" };
        string[] vals = { Clock(time), kills.ToString(), level.ToString(), "+" + gold };
        for (int i = 0; i < 4; i++)
        {
            var box = Box(s, new Vector2(.5f, 1), new Vector2(i % 2 == 0 ? -225 : 225, -540 - (i / 2) * 210), new Vector2(420, 180), new Color(1, 1, 1, 0.08f));
            Txt(box, labels[i], 30, new Vector2(.5f, .5f), new Vector2(0, 45), new Color(1, 1, 1, 0.6f));
            var v = Txt(box, vals[i], 64, new Vector2(.5f, .5f), new Vector2(0, -20), i == 3 ? Gold : Bone);
            StartCoroutine(Pop(box, 0.1f * i));
        }
        if (Spooky.On && Game.I.lastCandy > 0)
            Txt(s, "+" + Game.I.lastCandy + " CANDY", 44, new Vector2(.5f, 1), new Vector2(0, -985), Kit.Hex("#FF9A3C"));
        float y = 640;
        if (canRevive)
        {
            var rv = Btn(s, reviveLabel, new Vector2(.5f, 0), new Vector2(0, y + 190), new Vector2(620, 140), Gold, Ink, () => g.Revive(), 50);
            StartCoroutine(Pulse(rv.transform));
        }
        Btn(s, "PLAY AGAIN", new Vector2(.5f, 0), new Vector2(0, y), new Vector2(620, 160), Toxic, Ink, () => g.StartRun(), 64);
        var share = Btn(s, "SHARE", new Vector2(.5f, 0), new Vector2(-165, y - 180), new Vector2(290, 120), new Color(1, 1, 1, 0.16f), Bone, () => { }, 46);
        share.gameObject.AddComponent<ShareOnPress>().Text = () => g.ShareText();
        Btn(s, "MENU", new Vector2(.5f, 0), new Vector2(165, y - 180), new Vector2(290, 120), new Color(1, 1, 1, 0.16f), Bone, () => g.GoMenu(), 46);
    }

    public static string Clock(float secs) { int s = Mathf.FloorToInt(secs); return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }

    // ---------------------------------------------------------------- per frame
    void Update()
    {
        float aspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
        scaler.matchWidthOrHeight = aspect > 0.75f ? 1f : 0f;
        UpdateJoystick();
        var g = Game.I;
        float udt = Time.unscaledDeltaTime;

        if (hud.gameObject.activeSelf && g.Hero)
        {
            // XP row spans the top, capped on wide screens
            // landscape: the canvas matches height, so scale the whole HUD up to a comfortable size
            float hs = root.rect.width > root.rect.height ? 1.45f : 1f;
            hud.anchorMin = hud.anchorMax = new Vector2(.5f, .5f);
            hud.sizeDelta = root.rect.size / hs; hud.localScale = Vector3.one * hs;
            xpRow.sizeDelta = new Vector2(Mathf.Min(hud.rect.width - 150, 1400), 44);
            float need = Defs.XpForLevel(g.Level), xp01 = Mathf.Clamp01(g.Xp / need);
            if (g.Level != lastLevel)
            {
                if (lastLevel > 0 && g.Level > lastLevel) { lvPop = 1f; xpBar.Shine(); xpBar.Flash(); }
                xpBar.Reset(xp01); lastLevel = g.Level;
            }
            xpBar.Set(xp01, udt);
            xpText.text = g.Xp + " / " + Mathf.RoundToInt(need);
            lvNum.text = g.Level.ToString();
            lvRing.fillAmount = Mathf.Lerp(lvRing.fillAmount, xp01, 1f - Mathf.Exp(-udt * 8f));
            lvPop = Mathf.Max(0, lvPop - udt * 1.6f);
            lvBadge.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Clamp01(1f - lvPop) * Mathf.PI) * 0.22f * (lvPop > 0 ? 1 : 0));
            lvFlash.color = Kit.A(Toxic, lvPop * 0.85f);
            lvFlash.rectTransform.localScale = Vector3.one * (1.5f - lvPop * 0.5f);

            // timer
            float left = Mathf.Max(0, Defs.RunLength - g.RunTime);
            timerText.text = Clock(g.RunTime);
            timerText.color = left < 30 ? Color.Lerp(Bone, Gold, Mathf.PingPong(Time.unscaledTime * 3, 1)) : Bone;
            dawnText.text = left > 0 ? "DAWN IN " + Clock(left) : "DAWN!";

            // vitals
            var hp = g.Hero; float hp01 = Mathf.Clamp01(hp.Hp / Mathf.Max(1, hp.MaxHp));
            if (lastHp >= 0 && hp.Hp > lastHp + 0.5f) { hpBar.Flash(0.9f); miniHp.Flash(0.9f); }
            if (lastHp >= 0 && hp.Hp < lastHp - 0.01f) { miniShow = 2.5f; heartHit = 1f; }
            lastHp = hp.Hp;
            hpBar.Low = miniHp.Low = hp01 < 0.3f;
            hpBar.Set(hp01, udt);
            hpBar.Label.text = Mathf.CeilToInt(hp.Hp) + " / " + Mathf.RoundToInt(hp.MaxHp);
            heartPhase += udt * (hp01 < 0.3f ? 2.4f : 1.1f);
            float beat = Mathf.Pow(Mathf.Max(0, Mathf.Sin(heartPhase * Mathf.PI * 2f)), 10f);
            heartHit = Mathf.Max(0, heartHit - udt * 4f);
            heartRt.localScale = Vector3.one * (1f + beat * (hp01 < 0.3f ? 0.16f : 0.08f) + heartHit * 0.25f);
            heartRt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 60f) * 10f * heartHit);
            heartGlow.color = Kit.A(Blood, 0.22f + beat * 0.25f + (hp01 < 0.3f ? 0.2f : 0f));
            float fuel = Lantern.I.Fuel01;
            oilBar.Low = fuel < 0.22f;
            oilBar.Col = Color.Lerp(Dusk, Oil, Mathf.Clamp01(fuel * 3f));
            oilBar.Set(fuel, udt);

            // counters roll up and punch
            int kills = Horde.I.Kills, gold = g.RunGold;
            if (lastKills < 0 || kills < lastKills) shownKills = kills;
            if (lastGold < 0 || gold < lastGold) shownGold = gold;
            if (kills > lastKills && lastKills >= 0) killPunch = Mathf.Min(1f, killPunch + 0.35f);
            if (gold > lastGold && lastGold >= 0) goldPunch = 1f;
            lastKills = kills; lastGold = gold;
            shownKills = Mathf.MoveTowards(shownKills, kills, Mathf.Max(1f, (kills - shownKills) * 10f) * udt);
            shownGold = Mathf.MoveTowards(shownGold, gold, Mathf.Max(1f, (gold - shownGold) * 10f) * udt);
            killText.text = Mathf.RoundToInt(shownKills).ToString();
            goldText.text = Mathf.RoundToInt(shownGold).ToString();
            killPunch = Mathf.Max(0, killPunch - udt * 4f); goldPunch = Mathf.Max(0, goldPunch - udt * 4f);
            killPill.localScale = Vector3.one * (1f + killPunch * 0.07f);
            goldPill.localScale = Vector3.one * (1f + goldPunch * 0.12f);
            candyText.text = Spooky.On ? g.RunCandy + " CANDY" : "";

            // mini HP under the hero: only after a hit or while hurting
            var sp = g.Cam.WorldToScreenPoint(hp.transform.position + Vector3.down * 0.3f);
            miniHp.Root.position = sp;
            miniShow = Mathf.Max(0, miniShow - udt);
            miniGroup.alpha = Mathf.MoveTowards(miniGroup.alpha, miniShow > 0 || hp01 < 0.5f ? 1f : 0f, udt * 4f);
            miniHp.Set(hp01, udt);

            // boss
            var boss = Horde.I.Boss;
            bossBar.Root.gameObject.SetActive(boss != null);
            if (boss != null)
            {
                if (!bossWas) { bossBar.Reset(1f); bossBar.Shine(); }
                bossName.text = boss.def.name;
                bossBar.Set(boss.hp / boss.maxHp, udt);
            }
            bossWas = boss != null;
        }

        if (bannerT > 0)
        {
            bannerT -= udt;
            float k = bannerT > 1.9f ? (2.2f - bannerT) / 0.3f : 1f;
            bannerText.rectTransform.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k));
            bannerText.color = Kit.A(bannerText.color, Mathf.Clamp01(bannerT * 2f));
            if (bannerT <= 0) bannerText.gameObject.SetActive(false);
        }

        vignetteT = Mathf.Max(0, vignetteT - udt);
        float low = g.Hero && g.State == Game.S.Playing ? Mathf.Clamp01(1f - g.Hero.Hp / g.Hero.MaxHp / 0.3f) * (0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f)) : 0;
        vignette.color = new Color(0.8f, 0, 0.05f, Mathf.Max(vignetteT * 1.6f, low));

        foreach (var n in nums)
        {
            if (!n.active) continue;
            n.life -= Time.deltaTime;
            if (n.life <= 0) { n.active = false; n.t.gameObject.SetActive(false); continue; }
            float k = 1f - n.life / 0.6f;
            var sp = g.Cam.WorldToScreenPoint(n.world);
            n.t.rectTransform.position = sp + Vector3.up * (k * 70f * canvas.scaleFactor);
            n.t.rectTransform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.2f, k / 0.15f) : 1f);
            n.t.color = Kit.A(n.t.color, Mathf.Clamp01(n.life * 4f));
        }
    }

    void UpdateJoystick()
    {
        Vector2? pos = null;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (joyId == -99 && t.phase == TouchPhase.Began && !OverUI(t.fingerId)) { joyId = t.fingerId; joyOrigin = t.position; joyMouse = false; }
                if (t.fingerId == joyId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) joyId = -99;
                    else pos = t.position;
                }
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0) && !OverUI(-1)) { joyId = -1; joyOrigin = Input.mousePosition; joyMouse = true; }
            if (joyMouse && joyId == -1) { if (Input.GetMouseButton(0)) pos = Input.mousePosition; else joyId = -99; }
        }
        if (pos.HasValue && Game.I.State == Game.S.Playing)
        {
            float r = JoyRadius * canvas.scaleFactor;
            var d = pos.Value - joyOrigin;
            if (d.magnitude > r) { joyOrigin += d.normalized * (d.magnitude - r); d = pos.Value - joyOrigin; }
            Joy = d / r;
            joyBase.gameObject.SetActive(true); joyBase.position = joyOrigin; joyKnob.anchoredPosition = d / canvas.scaleFactor;
        }
        else
        {
            Joy = Vector2.zero; joyBase.gameObject.SetActive(false);
            if (!pos.HasValue) joyId = -99;
        }
    }

    static bool OverUI(int id)
    {
        if (!EventSystem.current) return false;
        return id < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(id);
    }
}
