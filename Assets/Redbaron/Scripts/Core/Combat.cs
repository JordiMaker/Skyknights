using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>Utilidades de combate: línea de visión, trazadoras y explosiones low poly.</summary>
    public static class Combat
    {
        /// <summary>Capas que tapan la línea de tiro. Por defecto todas menos Ignore Raycast.</summary>
        public static LayerMask LosMask = ~(1 << 2);

        /// <summary>
        /// Hay visión si nada se interpone, o si lo que se interpone es la propia cobertura del objetivo
        /// (a menos de 2,5 m de él). Esa cobertura ya se tiene en cuenta en la probabilidad de acierto.
        /// </summary>
        public static bool HasLineOfSight(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.01f) return true;
            var hits = Physics.RaycastAll(from, d / dist, dist, LosMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (target != null && h.transform.IsChildOf(target)) continue;
                if (h.collider.GetComponentInParent<SoldierAI>() != null) continue;
                if ((h.point - to).magnitude < 2.5f) continue;
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- Trazadoras
        static Material tracerMat;
        static readonly Queue<LineRenderer> pool = new Queue<LineRenderer>();
        static Transform fxRoot;

        static Transform FxRoot
        {
            get
            {
                if (fxRoot == null) fxRoot = new GameObject("Redbaron_FX").transform;
                return fxRoot;
            }
        }

        public static void Tracer(Vector3 a, Vector3 b, float life = 0.09f, float width = 0.05f)
        {
            if (tracerMat == null)
            {
                var sh = Shader.Find("Sprites/Default");
                tracerMat = new Material(sh) { color = new Color(1f, 0.83f, 0.42f, 0.9f) };
            }
            LineRenderer lr = pool.Count > 0 ? pool.Dequeue() : null;
            if (lr == null)
            {
                var go = new GameObject("Trazadora");
                go.transform.SetParent(FxRoot, false);
                lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = tracerMat;
                lr.widthMultiplier = 0.05f;
                lr.positionCount = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                go.AddComponent<FxLife>();
            }
            lr.gameObject.SetActive(true);
            lr.widthMultiplier = width;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.GetComponent<FxLife>().Begin(life, () => { lr.gameObject.SetActive(false); pool.Enqueue(lr); });
        }

        // ---------------------------------------------------------------- Humo y fuego
        static Mesh puffMesh;
        static readonly Dictionary<int, Material> puffMats = new Dictionary<int, Material>();

        static Material PuffMat(Color c)
        {
            int key = ((int)(c.r * 255) << 24) | ((int)(c.g * 255) << 16) | ((int)(c.b * 255) << 8) | (int)(c.a * 255);
            if (puffMats.TryGetValue(key, out var m)) return m;
            m = new Material(Shader.Find("Sprites/Default")) { color = c };
            puffMats[key] = m;
            return m;
        }

        static Mesh PuffMesh()
        {
            if (puffMesh != null) return puffMesh;
            // Icosaedro low poly
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var acc = new MeshAccumulator();
            for (int i = 0; i < f.Length; i += 3) acc.AddTriangle(v[f[i]].normalized, v[f[i + 1]].normalized, v[f[i + 2]].normalized, Color.white);
            puffMesh = acc.ToMesh("Puff");
            return puffMesh;
        }

        public static void Puff(Vector3 pos, Color color, float life, float grow, float rise)
        {
            var go = new GameObject("Humo");
            go.transform.SetParent(FxRoot, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = PuffMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = PuffMat(color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fx = go.AddComponent<FxLife>();
            fx.grow = grow; fx.rise = rise;
            fx.Begin(life, () => Object.Destroy(go));
        }

        public static void Explosion(Vector3 pos, bool big)
        {
            Sfx.Explosion(pos, big);
            Puff(pos, new Color(0.94f, 0.63f, 0.25f, 0.95f), 0.7f, big ? 10 : 7, 2);
            int n = big ? 9 : 7;
            for (int i = 0; i < n; i++)
                Puff(pos + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0f, 2f), Random.Range(-1.5f, 1.5f)),
                    i % 2 == 0 ? new Color(0.33f, 0.32f, 0.29f, 0.85f) : new Color(0.24f, 0.23f, 0.2f, 0.85f), 4.5f, Random.Range(3.5f, 5.5f), Random.Range(3f, 5f));
        }

        /// <summary>A shell or grenade bursting: smaller than a bomb.</summary>
        public static void SmallExplosion(Vector3 pos)
        {
            Sfx.Explosion(pos, false);
            Puff(pos, new Color(0.94f, 0.63f, 0.25f, 0.95f), 0.45f, 3.5f, 1.5f);
            for (int i = 0; i < 4; i++)
                Puff(pos + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0f, 1f), Random.Range(-0.8f, 0.8f)),
                    i % 2 == 0 ? new Color(0.33f, 0.32f, 0.29f, 0.8f) : new Color(0.42f, 0.38f, 0.3f, 0.8f), 2.5f, Random.Range(2f, 3f), Random.Range(1.5f, 2.5f));
        }

        /// <summary>A jet of flame from a flamethrower: a row of fire puffs from the nozzle to the target.</summary>
        public static void Flame(Vector3 from, Vector3 to)
        {
            Sfx.Thud(from, 0.25f);
            for (int i = 1; i <= 5; i++)
            {
                float k = i / 5f;
                Puff(Vector3.Lerp(from, to, k) + Random.insideUnitSphere * 0.25f * k,
                    i % 2 == 0 ? new Color(1f, 0.78f, 0.3f, 0.9f) : new Color(0.95f, 0.45f, 0.15f, 0.9f), 0.35f, 0.8f + 1.4f * k, 0.6f);
            }
            Puff(to + Vector3.up * 0.5f, new Color(0.25f, 0.23f, 0.2f, 0.7f), 1.6f, 2f, 1.5f);
        }

        /// <summary>Smoke at a gun's mouth.</summary>
        public static void MuzzleSmoke(Vector3 pos) => Puff(pos, new Color(0.55f, 0.53f, 0.48f, 0.7f), 0.8f, 1.6f, 0.8f);

        /// <summary>Something thrown in an arc (a grenade): it lands at to after time seconds, then land runs.</summary>
        public static void Lob(Vector3 from, Vector3 to, float time, System.Action land)
        {
            var go = new GameObject("Granada");
            go.transform.SetParent(FxRoot, false);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.14f;
            go.AddComponent<MeshFilter>().sharedMesh = PuffMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = PuffMat(new Color(0.2f, 0.22f, 0.18f, 1f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<FxLob>().Begin(from, to, time, land);
        }

        // ---------------------------------------------------------------- Damage
        /// <summary>How much of a hit gets through: armour takes a tenth of a bullet and half of a flame,
        /// and flames only scorch buildings. Everything else takes it in full.</summary>
        public static float Factor(DamageKind how, TargetKind target)
        {
            if (target == TargetKind.Vehicle) return how == DamageKind.Bullet ? 0.1f : how == DamageKind.Fragment ? 0.25f : how == DamageKind.Fire ? 0.5f : 1f;
            if (target != TargetKind.Soldier && how == DamageKind.Fire) return 0.5f;
            return 1f;
        }

        static readonly List<Health> blastTargets = new List<Health>();

        /// <summary>Damage around p for the side that is not from: full within 40 % of the radius, down to 30 % at its edge.
        /// No friendly fire, as with the bombs. A blast also breaks crates, barrels and wire and can fell a tree.</summary>
        public static void Blast(Vector3 p, float radius, float damage, Team from, DamageKind how, GameObject source)
        {
            if (radius <= 0) return;
            blastTargets.Clear();
            blastTargets.AddRange(Health.All);
            foreach (var h in blastTargets)
            {
                if (h == null || !h.Alive || h.team == from || h.kind == TargetKind.Plane) continue;
                float d = h.DistanceFrom(p);
                if (d > radius) continue;
                float k = d <= radius * 0.4f ? 1f : Mathf.Lerp(1f, 0.3f, (d - radius * 0.4f) / (radius * 0.6f));
                h.Damage(damage * k, source, how);
            }
            Breakable.BreakAround(p, radius);
            if (how == DamageKind.Blast && radius >= 2f) TreeDestructible.FellAround(p, radius * 0.5f);
            SuppressAround(p, radius * 1.8f, from, how == DamageKind.Fire ? 0.5f : 0.4f);
        }

        /// <summary>Suppression (see SoldierAI.Suppress) for the enemies of shooter within r of p, half as much at the edge.</summary>
        public static void SuppressAround(Vector3 p, float r, Team shooter, float amount)
        {
            if (r <= 0 || amount <= 0) return;
            foreach (var s in SoldierAI.All)
            {
                if (s.team == shooter || !s.Alive || s.IsVehicle) continue;
                Vector3 d = s.transform.position - p; d.y = 0;
                float m = d.magnitude;
                if (m < r) s.Suppress(amount * (1f - 0.5f * m / r));
            }
        }

        /// <summary>A grenade or bomb is on its way to p: the enemies of shooter within its radius may jump clear (SoldierAI.Evade).</summary>
        public static void WarnBlast(Vector3 p, float radius, Team shooter)
        {
            foreach (var s in SoldierAI.All)
            {
                if (s.team == shooter || !s.Alive || s.IsVehicle) continue;
                Vector3 d = s.transform.position - p; d.y = 0;
                if (d.magnitude < radius + 0.5f) s.Evade(p, radius);
            }
        }
    }

    /// <summary>Vida corta de un efecto: crece, sube y se desvanece.</summary>
    public class FxLife : MonoBehaviour
    {
        public float grow, rise;
        float t, life;
        System.Action done;
        MeshRenderer mr;
        Color c0;
        MaterialPropertyBlock mpb;

        public void Begin(float lifeSeconds, System.Action onDone)
        {
            life = lifeSeconds; t = 0; done = onDone;
            mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                c0 = mr.sharedMaterial.color;
                if (mpb == null) mpb = new MaterialPropertyBlock();
                transform.localScale = Vector3.one * 0.5f;
            }
            enabled = true;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = life > 0 ? t / life : 1;
            if (mr != null)
            {
                transform.localScale = Vector3.one * (0.5f + grow * Mathf.Min(1, k * 1.4f));
                transform.position += Vector3.up * rise * Time.deltaTime;
                var c = c0; c.a = c0.a * (1 - k * k);
                if (mpb == null) mpb = new MaterialPropertyBlock(); // a script reload in Play mode keeps mr but drops the block
                mpb.SetColor("_Color", c);
                mr.SetPropertyBlock(mpb);
            }
            if (k >= 1) { enabled = false; done?.Invoke(); }
        }
    }

    /// <summary>Flight of a thrown grenade: a low arc, then its landing action.</summary>
    public class FxLob : MonoBehaviour
    {
        Vector3 a, b; float t, time, lift; System.Action land;

        public void Begin(Vector3 from, Vector3 to, float seconds, System.Action onLand)
        {
            a = from; b = to; time = Mathf.Max(0.1f, seconds); t = 0; land = onLand;
            lift = 1.5f + Vector3.Distance(from, to) * 0.15f;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            transform.position = Vector3.Lerp(a, b, k) + Vector3.up * lift * 4f * k * (1f - k);
            if (k < 1f) return;
            land?.Invoke(); // a script reload in Play mode drops the action: the grenade is then a dud
            Destroy(gameObject);
        }
    }
}
