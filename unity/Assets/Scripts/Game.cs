using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// GRAVE SHIFT — survive the night shift in a haunted graveyard until dawn.
public class Game : MonoBehaviour
{
    public static Game I;
    public enum S { Menu, Playing, LevelUp, Paused, Dead, Won }
    public S State = S.Menu;

    public SaveData Save;
    public Camera Cam;
    public Hero Hero;
    public float RunTime;
    public int Level = 1, Xp, RunGold, Loop, RunCandy;
    public bool AutoPlay, Dev;
    int pendingLevels, runKills;
    bool revived;
    Light moon;
    Transform map;

    // ---- stats: run upgrades x permanent shop
    Arsenal A => Arsenal.I;
    public float BaseHp => 100 + 10 * Save.shop[0];
    public float DmgMult => (1f + 0.06f * Save.shop[1]) * (1f + 0.12f * A.Level(Up.Skull));
    public float CdMult => Mathf.Pow(0.92f, A.Level(Up.Candle));
    public float MoveSpeed => 5f * (1f + 0.04f * Save.shop[2]) * (1f + 0.1f * A.Level(Up.Boots));
    public float MagnetR => 2.9f * (1f + 0.12f * Save.shop[3]) * (1f + 0.4f * A.Level(Up.Magnet));
    public float ArmorMult => Mathf.Pow(0.92f, A.Level(Up.Armor));
    public float GoldMult => 1f + 0.12f * Save.shop[4];

    // ======================================================================
    void Awake()
    {
        I = this;
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 30f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.antiAliasing = 2;
        QualitySettings.pixelLightCount = 0;

        Load();
        var url = Application.absoluteURL;
        Dev = url.Contains("dev=1") && (url.Contains("://localhost") || url.Contains("://127.0.0.1"));   // cheats never on the live site
        DevCam.Install(Dev);
        AutoPlay = url.Contains("bot=1");
        if (Dev && url.Contains("fresh=1")) Save = new SaveData();
        if (Dev && url.Contains("rich=1")) Save.candy = Mathf.Max(Save.candy, 999);
        var sm = System.Text.RegularExpressions.Regex.Match(url, @"speed=(\d+)");
        if (Dev && sm.Success) Time.timeScale = Mathf.Clamp(int.Parse(sm.Groups[1].Value), 1, 8);

        gameObject.AddComponent<Sfx>();
        Sfx.I.SetMuted(Save.muted);
        new GameObject("WebBridge").AddComponent<WebBridge>();

        Cam = Camera.main;
        Cam.fieldOfView = 33f; Cam.nearClipPlane = 1f; Cam.farClipPlane = 90f;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = Kit.Hex("#0b0a17");

        moon = new GameObject("Moon").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.transform.rotation = Quaternion.Euler(58, 35, 0);
        moon.color = Kit.Hex("#cdd3ff"); moon.intensity = 0.85f;   // cool, but neutral enough that Kenney oranges/greens stay true
        moon.shadows = LightShadows.Hard; moon.shadowStrength = 0.65f; moon.shadowBias = 0.05f; moon.shadowNormalBias = 0.35f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Kit.Hex("#46506e");
        RenderSettings.ambientEquatorColor = Kit.Hex("#2f3542");   // no magenta: bones stay bone-white, dirt stays brown
        RenderSettings.ambientGroundColor = Kit.Hex("#15101f");
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Kit.Hex("#0b0a17"); RenderSettings.fogStartDistance = 24f; RenderSettings.fogEndDistance = 52f;

        Fx.InitSprites();
        new GameObject("Fx").AddComponent<Fx>();
        new GameObject("Horde").AddComponent<Horde>();
        new GameObject("Pickups").AddComponent<Pickups>();
        new GameObject("Arsenal").AddComponent<Arsenal>();
        new GameObject("UI").AddComponent<UI>().Init();

        BuildMap(1337);
        Hero = Hero.Create(transform);
        Hero.ResetRun();
        GoMenu();
        WebBridge.Ready();
    }

    // ======================================================================
    // Save
    void Load()
    {
        var json = PlayerPrefs.GetString("gs_save", "");
        Save = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        if (Save.shop == null || Save.shop.Length < 5) Save.shop = new int[5];
    }
    public void Persist() { PlayerPrefs.SetString("gs_save", JsonUtility.ToJson(Save)); PlayerPrefs.Save(); }
    public void ToggleMute() { Save.muted = !Save.muted; Sfx.I.SetMuted(Save.muted); Persist(); }

    // ======================================================================
    // Map: a walled graveyard with grave rows, crypts, pines and lamps
    void BuildMap(int seed)
    {
        Obstacles.Clear();
        map = new GameObject("Map").transform;
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        Vector3 Polar(float a, float r) => new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
        GameObject Put(string m, float s, Vector3 p, float yaw, float obstacle = 0)
        {
            var go = Kit.Spawn("Graveyard/" + m, s, map, p, yaw);
            if (obstacle > 0) Obstacles.Add(p, obstacle);
            return go;
        }
        // Solid prop whose collision follows its real footprint (measured before rotating).
        GameObject PutSolid(string m, float s, Vector3 p, float yaw)
        {
            var go = Kit.Spawn("Graveyard/" + m, s, map, p, 0);
            var b = Kit.WorldBounds(go);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Obstacles.AddBox(p, yaw, b.extents.x * 0.92f, b.extents.z * 0.92f);
            return go;
        }

        // ground
        var ground = Kit.MeshObject("Ground", Kit.BuildQuad(100f, 14f));
        ground.transform.SetParent(map, false);
        ground.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var gm = new Material(Shader.Find("Standard")) { mainTexture = Kit.Noise(256, Kit.Hex("#1f2a1d"), Kit.Hex("#3b4a2c"), 0.05f, 7, 0) };
        gm.SetFloat("_Glossiness", 0.02f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = gm;
        // worn dirt clearing at the centre
        Kit.FloorQuad("clearing", Kit.Glow, Kit.A(Kit.Hex("#4a3b2c"), 0.8f), 16f, map, Vector3.zero, 0.01f);

        // iron fence ring (the arena wall) with a gate to the south
        float fr = Defs.ArenaR + 0.4f;
        int segs = Mathf.CeilToInt(2 * Mathf.PI * fr / 1.55f);
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f;
            var p = Polar(a, fr);
            bool gate = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f)) < 4f;
            Put(gate ? "iron-fence-damaged" : (i % 7 == 3 ? "iron-fence-damaged" : "iron-fence"), 1.6f, p, -a * Mathf.Rad2Deg + 90f);
        }
        // forest outside the fence
        for (int i = 0; i < 90; i++)
        {
            float a = R() * Mathf.PI * 2f, r = fr + 2f + R() * 14f;
            Put(R() < 0.3f ? "pine-crooked" : "pine", 2.0f + R() * 1.4f, Polar(a, r), R() * 360f);
        }

        // grave rows in little sections
        string[] stones = { "gravestone-round", "gravestone-cross", "gravestone-bevel", "gravestone-broken", "gravestone-decorative", "gravestone-wide", "cross", "cross-wood" };
        for (int c = 0; c < 13; c++)
        {
            float a = R() * Mathf.PI * 2f, r = 7f + R() * 19f;
            var center = Polar(a, r);
            float yaw = R() * 360f;
            var q = Quaternion.Euler(0, yaw, 0);
            int rows = 2 + rng.Next(3), cols = 3 + rng.Next(3);
            for (int ri = 0; ri < rows; ri++)
                for (int ci = 0; ci < cols; ci++)
                {
                    if (R() < 0.15f) continue;
                    var p = center + q * new Vector3((ci - (cols - 1) * 0.5f) * 1.9f, 0, (ri - (rows - 1) * 0.5f) * 2.4f);
                    if (p.magnitude > Defs.ArenaR - 2f || p.magnitude < 5f) continue;
                    PutSolid(stones[rng.Next(stones.Length)], 1.15f + R() * 0.3f, p, yaw + 180f + (R() - 0.5f) * 20f);
                    if (R() < 0.18f) Kit.Tint(Put("grave", 1.1f, p + q * new Vector3(0, 0, -0.8f), yaw), new Color(0.62f, 0.5f, 0.42f));   // fresh earth, not pink
                }
            if (R() < 0.5f) Put("fire-basket", 1.8f, center + q * new Vector3((cols * 0.5f + 1f) * 1.9f, 0, 0), 0, 0.3f).AddComponent<Flicker>();
        }

        // crypts
        string[] crypts = { "crypt", "crypt-small", "crypt-large" };
        for (int i = 0; i < 6; i++)
        {
            float a = (i / 6f + R() * 0.08f) * Mathf.PI * 2f, r = 12f + R() * 13f;
            var p = Polar(a, r);
            var go = Put(crypts[rng.Next(crypts.Length)], 2.3f, p, Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg);
            var b = Kit.WorldBounds(go);
            Obstacles.Add(p, Mathf.Max(b.extents.x, b.extents.z) * 0.95f);
        }

        // trees, lamps, and clutter
        for (int i = 0; i < 26; i++)
        {
            var p = Polar(R() * Mathf.PI * 2f, 6f + R() * 23f);
            if (Obstacles.Blocked(p, 1.2f)) continue;
            Put(R() < 0.5f ? "pine" : "pine-crooked", 1.8f + R() * 0.9f, p, R() * 360f, 0.55f);
        }
        for (int i = 0; i < 12; i++)
        {
            var p = Polar(i / 12f * Mathf.PI * 2f + R() * 0.2f, 9f + (i % 3) * 7f);
            if (Obstacles.Blocked(p, 0.8f)) continue;
            Put(R() < 0.5f ? "lightpost-single" : "lightpost-double", 2.0f, p, R() * 360f, 0.25f);
            Kit.FloorQuad("lamp", Kit.Glow, Kit.A(Kit.Hex("#ffcf7a"), 0.32f), 7f, map, p, 0.02f);
        }
        string[] clutter = { "pumpkin", "pumpkin-carved", "pumpkin-tall-carved", "rocks", "debris", "trunk", "coffin", "coffin-old", "candle-multiple", "urn-round", "shovel-dirt", "rocks-tall" };
        for (int i = 0; i < 70; i++)
        {
            var p = Polar(R() * Mathf.PI * 2f, 4f + R() * 25f);
            if (Obstacles.Blocked(p, 0.9f)) continue;
            var m = clutter[rng.Next(clutter.Length)];
            bool solid = m == "rocks-tall" || m == "trunk";
            var go = Put(m, 1.15f + R() * 0.35f, p, R() * 360f, solid ? 0.4f : 0);
            if (m.Contains("carved"))
                Kit.FloorQuad("pglow", Kit.Glow, Kit.A(Kit.Hex("#FF8C42"), 0.35f), 2.6f, map, p, 0.03f);
        }
        // candle circle altar at the centre
        Put("altar-stone", 1.8f, new Vector3(0, 0, 3.2f), 180f, 0.6f);
        for (int i = 0; i < 8; i++) Put("candle-multiple", 1.4f, Polar(i / 8f * Mathf.PI * 2f, 2.6f) + new Vector3(0, 0, 3.2f), R() * 360f);
        Kit.FloorQuad("altarGlow", Kit.Glow, Kit.A(Kit.Hex("#ffb56b"), 0.3f), 7f, map, new Vector3(0, 0, 3.2f), 0.02f);

        if (Spooky.On) SpookyDecor(rng);

        // merge the static set into a handful of draw calls
        StaticBatchingUtility.Combine(map.gameObject);
    }

    // Spooktober dressing: lit jack-o'-lanterns, a bubbling cauldron at the altar, open coffins, candy buckets.
    void SpookyDecor(System.Random rng)
    {
        float R() => (float)rng.NextDouble();
        GameObject Prop(string m, float h, Vector3 p, float yaw, float obstacle)
        {
            var go = Kit.Spawn("Spooky/" + m, 1f, map, p, yaw);
            var b = Kit.WorldBounds(go);
            go.transform.localScale = Vector3.one * (h / Mathf.Max(0.01f, b.size.y));
            if (obstacle > 0) Obstacles.Add(p, obstacle);
            return go;
        }
        // cauldron on the altar's left, candy bucket on the right
        Prop("cauldron", 1.1f, new Vector3(-2.2f, 0, 4.4f), 30, 0.55f);
        Kit.FloorQuad("cauldronGlow", Kit.Glow, Kit.A(Kit.Hex("#7CFF6B"), 0.4f), 4.5f, map, new Vector3(-2.2f, 0, 4.4f), 0.03f);
        Prop("candyBucket", 0.7f, new Vector3(2.2f, 0, 4.4f), -30, 0.35f);
        for (int i = 0; i < 26; i++)
        {
            var p = new Vector3(Mathf.Cos(R() * 6.283f), 0, Mathf.Sin(R() * 6.283f)) * (5f + R() * 23f);
            p = Quaternion.Euler(0, R() * 360f, 0) * p;
            if (Obstacles.Blocked(p, 1.0f)) continue;
            bool big = i % 3 == 0;
            Prop(big ? "jackolantern_big" : "jackolantern_small", big ? 0.95f : 0.6f, p, R() * 360f, big ? 0.45f : 0);
            Kit.FloorQuad("jackGlow", Kit.Glow, Kit.A(Kit.Hex("#FF9A3C"), 0.42f), big ? 3.4f : 2.4f, map, p, 0.03f);
        }
        for (int i = 0; i < 6; i++)
        {
            var p = new Vector3(Mathf.Cos(i * 1.047f + 0.4f), 0, Mathf.Sin(i * 1.047f + 0.4f)) * (10f + R() * 14f);
            if (Obstacles.Blocked(p, 1.3f)) continue;
            bool a = R() < 0.5f;
            float yaw = R() * 360f;
            Prop(a ? "coffinA_bottom" : "coffinB_bottom", 0.45f, p, yaw, 0.6f);
            var lid = Prop(a ? "coffinA_top" : "coffinB_top", 0.18f, p + Quaternion.Euler(0, yaw, 0) * new Vector3(0.9f, 0, 0), yaw + 25f, 0);
        }
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3(Mathf.Cos(R() * 6.283f), 0, Mathf.Sin(R() * 6.283f)) * (8f + R() * 20f);
            if (Obstacles.Blocked(p, 1.0f)) continue;
            Prop(new[] { "treeA_graveyard", "treeB_graveyard", "treeC_graveyard", "treeD_graveyard" }[i % 4], 3.2f + R() * 1.6f, p, R() * 360f, 0.35f);
        }
        for (int i = 0; i < 10; i++)
        {
            var p = new Vector3(Mathf.Cos(R() * 6.283f), 0, Mathf.Sin(R() * 6.283f)) * (6f + R() * 20f);
            if (Obstacles.Blocked(p, 0.6f)) continue;
            Prop(i % 2 == 0 ? "candleBundle" : "pumpkinSmall", i % 2 == 0 ? 0.55f : 0.45f, p, R() * 360f, 0);
        }
    }

    // ======================================================================
    // Flow
    public void GoMenu()
    {
        State = S.Menu;
        Time.timeScale = Dev && Application.absoluteURL.Contains("speed=") ? Time.timeScale : 1f;
        Horde.I.Clear(); Pickups.I.Clear(); Arsenal.I.ResetRun(); Fx.I.ClearAll();
        Hero.ResetRun();
        Hero.ApplyHat(Save.hat);
        Horde.I.Attract = true;
        for (int i = 0; i < 7; i++)
            Horde.I.Spawn(i % 3 == 0 ? EType.Skeleton : EType.Zombie, new Vector3(Mathf.Cos(i) * 7f, 0, Mathf.Sin(i) * 7f), false);
        UI.I.ShowHud(false);
        UI.I.ShowMenu();
        WebBridge.Gameplay(false);
        if (AutoPlay) StartCoroutine(AutoStart());
    }

    IEnumerator DevBossSoon() { yield return new WaitForSeconds(3f); Horde.I.DevBoss(EType.BossPumpkin); }

    IEnumerator AutoStart() { yield return new WaitForSecondsRealtime(1.5f); if (State == S.Menu) StartRun(); }

    public void StartRun()
    {
        UI.I.CloseScreens();
        Horde.I.Clear(); Pickups.I.Clear(); Arsenal.I.ResetRun(); Fx.I.ClearAll();
        Horde.I.Attract = false;
        Hero.ResetRun();
        RunTime = 0; Level = 1; Xp = 0; RunGold = 0; RunCandy = 0; pendingLevels = 0; revived = false;
        A.Lv[Up.Blaster] = 1;
        State = S.Playing;
        if (!(Dev && Application.absoluteURL.Contains("speed="))) Time.timeScale = 1f;
        UI.I.ShowHud(true);
        UI.I.Banner("SURVIVE UNTIL DAWN", Kit.Hex("#EDE6D6"));
        Sfx.I.StartMusic();
        WebBridge.Gameplay(true);
        WebBridge.Event("run_start", Save.runs);
        if (Dev && Application.absoluteURL.Contains("boss=pk")) StartCoroutine(DevBossSoon());
    }

    public void Pause()
    {
        if (State != S.Playing) return;
        State = S.Paused; Time.timeScale = 0;
        UI.I.ShowPause();
        WebBridge.Gameplay(false);
    }

    public void Resume()
    {
        State = S.Playing; Time.timeScale = 1;
        UI.I.CloseScreens();
        WebBridge.Gameplay(true);
    }

    // ---------------------------------------------------------------- xp & level ups
    public void AddXp(int v)
    {
        Xp += v;
        while (Xp >= Defs.XpForLevel(Level)) { Xp -= Defs.XpForLevel(Level); Level++; pendingLevels++; }
    }

    void OpenLevelUp()
    {
        State = S.LevelUp;
        Time.timeScale = 0;
        Sfx.I.LevelUp();
        Fx.I.Shockwave(Hero.transform.position, Kit.Hex("#7CFFB2"), 5f);
        var opts = Roll(3);
        if (opts.Count == 0) { pendingLevels = 0; Hero.Heal(30); RunGold += 5; Resume(); return; }
        if (AutoPlay) { Pick(opts[0]); return; }
        UI.I.ShowLevelUp(opts, Level, Pick);
    }

    void Pick(Up u)
    {
        A.Lv[u] = A.Level(u) + 1;
        A.OnLevel(u);
        if (u == Up.Heart) { Hero.MaxHp += 25; Hero.Heal(25); }
        pendingLevels--;
        WebBridge.Event("pick_" + u.ToString().ToLower(), A.Level(u));
        if (pendingLevels > 0) OpenLevelUp();
        else { UI.I.CloseScreens(); State = S.Playing; Time.timeScale = Dev && Application.absoluteURL.Contains("speed=") ? Mathf.Max(1, Time.timeScale) : 1; }
    }

    List<Up> Roll(int n)
    {
        var pool = new List<(Up u, float w)>();
        int weapons = 0;
        foreach (var kv in Defs.U) if (kv.Value.weapon && A.Level(kv.Key) > 0) weapons++;
        foreach (var kv in Defs.U)
        {
            int lv = A.Level(kv.Key);
            if (lv >= kv.Value.max) continue;
            if (kv.Value.weapon && lv == 0 && weapons >= 4) continue;           // weapon slots
            float w = lv > 0 ? 1.5f : kv.Value.weapon ? 1.2f : 0.8f;          // deepen builds, still offer variety
            pool.Add((kv.Key, w));
        }
        var picked = new List<Up>();
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            float total = 0; foreach (var p in pool) total += p.w;
            float r = UnityEngine.Random.value * total;
            int k = 0;
            for (; k < pool.Count - 1; k++) { r -= pool[k].w; if (r <= 0) break; }
            picked.Add(pool[k].u);
            pool.RemoveAt(k);
        }
        return picked;
    }

    public void OpenChest()
    {
        Sfx.I.Chest();
        UI.I.Banner("TREASURE!", Kit.Hex("#FFD166"));
        RunGold += 15;
        pendingLevels += 2;
        Pickups.I.VacuumAll();
        Fx.I.Shockwave(Hero.transform.position, Kit.Hex("#FFD166"), 6f);
    }

    public void BossDefeated(Enemy e)
    {
        UI.I.Banner(e.def.name + " FALLS", Kit.Hex("#FFD166"));
        Fx.I.Shake(0.8f);
        Pickups.I.VacuumAll();
        StartCoroutine(SlowMo());
        WebBridge.Event("boss_" + e.type.ToString().ToLower(), (int)RunTime);
    }

    IEnumerator SlowMo()
    {
        Time.timeScale = 0.25f;
        yield return new WaitForSecondsRealtime(0.9f);
        if (State == S.Playing) Time.timeScale = 1f;
    }

    // ---------------------------------------------------------------- end states
    public void Die()
    {
        if (State != S.Playing) return;
        State = S.Dead;
        Sfx.I.Lose();
        WebBridge.Vibrate(250);
        Fx.I.Poof(Hero.transform.position + Vector3.up, Kit.Hex("#FF4D6D"), 2.5f);
        StartCoroutine(ShowEnd(false, 1.2f));
    }

    void Win()
    {
        State = S.Won;
        Sfx.I.Win();
        UI.I.Banner("DAWN BREAKS!", Kit.Hex("#FFD166"));
        StartCoroutine(Sunrise());
        StartCoroutine(ShowEnd(true, 2.6f));
    }

    IEnumerator Sunrise()
    {
        // the sun burns the horde away, row by row
        float k = 0;
        var fog0 = RenderSettings.fogColor; var amb0 = RenderSettings.ambientSkyColor;
        while (k < 1f)
        {
            k += Time.unscaledDeltaTime / 2f;
            RenderSettings.fogColor = Color.Lerp(fog0, Kit.Hex("#f2a15a"), k);
            RenderSettings.ambientSkyColor = Color.Lerp(amb0, Kit.Hex("#ffd3a1"), k);
            Cam.backgroundColor = RenderSettings.fogColor;
            moon.color = Color.Lerp(Kit.Hex("#cdd3ff"), Kit.Hex("#ffd8a8"), k);
            var list = Horde.I.Alive.ToArray();
            for (int i = 0; i < list.Length && i < 6; i++) Horde.I.Damage(list[i], 99999, Vector3.zero, 0, true);
            yield return null;
        }
    }

    void ResetSky()
    {
        RenderSettings.fogColor = Kit.Hex("#0b0a17");
        RenderSettings.ambientSkyColor = Kit.Hex("#46506e");
        Cam.backgroundColor = Kit.Hex("#0b0a17");
        moon.color = Kit.Hex("#cdd3ff");
    }

    int bankedGold;
    public int lastCandy;
    IEnumerator ShowEnd(bool won, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;
        int earned = Mathf.RoundToInt(RunGold * GoldMult) + (won ? 50 : 0);
        Save.gold += earned; bankedGold = earned; RunGold = 0;
        Save.candy += RunCandy; lastCandy = RunCandy; RunCandy = 0;
        Save.runs++;
        if (won) Save.wins++;
        if (RunTime > Save.bestTime) Save.bestTime = Mathf.FloorToInt(RunTime);
        if (Horde.I.Kills > Save.bestKills) Save.bestKills = Horde.I.Kills;
        Persist();
        runKills = Horde.I.Kills;
        bool canRevive = !won && !revived && (WebBridge.AdsAvailable || Save.gold >= 60);
        string label = WebBridge.AdsAvailable ? "REVIVE  (WATCH AD)" : "REVIVE  (60 GOLD)";
        UI.I.ShowHud(false);
        UI.I.ShowResults(won, RunTime, runKills, Level, earned, canRevive, label);
        WebBridge.Gameplay(false);
        WebBridge.Event(won ? "run_win" : "run_death", (int)RunTime);
        if (won) ResetSkyLater();
        if (AutoPlay) { yield return new WaitForSecondsRealtime(3f); StartRun(); }
    }

    void ResetSkyLater() => StartCoroutine(ResetSkyCo());
    IEnumerator ResetSkyCo() { yield return new WaitForSecondsRealtime(0.5f); ResetSky(); }

    public void EndRun(bool won)
    {
        if (State == S.Paused) { Time.timeScale = 1; State = S.Playing; }
        UI.I.CloseScreens();
        State = S.Dead;
        StartCoroutine(ShowEnd(won, 0.1f));
    }

    public void Revive()
    {
        if (revived) return;
        if (WebBridge.AdsAvailable) WebBridge.I.ShowRewarded(ok => { if (ok) DoRevive(); });
        else if (Save.gold >= 60) { Save.gold -= 60; Persist(); DoRevive(); }
    }

    void DoRevive()
    {
        revived = true;
        UI.I.CloseScreens();
        UI.I.ShowHud(true);
        Hero.Hp = Hero.MaxHp * 0.6f;
        foreach (var e in Horde.I.Near(Hero.transform.position, 6f).ToArray()) Horde.I.Damage(e, 99999, e.t.position - Hero.transform.position, 8f, true);
        Fx.I.Shockwave(Hero.transform.position, Kit.Hex("#FFD166"), 12f);
        Sfx.I.Heal();
        State = S.Playing;
        Time.timeScale = 1f;
        WebBridge.Gameplay(true);
        WebBridge.Event("revive", (int)RunTime);
    }

    public string ShareText()
    {
        string head = State == S.Won ? "\U0001F305 I survived until DAWN in GRAVE SHIFT!" : "\U0001FAA6 I survived " + UI.Clock(RunTime) + " in GRAVE SHIFT";
        return head + "\n\U0001F9DF " + runKills + " undead destroyed · ⭐ LV " + Level + "\nCan you make it to dawn?";
    }

    // ======================================================================
    void Update()
    {
        float dt = Time.deltaTime;
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown) Sfx.I.StartMusic();

        if (State == S.Playing)
        {
            RunTime += dt;
            if (RunTime >= Defs.RunLength) Win();
            else if (pendingLevels > 0) OpenLevelUp();
            Sfx.I.MusicIntensity(Mathf.Clamp01(Horde.I.Alive.Count / 80f));
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) Pause();
        }
        else if (State == S.Paused && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))) Resume();

        if (Dev && Input.GetKeyDown(KeyCode.L)) AddXp(Defs.XpForLevel(Level));
        if (Dev && Input.GetKeyDown(KeyCode.T)) RunTime += 60f;
        AdaptQuality();
    }

    float qT, qFrames; int qLevel;
    void AdaptQuality()
    {
        if (qLevel >= 2 || State != S.Playing) return;
        qT += Time.unscaledDeltaTime; qFrames++;
        if (qT < 5f) return;
        float fps = qFrames / qT; qT = 0; qFrames = 0;
        if (fps >= 42f) { qLevel = 2; return; }
        qLevel++;
        moon.shadows = LightShadows.None;
        QualitySettings.antiAliasing = 0;
        WebBridge.Event("quality_drop", (int)fps);
    }

    void LateUpdate()
    {
        if (!Hero) return;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float dist = Mathf.Lerp(1.32f, 1.0f, Mathf.InverseLerp(0.46f, 1.3f, aspect));
        var want = Hero.transform.position + new Vector3(0, 15.8f, -12.3f) * dist;   // 52°: see bodies, not just hat tops
        Cam.transform.position = Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f)) + Fx.I.ShakeOffset;
        Cam.transform.rotation = Quaternion.Euler(52f, 0, 0);
    }
}

// Fire baskets flicker their glow.
public class Flicker : MonoBehaviour
{
    SpriteRenderer glow;
    void Start()
    {
        glow = new GameObject("flame").AddComponent<SpriteRenderer>();
        glow.sprite = Fx.Glow; glow.transform.SetParent(transform.parent, false);
        glow.transform.position = transform.position + Vector3.up * 0.05f;
        glow.transform.rotation = Quaternion.Euler(90, 0, 0);
    }
    void Update()
    {
        float f = 0.8f + Mathf.PerlinNoise(Time.time * 6f, transform.position.x) * 0.4f;
        glow.transform.localScale = Vector3.one * 4.2f * f;
        glow.color = new Color(1f, 0.55f, 0.2f, 0.32f * f);
    }
}
