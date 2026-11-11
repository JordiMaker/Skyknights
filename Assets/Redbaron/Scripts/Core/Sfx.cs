using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Battle sound effects, synthesised at start-up (no audio files needed): aircraft machine guns, rifles,
    /// bunker machine guns, metal hits on planes, thuds on the ground, soldiers and buildings, and explosions.
    /// All are 3D sounds played from a small pool of sources, with a per-sound rate limit so that a whole
    /// battalion firing at once does not saturate the mix. Assign clips in the fields below to replace any
    /// synthesised one with a recorded sound.
    /// </summary>
    public static class Sfx
    {
        public static float Volume = 1f;
        /// <summary>Optional recorded clips; when set they replace the synthesised ones.</summary>
        public static AudioClip PlaneGunClip, RifleClip, BunkerGunClip, PlaneHitClip, ThudClip, ExplosionClip, BigExplosionClip;

        const int Rate = 44100, Voices = 32;
        static AudioSource[] pool;
        static int next;
        static readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        static AudioClip planeGun, rifle, bunkerGun, planeHit, thud, explosion, bigExplosion;

        // ------------------------------------------------------------ Public calls
        public static void PlaneGun(Vector3 p, bool player) =>
            Play(Pick(PlaneGunClip, ref planeGun, () => MakeGun("Sfx_AmetralladoraAvion", 0.14f, 3600f, 105f)), p, player ? 0.5f : 0.42f, player ? 0.25f : 1f, 0.07f, 8f, 140f, 0.025f);
        public static void Rifle(Vector3 p) =>
            Play(Pick(RifleClip, ref rifle, MakeRifle), p, 0.26f, 1f, 0.12f, 4f, 80f, 0.035f);
        public static void BunkerGun(Vector3 p) =>
            Play(Pick(BunkerGunClip, ref bunkerGun, () => MakeGun("Sfx_AmetralladoraCasamata", 0.16f, 2600f, 85f)), p, 0.34f, 1f, 0.08f, 5f, 100f, 0.03f);
        public static void PlaneHit(Vector3 p) =>
            Play(Pick(PlaneHitClip, ref planeHit, MakeMetalHit), p, 0.55f, 1f, 0.15f, 6f, 110f, 0.03f);
        public static void Thud(Vector3 p, float volume = 0.3f) =>
            Play(Pick(ThudClip, ref thud, MakeThud), p, volume, 1f, 0.2f, 3f, 60f, 0.04f);
        public static void Explosion(Vector3 p, bool big) =>
            Play(big ? Pick(BigExplosionClip, ref bigExplosion, () => MakeExplosion("Sfx_ExplosionGrande", 3.0f, 0.75f))
                     : Pick(ExplosionClip, ref explosion, () => MakeExplosion("Sfx_Explosion", 2.2f, 1f)),
                 p, big ? 1f : 0.85f, 1f, 0.1f, 14f, 320f, 0.05f);

        /// <summary>The recorded clip if one is set, otherwise the synthesised one (made on first use).
        /// Uses Unity's null check, so clips destroyed when Play mode ends are made again.</summary>
        static AudioClip Pick(AudioClip recorded, ref AudioClip synth, System.Func<AudioClip> make)
        {
            if (recorded != null) return recorded;
            if (synth == null) synth = make();
            return synth;
        }

        // ------------------------------------------------------------ Playback
        static void Play(AudioClip clip, Vector3 pos, float volume, float spatial, float pitchJitter, float minDist, float maxDist, float minInterval)
        {
            if (clip == null || !Application.isPlaying) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(clip, out float t) && now - t < minInterval) return;
            lastPlayed[clip] = now;
            EnsurePool();
            var src = pool[next]; next = (next + 1) % Voices;
            if (src == null) { pool = null; EnsurePool(); src = pool[0]; }
            src.transform.position = pos;
            src.clip = clip;
            src.volume = volume * Volume;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.spatialBlend = spatial;
            src.minDistance = minDist; src.maxDistance = maxDist;
            src.Play();
        }

        static void EnsurePool()
        {
            if (pool != null && pool[0] != null) return;
            var root = new GameObject("Redbaron_Sonidos").transform;
            pool = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = new GameObject("Voz").AddComponent<AudioSource>();
                s.transform.SetParent(root, false);
                s.playOnAwake = false; s.rolloffMode = AudioRolloffMode.Logarithmic; s.dopplerLevel = 0.3f;
                pool[i] = s;
            }
        }

        // ------------------------------------------------------------ Synthesis
        static System.Random rng = new System.Random(1916);
        static float N() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Lp(ref float state, float x, float cutoff) { state += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate)) * (x - state); return state; }

        static AudioClip Build(string name, float seconds, System.Func<float, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 1e-4f;
            for (int i = 0; i < n; i++) { data[i] = sample(i / (float)Rate); peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
            float g = 0.95f / peak;
            for (int i = 0; i < n; i++) data[i] *= g;
            // short fade-out so no clip ends with a click
            int fade = Mathf.Min(n, Rate / 100);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Machine-gun round: a sharp crack, a noisy blast and a low thump.</summary>
        static AudioClip MakeGun(string name, float len, float bright, float body)
        {
            float a = 0, b = 0;
            return Build(name, len, t =>
            {
                float n = N();
                float crack = t < 0.004f ? n : 0f;
                float blast = Lp(ref a, n, bright) * Mathf.Exp(-t / 0.028f);
                float low = Lp(ref b, n, 300f) * Mathf.Exp(-t / 0.05f) * 1.6f;
                float thump = Mathf.Sin(2f * Mathf.PI * body * t) * Mathf.Exp(-t / 0.045f) * 0.7f;
                return crack * 0.8f + blast + low + thump;
            });
        }

        /// <summary>Rifle shot: a crack, a body and a short rolling tail.</summary>
        static AudioClip MakeRifle()
        {
            float a = 0, b = 0;
            return Build("Sfx_Fusil", 0.45f, t =>
            {
                float n = N();
                float crack = n * Mathf.Exp(-t / 0.006f);
                float body = Lp(ref a, n, 1400f) * Mathf.Exp(-t / 0.06f);
                float tail = Lp(ref b, n, 380f) * Mathf.Exp(-t / 0.22f) * 0.8f;
                return crack * 0.7f + body + tail;
            });
        }

        /// <summary>Bullet striking a plane: a short click and ringing metal partials.</summary>
        static AudioClip MakeMetalHit()
        {
            return Build("Sfx_ImpactoAvion", 0.3f, t =>
            {
                float click = N() * Mathf.Exp(-t / 0.004f);
                float ring = Mathf.Sin(2f * Mathf.PI * 1720f * t) * Mathf.Exp(-t / 0.07f)
                           + Mathf.Sin(2f * Mathf.PI * 2650f * t) * Mathf.Exp(-t / 0.05f) * 0.7f
                           + Mathf.Sin(2f * Mathf.PI * 4100f * t) * Mathf.Exp(-t / 0.03f) * 0.5f;
                return click * 0.8f + ring * 0.6f;
            });
        }

        /// <summary>Dull hit: a round striking earth, a sandbag, a body or a wall.</summary>
        static AudioClip MakeThud()
        {
            float a = 0;
            return Build("Sfx_Impacto", 0.14f, t =>
            {
                float dull = Lp(ref a, N(), 700f) * Mathf.Exp(-t / 0.025f);
                float knock = Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-t / 0.035f) * 0.6f;
                return dull * 1.5f + knock;
            });
        }

        /// <summary>Explosion: a crack, a noisy blast that darkens as it fades and a falling sub-bass rumble.</summary>
        static AudioClip MakeExplosion(string name, float len, float tone)
        {
            float a = 0, b = 0, c = 0;
            return Build(name, len, t =>
            {
                float n = N();
                float crack = n * Mathf.Exp(-t / 0.015f);
                float blast = Lp(ref a, n, Mathf.Lerp(2200f, 260f, Mathf.Clamp01(t / 0.6f)) * tone) * Mathf.Exp(-t / 0.42f);
                float rumble = Lp(ref b, n, 120f * tone) * Mathf.Exp(-t / (len * 0.4f)) * 2.2f;
                float f = (58f - 22f * Mathf.Clamp01(t / len)) * tone;
                float sub = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / (len * 0.3f)) * 0.8f;
                float debris = Lp(ref c, n, 3000f) * Mathf.Exp(-t / 0.9f) * (Random.value < 0.002f ? 6f : 0.15f);
                return crack * 0.6f + blast + rumble + sub + debris;
            });
        }
    }
}
