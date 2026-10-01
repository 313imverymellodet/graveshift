using System;
using System.Collections.Generic;
using UnityEngine;

// SPOOKTOBER: a seasonal event that runs through October (or with ?spooky=1).
// Candy drops, a decorated graveyard, hopping jack-o'-lanterns, the Pumpkin King, and hat cosmetics.
public static class Spooky
{
    static int on = -1;
    public static bool On
    {
        get
        {
            if (on < 0)
            {
                var url = Application.absoluteURL ?? "";
                on = url.Contains("spooky=0") ? 0 : url.Contains("spooky=1") || DateTime.Now.Month == 10 ? 1 : 0;
            }
            return on == 1;
        }
    }

    public static readonly string[] Candies = { "Spooky/candyA", "Spooky/candyB", "Spooky/candyC", "Spooky/candyD", "Spooky/candyCorn", "Spooky/lollipopA", "Spooky/lollipopB" };

    // cosmetics bought with candy
    public class Hat { public string id, name, model, part; public int cost; public float lift, size; }
    public static readonly Hat[] Hats =
    {
        new Hat { id = "witch", name = "WITCH HAT", model = "Spooky/character_witch", part = "character_witchHat", cost = 120, lift = 0f, size = 1.5f },
        new Hat { id = "pumpkin", name = "PUMPKIN HEAD", model = "Spooky/character_jack", part = "character_jackHat", cost = 300, lift = 0f, size = 1.0f },
    };
    public static Hat HatById(string id) { foreach (var h in Hats) if (h.id == id) return h; return null; }

    // Pull one named part (a hat, a head) out of a KayKit costume model.
    public static GameObject Part(string model, string part)
    {
        var src = Resources.Load<GameObject>("Kenney/" + model);
        if (!src) return null;
        var inst = UnityEngine.Object.Instantiate(src);
        Transform found = null;
        foreach (var t in inst.GetComponentsInChildren<Transform>()) if (t.name == part) { found = t; break; }
        if (!found) { UnityEngine.Object.Destroy(inst); return null; }
        var holder = new GameObject(part);
        found.SetParent(holder.transform, true);
        UnityEngine.Object.Destroy(inst);
        Kit.FixMaterials(holder);
        return holder;
    }
}

// KayKit Rig_Medium characters: skinned meshes driven by the shared Rig_Medium clip library.
public static class KayRig
{
    static Dictionary<string, AnimationClip> clips;

    static void LoadClips()
    {
        if (clips != null) return;
        clips = new Dictionary<string, AnimationClip>();
        foreach (var f in new[] { "Kenney/KaySkel/Rig_Medium_General", "Kenney/KaySkel/Rig_Medium_MovementBasic" })
            foreach (var c in Resources.LoadAll<AnimationClip>(f))
                if (!c.name.StartsWith("__preview") && !clips.ContainsKey(c.name)) clips[c.name] = c;
    }

    // Builds the character under a fresh root, standing on y=0, about `height` tall.
    public static GameObject Create(string model, float height, Transform parent, out Animation anim, out float scale, string rightHand = null, string leftHand = null)
    {
        LoadClips();
        var root = new GameObject(model.Substring(model.LastIndexOf('/') + 1));
        root.transform.SetParent(parent, false);
        var src = Resources.Load<GameObject>("Kenney/" + model);
        var inst = UnityEngine.Object.Instantiate(src, root.transform, false);
        inst.name = "model";
        Kit.FixMaterials(root);
        var b = Kit.WorldBounds(inst);
        scale = height / Mathf.Max(0.01f, b.size.y);
        inst.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        anim = inst.GetComponent<Animation>();
        if (!anim) anim = inst.AddComponent<Animation>();
        foreach (var kv in clips) if (anim.GetClip(kv.Key) == null) anim.AddClip(kv.Value, kv.Key);
        anim.cullingType = AnimationCullingType.AlwaysAnimate;
        foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
        foreach (var once in new[] { "Death_A", "Death_B", "Spawn_Ground", "Spawn_Air" }) if (anim[once] != null) anim[once].wrapMode = WrapMode.ClampForever;
        foreach (var layered in new[] { "Hit_A", "Hit_B", "Throw", "Use_Item", "Interact" })
            if (anim[layered] != null) { anim[layered].wrapMode = WrapMode.Once; anim[layered].layer = 1; }
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        if (rightHand != null) Hold(inst.transform, "handslot.r", rightHand);
        if (leftHand != null) Hold(inst.transform, "handslot.l", leftHand);
        return root;
    }

    static void Hold(Transform model, string slot, string prop)
    {
        Transform s = null;
        foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == slot) { s = t; break; }
        var src = Resources.Load<GameObject>("Kenney/" + prop);
        if (!s || !src) return;
        var w = UnityEngine.Object.Instantiate(src, s, false);
        w.transform.localPosition = Vector3.zero; w.transform.localRotation = Quaternion.identity;
        Kit.FixMaterials(w);
    }
}
