using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Anima las piezas móviles de las naves del hangar Redbaron. Ponlo en la raíz del modelo importado.
    /// Hélices "Helice_*": giran sobre su eje Z local. Rotores "Rotor" y "Rotor_*": giran sobre su eje Y local.
    /// Alas batientes "Ala_I" y "Ala_D" (Murciélago): suben y bajan sobre su eje Z local.
    /// </summary>
    public class GiroPiezas : MonoBehaviour
    {
        public float rpmHelice = 1400f;
        public float rpmRotor = 60f;
        [Tooltip("Aleteos por segundo del ornitóptero.")]
        public float aleteos = 0.65f;
        [Tooltip("Amplitud del aleteo en grados.")]
        public float amplitud = 26f;

        Transform[] helices, rotores;
        Transform alaI, alaD;
        Quaternion alaI0, alaD0;

        void Awake()
        {
            var h = new System.Collections.Generic.List<Transform>();
            var r = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("_Malla")) continue;
                if (t.name.StartsWith("Helice_")) h.Add(t);
                else if (t.name.StartsWith("Rotor")) r.Add(t);
                else if (t.name == "Ala_I") { alaI = t; alaI0 = t.localRotation; }
                else if (t.name == "Ala_D") { alaD = t; alaD0 = t.localRotation; }
            }
            helices = h.ToArray(); rotores = r.ToArray();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var t in helices) t.Rotate(0, 0, rpmHelice * 6f * dt, Space.Self);
            foreach (var t in rotores) t.Rotate(0, rpmRotor * 6f * dt, 0, Space.Self);
            float a = Mathf.Sin(Time.time * aleteos * Mathf.PI * 2f) * amplitud;
            if (alaI != null) alaI.localRotation = alaI0 * Quaternion.Euler(0, 0, -a);
            if (alaD != null) alaD.localRotation = alaD0 * Quaternion.Euler(0, 0, a);
        }
    }
}
