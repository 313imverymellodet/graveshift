using UnityEngine;

// All audio is synthesized at boot: zero audio files.
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    const int SR = 22050;
    const float TAU = Mathf.PI * 2f;
    AudioSource[] voices; int next;
    AudioSource music;
    AudioClip shoot, scatter, hit, die, bigDie, gem, coin, levelUp, hurt, slam, boom, roar, dash, heal, click, win, lose, chest;
    public bool Muted { get; private set; }
    System.Random rnd = new System.Random(11);
    float N() => (float)(rnd.NextDouble() * 2 - 1);
    float lastHit, lastGem; int gemStep;

    void Awake()
    {
        I = this;
        voices = new AudioSource[10];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = 0.3f; music.playOnAwake = false;
        Build();
        music.clip = Music();
    }

    public void SetMuted(bool m) { Muted = m; AudioListener.volume = m ? 0 : 1; }
    public void StartMusic() { if (!music.isPlaying) music.Play(); }
    public void MusicIntensity(float k) { music.volume = Mathf.Lerp(0.22f, 0.36f, k); music.pitch = 1f + k * 0.04f; }

    void Play(AudioClip c, float vol, float pitch = 1f)
    {
        var s = voices[next]; next = (next + 1) % voices.Length;
        s.pitch = pitch; s.PlayOneShot(c, vol);
    }

    public void Shoot() => Play(shoot, 0.22f, Random.Range(0.94f, 1.06f));
    public void Scatter() => Play(scatter, 0.35f, Random.Range(0.95f, 1.05f));
    public void Hit() { if (Time.unscaledTime - lastHit < 0.035f) return; lastHit = Time.unscaledTime; Play(hit, 0.16f, Random.Range(0.9f, 1.2f)); }
    public void EnemyDie(bool boss) => Play(boss ? bigDie : die, boss ? 0.8f : 0.2f, Random.Range(0.85f, 1.15f));
    public void Gem()
    {
        if (Time.unscaledTime - lastGem > 0.4f) gemStep = 0;
        if (Time.unscaledTime - lastGem < 0.03f) return;
        lastGem = Time.unscaledTime;
        Play(gem, 0.2f, 1f + Mathf.Min(gemStep++, 16) * 0.035f);
    }
    public void Coin() => Play(coin, 0.3f, Random.Range(0.95f, 1.1f));
    public void LevelUp() => Play(levelUp, 0.55f);
    public void Hurt() => Play(hurt, 0.55f);
    public void Slam() => Play(slam, 0.55f, Random.Range(0.9f, 1.05f));
    public void Boom() => Play(boom, 0.45f, Random.Range(0.9f, 1.1f));
    public void Roar() => Play(roar, 0.65f);
    public void Dash() => Play(dash, 0.5f);
    public void Heal() => Play(heal, 0.5f);
    public void Click() => Play(click, 0.4f);
    public void Win() => Play(win, 0.7f);
    public void Lose() => Play(lose, 0.7f);
    public void Chest() => Play(chest, 0.7f);

    static AudioClip Clip(string n, float[] d) { var c = AudioClip.Create(n, d.Length, 1, SR, false); c.SetData(d, 0); return c; }
    delegate float Gen(float t, float dt);
    static float[] R(float dur, Gen g)
    {
        int n = (int)(SR * dur); var d = new float[n]; float dt = 1f / SR;
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(g(i * dt, dt) * Mathf.Clamp01((n - i) / (SR * 0.008f)), -1, 1);
        return d;
    }

    float[] Arp(float[] notes, float step, float tail, float vol)
    {
        float ph = 0;
        return R(step * notes.Length + tail, (t, dt) =>
        {
            int k = Mathf.Min((int)(t / step), notes.Length - 1);
            ph += TAU * notes[k] * dt;
            float lt = t - k * step;
            return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2) + 0.15f * Mathf.Sin(ph * 3)) * Mathf.Exp(-lt * (k < notes.Length - 1 ? 14 : 3.5f)) * vol;
        });
    }

    void Build()
    {
        float ph = 0, lp = 0;
        shoot = Clip("shoot", R(0.11f, (t, dt) => { ph += TAU * Mathf.Lerp(1400, 380, t / 0.11f) * dt; return (Mathf.Sign(Mathf.Sin(ph)) * 0.3f + Mathf.Sin(ph) * 0.4f) * Mathf.Exp(-t * 30); }));
        lp = 0;
        scatter = Clip("scatter", R(0.28f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.8f, 0.08f, t / 0.28f); ph += TAU * Mathf.Lerp(220, 60, t / 0.28f) * dt; return (lp * 0.9f + Mathf.Sin(ph) * 0.5f) * Mathf.Exp(-t * 14); }));
        ph = 0;
        hit = Clip("hit", R(0.05f, (t, dt) => { ph += TAU * Mathf.Lerp(900, 300, t / 0.05f) * dt; return (Mathf.Sin(ph) * 0.6f + N() * 0.3f) * Mathf.Exp(-t * 70); }));
        lp = 0; ph = 0;
        die = Clip("die", R(0.22f, (t, dt) => { lp += (N() - lp) * 0.3f; ph += TAU * Mathf.Lerp(300, 90, t / 0.22f) * dt; return (Mathf.Sin(ph) * 0.6f + lp * 0.5f) * Mathf.Exp(-t * 16); }));
        lp = 0; ph = 0;
        bigDie = Clip("bigdie", R(1.6f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.5f, 0.02f, t / 1.6f); ph += TAU * Mathf.Lerp(120, 30, t / 1.6f) * dt; return (Mathf.Sin(ph) * 0.7f + lp * 0.9f) * Mathf.Exp(-t * 2.2f); }));
        ph = 0;
        gem = Clip("gem", R(0.12f, (t, dt) => { ph += TAU * (t < 0.03f ? 1568 : 2093) * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 30) * 0.7f; }));
        ph = 0;
        coin = Clip("coin", R(0.2f, (t, dt) => { ph += TAU * (t < 0.05f ? 1976 : 2637) * dt; return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2)) * Mathf.Exp(-t * 18) * 0.5f; }));
        levelUp = Clip("lvl", Arp(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f }, 0.06f, 0.6f, 0.4f));
        chest = Clip("chest", Arp(new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f, 1318.5f }, 0.07f, 0.9f, 0.4f));
        win = Clip("win", Arp(new[] { 523.25f, 523.25f, 659.25f, 783.99f, 659.25f, 783.99f, 1046.5f }, 0.12f, 1.2f, 0.45f));
        lose = Clip("lose", Arp(new[] { 392f, 369.99f, 349.23f, 311.13f }, 0.22f, 1.0f, 0.4f));
        heal = Clip("heal", Arp(new[] { 659.25f, 880f, 1108.73f }, 0.07f, 0.4f, 0.4f));
        ph = 0; lp = 0;
        hurt = Clip("hurt", R(0.28f, (t, dt) => { lp += (N() - lp) * 0.2f; ph += TAU * Mathf.Lerp(200, 70, t / 0.28f) * dt; return (Mathf.Sign(Mathf.Sin(ph)) * 0.35f + lp * 0.6f) * Mathf.Exp(-t * 10); }));
        ph = 0; lp = 0;
        slam = Clip("slam", R(0.5f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.4f, 0.02f, t / 0.5f); ph += TAU * (60 + 90 * Mathf.Exp(-t * 20)) * dt; return (Mathf.Sin(ph) * 0.9f + lp * 0.7f) * Mathf.Exp(-t * 6); }));
        lp = 0; ph = 0;
        boom = Clip("boom", R(0.8f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.9f, 0.03f, t / 0.8f); ph += TAU * (45 + 70 * Mathf.Exp(-t * 12)) * dt; return (lp * 1.1f + Mathf.Sin(ph) * 0.6f) * Mathf.Exp(-t * 4.5f); }));
        lp = 0; ph = 0;
        roar = Clip("roar", R(1.3f, (t, dt) =>
        {
            float f = 70 + 25 * Mathf.Sin(t * 9) + 30 * Mathf.Exp(-t * 2);
            ph += TAU * f * dt;
            lp += (N() - lp) * 0.08f;
            float saw = 2f * (ph / TAU % 1f) - 1f;
            return (saw * 0.45f + lp * 1.2f) * Mathf.Min(t * 8, 1) * Mathf.Exp(-t * 1.6f);
        }));
        lp = 0;
        dash = Clip("dash", R(0.45f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(t / 0.45f * Mathf.PI)); return lp * Mathf.Sin(t / 0.45f * Mathf.PI) * 1.2f; }));
        ph = 0;
        click = Clip("click", R(0.04f, (t, dt) => { ph += TAU * 1200 * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 90) * 0.6f; }));
    }

    // 110 bpm spooky synthwave in D minor: Dm – Bb – C – A, organ pad, pulsing bass, tom hits.
    AudioClip Music()
    {
        float bpm = 110f, beat = 60f / bpm;
        int bars = 4; float dur = beat * 4 * bars;
        int n = (int)(SR * dur); var d = new float[n];
        float[][] chords =
        {
            new[] { 293.66f, 349.23f, 440f },
            new[] { 233.08f, 293.66f, 349.23f },
            new[] { 261.63f, 329.63f, 392f },
            new[] { 220f, 277.18f, 329.63f },
        };
        float[] roots = { 73.42f, 58.27f, 65.41f, 55f };
        float[] lead = { 587.33f, 698.46f, 880f, 698.46f, 587.33f, 523.25f, 587.33f, 440f };
        float hp = 0, blp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, bt = t / beat;
            int bi = (int)bt, bar = (bi / 4) % bars;
            float ib = (bt - bi) * beat;
            float nz = N();
            float kick = Mathf.Sin(TAU * (48 + 90 * Mathf.Exp(-ib * 30)) * ib) * Mathf.Exp(-ib * 8) * 0.55f;
            float tom = (bi % 4 == 3) ? Mathf.Sin(TAU * (110 + 60 * Mathf.Exp(-ib * 20)) * ib) * Mathf.Exp(-ib * 10) * 0.25f : 0;
            float e8 = bt * 2; int e8i = (int)e8; float i8 = (e8 - e8i) * beat / 2;
            float hat = (e8i % 2 == 1) ? (nz - hp) * Mathf.Exp(-i8 * 60) * 0.06f : 0; hp = nz;
            // pulsing 16th bass
            float e16 = bt * 4; int e16i = (int)e16; float i16 = (e16 - e16i) * beat / 4;
            float bf = roots[bar] * (e16i % 4 == 2 ? 2f : 1f);
            float saw = 2f * ((bf * t) % 1f) - 1f;
            blp += (saw - blp) * 0.09f;
            float bass = blp * Mathf.Exp(-i16 * 9) * 0.32f;
            // organ pad (additive, slight detune)
            float pad = 0;
            foreach (var f in chords[bar])
                pad += Mathf.Sin(TAU * f * t) + 0.5f * Mathf.Sin(TAU * f * 2.003f * t) + 0.25f * Mathf.Sin(TAU * f * 3.01f * t);
            pad *= 0.028f * (0.8f + 0.2f * Mathf.Sin(t * 6f));
            // eerie lead on 8ths, bars 3-4 only
            float ld = 0;
            if (bar >= 2)
            {
                float lf = lead[e8i % 8];
                ld = (Mathf.Sin(TAU * lf * i8 + Mathf.Sin(TAU * 5 * t) * 0.8f)) * Mathf.Exp(-i8 * 7) * 0.07f;
            }
            float duck = 1f - 0.45f * Mathf.Exp(-ib * 10);
            d[i] = Mathf.Clamp((kick + tom + hat + (bass + pad + ld) * duck) * 0.85f, -1, 1);
        }
        return Clip("music", d);
    }
}
