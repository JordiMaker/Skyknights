using UnityEngine;

namespace Redbaron
{
    /// <summary>Crea los cuatro pilotos del prototipo: Fokker y Albatros (Centrales), dos Camel (Entente).</summary>
    public class AirWar : MonoBehaviour
    {
        [Tooltip("Material con el shader Redbaron/LowPolyVertexColor. Si se deja vacío se usa el del constructor del mapa.")]
        public Material planeMaterial;
        public bool spawnOnStart = true;
        [Tooltip("You fly the first Entente plane (left/right roll, up/down speed, Z or Space fire). Off = four AI pilots.")]
        public bool playerFliesCamel = true;
        public string playerPilotName = "Tu Camel";

        [Header("Flight plane")]
        [Tooltip("All aircraft and air-to-air bullets share this one height (m). 0 = automatic: just above the highest obstacle inside the map. Negative = terrain following, as in the web prototype.")]
        public float flightAltitude = 0f;
        [Tooltip("Room left above the highest obstacle when the flight altitude is automatic (m).")]
        public float altitudeMargin = 3f;

        [Header("Plane models")]
        [Tooltip("Every spawn and respawn picks one of these at random, for every pilot. The team is told apart by its tint.")]
        public GameObject[] planeModels;
        [Tooltip("Also pick among the kit's built biplanes (Camel, Fokker triplane, Albatros).")]
        public bool includeBuiltBiplanes = true;
        [Tooltip("Scale every model to Model Span (on), or use Model Scale (off; the Redbaron hangar ships are drawn at 0.6, their game scale).")]
        public bool fitWingspan = false;
        [Tooltip("Wingspan the models are scaled to when Fit Wingspan is on (m).")]
        public float modelSpan = 7.5f;
        public float modelScale = 0.6f;

        [Header("Engine sound (Vehicle - Essentials)")]
        [Tooltip("Engine loop for your plane; its pitch follows your speed.")]
        public AudioClip playerEngine;
        [Tooltip("Engine loop for AI planes (3D sound).")]
        public AudioClip aiEngine;
        [Tooltip("Engine loop heard inside the cockpit (first-person camera).")]
        public AudioClip cockpitEngine;

        [Header("Refill")]
        [Tooltip("A floating icon over each airfield: flying through it restores energy, ammunition and bombs for that side.")]
        public bool refillIcons = true;

        public BiplaneAI Fokker { get; private set; }
        /// <summary>The plane the player flies (null when all four are AI).</summary>
        public BiplaneAI Player { get; private set; }

        void Start()
        {
            if (!spawnOnStart) return;
            var mat = planeMaterial;
            if (mat == null && RedbaronMapBuilder.Instance != null) mat = RedbaronMapBuilder.Instance.vertexColorMaterial;
            if (mat == null) mat = new Material(Shader.Find("Redbaron/LowPolyVertexColor"));

            BiplaneAI.ResetObstacleGrid();
            BiplaneAI.FlightAltitude = flightAltitude < 0 ? float.NaN : flightAltitude > 0 ? flightAltitude : HighestObstacleInside() + altitudeMargin;
            if (!float.IsNaN(BiplaneAI.FlightAltitude)) Debug.Log($"[Redbaron] Flight plane at {BiplaneAI.FlightAltitude:0.0} m");

            if (refillIcons && !float.IsNaN(BiplaneAI.FlightAltitude))
                foreach (var t in new[] { Team.Entente, Team.Centrales })
                {
                    Rect r = RedbaronMap.AirfieldWorldRect(t);
                    AirPickup.Create(t, new Vector3(r.center.x, BiplaneAI.FlightAltitude, r.center.y), mat).transform.SetParent(transform, true);
                }

            var first = playerFliesCamel
                ? BiplaneAI.Spawn(playerPilotName, Team.Entente, PlaneModel.Camel, 4.2f, 1.7f, 9.5f, mat)
                : BiplaneAI.Spawn("Camel", Team.Entente, PlaneModel.Camel, 3.0f, 1.4f, 9f, mat);
            if (playerFliesCamel) { first.playerControlled = true; Player = first; }
            Setup(first);
            Fokker = BiplaneAI.Spawn("Fokker", Team.Centrales, PlaneModel.FokkerTriplano, 2.4f, 1.0f, 9.5f, mat);
            Setup(Fokker);
            Setup(BiplaneAI.Spawn("Albatros", Team.Centrales, PlaneModel.Albatros, 3.4f, 1.6f, 8.5f, mat));
            Setup(BiplaneAI.Spawn("Camel", Team.Entente, PlaneModel.Camel, 2.6f, 1.1f, 9f, mat));
        }

        int slotsEntente, slotsCentrales;

        void Setup(BiplaneAI ai)
        {
            ai.modelPool = planeModels;
            ai.poolIncludesBuilt = includeBuiltBiplanes && planeModels != null && planeModels.Length > 0;
            ai.visualSpan = fitWingspan ? modelSpan : 0f;
            ai.visualScale = modelScale;
            // Each pilot always comes back on its own side of its own airfield.
            ai.homeSlot = ai.team == Team.Entente ? slotsEntente++ : slotsCentrales++;
            var clip = ai.playerControlled ? playerEngine : (aiEngine != null ? aiEngine : playerEngine);
            if (clip != null)
            {
                var a = ai.gameObject.AddComponent<PlaneEngineAudio>();
                a.plane = ai; a.exterior = clip; a.cockpit = ai.playerControlled ? cockpitEngine : null;
            }
        }

        /// <summary>Highest terrain, tree, rock or building inside the map, sampled every 1.5 m with a thin sphere so tree trunks are not missed.</summary>
        static float HighestObstacleInside()
        {
            float W = RedbaronMap.WorldW, L = RedbaronMap.WorldL, top = 0f;
            LayerMask mask = ~(1 << 2);
            for (float x = 1f; x < W - 1f; x += 1.5f)
                for (float z = 1f; z < L - 1f; z += 1.5f)
                    if (Physics.SphereCast(new Vector3(x, 250f, z), 0.75f, Vector3.down, out var hit, 400f, mask, QueryTriggerInteraction.Ignore))
                        top = Mathf.Max(top, hit.point.y);
            return top;
        }
    }
}
