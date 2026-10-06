using System.Collections.Generic;
using UnityEngine;

// Static circular obstacles (gravestones, crypts, trees) with a coarse grid for fast push-out.
public static class Obstacles
{
    struct O { public Vector2 p; public float r; }
    static readonly Dictionary<long, List<O>> grid = new Dictionary<long, List<O>>();
    const float Cell = 3f;
    static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    public static void Clear() => grid.Clear();

    // Queries only look in the agent's own cell, so each obstacle is registered in every cell an agent
    // (up to boss size) could be standing in while touching it. Without this padding, enemies near a
    // cell border walked straight through gravestones.
    const float AgentPad = 1.4f;

    public static void Add(Vector3 pos, float r)
    {
        var o = new O { p = new Vector2(pos.x, pos.z), r = r };
        float pr = r + AgentPad;
        int x0 = Mathf.FloorToInt((pos.x - pr) / Cell), x1 = Mathf.FloorToInt((pos.x + pr) / Cell);
        int z0 = Mathf.FloorToInt((pos.z - pr) / Cell), z1 = Mathf.FloorToInt((pos.z + pr) / Cell);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                var k = Key(x, z);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<O>();
                l.Add(o);
            }
    }

    // Push a circle out of any obstacle it overlaps. Returns the corrected position.
    public static Vector3 Resolve(Vector3 pos, float r)
    {
        if (!grid.TryGetValue(Key(Mathf.FloorToInt(pos.x / Cell), Mathf.FloorToInt(pos.z / Cell)), out var l)) return pos;
        for (int i = 0; i < l.Count; i++)
        {
            var o = l[i];
            float dx = pos.x - o.p.x, dz = pos.z - o.p.y;
            float d2 = dx * dx + dz * dz, min = o.r + r;
            if (d2 < min * min && d2 > 1e-6f)
            {
                float d = Mathf.Sqrt(d2);
                pos.x = o.p.x + dx / d * min;
                pos.z = o.p.y + dz / d * min;
            }
        }
        return pos;
    }

    // A thin, wide prop (gravestone, crypt wall) as a row of circles along its width.
    public static void AddBox(Vector3 center, float yawDeg, float halfWidth, float halfDepth)
    {
        halfDepth = Mathf.Max(halfDepth, 0.18f);
        if (halfWidth <= halfDepth * 1.3f) { Add(center, Mathf.Max(halfWidth, halfDepth)); return; }
        var right = Quaternion.Euler(0, yawDeg, 0) * Vector3.right;
        int n = Mathf.CeilToInt(halfWidth / halfDepth);
        float r = halfDepth * 1.15f;
        for (int i = 0; i < n; i++)
        {
            float t = n == 1 ? 0 : Mathf.Lerp(-halfWidth + r * 0.8f, halfWidth - r * 0.8f, i / (float)(n - 1));
            Add(center + right * t, r);
        }
    }

    public static bool Blocked(Vector3 pos, float r)
    {
        if (!grid.TryGetValue(Key(Mathf.FloorToInt(pos.x / Cell), Mathf.FloorToInt(pos.z / Cell)), out var l)) return false;
        foreach (var o in l) if ((new Vector2(pos.x, pos.z) - o.p).sqrMagnitude < (o.r + r) * (o.r + r)) return true;
        return false;
    }
}

public static class Rig
{
    public static Animation Setup(GameObject root, bool armsAim)
    {
        var anim = root.GetComponentInChildren<Animation>();
        if (!anim) return null;
        anim.cullingType = AnimationCullingType.BasedOnRenderers;
        foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
        if (anim["die"] != null) anim["die"].wrapMode = WrapMode.ClampForever;
        if (anim["attack-melee-right"] != null) { anim["attack-melee-right"].wrapMode = WrapMode.Once; anim["attack-melee-right"].layer = 1; }
        var aim = anim["holding-both-shoot"];
        if (aim != null && armsAim)
        {
            aim.layer = 1;
            foreach (var t in anim.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("arm-")) aim.AddMixingTransform(t, true);
            aim.weight = 1; aim.enabled = true;
        }
        anim.Play("walk");
        return anim;
    }
}

public class Enemy
{
    public EType type; public EDef def;
    public Transform t, model;
    public Animation anim;
    public Renderer[] rends;
    public MaterialPropertyBlock mpb;
    public float hp, maxHp, speed, dmg, radius, atkCd, flash, dieT, rise, special, special2, slow;
    public Vector3 knock;
    public bool alive;
    public int id;
    public float baseY;
    // animation names (Kenney minis vs KayKit Rig_Medium) and rigid-prop motion
    public string cWalk = "walk", cDie = "die", cAtk = "attack-melee-right", cRise, cHit;
    public float hitCd, phase;
    public bool proc;
}

public class Horde : MonoBehaviour
{
    public static Horde I;
    public readonly List<Enemy> Alive = new List<Enemy>();
    readonly List<Enemy> dying = new List<Enemy>();
    readonly Dictionary<EType, Stack<Enemy>> pool = new Dictionary<EType, Stack<Enemy>>();
    readonly Dictionary<long, List<Enemy>> grid = new Dictionary<long, List<Enemy>>();
    const float Cell = 2f;
    int nextId;
    public int Kills;
    public Enemy Boss;
    float spawnAcc, eventT;
    int nextEvent;
    public bool Attract;       // menu mode: wander, never hurt
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake() => I = this;

    static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    void RebuildGrid()
    {
        foreach (var l in grid.Values) l.Clear();
        foreach (var e in Alive)
        {
            var k = Key(Mathf.FloorToInt(e.t.position.x / Cell), Mathf.FloorToInt(e.t.position.z / Cell));
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<Enemy>();
            l.Add(e);
        }
    }

    readonly List<Enemy> nearBuf = new List<Enemy>();
    public List<Enemy> Near(Vector3 p, float r)
    {
        nearBuf.Clear();
        int x0 = Mathf.FloorToInt((p.x - r) / Cell), x1 = Mathf.FloorToInt((p.x + r) / Cell);
        int z0 = Mathf.FloorToInt((p.z - r) / Cell), z1 = Mathf.FloorToInt((p.z + r) / Cell);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                if (grid.TryGetValue(Key(x, z), out var l))
                    foreach (var e in l)
                    {
                        if (!e.alive || e.rise > 0) continue;
                        var d = e.t.position - p; d.y = 0;
                        float rr = r + e.radius;
                        if (d.sqrMagnitude < rr * rr) nearBuf.Add(e);
                    }
        return nearBuf;
    }

    public Enemy Nearest(Vector3 p, float maxR)
    {
        Enemy best = null; float bd = maxR * maxR;
        foreach (var e in Alive)
        {
            if (e.rise > 0.2f) continue;
            var d = e.t.position - p; d.y = 0;
            float m = d.sqrMagnitude;
            if (m < bd) { bd = m; best = e; }
        }
        return best;
    }

    public Enemy Random(Vector3 p, float maxR)
    {
        int n = 0; Enemy pick = null;
        foreach (var e in Alive)
        {
            if (e.rise > 0.2f) continue;
            var d = e.t.position - p; d.y = 0;
            if (d.sqrMagnitude < maxR * maxR && UnityEngine.Random.Range(0, ++n) == 0) pick = e;
        }
        return pick;
    }

    // ---------------------------------------------------------------- spawn / pool
    public Enemy Spawn(EType type, Vector3 pos, bool rise = true)
    {
        var def = Defs.E[type];
        if (!pool.TryGetValue(type, out var st)) pool[type] = st = new Stack<Enemy>();
        Enemy e;
        if (st.Count > 0) { e = st.Pop(); e.t.gameObject.SetActive(true); }
        else
        {
            e = new Enemy { type = type, def = def, mpb = new MaterialPropertyBlock() };
            GameObject go;
            if (def.kay != null)
            {
                var variant = def.kay[UnityEngine.Random.Range(0, def.kay.Length)];
                go = KayRig.Create(variant, def.height, transform, out e.anim, out var sc, def.weaponR, def.weaponL);
                def.scale = sc;
                e.cWalk = def.speed >= 2.5f ? "Running_A" : "Walking_A";
                e.cDie = "Death_A"; e.cAtk = "Throw"; e.cRise = "Spawn_Ground"; e.cHit = "Hit_A";
            }
            else if (def.proc)
            {
                go = Kit.Spawn(def.model, 1f, transform);
                var b = Kit.WorldBounds(go);
                def.scale = def.height / Mathf.Max(0.01f, b.size.y);
                e.proc = true;
                if (def.boss)
                {
                    var glow = new GameObject("glow").AddComponent<SpriteRenderer>();
                    glow.sprite = Fx.Glow; glow.color = Kit.A(Kit.Hex("#FF8C42"), 0.45f);
                    glow.transform.SetParent(go.transform, false); glow.transform.localPosition = new Vector3(0, 0.05f, 0);
                    glow.transform.localRotation = Quaternion.Euler(90, 0, 0); glow.transform.localScale = Vector3.one * 1.6f;
                }
            }
            else go = Kit.Spawn(def.model, def.scale, transform);
            e.t = go.transform;
            e.model = go.transform.GetChild(0);
            if (def.kay == null && !def.proc) e.anim = Rig.Setup(go, false);
            e.baseY = e.model.localPosition.y;
            e.rends = go.GetComponentsInChildren<Renderer>();
            foreach (var r in e.rends) r.shadowCastingMode = def.boss ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.35f), def.radius * 2.4f / def.scale, go.transform, Vector3.zero, 0.02f);
        }
        float t = Game.I.RunTime;
        float hpMult = 1f + t / 60f * 0.5f;
        e.id = ++nextId;
        e.maxHp = e.hp = def.hp * hpMult * (def.boss ? 1f + Game.I.Loop * 0.5f : 1f);
        e.speed = def.speed * UnityEngine.Random.Range(0.9f, 1.1f);
        e.dmg = def.dmg * (1f + t / 60f * 0.12f);
        e.radius = def.radius;
        e.atkCd = 0.5f; e.flash = 0; e.special = 3f; e.special2 = 6f; e.knock = Vector3.zero; e.slow = 0;
        e.alive = true; e.dieT = 0;
        bool kayRise = rise && e.cRise != null;
        e.rise = rise ? (kayRise ? 1.05f : 0.6f) : 0f;
        e.hitCd = 0; e.phase = UnityEngine.Random.value * 10f;
        pos.y = 0;
        e.t.position = pos;
        e.t.localScale = Vector3.one * def.scale;
        e.model.localScale = Vector3.one;
        e.model.localPosition = new Vector3(e.model.localPosition.x, e.baseY + (rise && !kayRise ? -0.95f : 0), e.model.localPosition.z);
        SetColor(e, def.tint);
        if (e.anim)
        {
            if (kayRise) { e.anim.Play(e.cRise); e.anim[e.cRise].speed = 1.25f; }
            else e.anim.Play(e.cWalk);
            e.anim[e.cWalk].speed = def.boss ? 0.6f : 1f;
        }
        if (def.boss) Boss = e;
        if (rise) Fx.I.Dirt(pos);
        Alive.Add(e);
        return e;
    }

    void SetColor(Enemy e, Color c)
    {
        // material-coloured props (KayKit pumpkins) must keep their own colours: clear the override instead of painting white
        if (e.proc && c == e.def.tint) { foreach (var r in e.rends) r.SetPropertyBlock(null); return; }
        e.mpb.SetColor(ColorId, c);
        foreach (var r in e.rends) r.SetPropertyBlock(e.mpb);
    }

    public void Damage(Enemy e, float dmg, Vector3 dir, float knock, bool quiet = false)
    {
        if (!e.alive || e.rise > 0.3f) return;
        e.hp -= dmg;
        e.flash = 0.09f;
        if (knock > 0 && !e.def.boss) { dir.y = 0; e.knock += dir.normalized * knock; }
        bool crit = dmg >= 40;
        if (!quiet || crit) Fx.I.DamageNumber(e.t.position + Vector3.up * (1.3f * e.def.scale / 1.45f), dmg, crit);   // AoE ticks stay silent
        if (!quiet) Sfx.I.Hit();
        if (e.hp <= 0) Kill(e);
        else if (e.cHit != null && e.anim && !e.def.boss && e.hitCd <= 0) { e.hitCd = 0.5f; e.anim.CrossFade(e.cHit, 0.04f, PlayMode.StopSameLayer); }
    }

    void Kill(Enemy e)
    {
        e.alive = false;
        Alive.Remove(e);
        Kills++;
        e.dieT = e.def.boss ? 2.2f : e.cRise != null ? 1.25f : 1.0f;
        if (e.anim)
        {
            if (e.anim[e.cAtk] != null) e.anim.Stop(e.cAtk);
            if (e.cHit != null) e.anim.Stop(e.cHit);
            e.anim.CrossFade(e.cDie, 0.08f);
        }
        SetColor(e, e.def.tint);
        dying.Add(e);
        var p = e.t.position;
        Pickups.I.DropXp(p, e.def.xp);
        if (UnityEngine.Random.value < (e.def.boss ? 1f : 0.035f)) Pickups.I.DropCoins(p, e.def.boss ? 25 : 1);
        if (!e.def.boss && UnityEngine.Random.value < 0.006f) Pickups.I.DropPotion(p);
        // oil: tough undead often carry some, the rabble rarely
        float oil = e.def.boss ? 1f : (e.type == EType.Brute || e.type == EType.Mage || e.type == EType.Vampire) ? 0.14f : 0.012f;
        if (UnityEngine.Random.value < oil) Pickups.I.DropOil(p + new Vector3(0.6f, 0, 0.4f));
        if (Spooky.On && !Attract)
        {
            float c = e.def.boss ? 1f : e.type == EType.Pumpkin ? 0.3f : 0.055f;
            if (UnityEngine.Random.value < c) Pickups.I.DropCandy(p, e.def.boss ? 20 : 1);
        }
        Fx.I.Poof(p + Vector3.up * 0.5f, e.def.ghost ? Kit.Hex("#9FE7FF") : Kit.Hex("#7CFFB2"), e.def.boss ? 3f : 1f);
        Sfx.I.EnemyDie(e.def.boss);
        if (e.def.boss)
        {
            Boss = null;
            Pickups.I.DropChest(p);
            Game.I.BossDefeated(e);
        }
    }

    public void Clear()
    {
        foreach (var e in Alive) Release(e);
        foreach (var e in dying) Release(e);
        Alive.Clear(); dying.Clear();
        Boss = null; Kills = 0; spawnAcc = 0; nextEvent = 0;
    }

    void Release(Enemy e)
    {
        e.alive = false;
        e.t.gameObject.SetActive(false);
        pool[e.type].Push(e);
    }

    // ---------------------------------------------------------------- per frame
    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        RebuildGrid();
        var hero = Game.I.Hero;
        var hp = hero ? hero.transform.position : Vector3.zero;

        if (Game.I.State == Game.S.Playing) Director(dt);

        for (int i = Alive.Count - 1; i >= 0; i--)
        {
            if (i >= Alive.Count) continue;
            var e = Alive[i];
            var pos = e.t.position;

            if (e.hitCd > 0) e.hitCd -= dt;
            if (e.rise > 0)
            {
                e.rise -= dt;
                if (e.cRise != null)
                {
                    // KayKit skeletons claw their own way out of the dirt
                    if (e.rise <= 0 && e.anim) e.anim.CrossFade(e.cWalk, 0.15f);
                    if (UnityEngine.Random.value < dt * 6f) Fx.I.Dirt(pos);
                    continue;
                }
                float k = 1f - Mathf.Clamp01(e.rise / 0.6f);
                var mp = e.model.localPosition; mp.y = e.baseY + Mathf.Lerp(-0.95f, 0, k * k); e.model.localPosition = mp;
                continue;
            }

            Vector3 target = Attract ? new Vector3(Mathf.Sin(Time.time * 0.3f + e.id) * 8f, 0, Mathf.Cos(Time.time * 0.27f + e.id * 1.7f) * 8f) : hp;
            var to = target - pos; to.y = 0;
            float dist = to.magnitude;
            var dir = dist > 0.01f ? to / dist : Vector3.forward;
            float spd = e.speed * (e.slow > 0 ? 0.55f : 1f);
            e.slow -= dt;

            if (e.def.boss && !Attract) BossBrain(e, dir, dist, dt, ref spd);
            if (e.type == EType.Mage && !Attract) MageBrain(e, dir, dist, dt, ref spd);
            if (e.proc)
            {
                // rigid models: pumpkins hop, the king lumbers and rocks side to side
                e.phase += dt * (e.def.boss ? 2.2f : 7f);
                var mp = e.model.localPosition;
                if (e.def.boss) { mp.y = e.baseY + Mathf.Abs(Mathf.Sin(e.phase)) * 0.08f; e.model.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(e.phase) * 6f); }
                else
                {
                    float hop = Mathf.Abs(Mathf.Sin(e.phase));
                    mp.y = e.baseY + hop * 0.55f;
                    e.model.localScale = new Vector3(1f + (1f - hop) * 0.15f, 1f - (1f - hop) * 0.18f, 1f + (1f - hop) * 0.15f);
                    spd *= 0.4f + hop * 1.2f;   // lunges while airborne
                }
                e.model.localPosition = mp;
            }

            // separation from neighbours
            Vector3 push = Vector3.zero;
            var neigh = Near(pos, e.radius + 0.6f);
            foreach (var o in neigh)
            {
                if (o == e) continue;
                var off = pos - o.t.position; off.y = 0;
                float d = off.magnitude, min = e.radius + o.radius;
                if (d < min && d > 0.001f) push += off / d * (min - d) * (o.def.boss ? 2f : 1f);
            }

            var move = dir * spd;
            pos += (move + e.knock) * dt + push * 0.6f;
            e.knock *= Mathf.Exp(-dt * 8f);
            if (!e.def.ghost) pos = Obstacles.Resolve(pos, e.radius * 0.8f);
            float ar = Defs.ArenaR + 3f;
            if (pos.magnitude > ar) pos = pos.normalized * ar;
            pos.y = 0;
            e.t.position = pos;

            if (dist > 0.05f)
            {
                var rot = Quaternion.LookRotation(dir);
                e.t.rotation = Quaternion.RotateTowards(e.t.rotation, rot, dt * 540f);
            }
            if (e.def.ghost)
            {
                var mp = e.model.localPosition; mp.y = e.baseY + 0.12f + Mathf.Sin(Time.time * 3f + e.id) * 0.06f; e.model.localPosition = mp;
            }

            // melee
            e.atkCd -= dt;
            if (!Attract && hero && dist < e.radius + 0.5f && e.atkCd <= 0)
            {
                e.atkCd = e.def.boss ? 1.2f : 0.9f;
                if (e.anim && e.anim[e.cAtk] != null) e.anim.CrossFade(e.cAtk, 0.05f, PlayMode.StopSameLayer);
                hero.Hurt(e.dmg);
            }

            // hit flash
            if (e.flash > 0)
            {
                e.flash -= dt;
                SetColor(e, e.flash > 0 ? new Color(2.4f, 2.4f, 2.4f) : e.def.tint);
            }
        }

        // death animations, then sink into the dirt
        for (int i = dying.Count - 1; i >= 0; i--)
        {
            var e = dying[i];
            e.dieT -= dt;
            if (e.proc) { e.model.localScale = Vector3.one * Mathf.Clamp01(e.dieT / (e.def.boss ? 2.2f : 1f)); e.model.Rotate(0, dt * 400f, 0); }
            if (e.dieT < 0.45f)
            {
                var mp = e.model.localPosition; mp.y -= dt * 2.2f / e.def.scale; e.model.localPosition = mp;
            }
            if (e.dieT <= 0) { dying.RemoveAt(i); Release(e); }
        }
    }

    // ---------------------------------------------------------------- bosses
    void BossBrain(Enemy e, Vector3 dir, float dist, float dt, ref float spd)
    {
        e.special -= dt;
        e.special2 -= dt;
        var hero = Game.I.Hero;
        switch (e.type)
        {
            case EType.BossZombie:
                // ground slam: telegraph then crush
                if (e.special <= 0 && dist < 9f)
                {
                    e.special = 5.5f;
                    var at = hero.transform.position;
                    Fx.I.Telegraph(at, 2.6f, 1.0f, () =>
                    {
                        Fx.I.Shockwave(at, Kit.Hex("#9CFF7A"), 6f);
                        Fx.I.Shake(0.5f);
                        Sfx.I.Slam();
                        if ((Game.I.Hero.transform.position - at).magnitude < 2.6f) Game.I.Hero.Hurt(e.dmg * 1.2f);
                    });
                    if (e.anim) e.anim.CrossFade(e.cAtk, 0.05f);
                }
                break;

            case EType.BossPumpkin:
                // pumpkin rain around the hero, then a patch of hoppers
                if (e.special <= 0 && dist < 14f)
                {
                    e.special = e.hp < e.maxHp * 0.5f ? 3.4f : 4.6f;
                    var c = hero.transform.position;
                    for (int i = 0; i < 4; i++)
                    {
                        var at = i == 0 ? c : c + new Vector3(UnityEngine.Random.Range(-4f, 4f), 0, UnityEngine.Random.Range(-4f, 4f));
                        Fx.I.Telegraph(at, 1.8f, 1.0f + i * 0.18f, () =>
                        {
                            Fx.I.Shockwave(at, Kit.Hex("#FF8C42"), 4f);
                            Fx.I.Poof(at + Vector3.up * 0.4f, Kit.Hex("#FF8C42"), 1.4f);
                            Sfx.I.Slam();
                            if ((Game.I.Hero.transform.position - at).magnitude < 1.8f) Game.I.Hero.Hurt(e.dmg * 0.8f);
                        });
                    }
                    Fx.I.Shake(0.25f);
                }
                if (e.special2 <= 0)
                {
                    e.special2 = 9f;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        Spawn(EType.Pumpkin, e.t.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.6f, false);
                    }
                    Sfx.I.Roar();
                }
                break;

            case EType.BossVampire:
                // blood dash + summon skeleton guard
                if (e.special2 <= 0)
                {
                    e.special2 = 9f;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        Spawn(EType.Skeleton, e.t.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.5f);
                    }
                    Sfx.I.Roar();
                }
                if (e.special <= 0 && dist < 11f)
                {
                    e.special = 4.2f;
                    e.special2 = Mathf.Max(e.special2, 1.5f);
                    e.knock = dir * 16f;   // lunge
                    Fx.I.Shockwave(e.t.position, Kit.Hex("#FF4D6D"), 3f);
                    Sfx.I.Dash();
                }
                break;

            case EType.BossOrc:
                spd *= e.hp < e.maxHp * 0.5f ? 1.35f : 1f;   // enrages at half health
                if (e.special <= 0 && dist < 10f)
                {
                    e.special = e.hp < e.maxHp * 0.5f ? 3.2f : 4.5f;
                    var at = hero.transform.position;
                    Fx.I.Telegraph(at, 3.2f, 0.9f, () =>
                    {
                        Fx.I.Shockwave(at, Kit.Hex("#FFB347"), 7f);
                        Fx.I.Shake(0.6f);
                        Sfx.I.Slam();
                        if ((Game.I.Hero.transform.position - at).magnitude < 3.2f) Game.I.Hero.Hurt(e.dmg);
                    });
                    if (e.anim) e.anim.CrossFade(e.cAtk, 0.05f);
                }
                if (e.special2 <= 0)
                {
                    e.special2 = 10f;
                    for (int i = 0; i < 4; i++) Spawn(EType.Brute, e.t.position + UnityEngine.Random.insideUnitSphere.normalized * 3f);
                    Sfx.I.Roar();
                }
                break;
        }
    }

    // Bone mages keep their distance and curse the ground under the hero.
    void MageBrain(Enemy e, Vector3 dir, float dist, float dt, ref float spd)
    {
        if (dist < 6f) spd = -spd * 0.6f;          // back off
        else if (dist < 8.5f) spd *= 0.15f;        // hold range
        e.special -= dt;
        if (e.special <= 0 && dist < 11f)
        {
            e.special = UnityEngine.Random.Range(3.4f, 4.6f);
            var at = Game.I.Hero.transform.position;
            float dmg = e.dmg;
            if (e.anim) e.anim.CrossFade("Use_Item", 0.05f, PlayMode.StopSameLayer);
            Fx.I.Telegraph(at, 1.6f, 1.1f, () =>
            {
                Fx.I.Shockwave(at, Kit.Hex("#B07CFF"), 3.2f);
                if ((Game.I.Hero.transform.position - at).magnitude < 1.6f) Game.I.Hero.Hurt(dmg);
            });
        }
    }

    // ---------------------------------------------------------------- wave director
    void Director(float dt)
    {
        float t = Game.I.RunTime;
        int maxAlive = Mathf.Min(26 + (int)(t * 0.19f), 115);
        float rate = 0.7f + t / 60f * 1.15f;   // gentle first minutes, relentless by the end
        spawnAcc += rate * dt;
        while (spawnAcc >= 1f && Alive.Count < maxAlive)
        {
            spawnAcc -= 1f;
            Spawn(PickType(t), SpawnPoint(13f, 17f));
        }
        if (spawnAcc > 3f) spawnAcc = 3f;

        if (beats == null)
        {
            var list = new List<(float time, System.Action act)>
            {
                (100f, () => Ring(EType.Zombie, 28, 11f, "THE DEAD RISE")),
                (180f, () => BossArrives(EType.BossZombie)),
                (260f, () => Ring(EType.Skeleton, 32, 12f, "BONE STORM")),
                (330f, () => Ring(EType.Ghost, 24, 10f, "WAILING HOUR")),
                (360f, () => BossArrives(EType.BossVampire)),
                (420f, () => BossArrives(EType.BossOrc)),
            };
            if (Spooky.On)
            {
                list.Add((140f, () => Ring(EType.Pumpkin, 22, 10f, "PUMPKIN PATCH")));
                list.Add((235f, () => BossArrives(EType.BossPumpkin)));
            }
            list.Sort((a, b) => a.time.CompareTo(b.time));
            beats = list.ToArray();
        }
        if (nextEvent < beats.Length && t >= beats[nextEvent].time) { beats[nextEvent].act(); nextEvent++; }
    }

    (float time, System.Action act)[] beats;

    EType PickType(float t)
    {
        float z = 1f, s = t > 40 ? 0.7f : 0, g = t > 110 ? 0.45f : 0, v = t > 200 ? 0.22f : 0, b = t > 250 ? 0.16f : 0;
        float m = t > 150 ? 0.16f : 0, pk = Spooky.On && t > 60 ? 0.35f : 0;
        float r = UnityEngine.Random.value * (z + s + g + v + b + m + pk);
        if ((r -= m) < 0) return EType.Mage;
        if ((r -= pk) < 0) return EType.Pumpkin;
        if ((r -= z) < 0) return EType.Zombie;
        if ((r -= s) < 0) return EType.Skeleton;
        if ((r -= g) < 0) return EType.Ghost;
        if ((r -= v) < 0) return EType.Vampire;
        return EType.Brute;
    }

    Vector3 SpawnPoint(float min, float max)
    {
        var hp = Game.I.Hero.transform.position;
        for (int i = 0; i < 12; i++)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2f, r = UnityEngine.Random.Range(min, max);
            var p = hp + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
            if (p.magnitude < Defs.ArenaR && !Obstacles.Blocked(p, 0.5f)) return p;
        }
        var fallback = -hp.normalized * (Defs.ArenaR - 2f);
        return hp.sqrMagnitude < 0.01f ? new Vector3(0, 0, Defs.ArenaR - 2f) : fallback;
    }

    void Ring(EType type, int n, float r, string banner)
    {
        var hp = Game.I.Hero.transform.position;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            var p = hp + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
            if (p.magnitude > Defs.ArenaR) p = p.normalized * (Defs.ArenaR - 1f);
            Spawn(type, p);
        }
        UI.I.Banner(banner, Kit.Hex("#9CFF7A"));
        Sfx.I.Roar();
    }

    public void DevBoss(EType type) => BossArrives(type);

    void BossArrives(EType type)
    {
        Spawn(type, SpawnPoint(11f, 13f));
        UI.I.Banner(Defs.E[type].name + " AWAKENS", Kit.Hex("#FF4D6D"));
        Sfx.I.Roar();
        Fx.I.Shake(0.4f);
    }
}
