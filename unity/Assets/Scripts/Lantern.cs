using System.Collections.Generic;
using UnityEngine;

// LANTERN: the graveyard is pitch dark. Your lantern's light is your range (weapons only reach what it lights)
// and your life clock (it burns down; oil refills it; an empty lantern lets the dark bite).
// Undead outside the light show only their glowing eyes.
public class Lantern : MonoBehaviour
{
    public static Lantern I;
    public const float StartR = 5.6f, MinR = 2.4f, BaseMax = 7.2f, OilGain = 1.5f;
    public float R = StartR;            // lantern radius (gameplay)
    public float Shown = StartR;        // smoothed radius used for visuals and reach
    public int OilCollected;
    public float Max => BaseMax + 0.5f * Arsenal.I.Level(Up.Aura);
    public float Fuel01 => Mathf.InverseLerp(MinR, Max, R);
    public float Reach => Shown + 0.4f;   // weapons can hit just past the visible edge
    public bool Empty => R <= MinR + 0.01f;

    const float PlaneY = 6.5f;          // the darkness sheet floats above every model, below the camera
    Transform dark, warm;
    SpriteRenderer warmSr;   // a warm pool of light (no real light: an extra pixel light doubles draw calls on phones)
    float oilT, biteT, flicker, flare;
    bool warned;
    static Material over;
    // glows that must shine through the dark (eyes, oil)
    public static Material Over
    {
        get
        {
            if (!over) { over = new Material(Kit.UnlitAlpha) { mainTexture = Fx.Glow.texture }; over.renderQueue = 3100; }
            return over;
        }
    }

    class Eyes { public Transform t; public SpriteRenderer a, b; }
    readonly Dictionary<Enemy, Eyes> eyes = new Dictionary<Enemy, Eyes>();
    readonly Stack<Eyes> eyePool = new Stack<Eyes>();
    readonly List<Enemy> gone = new List<Enemy>();
    static Material eyeMat;

    void Awake()
    {
        I = this;
        // darkness: a ring mesh with a soft hole, alpha in vertex colours (radius 1 = the light's edge)
        var go = new GameObject("Darkness");
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Kit.UnlitAlpha) { renderQueue = 3050 };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.sortingOrder = 50;   // sorting order beats render queue: keep world glows (fire baskets) under the dark
        mf.sharedMesh = DarkMesh();
        dark = go.transform;

        warm = new GameObject("LanternGlow").transform;
        warm.SetParent(transform, false);
        var ws = warm.gameObject.AddComponent<SpriteRenderer>();
        ws.sprite = Fx.Glow; ws.color = Kit.A(Kit.Hex("#FFB25A"), 0.2f); warmSr = ws;
        warm.rotation = Quaternion.Euler(90, 0, 0);

    }

    static Mesh DarkMesh()
    {
        float[] rr = { 0f, 0.6f, 0.78f, 0.9f, 1f, 1.1f, 60f };
        float[] aa = { 0f, 0f, 0.4f, 0.7f, 0.86f, 0.95f, 0.95f };
        const int seg = 72;
        var ink = Kit.Hex("#03020a");
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        for (int k = 0; k < rr.Length; k++)
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * rr[k], 0, Mathf.Sin(a) * rr[k]));
                c.Add(Kit.A(ink, aa[k]));
            }
        for (int k = 0; k < rr.Length - 1; k++)
            for (int i = 0; i < seg; i++)
            {
                int a0 = k * seg + i, a1 = k * seg + (i + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        var m = new Mesh(); m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateBounds();
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 200f);
        return m;
    }

    public void ResetRun()
    {
        R = Shown = StartR; OilCollected = 0; oilT = 3.5f; biteT = 1f; warned = false; flare = 0;
        foreach (var kv in eyes) { kv.Value.t.gameObject.SetActive(false); eyePool.Push(kv.Value); }
        eyes.Clear();
    }

    public void AddOil(float v, bool pickup)
    {
        R = Mathf.Min(Max, R + v);
        flare = 1f;
        warned = false;
        if (pickup)
        {
            OilCollected++;
            WebBridge.Event("oil_pickup", OilCollected);
            UI.I.Float(Game.I.Hero.transform.position + Vector3.up * 2.2f, "+LIGHT", Kit.Hex("#FFC27A"));
        }
    }

    public bool InLight(Vector3 p, float pad = 0f)
    {
        var d = p - Game.I.Hero.transform.position; d.y = 0;
        float r = Shown + pad;
        return d.sqrMagnitude < r * r;
    }

    void Update()
    {
        var g = Game.I;
        if (!g || !g.Hero) return;
        float dt = Time.deltaTime;
        var hp = g.Hero.transform.position;
        bool playing = g.State == Game.S.Playing;

        if (playing)
        {
            // burns faster as the night goes on
            float drain = 0.075f + 0.1f * Mathf.Clamp01(g.RunTime / Defs.RunLength);
            R = Mathf.Max(MinR, R - drain * dt);
            if (Fuel01 < 0.22f && !warned) { warned = true; UI.I.Banner("YOUR LANTERN IS DYING", Kit.Hex("#FFC27A")); Sfx.I.Hurt(); }
            if (Empty && (biteT -= dt) <= 0)
            {
                biteT = 1.1f;
                g.Hero.Hurt(5f + g.RunTime / 60f);
                UI.I.Float(hp + Vector3.up * 2.2f, "THE DARK BITES", Kit.Hex("#B98CFF"));
                WebBridge.Event("dark_bite", (int)g.RunTime);
            }
            // oil turns up out in the dark: go and get it
            if ((oilT -= dt) <= 0)
            {
                oilT = Random.Range(6.5f, 9.5f);
                SpawnOil(hp, g.RunTime < 6f);
            }
        }
        else if (g.State == Game.S.Menu) R = StartR;

        Shown = Mathf.Lerp(Shown, R, 1f - Mathf.Exp(-dt * 5f));
        flare = Mathf.Max(0, flare - dt * 1.6f);
        flicker = Mathf.Lerp(flicker, Random.Range(-1f, 1f), dt * 9f);
        float vis = Shown * (1f + flare * 0.12f + flicker * 0.012f);

        // place the darkness sheet where the camera's line to the hero crosses it, sized to match the ground
        var cam = g.Cam.transform.position;
        var h = hp + Vector3.up;
        float k = (cam.y - PlaneY) / Mathf.Max(0.1f, cam.y - h.y);
        dark.position = cam + (h - cam) * k;
        dark.localScale = Vector3.one * vis * k;

        warm.position = hp + Vector3.up * 0.04f;
        warm.localScale = Vector3.one * vis * 2.3f;
        warmSr.color = Kit.A(Kit.Hex("#FFB25A"), 0.2f + flare * 0.18f + flicker * 0.02f);

        UpdateEyes(hp, vis);
    }

    void SpawnOil(Vector3 hp, bool first)
    {
        Vector3 p = Vector3.zero;
        for (int tries = 0; tries < 12; tries++)
        {
            float a = Random.value * Mathf.PI * 2f;
            if (first) a = Mathf.PI * 0.5f + Random.Range(-0.6f, 0.6f);   // the first one appears up-screen, easy to see
            float d = first ? Shown * 0.85f : Shown * Random.Range(1.15f, 1.6f);
            p = hp + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * d;
            if (p.magnitude < Defs.ArenaR - 2f && !Obstacles.Blocked(p, 0.5f)) break;
        }
        Pickups.I.DropOil(p);
        if (first) UI.I.Float(p + Vector3.up * 1.6f, "OIL!", Kit.Hex("#FFC27A"));
    }

    void UpdateEyes(Vector3 hp, float vis)
    {
        var camRot = Game.I.Cam.transform.rotation;
        foreach (var e in Horde.I.Alive)
        {
            var d = e.t.position - hp; d.y = 0;
            bool dark = e.alive && e.rise <= 0.3f && d.magnitude > vis * 0.92f;
            eyes.TryGetValue(e, out var ey);
            if (!dark) { if (ey != null) { ey.t.gameObject.SetActive(false); eyePool.Push(ey); eyes.Remove(e); } continue; }
            if (ey == null) { ey = GetEyes(e); eyes[e] = ey; }
            float head = e.def.height > 0 ? e.def.height * 0.82f : 1.2f * e.def.scale / 1.45f;
            ey.t.position = e.t.position + Vector3.up * head;
            ey.t.rotation = camRot;
            float blink = Mathf.Repeat(Time.time * 0.37f + e.id * 0.13f, 1f) < 0.035f ? 0.1f : 1f;
            float fade = Mathf.Clamp01((d.magnitude - vis * 0.92f) / 1.2f) * blink;
            var c = e.def.ghost ? Kit.Hex("#8CF4FF") : e.def.boss ? Kit.Hex("#FFB13B") : Kit.Hex("#FF3B3B");
            ey.a.color = ey.b.color = Kit.A(c, fade);
        }
        // eyes of enemies that died or despawned
        gone.Clear();
        foreach (var kv in eyes) if (!kv.Key.alive) gone.Add(kv.Key);
        foreach (var e in gone) { eyes[e].t.gameObject.SetActive(false); eyePool.Push(eyes[e]); eyes.Remove(e); }
    }

    Eyes GetEyes(Enemy e)
    {
        Eyes ey;
        if (eyePool.Count > 0) { ey = eyePool.Pop(); ey.t.gameObject.SetActive(true); }
        else
        {
            if (!eyeMat) { eyeMat = new Material(Kit.UnlitAlpha) { renderQueue = 3100 }; }
            ey = new Eyes { t = new GameObject("eyes").transform };
            ey.t.SetParent(transform, false);
            ey.a = Eye(ey.t, -0.13f); ey.b = Eye(ey.t, 0.13f);
        }
        float s = e.def.boss ? 2.4f : 1f;
        ey.t.localScale = Vector3.one * s;
        return ey;
    }

    SpriteRenderer Eye(Transform p, float x)
    {
        var sr = new GameObject("eye").AddComponent<SpriteRenderer>();
        sr.sprite = Fx.Glow; sr.sharedMaterial = eyeMat; sr.sortingOrder = 60;
        sr.transform.SetParent(p, false);
        sr.transform.localPosition = new Vector3(x, 0, 0);
        sr.transform.localScale = new Vector3(0.36f, 0.26f, 1f);
        return sr;
    }
}
