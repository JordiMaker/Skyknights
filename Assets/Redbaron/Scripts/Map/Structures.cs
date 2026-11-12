using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>Aeródromo de un bando: aquí despegan los aviones y recargan su bomba.</summary>
    public class Airfield : MonoBehaviour
    {
        public static readonly List<Airfield> All = new List<Airfield>();
        public Team team;
        public Rect WorldRect => RedbaronMap.AirfieldWorldRect(team);
        public bool Contains(Vector3 p) => WorldRect.Contains(new Vector2(p.x, p.z));
        public Vector3 Center { get { var r = WorldRect; return new Vector3(r.center.x, 0, r.center.y); } }

        public static Airfield Of(Team t)
        {
            foreach (var a in All) if (a.team == t) return a;
            return null;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }

    /// <summary>Puesto de mando: si cae, gana el otro bando.</summary>
    [RequireComponent(typeof(Health))]
    public class CommandPost : MonoBehaviour
    {
        public static readonly List<CommandPost> All = new List<CommandPost>();
        public Health Health { get; private set; }
        Vector3 scale0;

        public static CommandPost Of(Team t)
        {
            foreach (var p in All) if (p.Health != null && p.Health.team == t) return p;
            return null;
        }

        void Awake()
        {
            Health = GetComponent<Health>();
            scale0 = transform.localScale;
            Health.Died += (h, src) =>
            {
                for (int k = 0; k < 4; k++) Combat.Explosion(transform.position + new Vector3(Random.Range(-5f, 5f), 3 + k, Random.Range(-3f, 3f)), true);
                transform.localScale = new Vector3(scale0.x, scale0.y * 0.3f, scale0.z);
                if (RedbaronGame.Instance != null) RedbaronGame.Instance.OnCommandPostDestroyed(this);
            };
        }

        public void ResetPost()
        {
            Health.Restore();
            transform.localScale = scale0;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }

    /// <summary>Casamata con ametralladora: ráfagas cortas contra la infantería enemiga.</summary>
    [RequireComponent(typeof(Health))]
    public class Bunker : MonoBehaviour
    {
        public static readonly List<Bunker> All = new List<Bunker>();
        public float range = 42f;
        public float burstEvery = 1.75f; // 20 % slower than the first version's 1.4 s
        public int shotsPerBurst = 2;
        public float baseHit = 0.3f;
        public Health Health { get; private set; }
        float cd;
        Vector3 scale0;

        void Awake()
        {
            Health = GetComponent<Health>();
            scale0 = transform.localScale;
            cd = Random.value;
            Health.Died += (h, src) =>
            {
                Combat.Explosion(transform.position + Vector3.up * 1.5f, true);
                transform.localScale = new Vector3(scale0.x, scale0.y * 0.35f, scale0.z);
                RedbaronGame.Log($"Casamata de {Health.team.Name()} destruida");
            };
        }

        public void ResetBunker()
        {
            Health.Restore();
            transform.localScale = scale0;
        }

        void Update()
        {
            if (!Health.Alive || (RedbaronGame.Instance != null && RedbaronGame.Instance.GameOver)) return;
            cd -= Time.deltaTime;
            if (cd > 0) return;
            Vector3 eye = transform.position + Vector3.up * 1.1f;
            SoldierAI best = null; float bd = range;
            foreach (var s in SoldierAI.All)
            {
                if (!s.Alive || s.team == Health.team) continue;
                float d = Vector3.Distance(s.transform.position, transform.position);
                if (d < bd && Combat.HasLineOfSight(eye, s.Health.AimPoint, s.transform)) { bd = d; best = s; }
            }
            if (best == null) { cd = 0.5f; return; }
            cd = burstEvery;
            Sfx.BunkerGun(eye);
            for (int k = 0; k < shotsPerBurst; k++)
            {
                float p = baseHit * Mathf.Max(0.2f, 1 - bd / 50f) * best.ProtectionAgainst(transform.position);
                bool hit = Random.value < p;
                Vector3 to = best.Health.AimPoint + (hit ? Vector3.zero : Random.insideUnitSphere * 1.5f);
                Combat.Tracer(eye, to);
                if (hit && best.Alive) best.Health.Damage(1, gameObject, DamageKind.Bullet); // little use against a tank
            }
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }

    /// <summary>Hangar of an airfield. It has no weapon, but bombs, shells, charges and gunfire bring it down like any other building.</summary>
    [RequireComponent(typeof(Health))]
    public class Hangar : MonoBehaviour
    {
        public static readonly List<Hangar> All = new List<Hangar>();
        public Health Health { get; private set; }
        Vector3 scale0;

        void Awake()
        {
            Health = GetComponent<Health>();
            scale0 = transform.localScale;
            Health.Died += (h, src) =>
            {
                for (int k = 0; k < 2; k++) Combat.Explosion(transform.position + new Vector3(Random.Range(-3f, 3f), 2 + k, Random.Range(-4f, 4f)), true);
                transform.localScale = new Vector3(scale0.x, scale0.y * 0.3f, scale0.z);
                RedbaronGame.Log($"Hangar de {Health.team.Name()} destruido");
            };
        }

        public void ResetHangar()
        {
            Health.Restore();
            transform.localScale = scale0;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }

    /// <summary>
    /// A prop with no hit points (crate, barrel, a stretch of barbed wire) that any blast breaks:
    /// it vanishes in a puff of dust and comes back for the next battle.
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        public static readonly List<Breakable> All = new List<Breakable>();
        public bool Broken { get; private set; }
        Renderer[] renderers;
        Vector3 center; float radius;

        void Awake() { Measure(); }

        void Measure()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { center = transform.position; radius = 0.5f; return; }
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            center = b.center;
            radius = Mathf.Max(0.4f, Mathf.Max(b.extents.x, b.extents.z));
        }

        public static void BreakAround(Vector3 p, float r)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i];
                if (b == null || b.Broken) continue;
                Vector3 d = b.center - p; d.y = 0;
                if (d.magnitude < r + b.radius * 0.5f) b.Break();
            }
        }

        public void Break()
        {
            if (Broken) return;
            Broken = true;
            foreach (var r in renderers) if (r != null) r.enabled = false;
            Combat.Puff(center, new Color(0.5f, 0.45f, 0.36f, 0.75f), 1.2f, 1.5f + radius, 1f);
        }

        public void Restore()
        {
            if (!Broken) return;
            Broken = false;
            foreach (var r in renderers) if (r != null) r.enabled = true;
        }

        public static void RestoreAll() { foreach (var b in All) if (b != null) b.Restore(); }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }

    /// <summary>
    /// Árbol que, al recibir una bomba o un avión encima, desaparece y deja un tocón
    /// y un tronco muerto tumbado que sirve de cobertura.
    /// </summary>
    public class TreeDestructible : MonoBehaviour
    {
        public static readonly List<TreeDestructible> All = new List<TreeDestructible>();
        public GameObject deadLogPrefab;
        public Material material;
        public float height = 12f;
        public bool Fallen { get; private set; }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static void FellAround(Vector3 p, float radius)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var t = All[i];
                if (t == null || t.Fallen) continue;
                Vector3 d = t.transform.position - p; d.y = 0;
                if (d.magnitude < radius) t.Fell();
            }
        }

        public void Fell()
        {
            if (Fallen) return;
            Fallen = true;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            float yaw = Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float len = 4f + height * 0.2f;
            Vector3 mid = transform.position + dir * (len / 2 + 0.4f) + Vector3.up * 0.38f;
            GameObject log;
            if (deadLogPrefab != null)
            {
                log = Instantiate(deadLogPrefab, mid, Quaternion.Euler(0, yaw, 0), transform.parent);
            }
            else
            {
                var acc = new MeshAccumulator();
                Color bark = new Color32(0x5b, 0x51, 0x45, 255);
                acc.AddCylinder(mid - dir * len / 2, mid + dir * len / 2, 0.42f, 0.34f, 6, bark);
                acc.AddCylinder(transform.position, transform.position + Vector3.up * 0.7f, 0.46f, 0.34f, 6, new Color32(0x4f, 0x46, 0x3b, 255));
                log = acc.ToObject("TroncoMuerto", transform.parent, material, true);
            }
            var cp = GetComponent<CoverPoint>();
            if (cp != null) Destroy(cp);
            var cover = new GameObject("Cobertura_Tronco");
            cover.transform.SetParent(log.transform, true);
            cover.transform.position = mid;
            var lc = cover.AddComponent<CoverPoint>();
            lc.type = CoverType.Tronco; lc.back = 0.9f;
            if (log.GetComponentInChildren<Collider>() == null) log.AddComponent<BoxCollider>();
            BiplaneAI.RefreshObstacleGrid(transform.position, 9f); // the crown is gone: planes may fly through here now
        }
    }
}
