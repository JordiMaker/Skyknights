using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Ciclo de marcha procedimental para los mechs del hangar Redbaron.
    /// Mueve los nodos Pierna_I/D (cadera), Rodilla_I/D, Brazo_I/D y Pata_N según la velocidad del mech.
    /// La velocidad se mide sola a partir del movimiento del objeto (sirve con NavMeshAgent o con cualquier script).
    /// "Torso" se puede girar desde otro script para apuntar; este no lo toca.
    /// </summary>
    public class AndarMech : MonoBehaviour
    {
        [Tooltip("Velocidad (m/s) a la que el paso alcanza su amplitud máxima.")]
        public float velocidadPlena = 2f;
        [Tooltip("Pasos por segundo a velocidad plena.")]
        public float cadencia = 0.8f;
        public float cadera = 18f, rodilla = 32f, brazo = 9f, pata = 10f;
        [Tooltip("Fuerza la marcha aunque el objeto no se mueva (para probar).")]
        public bool andarSiempre;

        class Joint { public Transform t; public Quaternion q0; public float side, bend; public int idx; }
        readonly List<Joint> legs = new List<Joint>(), knees = new List<Joint>(), arms = new List<Joint>(), patas = new List<Joint>();
        Vector3 last; float phase, amount;

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("_Malla")) continue;
                float side = t.name.EndsWith("_I") ? 1f : -1f;
                if (t.name.StartsWith("Pierna_")) legs.Add(new Joint { t = t, q0 = t.localRotation, side = side });
                else if (t.name.StartsWith("Rodilla_")) knees.Add(new Joint { t = t, q0 = t.localRotation, side = side, bend = t.localPosition.z > 0 ? 1f : -1f });
                else if (t.name.StartsWith("Brazo_")) arms.Add(new Joint { t = t, q0 = t.localRotation, side = side });
                else if (t.name.StartsWith("Pata_") && int.TryParse(t.name.Substring(5), out int n)) patas.Add(new Joint { t = t, q0 = t.localRotation, idx = n });
            }
            last = transform.position;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 v = (transform.position - last) / dt; v.y = 0; last = transform.position;
            float target = andarSiempre ? 1f : Mathf.Clamp01(v.magnitude / Mathf.Max(0.01f, velocidadPlena));
            amount = Mathf.MoveTowards(amount, target, dt * 3f);
            phase += dt * cadencia * Mathf.PI * 2f * Mathf.Max(0.25f, amount);
            float s = Mathf.Sin(phase);
            foreach (var j in legs) j.t.localRotation = j.q0 * Quaternion.Euler(j.side * s * cadera * amount, 0, 0);
            foreach (var j in knees)
            {
                float lift = Mathf.Max(0f, Mathf.Sin(phase + 1.4f + (j.side > 0 ? 0f : Mathf.PI)));
                j.t.localRotation = j.q0 * Quaternion.Euler(j.bend * lift * rodilla * amount, 0, 0);
            }
            foreach (var j in arms) j.t.localRotation = j.q0 * Quaternion.Euler(-j.side * s * brazo * amount, 0, 0);
            foreach (var j in patas) j.t.localRotation = j.q0 * Quaternion.Euler(0, Mathf.Sin(phase + (j.idx % 2) * Mathf.PI) * pata * amount, 0);
        }
    }
}
