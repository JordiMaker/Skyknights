using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Redbaron
{
    /// <summary>
    /// Junta muchas cajas y triángulos de colores en una sola malla low poly
    /// (caras planas, color por vértice). Se dibuja con el shader Redbaron/LowPolyVertexColor.
    /// </summary>
    public class MeshAccumulator
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<int> tris = new List<int>();

        public int VertexCount => verts.Count;

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            cols.Add(col); cols.Add(col); cols.Add(col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            AddTriangle(a, b, c, col);
            AddTriangle(a, c, d, col);
        }

        static readonly Vector3[] Corners =
        {
            new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
        };
        static readonly int[][] Faces =
        {
            new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 },
            new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 }
        };

        /// <summary>Caja centrada en <paramref name="center"/>, tamaño completo <paramref name="size"/>.</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, Color col)
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
                p[i] = center + rot * Vector3.Scale(Corners[i], size * 0.5f);
            foreach (var f in Faces) AddQuad(p[f[0]], p[f[1]], p[f[2]], p[f[3]], col);
        }

        public void AddBox(Vector3 center, Vector3 size, Color col) => AddBox(center, size, Quaternion.identity, col);

        /// <summary>Prisma de n lados (cilindro low poly) entre dos puntos.</summary>
        public void AddCylinder(Vector3 a, Vector3 b, float r0, float r1, int sides, Color col)
        {
            Vector3 axis = b - a;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float ang = i * Mathf.PI * 2 / sides;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                bottom[i] = a + rot * (dir * r0);
                top[i] = b + rot * (dir * r1);
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                AddQuad(bottom[i], top[i], top[j], bottom[j], col);
                AddTriangle(b, top[j], top[i], col);
                AddTriangle(a, bottom[i], bottom[j], col);
            }
        }

        /// <summary>Cono low poly (copa de árbol de reserva).</summary>
        public void AddCone(Vector3 basePos, float radius, float height, int sides, Color col)
        {
            Vector3 tip = basePos + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 p0 = basePos + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                Vector3 p1 = basePos + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                AddTriangle(p0, tip, p1, col);
                AddTriangle(basePos, p0, p1, col);
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Crea un GameObject con la malla acumulada.</summary>
        public GameObject ToObject(string name, Transform parent, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
    }
}
