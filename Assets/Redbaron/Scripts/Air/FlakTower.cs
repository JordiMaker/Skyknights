using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Redbaron
{
    /// <summary>
    /// Anti-aircraft tower (the "Torre Flak" model of the Redbaron colour kit). It fires flak shells at enemy planes in reach.
    /// There is no lead and no aim assist: the turret turns towards where the plane is, fires along its barrels once they
    /// point at it, and the shell bursts at the plane's distance at that moment, on the flight plane. A fast plane is gone
    /// by then; a slow or turning one is caught. Planes close to a burst lose one energy point.
    /// The tower stands lower than the flight plane, so planes pass over it. Bombs destroy it.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class FlakTower : MonoBehaviour
    {
        public static readonly List<FlakTower> All = new List<FlakTower>();

        [Tooltip("Horizontal reach (m).")]
        public float range = 60f;
        [Tooltip("Time between shells (s).")]
        public float fireEvery = 2.5f;
        [Tooltip("Shell speed (m/s).")]
        public float shellSpeed = 110f;
        [Tooltip("A plane whose edge is this close to a burst (m) takes a hit.")]
        public float burstRadius = 5f;
        [Tooltip("Random error of each burst around the point the barrels were aimed at (m).")]
        public float scatter = 2.5f;
        [Tooltip("How fast the turret turns and the barrels lift (degrees per second).")]
        public float turnRate = 80f;
        [Tooltip("The gun fires only when the turret points within this angle of its target (degrees).")]
        public float fireCone = 5f;

        public Health Health { get; private set; }
        Transform turret, barrels;
        float barrelRest; // the barrels' own elevation in the model (degrees)
        float cd, yaw, pitch, sweepT;
        BiplaneAI target;
        Vector3 scale0;
        FlakGun gun;

        // ------------------------------------------------------------ Building
        /// <summary>Puts a tower on the ground at ground, scaled to height (m), painted in its side's colours and facing
        /// the enemy. The kit's OBJ shares one origin for all parts, so the turret and the barrels get their own pivots.</summary>
        public static FlakTower Build(GameObject model, Team team, Vector3 ground, float height, Transform parent)
        {
            var root = new GameObject("TorreAntiaerea_" + team);
            root.transform.SetParent(parent, false);
            root.transform.position = ground;
            root.transform.rotation = Quaternion.Euler(0, team == Team.Centrales ? 180f : 0f, 0); // the Central Powers are north

            var m = BuildDisplay(model, team, root.transform, out Transform turretPivot, out Transform barrelPivot, out float rest, out Bounds local);
            float k = height / Mathf.Max(0.1f, local.size.y);
            m.transform.localScale = Vector3.one * k;

            Vector3 size = new Vector3(local.size.x * k * 0.8f, height, local.size.z * k * 0.8f), center = new Vector3(0, height / 2f, 0);
            var box = root.AddComponent<BoxCollider>();
            box.center = center; box.size = size;
            var ob = root.AddComponent<NavMeshObstacle>();
            ob.shape = NavMeshObstacleShape.Box; ob.center = center; ob.size = size; ob.carving = true;

            var hp = root.AddComponent<Health>();
            hp.team = team; hp.kind = TargetKind.Bunker; hp.maxHp = hp.hp = 18; hp.aimHeight = height * 0.5f;
            var tower = root.AddComponent<FlakTower>();
            tower.turret = turretPivot; tower.barrels = barrelPivot; tower.barrelRest = rest;
            return tower;
        }

        /// <summary>The tower model as a display piece (also used by the menu card): painted for its side,
        /// with its turret and barrels on their own pivots. local = the model's bounds before scaling.</summary>
        public static GameObject BuildDisplay(GameObject model, Team team, Transform parent, out Transform turretPivot, out Transform barrelPivot, out float barrelRest, out Bounds local)
        {
            var m = Instantiate(model, parent);
            m.name = "Modelo_" + model.name;
            m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);

            local = new Bounds(); bool first = true;
            Transform turretMesh = null, barrelMesh = null;
            Bounds tb = default, bb = default;
            foreach (var mf in m.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds; // OBJ parts sit at the model origin, so this is model space
                if (first) { local = b; first = false; } else local.Encapsulate(b);
                if (mf.name == "Torreta") { turretMesh = mf.transform; tb = b; }
                else if (mf.name == "Elevacion") { barrelMesh = mf.transform; bb = b; }
            }

            turretPivot = barrelPivot = null; barrelRest = 0;
            if (turretMesh != null)
            {
                turretPivot = new GameObject("Torreta").transform;
                turretPivot.SetParent(m.transform, false);
                turretPivot.localPosition = new Vector3(tb.center.x, tb.min.y, tb.center.z);
                turretMesh.SetParent(turretPivot, true);
                turretMesh.name = "Torreta_Malla";
            }
            if (barrelMesh != null)
            {
                // The trunnion: near the rear and the bottom of the barrels.
                Vector3 pv = new Vector3(bb.center.x, bb.min.y + 0.15f * bb.size.y, bb.min.z + 0.12f * bb.size.z);
                barrelPivot = new GameObject("Elevacion").transform;
                barrelPivot.SetParent(turretPivot != null ? turretPivot : m.transform, false);
                barrelPivot.position = m.transform.TransformPoint(pv);
                barrelPivot.localRotation = Quaternion.identity;
                barrelMesh.SetParent(barrelPivot, true);
                barrelMesh.name = "Elevacion_Malla";
                barrelRest = Mathf.Atan2(bb.max.y - pv.y, Mathf.Max(0.01f, bb.max.z - pv.z)) * Mathf.Rad2Deg;
            }

            var rs = m.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (TeamLivery.HasZones(rs)) TeamLivery.Apply(m, team, false);
            return m;
        }

        // ------------------------------------------------------------ Behaviour
        void Awake()
        {
            Health = GetComponent<Health>();
            scale0 = transform.localScale;
            cd = 1f + Random.value;
            gun = new FlakGun(Health.team, "La torre antiaérea") { shellSpeed = shellSpeed, burstRadius = burstRadius, scatter = scatter };
            Health.Died += (h, src) =>
            {
                Combat.Explosion(transform.position + Vector3.up * 2f, true);
                transform.localScale = new Vector3(scale0.x, scale0.y * 0.35f, scale0.z);
                target = null;
                RedbaronGame.Log($"Torre antiaérea de {Health.team.Name()} destruida");
            };
        }

        public void ResetTower()
        {
            Health.Restore();
            transform.localScale = scale0;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        Vector3 Muzzle => barrels != null ? barrels.position : transform.position + Vector3.up * Health.aimHeight * 2f;

        bool InReach(BiplaneAI p)
        {
            if (p == null || !p.Alive || p.team == Health.team) return false;
            Vector3 d = p.Body.Position - transform.position; d.y = 0;
            return d.magnitude <= range;
        }

        BiplaneAI Nearest()
        {
            BiplaneAI best = null; float bd = float.MaxValue;
            Vector3 muzzle = Muzzle;
            foreach (var p in BiplaneAI.All)
            {
                if (!InReach(p)) continue;
                float d = Vector3.Distance(p.Body.Position, transform.position);
                if (d < bd && Combat.HasLineOfSight(muzzle, p.Body.Position, p.transform)) { bd = d; best = p; }
            }
            return best;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            gun.team = Health.team;
            gun.Update(dt);
            if (!Health.Alive || (RedbaronGame.Instance != null && RedbaronGame.Instance.GameOver)) return;

            cd -= dt;
            if (!InReach(target)) target = null;
            if (target == null && cd <= 0.3f) target = Nearest();

            // Turn the turret towards where the target is now (or sweep), and lift the barrels.
            float wantYaw, wantPitch = barrelRest * 0.5f;
            Vector3 muzzle = Muzzle;
            if (target != null)
            {
                Vector3 to = target.Body.Position - muzzle;
                Vector3 local = transform.InverseTransformDirection(to);
                wantYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                wantPitch = Mathf.Clamp(Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -5f, 80f);
            }
            else { sweepT += dt; wantYaw = Mathf.Sin(sweepT * 0.45f) * 70f; }
            yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, turnRate * dt);
            pitch = Mathf.MoveTowards(pitch, wantPitch, turnRate * 0.6f * dt);
            if (turret != null) turret.localRotation = Quaternion.Euler(0, yaw, 0);
            if (barrels != null) barrels.localRotation = Quaternion.Euler(barrelRest - pitch, 0, 0);

            if (target == null || cd > 0) return;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, wantYaw)) > fireCone) return;
            Fire(muzzle, target.Body.Position);
        }

        /// <summary>One shell along the barrels' heading, timed to burst at the target's present distance.</summary>
        void Fire(Vector3 muzzle, Vector3 targetNow)
        {
            cd = fireEvery;
            float dist = new Vector2(targetNow.x - muzzle.x, targetNow.z - muzzle.z).magnitude;
            Vector3 heading = transform.rotation * Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            gun.Fire(muzzle, heading, dist, targetNow.y);
        }
    }

    /// <summary>
    /// The shells of an anti-aircraft gun: the tower's, or the scout mech's (SoldierAI). A shell flies along the gun's
    /// heading and bursts at the distance the target had when it was fired, on the flight plane; a plane whose edge is
    /// within burstRadius of the burst loses one energy point.
    /// </summary>
    public class FlakGun
    {
        public float shellSpeed = 110f, burstRadius = 5f, scatter = 2.5f;
        public Team team;
        /// <summary>Who gets the kill in the battle log.</summary>
        public string by;
        /// <summary>Shells fired and planes hit, for the tests.</summary>
        public static int Fired, Hits;

        class Shell { public Vector3 from, to; public float t, duration; }
        readonly List<Shell> shells = new List<Shell>();

        public FlakGun(Team team, string by) { this.team = team; this.by = by; }

        /// <summary>A shell from muzzle along heading (flat), bursting dist metres out at the planes' height (targetY with no flight plane).</summary>
        public void Fire(Vector3 muzzle, Vector3 heading, float dist, float targetY)
        {
            heading.y = 0;
            if (heading.sqrMagnitude < 1e-4f) return;
            Vector3 burst = muzzle + heading.normalized * dist + Random.insideUnitSphere * scatter;
            burst.y = float.IsNaN(BiplaneAI.FlightAltitude) ? targetY : BiplaneAI.FlightAltitude; // every burst is on the flight plane
            shells.Add(new Shell { from = muzzle, to = burst, duration = Mathf.Max(0.05f, Vector3.Distance(muzzle, burst) / shellSpeed) });
            Sfx.Thud(muzzle, 0.8f);
            Combat.Puff(muzzle, new Color(0.85f, 0.8f, 0.7f, 0.6f), 0.4f, 1.2f, 0.5f);
            Fired++;
        }

        public void Update(float dt)
        {
            for (int i = shells.Count - 1; i >= 0; i--)
            {
                var s = shells[i];
                s.t += dt;
                float f = Mathf.Clamp01(s.t / s.duration);
                Vector3 p = Vector3.Lerp(s.from, s.to, f), dir = (s.to - s.from).normalized;
                Combat.Tracer(p - dir * 2.5f, p, 0.03f, 0.18f);
                if (f < 1f) continue;
                Burst(s.to);
                shells.RemoveAt(i);
            }
        }

        void Burst(Vector3 p)
        {
            Sfx.Explosion(p, false);
            Combat.Puff(p, new Color(1f, 0.7f, 0.3f, 0.9f), 0.15f, 2.5f, 0f);
            Combat.Puff(p, new Color(0.13f, 0.12f, 0.11f, 0.9f), 1.6f, 4f, 0.4f);
            foreach (var plane in new List<BiplaneAI>(BiplaneAI.All))
            {
                if (!plane.Alive || plane.team == team) continue;
                Vector3 d = plane.Body.Position - p; d.y = 0;
                if (d.magnitude <= burstRadius + plane.BodyRadius) { plane.TakeHit(by, team); Hits++; }
            }
        }
    }
}
