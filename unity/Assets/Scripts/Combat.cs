using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================== FX
public class Fx : MonoBehaviour
{
    public static Fx I;
    public static Sprite Glow, Ring, Disc, Diamond;
    class P { public Transform t; public SpriteRenderer sr; public Vector3 v; public float life, max, size, grow; public Color c; public bool flat, active; public float gravity; }
    readonly List<P> ps = new List<P>();
    float trauma;
    public Vector3 ShakeOffset { get; private set; }

    public static void InitSprites()
    {
        Glow = Sprite.Create(Kit.Glow, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        Ring = Sprite.Create(Kit.Ring, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 128);
        Disc = Sprite.Create(Kit.Disc, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = Mathf.Abs(x + .5f - n / 2f) / (n / 2f), v = Mathf.Abs(y + .5f - n / 2f) / (n / 2f);
            float d = u * 1.4f + v;       // tall diamond
            float edge = Mathf.Clamp01((1f - d) * 12f);
            float shade = Mathf.Lerp(1f, 0.65f, (x > n / 2 ? 1 : 0) * 0.6f + (y < n / 2 ? 1 : 0) * 0.4f);
            px[y * n + x] = new Color(shade, shade, shade, edge);
        }
        t.SetPixels(px); t.Apply();
        Diamond = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
    }

    void Awake() { I = this; for (int i = 0; i < 220; i++) ps.Add(Make()); }

    P Make()
    {
        var go = new GameObject("fx");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Glow;
        go.SetActive(false);
        return new P { t = go.transform, sr = sr };
    }

    P Get()
    {
        foreach (var p in ps) if (!p.active) return p;
        if (ps.Count < 500) { var p = Make(); ps.Add(p); return p; }
        return ps[UnityEngine.Random.Range(0, ps.Count)];
    }

    void Emit(Vector3 pos, Vector3 v, Color c, float size, float life, Sprite s, bool flat, float grow = 0, float gravity = 0)
    {
        var p = Get();
        p.t.position = pos; p.v = v; p.c = c; p.size = size; p.max = p.life = life; p.flat = flat; p.grow = grow; p.gravity = gravity;
        p.sr.sprite = s; p.sr.color = c;
        p.t.localScale = Vector3.one * size;
        p.t.rotation = flat ? Quaternion.Euler(90, 0, 0) : Game.I.Cam.transform.rotation;
        p.active = true; p.t.gameObject.SetActive(true);
    }

    public void Poof(Vector3 pos, Color c, float scale = 1f)
    {
        for (int i = 0; i < 9 * scale; i++)
            Emit(pos, (UnityEngine.Random.insideUnitSphere + Vector3.up * 0.6f) * 3.2f * scale, c, UnityEngine.Random.Range(0.25f, 0.5f) * scale, UnityEngine.Random.Range(0.35f, 0.6f), Glow, false, 0, -4f);
        Emit(pos, Vector3.zero, Kit.A(c, 0.7f), 0.3f, 0.35f, Ring, true, 3.5f * scale);
    }

    public void Dirt(Vector3 pos)
    {
        for (int i = 0; i < 6; i++)
            Emit(pos + Vector3.up * 0.1f, (UnityEngine.Random.insideUnitSphere + Vector3.up * 1.5f) * 2f, Kit.Hex("#5a4636"), UnityEngine.Random.Range(0.18f, 0.32f), 0.5f, Disc, false, 0, -9f);
    }

    public void Shockwave(Vector3 pos, Color c, float size)
    {
        pos.y = 0.05f;
        Emit(pos, Vector3.zero, Kit.A(c, 0.9f), 0.4f, 0.45f, Ring, true, size * 2.2f);
        Emit(pos, Vector3.zero, Kit.A(c, 0.45f), size * 0.8f, 0.3f, Glow, true, 0);
    }

    public void Muzzle(Vector3 pos, Color c) => Emit(pos, Vector3.zero, c, 0.55f, 0.07f, Glow, false);

    public void Spark(Vector3 pos, Color c)
    {
        for (int i = 0; i < 3; i++) Emit(pos, UnityEngine.Random.insideUnitSphere * 3f, c, 0.16f, 0.2f, Glow, false);
    }

    public void Explosion(Vector3 pos, float r)
    {
        pos.y = 0.05f;
        Emit(pos, Vector3.zero, Kit.A(Kit.Hex("#FFB347"), 0.95f), r * 2.3f, 0.28f, Glow, true);
        Emit(pos + Vector3.up * 0.6f, Vector3.zero, Kit.A(Kit.Hex("#FFF1B8"), 0.9f), r * 1.4f, 0.18f, Glow, false);
        Emit(pos, Vector3.zero, Kit.A(Kit.Hex("#FF7A30"), 0.9f), 0.5f, 0.4f, Ring, true, r * 2.4f);
        for (int i = 0; i < 10; i++)
            Emit(pos + Vector3.up * 0.4f, (UnityEngine.Random.insideUnitSphere + Vector3.up) * 5f, Kit.Hex("#FF8C42"), 0.35f, 0.5f, Glow, false, 0, -10f);
    }

    public void Telegraph(Vector3 pos, float r, float delay, Action done) => StartCoroutine(Tele(pos, r, delay, done));

    System.Collections.IEnumerator Tele(Vector3 pos, float r, float delay, Action done)
    {
        pos.y = 0.04f;
        var ring = Get(); ring.active = true; ring.life = 99; ring.t.gameObject.SetActive(true);
        ring.sr.sprite = Ring; ring.t.position = pos; ring.t.rotation = Quaternion.Euler(90, 0, 0); ring.t.localScale = Vector3.one * r * 2f;
        var fill = Get(); fill.active = true; fill.life = 99; fill.t.gameObject.SetActive(true);
        fill.sr.sprite = Disc; fill.t.position = pos + Vector3.up * 0.01f; fill.t.rotation = Quaternion.Euler(90, 0, 0);
        float k = 0;
        while (k < 1f)
        {
            k += Time.deltaTime / delay;
            ring.sr.color = new Color(1f, 0.25f, 0.3f, 0.9f);
            fill.sr.color = new Color(1f, 0.2f, 0.25f, 0.35f);
            fill.t.localScale = Vector3.one * r * 2f * k;
            yield return null;
        }
        ring.active = false; ring.t.gameObject.SetActive(false);
        fill.active = false; fill.t.gameObject.SetActive(false);
        if (Game.I.State == Game.S.Playing) done?.Invoke();
    }

    public void Shake(float a) => trauma = Mathf.Min(1f, trauma + a);

    public void DamageNumber(Vector3 pos, float dmg, bool crit) => UI.I.Number(pos, dmg, crit);

    public void ClearAll() { foreach (var p in ps) { p.active = false; p.t.gameObject.SetActive(false); } trauma = 0; }

    void Update()
    {
        float dt = Time.deltaTime;
        var camRot = Game.I.Cam.transform.rotation;
        foreach (var p in ps)
        {
            if (!p.active || p.life > 50) continue;
            p.life -= dt;
            if (p.life <= 0) { p.active = false; p.t.gameObject.SetActive(false); continue; }
            float k = p.life / p.max;
            p.v.y += p.gravity * dt;
            p.t.position += p.v * dt;
            if (!p.flat) p.t.rotation = camRot;
            float s = p.grow > 0 ? Mathf.Lerp(p.size, p.grow, 1f - k * k) : p.size * (0.4f + 0.6f * k);
            p.t.localScale = Vector3.one * s;
            p.sr.color = Kit.A(p.c, p.c.a * Mathf.Clamp01(k * 1.6f));
        }
        trauma = Mathf.Max(0, trauma - Time.unscaledDeltaTime * 1.8f);
        float sh = trauma * trauma, tt = Time.unscaledTime * 35f;
        ShakeOffset = new Vector3(Mathf.PerlinNoise(tt, 0) - 0.5f, 0, Mathf.PerlinNoise(0, tt) - 0.5f) * sh * 1.2f;
    }
}

// ============================================================== Pickups
public class Pickups : MonoBehaviour
{
    public static Pickups I;
    enum K { Xp, Coin, Potion, Chest, Candy, Oil }
    class G { public K kind; public Transform t; public SpriteRenderer sr; public int value; public bool flying; public float speed, bob; }
    readonly List<G> live = new List<G>();
    readonly Dictionary<K, Stack<G>> pool = new Dictionary<K, Stack<G>>();

    void Awake() => I = this;

    G Get(K k, Vector3 pos)
    {
        if (!pool.TryGetValue(k, out var st)) pool[k] = st = new Stack<G>();
        G g;
        if (st.Count > 0) { g = st.Pop(); g.t.gameObject.SetActive(true); }
        else
        {
            g = new G { kind = k };
            if (k == K.Xp)
            {
                var go = new GameObject("gem");
                go.transform.SetParent(transform, false);
                g.sr = go.AddComponent<SpriteRenderer>();
                g.sr.sprite = Fx.Diamond;
                var glow = new GameObject("glow").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(go.transform, false);
                glow.sprite = Fx.Glow; glow.transform.localScale = Vector3.one * 2.4f; glow.sortingOrder = -1;
                g.t = go.transform;
            }
            else if (k == K.Candy)
            {
                var c = Kit.Spawn(Spooky.Candies[UnityEngine.Random.Range(0, Spooky.Candies.Length)], 1f, transform);
                var b = Kit.WorldBounds(c);
                c.transform.localScale = Vector3.one * (0.55f / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z))));
                g.t = c.transform;
                var glow = new GameObject("glow").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(g.t, false);
                glow.sprite = Fx.Glow; glow.transform.localScale = Vector3.one * (1.6f / c.transform.localScale.x);
                glow.transform.localPosition = new Vector3(0, 0.02f, 0); glow.transform.localRotation = Quaternion.Euler(90, 0, 0);
                glow.color = Kit.A(Kit.Hex("#FF8C42"), 0.55f);
            }
            else
            {
                string m = k == K.Coin ? "Dungeon/coin" : k == K.Potion ? "Dungeon/potion" : k == K.Oil ? "Graveyard/lantern-candle" : "Dungeon/chest";
                float s = k == K.Coin ? 1.0f : k == K.Potion ? 1.4f : k == K.Oil ? 1.6f : 2.2f;
                g.t = Kit.Spawn(m, s, transform).transform;
                var glow = new GameObject("glow").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(g.t, false);
                glow.sprite = Fx.Glow; glow.transform.localScale = Vector3.one * (k == K.Chest ? 2.2f : 1.4f);
                glow.transform.localPosition = new Vector3(0, 0.02f, 0); glow.transform.localRotation = Quaternion.Euler(90, 0, 0);
                glow.color = k == K.Coin ? Kit.A(Kit.Hex("#FFD166"), 0.5f) : k == K.Potion ? Kit.A(Kit.Hex("#FF6B6B"), 0.6f) : Kit.A(Kit.Hex("#FFD166"), 0.8f);
                if (k == K.Oil)
                {
                    // oil shines through the dark so you can go and fetch it
                    glow.sharedMaterial = Lantern.Over; glow.sortingOrder = 60; glow.color = Kit.A(Kit.Hex("#FFB25A"), 0.95f);
                    glow.transform.localScale = Vector3.one * 2.4f;
                }
            }
        }
        g.flying = false; g.speed = 0; g.bob = UnityEngine.Random.value * 6f;
        pos.y = k == K.Xp ? 0.35f : 0f;
        g.t.position = pos;
        live.Add(g);
        return g;
    }

    public void DropXp(Vector3 pos, int xp)
    {
        // too many gems on the floor: fold into a nearby one instead of spawning
        if (live.Count > 170)
            foreach (var o in live)
                if (o.kind == K.Xp && !o.flying && (o.t.position - pos).sqrMagnitude < 9f) { o.value += xp; Style(o); return; }
        var g = Get(K.Xp, pos + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0, UnityEngine.Random.Range(-0.3f, 0.3f)));
        g.value = xp;
        Style(g);
    }

    void Style(G g)
    {
        Color c = g.value >= 25 ? Kit.Hex("#FF5D8F") : g.value >= 5 ? Kit.Hex("#5DC8FF") : Kit.Hex("#7CFFB2");
        g.sr.color = c;
        g.t.GetChild(0).GetComponent<SpriteRenderer>().color = Kit.A(c, 0.45f);
        g.t.localScale = Vector3.one * (g.value >= 25 ? 0.6f : g.value >= 5 ? 0.48f : 0.36f);
    }

    public void DropCoins(Vector3 pos, int n)
    {
        for (int i = 0; i < n; i++)
        {
            var g = Get(K.Coin, pos + UnityEngine.Random.insideUnitSphere * Mathf.Min(0.3f * n, 2.5f));
            g.value = 1;
        }
    }

    public void DropPotion(Vector3 pos) => Get(K.Potion, pos).value = 30;
    public void DropCandy(Vector3 pos, int n)
    {
        for (int i = 0; i < n; i++)
            Get(K.Candy, pos + UnityEngine.Random.insideUnitSphere * Mathf.Min(0.35f * n, 2.5f)).value = 1;
    }
    public void DropChest(Vector3 pos) => Get(K.Chest, pos).value = 1;
    public void DropOil(Vector3 pos) => Get(K.Oil, pos).value = 1;
    public Vector3? NearestOil(Vector3 p, float maxR)
    {
        G best = null; float bd = maxR * maxR;
        foreach (var g in live)
        {
            if (g.kind != K.Oil) continue;
            float d = (g.t.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = g; }
        }
        return best != null ? best.t.position : (Vector3?)null;
    }

    public Vector3? NearestGem(Vector3 p, float maxR)
    {
        G best = null; float bd = maxR * maxR;
        foreach (var g in live)
        {
            if (g.flying) continue;
            float d = (g.t.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = g; }
        }
        return best != null ? best.t.position : (Vector3?)null;
    }

    public void VacuumAll() { foreach (var g in live) if (g.kind == K.Xp || g.kind == K.Coin || g.kind == K.Candy) g.flying = true; }

    public void Clear()
    {
        foreach (var g in live) { g.t.gameObject.SetActive(false); pool[g.kind].Push(g); }
        live.Clear();
    }

    void Update()
    {
        var hero = Game.I.Hero;
        if (!hero || Game.I.State != Game.S.Playing) return;
        float dt = Time.deltaTime;
        var hp = hero.transform.position;
        float mag = Game.I.MagnetR;
        var camRot = Game.I.Cam.transform.rotation;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var g = live[i];
            var d = hp - g.t.position; d.y = 0;
            float dist = d.magnitude;
            if (!g.flying && ((dist < mag && g.kind != K.Oil) || (g.kind == K.Chest && dist < 1.4f) || (g.kind == K.Potion && dist < 1.2f) || (g.kind == K.Oil && dist < 1.5f))) g.flying = true;
            if (g.flying)
            {
                g.speed = Mathf.Min(g.speed + dt * 28f, 22f);
                g.t.position += d.normalized * Mathf.Min(g.speed * dt, dist);
                if (dist < 0.45f) { Collect(g); live.RemoveAt(i); continue; }
            }
            if (g.kind == K.Xp)
            {
                g.t.rotation = camRot;
                var p = g.t.position; p.y = 0.35f + Mathf.Sin(Time.time * 4f + g.bob) * 0.07f; g.t.position = p;
            }
            else g.t.rotation = Quaternion.Euler(0, Time.time * 120f + g.bob * 60f, 0);
        }
    }

    void Collect(G g)
    {
        g.t.gameObject.SetActive(false);
        pool[g.kind].Push(g);
        switch (g.kind)
        {
            case K.Xp: Game.I.AddXp(g.value); Sfx.I.Gem(); break;
            case K.Coin: Game.I.RunGold += 1; Sfx.I.Coin(); break;
            case K.Potion: Game.I.Hero.Heal(g.value); Sfx.I.Heal(); UI.I.Float(Game.I.Hero.transform.position + Vector3.up * 2f, "+" + g.value + " HP", Kit.Hex("#FF6B6B")); break;
            case K.Chest: Game.I.OpenChest(); break;
            case K.Candy: Game.I.RunCandy += g.value; Sfx.I.Gem(); break;
            case K.Oil: Lantern.I.AddOil(Lantern.OilGain, true); Sfx.I.Heal(); Fx.I.Shockwave(Game.I.Hero.transform.position, Kit.Hex("#FFC27A"), 4f); break;
        }
    }
}

// ============================================================== Weapons
public class Arsenal : MonoBehaviour
{
    public static Arsenal I;
    public readonly Dictionary<Up, int> Lv = new Dictionary<Up, int>();
    public int Level(Up u) => Lv.TryGetValue(u, out var l) ? l : 0;

    class Bolt { public Transform t; public Vector3 vel; public float dmg, life, knock; public int pierce; public readonly List<int> hit = new List<int>(); public bool pellet; }
    readonly List<Bolt> bolts = new List<Bolt>();
    readonly Stack<Bolt> boltPool = new Stack<Bolt>(), pelletPool = new Stack<Bolt>();
    Material boltMat, pelletMat;

    float cdBlaster, cdScatter, cdTomb, cdGrenade, auraTick;
    readonly List<Transform> pumpkins = new List<Transform>();
    readonly Dictionary<int, float> pumpkinHit = new Dictionary<int, float>();
    float orbit;
    SpriteRenderer auraRing, auraGlow;

    void Awake()
    {
        I = this;
        boltMat = new Material(Kit.UnlitAlpha) { color = Kit.Hex("#B6FFD9") };
        pelletMat = new Material(Kit.UnlitAlpha) { color = Kit.Hex("#FFE9A3") };
    }

    public void ResetRun()
    {
        Lv.Clear();
        foreach (var b in bolts) { b.t.gameObject.SetActive(false); (b.pellet ? pelletPool : boltPool).Push(b); }
        bolts.Clear();
        foreach (var p in pumpkins) Destroy(p.gameObject);
        pumpkins.Clear();
        pumpkinHit.Clear();
        if (auraRing) { Destroy(auraRing.gameObject); Destroy(auraGlow.gameObject); auraRing = null; }
        cdBlaster = cdScatter = cdTomb = cdGrenade = auraTick = 0;
    }

    public void OnLevel(Up u)
    {
        if (u == Up.Pumpkins)
        {
            while (pumpkins.Count < Level(Up.Pumpkins))
            {
                var p = Kit.Spawn("Graveyard/pumpkin-carved", 1.5f, transform).transform;
                var glow = new GameObject("glow").AddComponent<SpriteRenderer>();
                glow.sprite = Fx.Glow; glow.color = Kit.A(Kit.Hex("#FF8C42"), 0.55f);
                glow.transform.SetParent(p, false); glow.transform.localScale = Vector3.one * 1.6f; glow.transform.localPosition = new Vector3(0, 0.25f, 0);
                pumpkins.Add(p);
            }
        }
        if (u == Up.Aura && !auraRing)
        {
            auraRing = new GameObject("auraRing").AddComponent<SpriteRenderer>();
            auraRing.sprite = Fx.Ring; auraRing.color = Kit.A(Kit.Hex("#FFB347"), 0.75f);
            auraRing.transform.rotation = Quaternion.Euler(90, 0, 0);
            auraGlow = new GameObject("auraGlow").AddComponent<SpriteRenderer>();
            auraGlow.sprite = Fx.Glow; auraGlow.color = Kit.A(Kit.Hex("#FF8C42"), 0.25f);
            auraGlow.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
    }

    Bolt GetBolt(bool pellet)
    {
        var st = pellet ? pelletPool : boltPool;
        Bolt b;
        if (st.Count > 0) { b = st.Pop(); b.t.gameObject.SetActive(true); }
        else
        {
            var go = Kit.MeshObject("bolt", Kit.SphereMesh);
            go.GetComponent<MeshRenderer>().sharedMaterial = pellet ? pelletMat : boltMat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform, false);
            go.transform.localScale = pellet ? new Vector3(0.14f, 0.14f, 0.3f) : new Vector3(0.18f, 0.18f, 0.55f);
            var glow = new GameObject("glow").AddComponent<SpriteRenderer>();
            glow.sprite = Fx.Glow; glow.color = pellet ? Kit.A(Kit.Hex("#FFD166"), 0.6f) : Kit.A(Kit.Hex("#7CFFB2"), 0.7f);
            glow.transform.SetParent(go.transform, false); glow.transform.localRotation = Quaternion.Euler(90, 0, 0);
            glow.transform.localScale = new Vector3(6f, 3f, 1f);
            glow.transform.localPosition = new Vector3(0, -3.5f, 0);
            b = new Bolt { t = go.transform, pellet = pellet };
        }
        b.hit.Clear();
        bolts.Add(b);
        return b;
    }

    void Fire(Vector3 from, Vector3 dir, float speed, float dmg, float life, int pierce, float knock, bool pellet)
    {
        var b = GetBolt(pellet);
        dir.y = 0; dir.Normalize();
        b.t.position = from; b.t.rotation = Quaternion.LookRotation(dir);
        b.vel = dir * speed; b.dmg = dmg; b.life = life; b.pierce = pierce; b.knock = knock;
    }

    static Vector3 Rotate(Vector3 v, float deg) => Quaternion.Euler(0, deg, 0) * v;

    void Update()
    {
        if (Game.I.State != Game.S.Playing) return;
        float dt = Time.deltaTime;
        var hero = Game.I.Hero;
        var hp = hero.transform.position;
        var muzzle = hero.Muzzle.position;
        float dm = Game.I.DmgMult, cm = Game.I.CdMult;
        var H = Horde.I;
        float reach = Lantern.I.Reach;

        // --- Soul Blaster
        int lb = Level(Up.Blaster);
        if (lb > 0 && (cdBlaster -= dt) <= 0)
        {
            var target = H.Nearest(hp, Mathf.Min(12f, reach));
            if (target != null)
            {
                int shots = lb >= 5 ? 3 : lb >= 3 ? 2 : 1;
                var dir = target.t.position - muzzle;
                for (int i = 0; i < shots; i++)
                    Fire(muzzle, Rotate(dir, (i - (shots - 1) * 0.5f) * 9f), 24f, (10 + 4 * (lb - 1)) * dm, 0.8f, lb >= 4 ? 1 : 0, 1.2f, false);
                cdBlaster = 0.6f * (1f - 0.07f * (lb - 1)) * cm;
                Fx.I.Muzzle(muzzle, Kit.Hex("#B6FFD9"));
                hero.Recoil();
                Sfx.I.Shoot();
            }
            else cdBlaster = 0.1f;
        }

        // --- Scattergun
        int ls = Level(Up.Scatter);
        if (ls > 0 && (cdScatter -= dt) <= 0)
        {
            var target = H.Nearest(hp, Mathf.Min(7f, reach));
            if (target != null)
            {
                int n = 4 + ls;
                var dir = target.t.position - muzzle;
                for (int i = 0; i < n; i++) Fire(muzzle, Rotate(dir, Mathf.Lerp(-28f, 28f, i / (float)(n - 1)) + UnityEngine.Random.Range(-4f, 4f)), 17f, (7 + 2.5f * ls) * dm, 0.34f, 0, 5f, true);
                cdScatter = 1.6f * (1f - 0.06f * (ls - 1)) * cm;
                Fx.I.Muzzle(muzzle, Kit.Hex("#FFE9A3"));
                Fx.I.Shake(0.08f);
                Sfx.I.Scatter();
                hero.Recoil();
            }
            else cdScatter = 0.1f;
        }

        // --- bolts
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            var b = bolts[i];
            b.life -= dt;
            b.t.position += b.vel * dt;
            var fromHero = b.t.position - hp; fromHero.y = 0;
            bool fizzle = fromHero.sqrMagnitude > (reach + 0.3f) * (reach + 0.3f);   // shots die in the dark
            bool dead = b.life <= 0 || fizzle || b.t.position.magnitude > Defs.ArenaR + 6f;
            if (fizzle && b.life > 0) Fx.I.Spark(b.t.position, Kit.Hex("#5A4C7A"));
            if (!dead)
                foreach (var e in H.Near(b.t.position, 0.3f))
                {
                    if (b.hit.Contains(e.id)) continue;
                    b.hit.Add(e.id);
                    H.Damage(e, b.dmg, b.vel, b.knock);
                    Fx.I.Spark(b.t.position, b.pellet ? Kit.Hex("#FFD166") : Kit.Hex("#7CFFB2"));
                    if (--b.pierce < 0) { dead = true; break; }
                }
            if (dead) { b.t.gameObject.SetActive(false); (b.pellet ? pelletPool : boltPool).Push(b); bolts.RemoveAt(i); }
        }

        // --- Jack-o'-Guard
        int lp = Level(Up.Pumpkins);
        if (lp > 0)
        {
            orbit += dt * (2.4f + 0.25f * lp);
            float r = 2.0f + 0.15f * lp;
            for (int i = 0; i < pumpkins.Count; i++)
            {
                float a = orbit + i * Mathf.PI * 2f / pumpkins.Count;
                var p = hp + new Vector3(Mathf.Cos(a) * r, 0.25f + Mathf.Sin(Time.time * 5f + i) * 0.1f, Mathf.Sin(a) * r);
                pumpkins[i].position = p;
                pumpkins[i].rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                foreach (var e in H.Near(p, 0.45f))
                {
                    if (pumpkinHit.TryGetValue(e.id, out var t) && Time.time < t) continue;
                    pumpkinHit[e.id] = Time.time + 0.45f;
                    H.Damage(e, (9 + 3.5f * lp) * dm, e.t.position - hp, 3f, true);
                    Fx.I.Spark(p, Kit.Hex("#FF8C42"));
                }
            }
            if (pumpkinHit.Count > 400) pumpkinHit.Clear();
        }

        // --- Tombfall
        int lt = Level(Up.Tombstones);
        if (lt > 0 && (cdTomb -= dt) <= 0)
        {
            cdTomb = 3.4f * (1f - 0.08f * (lt - 1)) * cm;
            int n = 1 + (lt + 1) / 2 - (lt == 1 ? 1 : 0) + (lt >= 5 ? 1 : 0);
            for (int i = 0; i < Mathf.Max(1, n); i++)
            {
                var target = H.Random(hp, Mathf.Min(10f, reach));
                if (target == null) break;
                StartCoroutine(Tomb(target.t.position, (28 + 10 * lt) * dm, 1.7f + 0.1f * lt, i * 0.15f));
            }
        }

        // --- Holy Grenade
        int lg = Level(Up.Grenades);
        if (lg > 0 && (cdGrenade -= dt) <= 0)
        {
            cdGrenade = 2.6f * (1f - 0.07f * (lg - 1)) * cm;
            for (int i = 0; i < (lg >= 4 ? 2 : 1); i++)
            {
                var target = H.Random(hp, Mathf.Min(9f, reach));
                if (target == null) break;
                StartCoroutine(Grenade(muzzle, target.t.position, (20 + 9 * lg) * dm, 2.1f + 0.2f * lg));
            }
        }

        // --- Lantern Aura
        int la = Level(Up.Aura);
        if (la > 0)
        {
            float r = 1.9f + 0.3f * la;
            auraRing.transform.position = hp + Vector3.up * 0.06f;
            auraRing.transform.rotation = Quaternion.Euler(90, Time.time * 40f, 0);
            auraRing.transform.localScale = Vector3.one * r * 2f * (1f + Mathf.Sin(Time.time * 6f) * 0.03f);
            auraGlow.transform.position = hp + Vector3.up * 0.05f;
            auraGlow.transform.localScale = Vector3.one * r * 2.4f;
            if ((auraTick -= dt) <= 0)
            {
                auraTick = 0.45f;
                foreach (var e in H.Near(hp, r).ToArray())
                {
                    H.Damage(e, (4 + 3 * la) * dm, e.t.position - hp, 0.5f, true);
                    if (la >= 3) e.slow = 0.5f;
                }
            }
        }
    }

    System.Collections.IEnumerator Tomb(Vector3 at, float dmg, float r, float delay)
    {
        yield return new WaitForSeconds(delay);
        var stone = Kit.Spawn(UnityEngine.Random.value < 0.5f ? "Graveyard/gravestone-round" : "Graveyard/gravestone-cross", 2.4f, transform).transform;
        stone.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
        float k = 0;
        at.y = 0;
        while (k < 1f) { k += Time.deltaTime / 0.32f; stone.position = at + Vector3.up * Mathf.Lerp(9f, 0f, k * k); yield return null; }
        Fx.I.Shockwave(at, Kit.Hex("#B8C0FF"), r * 1.3f);
        Fx.I.Dirt(at); Fx.I.Dirt(at);
        Fx.I.Shake(0.18f);
        Sfx.I.Slam();
        foreach (var e in Horde.I.Near(at, r).ToArray()) Horde.I.Damage(e, dmg, e.t.position - at, 4f, true);
        yield return new WaitForSeconds(0.7f);
        k = 0;
        while (k < 1f) { k += Time.deltaTime / 0.4f; stone.position = at + Vector3.down * k * 1.4f; yield return null; }
        Destroy(stone.gameObject);
    }

    System.Collections.IEnumerator Grenade(Vector3 from, Vector3 to, float dmg, float r)
    {
        var g = Kit.Spawn("Blaster/grenade-a", 2.2f, transform).transform;
        to.y = 0;
        float k = 0;
        while (k < 1f)
        {
            k += Time.deltaTime / 0.55f;
            g.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3f;
            g.rotation = Quaternion.Euler(k * 720f, 0, k * 300f);
            yield return null;
        }
        Destroy(g.gameObject);
        Fx.I.Explosion(to, r);
        Fx.I.Shake(0.22f);
        Sfx.I.Boom();
        foreach (var e in Horde.I.Near(to, r).ToArray()) Horde.I.Damage(e, dmg, e.t.position - to, 6f, true);
    }
}
