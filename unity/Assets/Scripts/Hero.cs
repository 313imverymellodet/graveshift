using UnityEngine;

public class Hero : MonoBehaviour
{
    public float Hp, MaxHp;
    public Transform Muzzle;
    Animation anim;
    Transform model, gun;
    Renderer[] rends;
    MaterialPropertyBlock mpb;
    float iframe, recoil, yaw, hurtFlash;
    Vector3 vel;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    public static Hero Create(Transform parent)
    {
        var root = new GameObject("Hero");
        root.transform.SetParent(parent, false);
        var h = root.AddComponent<Hero>();
        var m = Kit.Spawn("Characters/character-keeper", 1.55f, root.transform);
        h.model = m.transform;
        h.anim = Rig.Setup(m, true);
        h.rends = m.GetComponentsInChildren<Renderer>();
        h.mpb = new MaterialPropertyBlock();

        // blaster held at chest height in the two-hand aiming pose
        h.gun = Kit.Spawn("Blaster/blaster-a", 1.15f, root.transform, new Vector3(0.05f, 0.62f, 0.42f)).transform;
        foreach (var r in h.gun.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var mz = new GameObject("Muzzle").transform;
        mz.SetParent(root.transform, false);
        mz.localPosition = new Vector3(0.05f, 0.78f, 1.05f);
        h.Muzzle = mz;

        // lantern light pool + contact shadow (fake lighting: cheap on phones)
        var light = new GameObject("lanternGlow").AddComponent<SpriteRenderer>();
        light.sprite = Fx.Glow; light.color = Kit.A(Kit.Hex("#FFD9A0"), 0.34f);
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0, 0.03f, 0); light.transform.localRotation = Quaternion.Euler(90, 0, 0);
        light.transform.localScale = Vector3.one * 9f;
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.4f), 0.9f, root.transform, Vector3.zero, 0.04f);
        return h;
    }

    GameObject hatGo;
    public void ApplyHat(string id)
    {
        if (hatGo) Destroy(hatGo);
        var h = Spooky.HatById(id);
        if (h == null) return;
        Transform head = null;
        foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "head") { head = t; break; }
        if (!head) return;
        hatGo = Spooky.Part(h.model, h.part);
        if (!hatGo) return;
        // the head is a bone with no mesh of its own, so size from the whole body: chibi heads are ~45% of height
        var body = Kit.WorldBounds(model.gameObject);
        float H = body.size.y, top = body.max.y;
        var b = Kit.WorldBounds(hatGo);
        bool pumpkin = id == "pumpkin";
        float want = H * (pumpkin ? 0.84f : 0.62f * h.size / 1.5f);   // the pumpkin swallows the keeper's own head and hat
        hatGo.transform.localScale = Vector3.one * (want / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)));
        b = Kit.WorldBounds(hatGo);
        var hp = head.position;
        float y = pumpkin ? (top - H * 0.17f) - b.center.y : (top - b.size.y * 0.22f) - b.min.y;
        hatGo.transform.position += new Vector3(hp.x - b.center.x, y + h.lift, hp.z - b.center.z);
        hatGo.transform.SetParent(head, true);
        foreach (var r in hatGo.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        rends = model.GetComponentsInChildren<Renderer>();
    }

    public void ResetRun()
    {
        MaxHp = Hp = Game.I.BaseHp;
        transform.position = Vector3.zero;
        iframe = 0; vel = Vector3.zero;
        gameObject.SetActive(true);
        anim.Play("walk");
    }

    public void Recoil() => recoil = 1f;

    public void Heal(float v) => Hp = Mathf.Min(MaxHp, Hp + v);

    public void Hurt(float dmg)
    {
        if (iframe > 0 || Game.I.State != Game.S.Playing) return;
        if (Game.I.Dev && Application.absoluteURL.Contains("god=1")) return;
        dmg *= Game.I.ArmorMult;
        Hp -= dmg;
        iframe = 0.55f;
        hurtFlash = 0.18f;
        Fx.I.Shake(0.28f);
        Sfx.I.Hurt();
        UI.I.HurtVignette();
        WebBridge.Vibrate(40);
        if (Hp <= 0) { Hp = 0; Game.I.Die(); }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        bool playing = Game.I.State == Game.S.Playing;

        Vector2 input = playing ? UI.I.Joy : Vector2.zero;
        float kx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        float ky = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        if (playing && (kx != 0 || ky != 0)) input = new Vector2(kx, ky).normalized;
        if (playing && Game.I.AutoPlay) input = AutoInput();

        var want = new Vector3(input.x, 0, input.y) * Game.I.MoveSpeed;
        vel = Vector3.Lerp(vel, want, 1f - Mathf.Exp(-dt * 16f));
        var pos = transform.position + vel * dt;
        pos = Obstacles.Resolve(pos, 0.35f);
        if (pos.magnitude > Defs.ArenaR - 0.6f) pos = pos.normalized * (Defs.ArenaR - 0.6f);
        pos.y = 0;
        transform.position = pos;

        // face the nearest threat (auto-aim), otherwise the way we walk
        var target = playing ? Horde.I.Nearest(pos, Mathf.Min(12f, Lantern.I.Reach)) : null;
        Vector3 face = target != null ? target.t.position - pos : vel;
        face.y = 0;
        if (face.sqrMagnitude > 0.01f)
            yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, dt * 900f);
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        float sp = vel.magnitude / Mathf.Max(0.1f, Game.I.MoveSpeed);
        string clip = sp > 0.1f ? "walk" : "idle";
        if (!anim.IsPlaying(clip)) anim.CrossFade(clip, 0.12f);
        if (clip == "walk") anim["walk"].speed = Mathf.Max(0.7f, sp) * 1.5f;

        recoil = Mathf.MoveTowards(recoil, 0, dt * 8f);
        gun.localPosition = new Vector3(0.05f, 0.62f, 0.42f - recoil * 0.12f);

        iframe -= dt;
        if (hurtFlash > 0)
        {
            hurtFlash -= dt;
            mpb.SetColor(ColorId, hurtFlash > 0 ? new Color(2.2f, 0.6f, 0.6f) : Color.white);
            foreach (var r in rends) r.SetPropertyBlock(mpb);
        }
    }

    // ?bot=1: kite in circles, grab gems, steer clear of the pack. Used for trailers and soak tests.
    Vector2 AutoInput()
    {
        var p = transform.position;
        Vector3 away = Vector3.zero;
        foreach (var e in Horde.I.Near(p, 4.5f))
        {
            var d = p - e.t.position; d.y = 0;
            away += d.normalized / Mathf.Max(0.4f, d.magnitude) * (e.def.boss ? 3f : 1f);
        }
        var tangent = Vector3.Cross(Vector3.up, p.sqrMagnitude > 1 ? p.normalized : Vector3.forward);
        var home = -p * 0.04f;
        var gem = Pickups.I.NearestGem(p, 9f);
        var greed = gem.HasValue && away.magnitude < 1.2f ? (gem.Value - p).normalized * 1.3f : Vector3.zero;
        // a dimming lantern beats everything else: go get oil
        var oil = Lantern.I.Fuel01 < 0.6f ? Pickups.I.NearestOil(p, 25f) : null;
        if (oil.HasValue) greed = (oil.Value - p).normalized * 2.2f;
        var dir = away * 1.4f + tangent * (greed == Vector3.zero ? 0.8f : 0.25f) + home + greed;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) return Vector2.zero;
        dir.Normalize();
        return new Vector2(dir.x, dir.z);
    }
}
