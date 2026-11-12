using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Redbaron
{
    /// <summary>
    /// Saca oleadas de infantería por las tres líneas. La IA (Potencias Centrales) avanza sola;
    /// los soldados del jugador (Entente) van a un punto de reunión delante de su base y esperan órdenes.
    ///
    /// Each lane takes the next unit type of the rotation in turn: an infantry squad, then a tank or a mech, and so on.
    /// All units come from the same prefab (Quick RTS selection, NavMesh agent, collider) and wear a colour kit model (UnitModels).
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Prefabs (Toon Soldiers WW2 demo)")]
        [Tooltip("Soldado de las Potencias Centrales. Puede ser el mismo prefab que el de la Entente.")]
        public GameObject soldierCentrales;
        [Tooltip("Soldado de la Entente. Añádele el componente de unidad de Quick RTS para poder seleccionarlo.")]
        public GameObject soldierEntente;
        [Tooltip("Tiñe el uniforme con el color del bando (útil si los dos bandos usan el mismo modelo).")]
        public bool tintUniforms = true;
        [Tooltip("Altura a la que se escala el modelo (m).")]
        public float soldierHeight = 1.8f;

        [Header("Units (Redbaron colour kit)")]
        [Tooltip("Every unit type: its kit models, toughness and weapon. Empty = the prefabs' Toon Soldiers riflemen.")]
        public List<UnitType> unitTypes = new List<UnitType>();
        [Tooltip("Order in which each lane sends the types, by name. Each lane starts at a different point of the list.")]
        public string[] rotation = (string[])UnitType.DefaultRotation.Clone();
        [Tooltip("How far apart the lanes are in the rotation. With an even step, all three lanes send infantry together, then vehicles together.")]
        public int laneStep = 4;
        [Tooltip("When squads fall back, regroup and follow vehicles.")]
        public Squad.Settings squads = new Squad.Settings();
        [FormerlySerializedAs("kitSoldierMaterial")]
        [Tooltip("Material for the kit units: Redbaron/LowPolyVertexColor with GPU instancing on.")]
        public Material kitMaterial;

        [Header("Oleadas")]
        public float waveEvery = 28f;
        [Tooltip("Infantry per lane, for types that do not set their own count.")]
        public int minPerLane = 2, maxPerLane = 4;
        [Tooltip("Units per side. A vehicle counts as three.")]
        public int teamCap = 60;
        [Tooltip("Bando del jugador.")]
        public Team playerTeam = Team.Entente;
        [Tooltip("Si está activo, los soldados del jugador también avanzan solos por su línea.")]
        public bool playerAutoAdvance = false;
        [Tooltip("Franja (Zm, mitad norte) donde se reúnen los soldados del jugador al salir.")]
        public float rallyZm = 40f;

        float timer = 0.5f;

        void Awake() { Squad.Config = squads; }
        readonly Dictionary<Team, int> waveNo = new Dictionary<Team, int> { { Team.Centrales, 0 }, { Team.Entente, 0 } };

        void Update()
        {
            var g = RedbaronGame.Instance;
            if (g != null && g.GameOver) return;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = waveEvery;
            SpawnWave(Team.Centrales);
            SpawnWave(Team.Entente);
        }

        public void ResetWaves()
        {
            timer = 0.5f;
            waveNo[Team.Centrales] = waveNo[Team.Entente] = 0;
        }

        int Count(Team t)
        {
            int n = 0;
            foreach (var s in SoldierAI.All) if (s.team == t && s.Alive) n += s.IsVehicle ? 3 : 1;
            return n;
        }

        UnitType TypeNamed(string name)
        {
            foreach (var u in unitTypes) if (u != null && string.Equals(u.name, name, System.StringComparison.OrdinalIgnoreCase)) return u;
            return null;
        }

        /// <summary>The type lane sends in wave number wave: the rotation entry wave + lane × laneStep, skipping names with no type.</summary>
        public UnitType TypeFor(int wave, int lane)
        {
            if (unitTypes.Count == 0 || rotation == null || rotation.Length == 0) return unitTypes.Count > 0 ? unitTypes[0] : null;
            for (int k = 0; k < rotation.Length; k++)
            {
                var u = TypeNamed(rotation[((wave + lane * laneStep + k) % rotation.Length + rotation.Length) % rotation.Length]);
                if (u != null) return u;
            }
            return unitTypes[0];
        }

        public void SpawnWave(Team t)
        {
            if (Count(t) >= teamCap) return;
            var prefab = t == Team.Centrales ? soldierCentrales : soldierEntente;
            if (prefab == null) prefab = t == Team.Centrales ? soldierEntente : soldierCentrales;
            if (prefab == null) { Debug.LogWarning("[Redbaron] Falta el prefab de soldado en WaveSpawner."); return; }
            bool player = t == playerTeam;
            int wave = waveNo[t]++;
            for (int lane = 0; lane < 3; lane++)
            {
                var type = TypeFor(wave, lane);
                var path = RedbaronMap.LanePath(t, lane);
                int lo = type != null && type.minCount > 0 ? type.minCount : minPerLane;
                int hi = type != null && type.maxCount > 0 ? type.maxCount : maxPerLane;
                int n = Random.Range(lo, Mathf.Max(lo, hi) + 1);
                var squad = type != null && type.IsVehicle ? null : new Squad(t, lane); // the men of one lane fight as a squad
                for (int k = 0; k < n; k++)
                {
                    var unit = Spawn(prefab, t, lane, type, path, k, player); // spawn first: squad?.Add(Spawn(...)) would skip it for vehicles
                    squad?.Add(unit);
                }
            }
        }

        SoldierAI Spawn(GameObject prefab, Team t, int lane, UnitType type, List<Vector3> path, int k, bool player)
        {
            bool vehicle = type != null && type.IsVehicle;
            Vector3 p = path[0] + new Vector3(Random.Range(-2f, 2f), 0, (t == Team.Centrales ? -1 : 1) * k * (vehicle ? 7f : 1.8f));
            var ai = SpawnAt(t, type, p, Quaternion.LookRotation(path[1] - path[0]), lane, player, player ? playerAutoAdvance : true, prefab);
            if (player && !playerAutoAdvance)
            {
                float x = RedbaronMap.Lanes[lane][Mathf.Min(1, RedbaronMap.Lanes[lane].Length - 1)].x;
                if (lane == 1) x = 50;
                Vector3 rally = RedbaronMap.ToWorld(x + Random.Range(-4f, 4f), RedbaronMap.ForTeam(t, rallyZm + Random.Range(-3f, 3f)));
                ai.OrderMove(rally);
            }
            return ai;
        }

        /// <summary>One unit of type for side t at p (snapped to the NavMesh), dressed in its kit model. Used by the waves and
        /// by test scenes (AITestBench). prefab = null takes the side's soldier prefab.</summary>
        public SoldierAI SpawnAt(Team t, UnitType type, Vector3 p, Quaternion rot, int lane, bool player, bool advance, GameObject prefab = null)
        {
            if (prefab == null) prefab = t == Team.Centrales ? (soldierCentrales != null ? soldierCentrales : soldierEntente) : (soldierEntente != null ? soldierEntente : soldierCentrales);
            bool vehicle = type != null && type.IsVehicle;
            if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) p = hit.position;
            var go = Instantiate(prefab, p, rot);
            go.name = type != null ? $"{type.name}_{t}_{lane}" : $"Soldado_{t}_{lane}";
            var ai = RedbaronMapBuilder.GetOrAdd<SoldierAI>(go);
            ai.Init(t, lane, player, advance, type);

            UnitModels.Look look = null;
            var looks = type == null ? null : t == Team.Centrales ? type.modelsCentrales : type.modelsEntente;
            if (looks != null && looks.Length > 0)
                look = UnitModels.Apply(go, looks[Random.Range(0, looks.Length)], t, kitMaterial, type.maxHeight, vehicle);
            if (soldierHeight > 0 && !vehicle) RedbaronMapBuilder.FitHeight(go, soldierHeight);
            ai.SetLook(look);

            var agent = RedbaronMapBuilder.GetOrAdd<NavMeshAgent>(go);
            agent.Warp(p);
            if (go.GetComponent<Collider>() == null)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                float s = Mathf.Max(0.01f, go.transform.lossyScale.y);
                cc.height = 1.8f / s; cc.radius = 0.3f / s; cc.center = new Vector3(0, 0.9f / s, 0);
            }
            if (tintUniforms && look == null) Tint(go, t.Uniform()); // the kit units already wear their side's colours
            return ai;
        }

        /// <summary>The type with this name (case does not matter), or null.</summary>
        public UnitType TypeByName(string name) => TypeNamed(name);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static void Tint(GameObject go, Color c)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(ColorId, Color.Lerp(Color.white, c, 0.55f));
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
