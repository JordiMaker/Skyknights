using UnityEngine;
using UnityEngine.AI;

namespace Redbaron
{
    /// <summary>
    /// Saca oleadas de infantería por las tres líneas. La IA (Potencias Centrales) avanza sola;
    /// los soldados del jugador (Entente) van a un punto de reunión delante de su base y esperan órdenes.
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

        [Header("Big-headed soldiers (Redbaron colour kit)")]
        [Tooltip("Entente infantry models (Adrian helmet), one picked at random for each soldier. They replace the prefab's Toon Soldiers look. Empty = keep that look.")]
        public GameObject[] kitSoldiersEntente;
        [Tooltip("Central Powers infantry models (steel helmet).")]
        public GameObject[] kitSoldiersCentrales;
        [Tooltip("Material for the kit soldiers: Redbaron/LowPolyVertexColor with GPU instancing on.")]
        public Material kitSoldierMaterial;

        [Header("Oleadas")]
        public float waveEvery = 28f;
        public int minPerLane = 2, maxPerLane = 4;
        public int teamCap = 60;
        [Tooltip("Bando del jugador.")]
        public Team playerTeam = Team.Entente;
        [Tooltip("Si está activo, los soldados del jugador también avanzan solos por su línea.")]
        public bool playerAutoAdvance = false;
        [Tooltip("Franja (Zm, mitad norte) donde se reúnen los soldados del jugador al salir.")]
        public float rallyZm = 40f;

        float timer = 0.5f;

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

        public void ResetWaves() { timer = 0.5f; }

        int Count(Team t)
        {
            int n = 0;
            foreach (var s in SoldierAI.All) if (s.team == t && s.Alive) n++;
            return n;
        }

        public void SpawnWave(Team t)
        {
            if (Count(t) >= teamCap) return;
            var prefab = t == Team.Centrales ? soldierCentrales : soldierEntente;
            if (prefab == null) prefab = t == Team.Centrales ? soldierEntente : soldierCentrales;
            if (prefab == null) { Debug.LogWarning("[Redbaron] Falta el prefab de soldado en WaveSpawner."); return; }
            bool player = t == playerTeam;
            for (int lane = 0; lane < 3; lane++)
            {
                var path = RedbaronMap.LanePath(t, lane);
                int n = Random.Range(minPerLane, maxPerLane + 1);
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = path[0] + new Vector3(Random.Range(-2f, 2f), 0, (t == Team.Centrales ? -1 : 1) * k * 1.8f);
                    if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) p = hit.position;
                    var go = Instantiate(prefab, p, Quaternion.LookRotation(path[1] - path[0]));
                    go.name = $"Soldado_{t}_{lane}";
                    var looks = t == Team.Centrales ? kitSoldiersCentrales : kitSoldiersEntente;
                    bool kitLook = looks != null && looks.Length > 0
                        && SoldierModels.Apply(go, looks[Random.Range(0, looks.Length)], t, kitSoldierMaterial);
                    if (soldierHeight > 0) RedbaronMapBuilder.FitHeight(go, soldierHeight);
                    var agent = RedbaronMapBuilder.GetOrAdd<NavMeshAgent>(go);
                    agent.Warp(p);
                    if (go.GetComponent<Collider>() == null)
                    {
                        var cc = go.AddComponent<CapsuleCollider>();
                        float s = Mathf.Max(0.01f, go.transform.lossyScale.y);
                        cc.height = 1.8f / s; cc.radius = 0.3f / s; cc.center = new Vector3(0, 0.9f / s, 0);
                    }
                    var ai = RedbaronMapBuilder.GetOrAdd<SoldierAI>(go);
                    ai.Init(t, lane, player, player ? playerAutoAdvance : true);
                    if (tintUniforms && !kitLook) Tint(go, t.Uniform()); // the kit soldiers already wear their side's colours
                    if (player && !playerAutoAdvance)
                    {
                        float x = RedbaronMap.Lanes[lane][Mathf.Min(1, RedbaronMap.Lanes[lane].Length - 1)].x;
                        if (lane == 1) x = 50;
                        Vector3 rally = RedbaronMap.ToWorld(x + Random.Range(-4f, 4f), RedbaronMap.ForTeam(t, rallyZm + Random.Range(-3f, 3f)));
                        ai.OrderMove(rally);
                    }
                }
            }
        }

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
