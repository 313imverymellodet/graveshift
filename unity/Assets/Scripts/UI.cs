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
    RectTransform xpFill, hpBar, hpFill, bossBar, bossFill;
    Text lvText, timerText, killText, goldText, bossName, bannerText, candyText;
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
    void BuildHud()
    {
        hud = Fill("hud", root);
        var xpBg = Box(hud, new Vector2(.5f, 1), new Vector2(0, -34), new Vector2(1020, 30), new Color(0, 0, 0, 0.55f));
        xpBg.anchorMin = new Vector2(0, 1); xpBg.anchorMax = new Vector2(1, 1); xpBg.sizeDelta = new Vector2(-60, 30);
        xpFill = Box(xpBg, new Vector2(0, .5f), Vector2.zero, new Vector2(0, 22), Toxic);
        xpFill.pivot = new Vector2(0, .5f); xpFill.anchoredPosition = new Vector2(4, 0);
        lvText = Txt(xpBg, "LV 1", 26, new Vector2(.5f, .5f), Vector2.zero, Color.white);
        Outline(lvText, 2);

        timerText = Txt(hud, "00:00", 64, new Vector2(.5f, 1), new Vector2(0, -110), Bone);
        Outline(timerText, 3);
        killText = Txt(hud, "0", 38, new Vector2(1, 1), new Vector2(-150, -110), Bone, TextAnchor.MiddleRight, 260);
        Outline(killText, 2);
        var skull = Txt(hud, "KILLS", 22, new Vector2(1, 1), new Vector2(-150, -150), new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight, 260);
        goldText = Txt(hud, "0", 38, new Vector2(0, 1), new Vector2(170, -110), Gold, TextAnchor.MiddleLeft, 260);
        Outline(goldText, 2);
        candyText = Txt(hud, "", 34, new Vector2(0, 1), new Vector2(170, -165), Kit.Hex("#FF9A3C"), TextAnchor.MiddleLeft, 300);
        Outline(candyText, 2);
        Img(hud, Icon("coin"), new Vector2(0, 1), new Vector2(60, -110), new Vector2(64, 64));

        Btn(hud, "II", new Vector2(1, 1), new Vector2(-60, -200), new Vector2(90, 90), new Color(0, 0, 0, 0.5f), Color.white, () => Game.I.Pause(), 40);

        hpBar = Box(hud, Vector2.zero, Vector2.zero, new Vector2(120, 16), new Color(0, 0, 0, 0.6f));
        hpFill = Box(hpBar, new Vector2(0, .5f), new Vector2(3, 0), new Vector2(114, 10), Blood);
        hpFill.pivot = new Vector2(0, .5f);

        bossBar = Box(hud, new Vector2(.5f, 1), new Vector2(0, -210), new Vector2(760, 34), new Color(0, 0, 0, 0.65f));
        bossFill = Box(bossBar, new Vector2(0, .5f), new Vector2(4, 0), new Vector2(752, 24), Blood);
        bossFill.pivot = new Vector2(0, .5f);
        bossName = Txt(bossBar, "", 30, new Vector2(.5f, 1), new Vector2(0, 26), Blood);
        Outline(bossName, 2);
        bossBar.gameObject.SetActive(false);
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
        var tag = Txt(s, "SURVIVE THE NIGHT. EVERY NIGHT.", 40, new Vector2(.5f, 1), new Vector2(0, -525), Bone, TextAnchor.MiddleCenter, 1000);
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
            float need = Defs.XpForLevel(g.Level);
            var xpBg = (RectTransform)xpFill.parent;
            xpFill.sizeDelta = new Vector2(Mathf.Max(0, (xpBg.rect.width - 8) * Mathf.Clamp01(g.Xp / need)), 22);
            lvText.text = "LV " + g.Level;
            float left = Mathf.Max(0, Defs.RunLength - g.RunTime);
            timerText.text = Clock(g.RunTime);
            timerText.color = left < 30 ? Color.Lerp(Bone, Gold, Mathf.PingPong(Time.unscaledTime * 3, 1)) : Bone;
            killText.text = Horde.I.Kills.ToString();
            goldText.text = g.RunGold.ToString();
            candyText.text = Spooky.On ? g.RunCandy + " CANDY" : "";

            var hp = g.Hero;
            var sp = g.Cam.WorldToScreenPoint(hp.transform.position + Vector3.down * 0.25f);
            hpBar.position = sp;
            hpFill.sizeDelta = new Vector2(114 * Mathf.Clamp01(hp.Hp / hp.MaxHp), 10);
            hpFill.GetComponent<Image>().color = hp.Hp / hp.MaxHp < 0.3f ? Color.Lerp(Blood, Color.white, Mathf.PingPong(Time.unscaledTime * 4, 1) * 0.5f) : Blood;

            var boss = Horde.I.Boss;
            bossBar.gameObject.SetActive(boss != null);
            if (boss != null)
            {
                bossName.text = boss.def.name;
                bossFill.sizeDelta = new Vector2(752 * Mathf.Clamp01(boss.hp / boss.maxHp), 24);
            }
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
