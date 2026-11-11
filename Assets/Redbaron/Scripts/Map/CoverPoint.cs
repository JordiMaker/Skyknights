using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    public enum CoverType { Roca, Arbol, Trinchera, Crater, Tronco }

    /// <summary>
    /// Punto de cobertura. Las rocas, árboles y troncos protegen solo si el tirador está al otro lado;
    /// trincheras y cráteres protegen en todas direcciones.
    /// </summary>
    public class CoverPoint : MonoBehaviour
    {
        public static readonly List<CoverPoint> All = new List<CoverPoint>();

        public CoverType type = CoverType.Roca;
        [Tooltip("Distancia detrás del objeto a la que se coloca el soldado (m).")]
        public float back = 1.2f;
        [Tooltip("Máximo de soldados que caben.")]
        public int capacity = 2;
        [HideInInspector] public int occupants;

        /// <summary>Multiplicador de la probabilidad de acierto contra quien está aquí.</summary>
        public static float Protection(CoverType t)
        {
            switch (t)
            {
                case CoverType.Roca: return 0.38f;
                case CoverType.Arbol: return 0.55f;
                case CoverType.Trinchera: return 0.32f;
                case CoverType.Crater: return 0.6f;
                default: return 0.45f;
            }
        }

        public bool Directional => type == CoverType.Roca || type == CoverType.Arbol || type == CoverType.Tronco;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); occupants = 0; }

        /// <summary>Dónde ponerse para cubrirse de un enemigo en <paramref name="enemyPos"/>.</summary>
        public Vector3 SpotAgainst(Vector3 enemyPos)
        {
            Vector3 p = transform.position;
            Vector3 d = enemyPos - p; d.y = 0;
            if (d.sqrMagnitude < 0.01f || back <= 0) return p;
            Vector3 s = p - d.normalized * back;
            Vector2 D = RedbaronMap.ToDesign(s);
            s.y = RedbaronMap.WalkHeight(D.x, D.y);
            return s;
        }

        /// <summary>Protección efectiva para alguien en esta cobertura contra un tirador en <paramref name="shooter"/>.</summary>
        public float ProtectionAgainst(Vector3 occupantPos, Vector3 shooter)
        {
            if (!Directional) return Protection(type);
            Vector3 a = shooter - transform.position, b = occupantPos - transform.position;
            a.y = 0; b.y = 0;
            float dot = Vector3.Dot(a.normalized, b.normalized);
            return dot < -0.2f ? Protection(type) : 0.75f;
        }
    }
}
