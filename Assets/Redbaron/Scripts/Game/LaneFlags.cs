using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Redbaron
{
    /// <summary>
    /// The player's orders, one per lane, shown as a big flag. A flag at the enemy command post (where all three start)
    /// means the lane's default order: advance and destroy the enemy position. A flag anywhere else is an intermediate
    /// objective: the lane's troops go there, fighting whatever comes within reach, spread out over the stretch around
    /// the flag and take the tactical spots there (rocks, trenches, high ground) until the flag moves again.
    /// Click a lane in the RTS test screen to move its flag (AITestBench); LaneFlags.Set does it from code.
    /// </summary>
    public class LaneFlags : MonoBehaviour
    {
        public static LaneFlags Instance { get; private set; }

        [Tooltip("The side whose lanes these are.")]
        public Team team = Team.Entente;
        [Tooltip("Flag pole height and cloth size (m).")]
        public float poleHeight = 12f, clothSize = 4.5f;
        [Tooltip("How far the lane's troops spread to each side of the flag, and along the lane (m).")]
        public float spreadSide = 14f, spreadAlong = 16f;
        [Tooltip("A flag this close to the enemy command post means the default order (m).")]
        public float postRadius = 14f;

        class Flag { public Transform root; public Vector3 pos, along, side; public bool atPost = true; }
        readonly Flag[] flags = new Flag[3];
        Material material;
        bool placed;

        public int Count => flags.Length;
        /// <summary>Where the flag of lane stands.</summary>
        public Vector3 Position(int lane) => flags[lane] != null ? flags[lane].pos : Vector3.zero;
        /// <summary>True when the lane's order is the default: advance on the enemy command post.</summary>
        public bool AtPost(int lane) => flags[lane] == null || flags[lane].atPost;

        /// <summary>The scene's flags, made if there are none (RedbaronGame does this).</summary>
        public static LaneFlags Ensure(Team playerTeam)
        {
            if (Instance != null) return Instance;
            var found = FindAnyObjectByType<LaneFlags>();
            if (found != null) { Instance = found; return found; }
            var go = new GameObject("Banderas");
            var f = go.AddComponent<LaneFlags>();
            f.team = playerTeam;
            return f;
        }

        /// <summary>The intermediate objective of team's lane, if its flag is away from the enemy command post.</summary>
        public static bool TryObjective(Team t, int lane, out Vector3 p)
        {
            p = Vector3.zero;
            var i = Instance;
            if (i == null || t != i.team || lane < 0 || lane >= i.flags.Length || i.flags[lane] == null || i.flags[lane].atPost) return false;
            p = i.flags[lane].pos;
            return true;
        }

        /// <summary>A unit's own spot on the stretch: u and v in -1..1 pick a place across and along the lane.</summary>
        public Vector3 SpreadPoint(int lane, float u, float v, bool vehicle)
        {
            var f = flags[lane];
            if (f == null) return Vector3.zero;
            Vector3 p = f.pos + f.side * u * (vehicle ? spreadSide * 0.5f : spreadSide) + f.along * v * spreadAlong;
            if (NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas)) return hit.position;
            return f.pos;
        }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var sh = Shader.Find("Redbaron/LowPolyVertexColor");
            material = new Material(sh != null ? sh : Shader.Find("Standard")) { name = "Banderas_VertexColor" };
            for (int i = 0; i < flags.Length; i++) flags[i] = new Flag { root = Build(i) };
            ResetAll();
        }

        void Update()
        {
            if (!placed) ResetAll(); // the command posts may be built after this
        }

        Transform Build(int lane)
        {
            var acc = new MeshAccumulator();
            Color cloth = team.FlagColor();
            acc.AddCylinder(Vector3.zero, Vector3.up * poleHeight, 0.3f, 0.18f, 8, new Color(0.35f, 0.28f, 0.2f));
            acc.AddCone(Vector3.up * poleHeight, 0.45f, 0.9f, 8, new Color(0.85f, 0.75f, 0.3f));
            float s = clothSize;
            // The cloth, and a second one across it, so the flag reads from the RTS camera above as well as from the ground.
            acc.AddBox(new Vector3(s * 0.75f + 0.12f, poleHeight - s / 2 - 0.2f, 0), new Vector3(s * 1.5f, s, 0.14f), cloth);
            acc.AddBox(new Vector3(0, poleHeight - s / 2 - 0.2f, s * 0.75f + 0.12f), new Vector3(0.14f, s, s * 1.5f), cloth);
            // The lane number as bars on the cloth, in the kit's pale paint.
            for (int k = 0; k <= lane; k++)
                acc.AddBox(new Vector3(s * 0.75f + 0.12f + (k - lane * 0.5f) * 0.5f, poleHeight - s / 2 - 0.2f, 0), new Vector3(0.22f, s * 0.6f, 0.14f), new Color(0.93f, 0.9f, 0.82f));
            // A base ring on the ground, so the spot reads from above.
            acc.AddCylinder(Vector3.up * 0.05f, Vector3.up * 0.3f, 3.2f, 3.2f, 20, cloth);
            acc.AddCylinder(Vector3.up * 0.31f, Vector3.up * 0.36f, 2.4f, 2.4f, 20, new Color(0.2f, 0.18f, 0.15f));
            var go = acc.ToObject("Bandera_Carril" + (lane + 1), transform, material, false);
            return go.transform;
        }

        /// <summary>The flags change hands (the test screen's side switch): rebuilt in the new side's colour, back at its enemy's post.</summary>
        public void SetTeam(Team t)
        {
            team = t;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i] != null && flags[i].root != null) Destroy(flags[i].root.gameObject);
                flags[i] = new Flag { root = Build(i) };
            }
            placed = false;
            ResetAll();
        }

        /// <summary>Every flag back at the enemy command post: the default order on all three lanes.</summary>
        public void ResetAll()
        {
            var post = CommandPost.Of(team.Enemy());
            if (post == null) return;
            placed = true;
            for (int i = 0; i < flags.Length; i++)
            {
                Vector3 p = post.transform.position + new Vector3((i - 1) * 7f, 0, 0);
                Set(i, p, false);
            }
        }

        /// <summary>Puts the flag of lane at world: the lane's new objective, or the default order when it is at the enemy post.</summary>
        public void Set(int lane, Vector3 world, bool log = true)
        {
            if (lane < 0 || lane >= flags.Length || flags[lane] == null) return;
            var f = flags[lane];
            var post = CommandPost.Of(team.Enemy());
            if (NavMesh.SamplePosition(world, out var hit, 12f, NavMesh.AllAreas)) world = hit.position;
            f.pos = world;
            f.atPost = post != null && Flat(post.transform.position - world) < postRadius;
            Vector3 toEnemy = post != null ? post.transform.position - world : Vector3.forward; toEnemy.y = 0;
            f.along = toEnemy.sqrMagnitude > 0.01f ? toEnemy.normalized : Vector3.forward;
            f.side = new Vector3(-f.along.z, 0, f.along.x);
            if (f.root != null)
            {
                f.root.position = world;
                f.root.rotation = Quaternion.LookRotation(f.side);
            }
            if (log) RedbaronGame.Log(f.atPost ? $"Carril {lane + 1}: avanzar hasta destruir el cuartel enemigo" : $"Carril {lane + 1}: ocupar el tramo de la bandera");
        }

        /// <summary>The lane whose path runs closest to world.</summary>
        public int NearestLane(Vector3 world)
        {
            int best = 1; float bd = float.MaxValue;
            for (int lane = 0; lane < flags.Length; lane++)
            {
                var path = RedbaronMap.LanePath(team, lane);
                for (int i = 1; i < path.Count; i++)
                {
                    float d = DistanceToSegment(world, path[i - 1], path[i]);
                    if (d < bd) { bd = d; best = lane; }
                }
            }
            return best;
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
    }
}
