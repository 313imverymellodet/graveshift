using System;
using System.Collections.Generic;
using UnityEngine;

public enum EType { Zombie, Skeleton, Ghost, Vampire, Brute, BossZombie, BossVampire, BossOrc }

public class EDef
{
    public string name, model;
    public float hp, speed, dmg, radius, scale;
    public int xp;
    public Color tint = Color.white;
    public bool ghost, boss;
}

public enum Up { Blaster, Scatter, Pumpkins, Tombstones, Grenades, Aura, Boots, Heart, Magnet, Candle, Skull, Armor }

public class UpDef
{
    public Up id;
    public string name, icon, desc;
    public int max = 5;
    public bool weapon;
    public Color color;
}

public static class Defs
{
    public const float ArenaR = 30f;
    public const float RunLength = 480f;   // survive 8:00 until dawn

    public static readonly Dictionary<EType, EDef> E = new Dictionary<EType, EDef>
    {
        [EType.Zombie]      = new EDef { name = "Zombie", model = "Characters/character-zombie", hp = 12, speed = 1.7f, dmg = 8, radius = 0.42f, scale = 1.45f, xp = 1 },
        [EType.Skeleton]    = new EDef { name = "Skeleton", model = "Characters/character-skeleton", hp = 8, speed = 2.9f, dmg = 6, radius = 0.4f, scale = 1.4f, xp = 1 },
        [EType.Ghost]       = new EDef { name = "Ghost", model = "Characters/character-ghost", hp = 16, speed = 2.1f, dmg = 9, radius = 0.42f, scale = 1.45f, xp = 2, ghost = true, tint = new Color(0.75f, 0.9f, 1.25f) },
        [EType.Vampire]     = new EDef { name = "Vampire", model = "Characters/character-vampire", hp = 45, speed = 2.5f, dmg = 12, radius = 0.45f, scale = 1.55f, xp = 5 },
        [EType.Brute]       = new EDef { name = "Brute", model = "Dungeon/Characters/character-orc", hp = 80, speed = 1.35f, dmg = 18, radius = 0.6f, scale = 1.9f, xp = 7 },
        [EType.BossZombie]  = new EDef { name = "ROTTING GIANT", model = "Characters/character-zombie", hp = 1400, speed = 1.25f, dmg = 25, radius = 1.2f, scale = 4.2f, xp = 60, boss = true, tint = new Color(0.8f, 1.15f, 0.8f) },
        [EType.BossVampire] = new EDef { name = "COUNT NOCTIS", model = "Characters/character-vampire", hp = 3200, speed = 1.9f, dmg = 30, radius = 1.0f, scale = 3.6f, xp = 120, boss = true, tint = new Color(1.2f, 0.75f, 0.8f) },
        [EType.BossOrc]     = new EDef { name = "ORC WARLORD", model = "Dungeon/Characters/character-orc", hp = 7000, speed = 1.6f, dmg = 38, radius = 1.3f, scale = 4.6f, xp = 200, boss = true, tint = new Color(1.1f, 0.85f, 0.7f) },
    };

    public static readonly Dictionary<Up, UpDef> U = new Dictionary<Up, UpDef>
    {
        [Up.Blaster]    = new UpDef { id = Up.Blaster, weapon = true, name = "Soul Blaster", icon = "blaster-a", desc = "Auto-fires at the nearest undead.", color = Kit.Hex("#7CFFB2") },
        [Up.Scatter]    = new UpDef { id = Up.Scatter, weapon = true, name = "Scattergun", icon = "blaster-e", desc = "Point-blank spread that knocks them back.", color = Kit.Hex("#FFD166") },
        [Up.Pumpkins]   = new UpDef { id = Up.Pumpkins, weapon = true, name = "Jack-o'-Guard", icon = "pumpkin-carved", desc = "Burning pumpkins orbit you.", color = Kit.Hex("#FF8C42") },
        [Up.Tombstones] = new UpDef { id = Up.Tombstones, weapon = true, name = "Tombfall", icon = "gravestone-round", desc = "Gravestones crash down on crowds.", color = Kit.Hex("#B8C0FF") },
        [Up.Grenades]   = new UpDef { id = Up.Grenades, weapon = true, name = "Holy Grenade", icon = "grenade-a", desc = "Lobbed blasts that clear packs.", color = Kit.Hex("#FF5D73") },
        [Up.Aura]       = new UpDef { id = Up.Aura, weapon = true, name = "Lantern Aura", icon = "lantern-candle", desc = "Sacred fire burns anything close.", color = Kit.Hex("#FFB347") },
        [Up.Boots]      = new UpDef { id = Up.Boots, name = "Grave Boots", icon = "shovel-dirt", desc = "+10% move speed.", color = Kit.Hex("#9AD1FF") },
        [Up.Heart]      = new UpDef { id = Up.Heart, name = "Iron Heart", icon = "potion", desc = "+25 max HP and heal 25.", color = Kit.Hex("#FF6B6B") },
        [Up.Magnet]     = new UpDef { id = Up.Magnet, name = "Soul Magnet", icon = "chest", desc = "+40% pickup range.", color = Kit.Hex("#C792EA") },
        [Up.Candle]     = new UpDef { id = Up.Candle, name = "Vigil Candle", icon = "candle-multiple", desc = "-8% weapon cooldowns.", color = Kit.Hex("#FFE66D") },
        [Up.Skull]      = new UpDef { id = Up.Skull, name = "Cursed Urn", icon = "urn-round", desc = "+12% damage.", color = Kit.Hex("#E0E0E0") },
        [Up.Armor]      = new UpDef { id = Up.Armor, name = "Coffin Plate", icon = "coffin", desc = "-8% damage taken.", color = Kit.Hex("#A0A7B8") },
    };

    public static int XpForLevel(int lv) => 3 + lv * 3 + Mathf.RoundToInt(Mathf.Pow(lv, 1.6f));   // 7, 12, 17, 23, 29 ... fast early picks

    // Permanent shop (gold between runs)
    public static readonly string[] ShopNames = { "VITALITY", "MIGHT", "SWIFTNESS", "ATTRACTION", "GREED" };
    public static readonly string[] ShopDesc = { "+10 max HP", "+6% damage", "+4% speed", "+12% pickup range", "+12% gold" };
    public const int ShopMax = 8;
    public static int ShopCost(int lv) => Mathf.RoundToInt(40 * Mathf.Pow(1.55f, lv));
}

[Serializable]
public class SaveData
{
    public int gold;
    public int bestTime, bestKills, runs, wins;
    public int[] shop = new int[5];
    public bool muted;
    public bool tut;
}
