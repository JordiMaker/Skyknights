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
                mpb.SetColor("_Color", c);
                mr.SetPropertyBlock(mpb);
            }
            if (k >= 1) { enabled = false; done?.Invoke(); }
        }
    }
}
