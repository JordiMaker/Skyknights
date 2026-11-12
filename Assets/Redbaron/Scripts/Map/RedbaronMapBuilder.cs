using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Redbaron
{
    /// <summary>
    /// Construye el mapa completo del prototipo web: terreno low poly, pared de roca, agua,
    /// trincheras excavadas, puentes, aeródromos, bases, casamatas, vegetación y coberturas.
    /// Se ejecuta al darle a Play (buildOnAwake) o desde el menú Redbaron > Construir mapa.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RedbaronMapBuilder : MonoBehaviour
    {
        [Header("Construcción")]
        public bool buildOnAwake = true;
        public bool bakeNavMeshOnAwake = true;
        public int seed = 1916;
        [Tooltip("Capa del suelo. Pon la que use Quick RTS para hacer clic en el terreno (por ejemplo 'Ground').")]
        public int groundLayer = 0;
        [Tooltip("Tag for the ground, trenches and bridges. Quick RTS only gives move orders on objects tagged 'Terrain'. Leave empty to keep Untagged.")]
        public string groundTag = "";

        [Header("Materiales")]
        [Tooltip("Material con el shader Redbaron/LowPolyVertexColor. Si se deja vacío se crea uno.")]
        public Material vertexColorMaterial;
        public Material waterMaterial;

        [Header("Low Poly Environment (Sky Den Games)")]
        [Tooltip("Prefabs de árbol. Se escalan a 9-14 m de alto.")]
        public GameObject[] treePrefabs;
        [Tooltip("Prefabs de roca. Se escalan a 1 m de alto (parapeto).")]
        public GameObject[] rockPrefabs;
        [Tooltip("Tronco muerto tumbado. Sustituye a los árboles derribados.")]
        public GameObject deadLogPrefab;
        public Vector2 treeHeight = new Vector2(9f, 14f);
        public float rockHeight = 1f;

        [Header("Look: soft terrain and set dressing")]
        [Tooltip("Soft colour patches and smooth shading on gentle ground; flat facets only on steep slopes. Off = the web prototype's colour per triangle.")]
        public bool smoothTerrain = true;
        [Tooltip("Slopes steeper than this (degrees) keep flat facets, so mountains and banks read as rock.")]
        public float facetSlope = 38f;
        [Tooltip("Trees for the groves near the front (design Z beyond Dead Trees From Z): shell-shattered trunks.")]
        public GameObject[] deadTreePrefabs;
        public float deadTreesFromZ = 88f;
        [Tooltip("Give every tree a solid crown (a capsule named Copa), so planes and bullets hit tree tops, not just trunks. Not part of the NavMesh.")]
        public bool canopyColliders = true;
        [Tooltip("Grass tufts and small plants on open ground. Decoration only: no colliders, not in the NavMesh.")]
        public GameObject[] groundCoverPrefabs;
        public int groundCoverCount = 2200;
        public Vector2 groundCoverScale = new Vector2(0.9f, 1.7f);
        [Tooltip("Large rocks on the mountain slopes. They block line of sight and planes, but not the NavMesh.")]
        public GameObject[] mountainRockPrefabs;
        public int mountainRockCount = 140;
        public Vector2 mountainRockHeight = new Vector2(2.5f, 6f);
        [Tooltip("Barrels, crates and similar props around command posts and hangars. Decoration only.")]
        public GameObject[] basePropPrefabs;
        public int basePropsPerSite = 6;

        [Header("Boundary wall")]
        [Tooltip("Complete rock models that build the cliff around the map, in two overlapping tiers. Empty = the old generated wall is drawn instead.")]
        public GameObject[] wallRockPrefabs;
        [Tooltip("Boulders along the foot of the cliff. Decoration only: no colliders.")]
        public GameObject[] wallBoulderPrefabs;
        [Tooltip("Height (m) of each lower-tier rock. The upper tier fills the rest, up to the wall's rim.")]
        public Vector2 wallRockHeight = new Vector2(13f, 17f);
        [Tooltip("Distance between neighbouring rocks, as a share of a rock's width. Below 1 they overlap, so no gaps show.")]
        [Range(0.3f, 1f)] public float wallRockOverlap = 0.55f;
        public int wallBoulderCount = 70;

        [Header("Estructuras (opcional: si se dejan vacías se generan low poly)")]
        public GameObject bunkerPrefab;
        public GameObject commandPostPrefab;

        public Transform Root { get; private set; }
        public GameObject Ground { get; private set; }
        public static RedbaronMapBuilder Instance { get; private set; }

        Mulberry32 rng;
        readonly List<Vector2> treesN = new List<Vector2>();
        readonly List<Vector2> groves = new List<Vector2>();
        Transform waterBlockers;

        const int SX = 86, SZ = 258;
        static readonly Color Grass1 = new Color32(0x6b, 0x7a, 0x3e, 255), Grass2 = new Color32(0x76, 0x84, 0x3f, 255),
            Grass3 = new Color32(0x5f, 0x6d, 0x38, 255), Grass4 = new Color32(0x7d, 0x8a, 0x48, 255),
            Mud1 = new Color32(0x7b, 0x64, 0x49, 255), Mud2 = new Color32(0x6a, 0x53, 0x3b, 255), Mud3 = new Color32(0x84, 0x70, 0x4f, 255),
            Dirt = new Color32(0xa0, 0x86, 0x60, 255), TrenchC = new Color32(0x4a, 0x3b, 0x2b, 255), Bed = new Color32(0x5a, 0x4a, 0x38, 255),
            Shore = new Color32(0x9a, 0x8a, 0x66, 255), Rock1 = new Color32(0x8b, 0x86, 0x7c, 255), Rock2 = new Color32(0xa1, 0x9c, 0x90, 255),
            Peak = new Color32(0xbd, 0xb8, 0xaa, 255), Runway1 = new Color32(0x9a, 0xa4, 0x68, 255), Runway2 = new Color32(0x8f, 0x9a, 0x5c, 255),
            Concrete = new Color32(0x8d, 0x8a, 0x80, 255), Concrete2 = new Color32(0x76, 0x73, 0x6a, 255), Sandbag = new Color32(0x9b, 0x8a, 0x62, 255),
            Wood = new Color32(0x6b, 0x4f, 0x33, 255), Earth = new Color32(0x6e, 0x5a, 0x42, 255), Slit = new Color32(0x1f, 0x1c, 0x16, 255),
            Stone = new Color32(0x9a, 0x95, 0x8a, 255), Plank = new Color32(0x7a, 0x56, 0x34, 255), Pole = new Color32(0x3a, 0x33, 0x28, 255);

        void Awake()
        {
            Instance = this;
            if (buildOnAwake && Application.isPlaying) Build();
            else Root = transform.Find("Redbaron_Mapa");
            if (bakeNavMeshOnAwake && Application.isPlaying) BakeNavMesh();
        }

        // ------------------------------------------------------------------
        public void Build()
        {
            Instance = this;
            var old = transform.Find("Redbaron_Mapa");
            if (old != null) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
            Root = new GameObject("Redbaron_Mapa").transform;
            Root.SetParent(transform, false);
            rng = new Mulberry32((uint)seed);
            treesN.Clear(); groves.Clear();
            if (vertexColorMaterial == null)
            {
                var sh = Shader.Find("Redbaron/LowPolyVertexColor");
                vertexColorMaterial = new Material(sh != null ? sh : Shader.Find("Standard")) { name = "Redbaron_VertexColor" };
            }
            if (waterMaterial == null) waterMaterial = MakeWaterMaterial();

            BuildTerrain();
            BuildWater();
            BuildWall();
            BuildTrenches();
            BuildBridges();
            BuildAirfields();
            BuildBases();
            PlaceTrees();
            PlaceRocks();
            BuildWire();
            AddCraterAndTrenchCovers();
            BuildSectionMarkers();
            BuildDecor();
        }

        Transform Group(string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(Root, false);
            return g;
        }

        Color Jitter(Color c, float amt)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            v = Mathf.Clamp01(v + (rng.Next() - 0.5f) * amt);
            return Color.HSVToRGB(h, s, v);
        }

        // ------------------------------------------------------------------ Terreno
        void BuildTerrain()
        {
            float W = RedbaronMap.WorldW, L = RedbaronMap.WorldL;
            var grid = new Vector3[SX + 1, SZ + 1];
            for (int i = 0; i <= SX; i++)
                for (int j = 0; j <= SZ; j++)
                {
                    float x = W * i / SX, z = L * j / SZ;
                    bool border = i == 0 || j == 0 || i == SX || j == SZ;
                    if (!border) { x += (rng.Next() - 0.5f) * 0.7f; z += (rng.Next() - 0.5f) * 0.7f; }
                    Vector2 d = RedbaronMap.ToDesign(new Vector3(x, 0, z));
                    grid[i, j] = new Vector3(x, RedbaronMap.Height(d.x, d.y), z);
                }
            if (smoothTerrain) Ground = BuildSmoothTerrain(grid);
            else
            {
                var acc = new MeshAccumulator();
                for (int i = 0; i < SX; i++)
                    for (int j = 0; j < SZ; j++)
                    {
                        Vector3 v00 = grid[i, j], v10 = grid[i + 1, j], v01 = grid[i, j + 1], v11 = grid[i + 1, j + 1];
                        acc.AddTriangle(v00, v01, v11, TerrainColor(v00, v01, v11));
                        acc.AddTriangle(v00, v11, v10, TerrainColor(v00, v11, v10));
                    }
                Ground = acc.ToObject("Terreno", Root, vertexColorMaterial, true);
            }
            Ground.layer = groundLayer; SetGroundTag(Ground);
            Ground.isStatic = true;
        }

        // ------------------------------------------------------------------ Soft terrain
        // Stone greys for the soft terrain, closer to the set-dressing rocks than the prototype's beige.
        static readonly Color SoftRock1 = new Color32(0x6f, 0x6c, 0x66, 255), SoftRock2 = new Color32(0x84, 0x81, 0x7a, 255), SoftPeak = new Color32(0xa6, 0xa3, 0x9b, 255);

        static float SStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Shared-vertex terrain: colours vary in soft patches and gentle ground is smooth-shaded.
        /// Triangles steeper than facetSlope get their own vertices, so they stay faceted like rock.</summary>
        GameObject BuildSmoothTerrain(Vector3[,] grid)
        {
            int nz = SZ + 1;
            var verts = new List<Vector3>((SX + 1) * nz + 30000);
            var cols = new List<Color>(verts.Capacity);
            var tris = new List<int>(SX * SZ * 6);
            for (int i = 0; i <= SX; i++)
                for (int j = 0; j <= SZ; j++) { verts.Add(grid[i, j]); cols.Add(SoftColor(grid[i, j])); }
            float minUp = Mathf.Cos(facetSlope * Mathf.Deg2Rad);
            void Tri(int ia, int ib, int ic, Color protoColor)
            {
                Vector3 a = verts[ia], b = verts[ib], c = verts[ic];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (n.y >= minUp) { tris.Add(ia); tris.Add(ib); tris.Add(ic); return; }
                Vector3 m = (a + b + c) / 3f;
                Vector2 d = RedbaronMap.ToDesign(m);
                Color avg = (cols[ia] + cols[ib] + cols[ic]) / 3f;
                Color face = RedbaronMap.Sample(d.x, d.y).mountain > 3 && m.y > 2.2f ? Color.Lerp(avg, protoColor, 0.6f) : avg * 0.94f;
                face.a = 1;
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                cols.Add(face); cols.Add(face); cols.Add(face);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            for (int i = 0; i < SX; i++)
                for (int j = 0; j < SZ; j++)
                {
                    int i00 = i * nz + j, i01 = i * nz + j + 1, i10 = (i + 1) * nz + j, i11 = (i + 1) * nz + j + 1;
                    // TerrainColor keeps consuming the random stream exactly as the web prototype, so trees, rocks and craters stay where they were.
                    Color c1 = TerrainColor(grid[i, j], grid[i, j + 1], grid[i + 1, j + 1]); Tri(i00, i01, i11, c1);
                    Color c2 = TerrainColor(grid[i, j], grid[i + 1, j + 1], grid[i + 1, j]); Tri(i00, i11, i10, c2);
                }
            var mesh = new Mesh { name = "Terreno" };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Terreno");
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = vertexColorMaterial; mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>Colour of the ground at a point, from continuous noise and the map zones (no per-triangle randomness).</summary>
        Color SoftColor(Vector3 v, bool withTrench = true)
        {
            Vector2 d = RedbaronMap.ToDesign(v);
            var s = RedbaronMap.Sample(d.x, d.y);
            float n1 = Mathf.PerlinNoise(v.x * 0.035f + 17.3f, v.z * 0.035f + 5.1f);
            float n2 = Mathf.PerlinNoise(v.x * 0.12f + 3.7f, v.z * 0.12f + 41.2f);
            float n = Mathf.Clamp01(n1 * 0.75f + n2 * 0.25f);
            Color grass = Color.Lerp(Color.Lerp(Grass3, Grass1, SStep(0.2f, 0.55f, n)), Grass4, SStep(0.55f, 0.85f, n));
            // No man's land: churned mud with a few surviving grass patches
            float mud = SStep(106f, 120f, s.zm);
            Color mudCol = Color.Lerp(Color.Lerp(Mud2, Mud3, n2), Mud1, SStep(0.4f, 0.7f, n1) * 0.6f);
            mudCol = Color.Lerp(mudCol, Grass3, SStep(0.66f, 0.78f, n1) * 0.55f);
            Color col = Color.Lerp(grass, mudCol, mud);
            // Lanes: worn dirt tracks with soft edges
            col = Color.Lerp(col, Color.Lerp(Dirt, Mud3, n2 * 0.6f), (1f - SStep(1.5f, 3.2f, s.laneDist)) * 0.9f);
            // Runways
            if (RedbaronMap.OnAirfield(d.x, s.zm) && s.zm > RedbaronMap.Runway.yMin && s.zm < RedbaronMap.Runway.yMax)
                col = Color.Lerp(Runway1, Runway2, n2);
            // Mountains: grass gives way to rock and pale peaks with height
            float rock = SStep(2f, 4f, s.mountain) * SStep(2.2f, 5f, v.y);
            Color rockCol = Color.Lerp(Color.Lerp(SoftRock1, SoftRock2, SStep(5f, 10f, v.y)), SoftPeak, SStep(12f, 15f, v.y));
            col = Color.Lerp(col, Color.Lerp(rockCol, SoftRock1, n2 * 0.35f), rock);
            // Trenches, banks and river bed
            if (withTrench) col = Color.Lerp(col, TrenchC, Mathf.Clamp01(s.trench * 1.4f));
            if (s.wet) col = Color.Lerp(col, Shore, SStep(-0.3f, -0.7f, v.y));
            col = Color.Lerp(col, Bed, SStep(-0.8f, -1.2f, v.y));
            col.a = 1;
            return col;
        }

        // ------------------------------------------------------------------ Set dressing
        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
        }

        /// <summary>A plain ground frame around the map, so the horizon is not empty beyond the bases. Leaves the map itself untouched.</summary>
        void BuildOuterGround(Transform parent)
        {
            float W = RedbaronMap.WorldW, L = RedbaronMap.WorldL, M = 600f, y = -0.06f;
            var acc = new MeshAccumulator();
            Color c = Color.Lerp(Grass3, Grass1, 0.5f);
            void Q(float x0, float z0, float x1, float z1) => acc.AddQuad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), c);
            Q(-M, -M, W + M, 0); Q(-M, L, W + M, L + M); Q(-M, 0, 0, L); Q(W, 0, W + M, L);
            var go = acc.ToObject("Suelo_Exterior", parent, vertexColorMaterial, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static GameObject Pick(GameObject[] pool, Mulberry32 r) => pool[Mathf.FloorToInt(r.Next() * pool.Length) % pool.Length];

        /// <summary>Grass, mountain rocks and base props. Uses its own random stream, so the battle map itself does not change.</summary>
        void BuildDecor()
        {
            var decor = Group("Decorado");
            var r = new Mulberry32((uint)seed * 31u + 7u);
            float W = RedbaronMap.WorldW, L = RedbaronMap.WorldL;

            if (groundCoverPrefabs != null && groundCoverPrefabs.Length > 0)
            {
                var g = new GameObject("Hierba").transform; g.SetParent(decor, false);
                int placed = 0;
                for (int tries = 0; placed < groundCoverCount && tries < groundCoverCount * 8; tries++)
                {
                    float x = r.Next() * W, z = r.Next() * L;
                    Vector2 d = RedbaronMap.ToDesign(new Vector3(x, 0, z));
                    var s = RedbaronMap.Sample(d.x, d.y);
                    if (s.wet || s.trench > 0.02f || s.laneDist < 2.8f || s.mountain > 5f || s.h < -0.6f || RedbaronMap.OnAirfield(d.x, s.zm, 1.5f)) continue;
                    if (s.zm > 110 && r.Next() < 0.8f) continue;
                    var go = Instantiate(Pick(groundCoverPrefabs, r), new Vector3(x, s.h - 0.04f, z), Quaternion.Euler(0, r.Next() * 360f, 0), g);
                    go.transform.localScale *= r.Range(groundCoverScale.x, groundCoverScale.y);
                    StripColliders(go);
                    placed++;
                }
            }

            if (mountainRockPrefabs != null && mountainRockPrefabs.Length > 0)
            {
                var g = new GameObject("Rocas_Montana").transform; g.SetParent(decor, false);
                int placed = 0;
                for (int tries = 0; placed < mountainRockCount && tries < mountainRockCount * 40; tries++)
                {
                    float x = r.Next() * W, z = r.Next() * L;
                    Vector2 d = RedbaronMap.ToDesign(new Vector3(x, 0, z));
                    var s = RedbaronMap.Sample(d.x, d.y);
                    if (s.mountain < 4f || s.laneDist < 4.5f) continue;
                    float hgt = r.Range(mountainRockHeight.x, mountainRockHeight.y) * Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(4f, 12f, s.mountain));
                    var go = Instantiate(Pick(mountainRockPrefabs, r), new Vector3(x, s.h, z), Quaternion.Euler(r.Range(-12f, 12f), r.Next() * 360f, r.Range(-12f, 12f)), g);
                    FitHeight(go, hgt);
                    go.transform.position -= Vector3.up * hgt * 0.3f;
                    if (go.GetComponentInChildren<Collider>() == null) AddBoxColliders(go, 0.8f);
                    placed++;
                }
            }

            if (basePropPrefabs != null && basePropPrefabs.Length > 0)
            {
                var g = new GameObject("Atrezo_Bases").transform; g.SetParent(decor, false);
                var sites = new List<Vector2> { RedbaronMap.CommandPost };
                sites.AddRange(RedbaronMap.Hangars);
                foreach (var site in sites)
                    foreach (bool south in new[] { false, true })
                        for (int k = 0; k < basePropsPerSite; k++)
                        {
                            float a = r.Next() * Mathf.PI * 2f, rad = r.Range(4.5f, 8f);
                            float X = site.x + Mathf.Cos(a) * rad, Zn = site.y + Mathf.Sin(a) * rad * 0.6f;
                            if (Zn < 1.5f || Zn > 12.5f) continue;
                            float Z = south ? RedbaronMap.MirrorZ(Zn) : Zn;
                            var go = Instantiate(Pick(basePropPrefabs, r), RedbaronMap.ToWorld(X, Z), Quaternion.Euler(0, r.Next() * 360f, 0), g);
                            go.transform.localScale *= r.Range(1.0f, 1.3f);
                            StripColliders(go);
                            go.AddComponent<Breakable>(); // crates and barrels burst in a blast
                        }
            }

            if (smoothTerrain) BuildOuterGround(decor);
            if (Application.isPlaying) StaticBatchingUtility.Combine(decor.gameObject);
        }

        Color TerrainColor(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 m = (a + b + c) / 3f;
            Vector2 d = RedbaronMap.ToDesign(m);
            var s = RedbaronMap.Sample(d.x, d.y);
            float ay = m.y;
            Color col;
            if (ay < -1.0f) col = Bed;
            else if (s.wet && ay < -0.55f) col = Shore;
            else if (s.trench > 0.3f) col = TrenchC;
            else if (s.mountain > 3 && ay > 2.2f) col = ay > 12 ? Peak : ay > 7.5f ? Rock2 : Rock1;
            else if (RedbaronMap.OnAirfield(d.x, s.zm) && s.zm > RedbaronMap.Runway.yMin && s.zm < RedbaronMap.Runway.yMax) col = rng.Next() < 0.5f ? Runway1 : Runway2;
            else if (s.laneDist < 2.3f) col = Dirt;
            else if (s.zm > 118) col = rng.Next() < 0.14f ? Grass3 : (rng.Next() < 0.5f ? Mud1 : (rng.Next() < 0.5f ? Mud2 : Mud3));
            else if (s.zm > 108 && rng.Next() < 0.4f) col = Mud1;
            else { float r = rng.Next(); col = r < 0.25f ? Grass1 : r < 0.5f ? Grass2 : r < 0.75f ? Grass3 : Grass4; }
            return Jitter(col, 0.045f);
        }

        // ------------------------------------------------------------------ Agua
        Material MakeWaterMaterial()
        {
            var mat = new Material(Shader.Find("Standard")) { name = "Redbaron_Agua" };
            mat.color = new Color32(0x4e, 0x6f, 0x78, 214);
            mat.SetFloat("_Mode", 2);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.SetFloat("_Glossiness", 0.7f);
            return mat;
        }

        void BuildWater()
        {
            float W = RedbaronMap.WorldW, L = RedbaronMap.WorldL;
            var water = new MeshAccumulator();
            var block = new MeshAccumulator();
            float y = RedbaronMap.WaterLevel;
            for (int i = 0; i < SX; i++)
                for (int j = 0; j < SZ; j++)
                {
                    var q = new[]
                    {
                        new Vector3(W * i / SX, y, L * j / SZ), new Vector3(W * (i + 1) / SX, y, L * j / SZ),
                        new Vector3(W * (i + 1) / SX, y, L * (j + 1) / SZ), new Vector3(W * i / SX, y, L * (j + 1) / SZ)
                    };
                    bool wet = false;
                    foreach (var p in q) { var d = RedbaronMap.ToDesign(p); if (RedbaronMap.Sample(d.x, d.y).wet) { wet = true; break; } }
                    if (!wet) continue;
                    water.AddTriangle(q[0], q[3], q[2], Color.white);
                    water.AddTriangle(q[0], q[2], q[1], Color.white);
                    // Bloqueo de NavMesh: todo el agua salvo el vado de la línea oeste y bajo los puentes.
                    Vector2 c = RedbaronMap.ToDesign((q[0] + q[2]) * 0.5f);
                    bool ford = c.x > 9 && c.x < 23;
                    if (!ford)
                    {
                        Vector3 up = Vector3.up * 0.05f;
                        block.AddTriangle(q[0] + up, q[3] + up, q[2] + up, Color.clear);
                        block.AddTriangle(q[0] + up, q[2] + up, q[1] + up, Color.clear);
                    }
                }
            var wgo = water.ToObject("Agua", Root, waterMaterial, false);
            wgo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var bgo = block.ToObject("Agua_BloqueoNavMesh", Root, vertexColorMaterial, true);
            bgo.GetComponent<MeshRenderer>().enabled = false;
            waterBlockers = bgo.transform;
        }

        // ------------------------------------------------------------------ Pared de roca
        void BuildWall()
        {
            var per = new List<Vector4>(); // X, Z, nx, nz en diseño (n en mundo)
            float dx = 100f / SX, dz = 300f / SZ;
            // En mundo: el norte (Z=0) está en +z. Normal hacia fuera.
            for (int i = 0; i < SX; i++) per.Add(new Vector4(i * dx, 0, 0, 1));
            for (int i = 0; i < SZ; i++) per.Add(new Vector4(100, i * dz, 1, 0));
            for (int i = 0; i < SX; i++) per.Add(new Vector4(100 - i * dx, 300, 0, -1));
            for (int i = 0; i < SZ; i++) per.Add(new Vector4(0, 300 - i * dz, -1, 0));
            for (int k = 0; k < per.Count; k++)
            {
                var p = per[k];
                if ((p.x == 0 || p.x == 100) && (p.y == 0 || p.y == 300))
                    per[k] = new Vector4(p.x, p.y, Mathf.Sign(p.x - 50) * 0.7071f, (p.y == 0 ? 1 : -1) * 0.7071f);
            }
            float[] rows = { 0, 0.1f, 0.22f, 0.36f, 0.5f, 0.64f, 0.78f, 0.9f, 1 };
            int R = rows.Length, N = per.Count;
            var cols = new List<Vector3[]>();
            var bases = new float[N]; var rims = new float[N];
            for (int j = 0; j < N; j++)
            {
                var p = per[j];
                float bas = RedbaronMap.Height(p.x, p.y);
                float rim = RedbaronMap.WallHeight + Mathf.Sin(j * 0.07f) * 2.5f + Mathf.Sin(j * 0.23f) * 1.2f;
                bases[j] = bas; rims[j] = rim;
                var col = new Vector3[R + 1];
                for (int r = 0; r < R; r++)
                {
                    float y = bas, off = 0, tj = 0;
                    if (r > 0)
                    {
                        y = bas + (rim - bas) * rows[r] + (r < R - 1 ? (rng.Next() - 0.5f) * 1.3f : 0);
                        off = 0.2f + rng.Next() * 1.6f + r * 0.35f + (r == 3 || r == 6 ? 1.3f : 0);
                        tj = (rng.Next() - 0.5f) * 0.9f;
                    }
                    Vector3 w = RedbaronMap.ToWorld(p.x, p.y, y);
                    w.x += p.z * off - p.w * tj; w.z += p.w * off + p.z * tj;
                    col[r] = w;
                }
                Vector3 far = RedbaronMap.ToWorld(p.x, p.y, rim + 1);
                far.x += p.z * 400; far.z += p.w * 400;
                col[R] = far;
                cols.Add(col);
            }
            cols.Add(cols[0]);
            Color[] rock =
            {
                new Color32(0x6a, 0x62, 0x56, 255), new Color32(0x7d, 0x74, 0x66, 255), new Color32(0x8f, 0x85, 0x76, 255), new Color32(0x76, 0x6d, 0x60, 255),
                new Color32(0x93, 0x88, 0x78, 255), new Color32(0x85, 0x7b, 0x6b, 255), new Color32(0x7a, 0x70, 0x62, 255), new Color32(0x8a, 0x7f, 0x6c, 255)
            };
            Color top = new Color32(0x66, 0x75, 0x40, 255);
            Vector3 center = new Vector3(RedbaronMap.WorldW / 2, 0, RedbaronMap.WorldL / 2);
            bool rockWall = wallRockPrefabs != null && wallRockPrefabs.Length > 0;
            var acc = new MeshAccumulator();
            var plateau = new MeshAccumulator();
            Vector3 down = Vector3.down * 2f; // the plateau sits just under the rocks' tops
            for (int j = 0; j < N; j++)
                for (int r = 0; r < R; r++)
                {
                    Vector3 a = cols[j][r], b = cols[j + 1][r], c = cols[j][r + 1], d = cols[j + 1][r + 1];
                    Color col = Jitter(r == R - 1 ? top : rock[Mathf.Min(r, rock.Length - 1)], 0.06f);
                    Vector3 inward = center - a; inward.y = 0; inward = inward.normalized + Vector3.up * 0.5f;
                    AddFacing(acc, a, b, c, col, inward);
                    AddFacing(acc, b, d, c, col, inward);
                    if (r == R - 1) { AddFacing(plateau, a + down, b + down, c + down, col, Vector3.up); AddFacing(plateau, b + down, d + down, c + down, col, Vector3.up); }
                }
            var go = acc.ToObject("Pared", Root, vertexColorMaterial, true);
            go.isStatic = true;
            if (!rockWall) return;

            // The generated wall stays as an invisible collider: planes, bullets and the NavMesh still stop at it.
            go.GetComponent<MeshRenderer>().enabled = false;
            var pl = plateau.ToObject("Pared_Meseta", Root, vertexColorMaterial, false);
            pl.isStatic = true;
            BuildRockWall(per, cols, bases, rims);
        }

        /// <summary>The visible cliff: complete rock models in two overlapping tiers along the wall, plus boulders at its foot.
        /// Uses its own random stream, so the rest of the map does not change.</summary>
        void BuildRockWall(List<Vector4> per, List<Vector3[]> cols, float[] bases, float[] rims)
        {
            var g = Group("Pared_Rocas");
            var r = new Mulberry32((uint)seed * 53u + 11u);
            int N = per.Count;
            // two walks along the wall; the upper one starts half a rock later so the seams do not line up
            for (int tier = 0; tier < 2; tier++)
            {
                float gap = tier == 0 ? 0f : 4f, run = 0f;
                for (int j = 0; j < N; j++)
                {
                    Vector3 p0 = cols[j][0], p1 = cols[(j + 1) % N][0];
                    run += Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(p1.x, 0, p1.z));
                    if (run < gap) continue;
                    run = 0f;
                    var n = new Vector2(per[j].z, per[j].w);
                    float bottom, height, outward;
                    if (tier == 0) { bottom = bases[j] - 1.5f; height = r.Range(wallRockHeight.x, wallRockHeight.y); outward = r.Range(0f, 1.2f); }
                    else
                    {
                        bottom = bases[j] + wallRockHeight.x * 0.62f;
                        height = Mathf.Max(6f, rims[j] + r.Range(0.6f, 2.4f) - bottom);
                        outward = r.Range(2f, 3.5f);
                    }
                    var rock = PlaceWallRock(Pick(wallRockPrefabs, r), g, p0, n, bottom, height, outward, r, out float width);
                    foreach (var rd in rock.GetComponentsInChildren<Renderer>())
                        rd.shadowCastingMode = tier == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    AddBoxColliders(rock, 0.85f);
                    gap = width * wallRockOverlap;
                }
            }
            // Corners: each side pushes its rocks out along its own normal, which leaves a notch behind the corner.
            // Fill it with rocks pushed out along the diagonal.
            for (int j = 0; j < N; j++)
            {
                if (Mathf.Abs(per[j].z) < 0.5f || Mathf.Abs(per[j].w) < 0.5f) continue;
                var n = new Vector2(per[j].z, per[j].w);
                for (int k = 0; k < 4; k++)
                {
                    bool upper = k >= 2;
                    float bottom = upper ? bases[j] + wallRockHeight.x * 0.62f : bases[j] - 1.5f;
                    float height = upper ? Mathf.Max(6f, rims[j] + r.Range(0.6f, 2.4f) - bottom) : r.Range(wallRockHeight.x, wallRockHeight.y);
                    var rock = PlaceWallRock(Pick(wallRockPrefabs, r), g, cols[j][0], n, bottom, height, (k % 2) * 4f + r.Range(0f, 1f), r, out _);
                    foreach (var rd in rock.GetComponentsInChildren<Renderer>()) rd.shadowCastingMode = upper ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    AddBoxColliders(rock, 0.85f);
                }
            }
            if (wallBoulderPrefabs != null && wallBoulderPrefabs.Length > 0)
                for (int k = 0; k < wallBoulderCount; k++)
                {
                    int j = Mathf.FloorToInt(r.Next() * N) % N;
                    var n = new Vector2(per[j].z, per[j].w);
                    float h = r.Range(2.2f, 5f);
                    var b = PlaceWallRock(Pick(wallBoulderPrefabs, r), g, cols[j][0], n, bases[j] - h * 0.3f, h, -r.Range(1.5f, 4f), r, out _);
                    foreach (var rd in b.GetComponentsInChildren<Renderer>()) rd.shadowCastingMode = ShadowCastingMode.Off;
                }
            if (Application.isPlaying) StaticBatchingUtility.Combine(g.gameObject);
        }

        /// <summary>Puts one rock against the wall: scaled to the height, its bottom at bottomY, its inner face near the
        /// wall line at basePoint (n = outward normal in world xz), pushed outward by the given metres. Returns its width along the wall.</summary>
        GameObject PlaceWallRock(GameObject prefab, Transform parent, Vector3 basePoint, Vector2 n, float bottomY, float height, float outward, Mulberry32 r, out float width)
        {
            var go = Instantiate(prefab, parent);
            go.transform.rotation = Quaternion.Euler(r.Range(-4f, 4f), r.Next() * 360f, r.Range(-4f, 4f));
            StripColliders(go);
            FitHeight(go, height);
            Bounds b = RenderBounds(go);
            float depth = Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.y) * b.extents.z;
            width = 2f * (Mathf.Abs(n.y) * b.extents.x + Mathf.Abs(n.x) * b.extents.z);
            Vector3 target = basePoint + new Vector3(n.x, 0, n.y) * (depth * 0.8f + outward);
            go.transform.position += new Vector3(target.x - b.center.x, bottomY - b.min.y, target.z - b.center.z);
            return go;
        }

        static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var rd in rs) b.Encapsulate(rd.bounds);
            return b;
        }

        /// <summary>A box collider fitted to each mesh of a model, narrowed on x and z. Mesh colliders cannot be built
        /// at runtime in a phone build from models that are not Read/Write enabled, but box colliders always can.</summary>
        static void AddBoxColliders(GameObject go, float narrow)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                Bounds mb = mf.sharedMesh.bounds;
                bc.center = mb.center;
                bc.size = new Vector3(mb.size.x * narrow, mb.size.y, mb.size.z * narrow);
            }
        }

        static void AddFacing(MeshAccumulator acc, Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 desired)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, desired) >= 0) acc.AddTriangle(a, b, c, col);
            else acc.AddTriangle(a, c, b, col);
        }

        // ------------------------------------------------------------------ Trincheras
        void BuildTrenches()
        {
            var floor = new MeshAccumulator();
            var walls = new MeshAccumulator();
            var covers = Group("Coberturas_Trinchera");
            float TW = RedbaronMap.TrenchWidth, TH = RedbaronMap.TrenchDepth, K = RedbaronMap.K;
            foreach (float bz in RedbaronMap.TrenchLines)
                for (float X = 0; X < 99.99f; X += 2.5f)
                {
                    float X1 = X + 2.5f;
                    float z0 = RedbaronMap.TrenchZ(bz, X), z1 = RedbaronMap.TrenchZ(bz, X1);
                    if (RedbaronMap.LaneDist((X + X1) / 2, (z0 + z1) / 2) * K < 4.6f) continue;
                    foreach (bool south in new[] { false, true })
                    {
                        float za = south ? RedbaronMap.MirrorZ(z0) : z0, zb = south ? RedbaronMap.MirrorZ(z1) : z1;
                        Vector3 A = RedbaronMap.ToWorld(X, za, 0), B = RedbaronMap.ToWorld(X1, zb, 0);
                        Vector3 dir = B - A; float Lg = dir.magnitude; dir /= Lg;
                        float len = Lg + 0.4f;
                        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up); // eje z = a lo largo
                        Vector3 n = new Vector3(dir.z, 0, -dir.x);
                        Vector3 c = (A + B) / 2f;
                        float g0 = RedbaronMap.Sample((X + X1) / 2, (za + zb) / 2).h0, fl = g0 - TH;
                        floor.AddBox(new Vector3(c.x, fl - 0.1f, c.z), new Vector3(TW + 0.2f, 0.2f, len), rot, Jitter(TrenchC, 0.04f));
                        floor.AddBox(new Vector3(c.x, fl + 0.03f, c.z), new Vector3(0.7f, 0.06f, len - 0.3f), rot, Wood);
                        foreach (int s in new[] { -1, 1 })
                        {
                            Vector3 o(float d) => new Vector3(c.x + n.x * s * d, 0, c.z + n.z * s * d);
                            Vector3 v = o(TW / 2 + 1.5f); v.y = fl + (TH + 0.04f) / 2 - 0.05f;
                            Color berm = Jitter(Earth, 0.05f);
                            // Soft look: the berm takes the ground's colour so it blends in, and only the cut and the sandbags read.
                            if (smoothTerrain) { berm = SoftColor(new Vector3(v.x, g0, v.z), false) * 0.93f; berm.a = 1; }
                            walls.AddBox(v, new Vector3(3.0f, TH + 0.14f, len), rot, berm);
                            v = o(TW / 2 - 0.03f); v.y = fl + TH / 2;
                            walls.AddBox(v, new Vector3(0.08f, TH - 0.05f, len), rot, Wood);
                            v = o(TW / 2 + 0.55f); v.y = g0 + 0.17f;
                            Color bags = Jitter(Sandbag, 0.05f);
                            if (smoothTerrain) bags = Color.Lerp(bags, Earth, 0.35f);
                            walls.AddBox(v, new Vector3(0.8f, 0.34f, smoothTerrain ? Lg + 0.05f : len), rot, bags);
                        }
                        var cp = new GameObject("Trinchera").AddComponent<CoverPoint>();
                        cp.transform.SetParent(covers, false);
                        cp.transform.position = new Vector3(c.x, fl, c.z);
                        cp.type = CoverType.Trinchera; cp.back = 0; cp.capacity = 2;
                    }
                }
            var f = floor.ToObject("Trincheras_Suelo", Root, vertexColorMaterial, true);
            var w = walls.ToObject("Trincheras_Paredes", Root, vertexColorMaterial, true);
            f.layer = groundLayer; w.layer = groundLayer; SetGroundTag(f); SetGroundTag(w);
        }

        // ------------------------------------------------------------------ Puentes y vado
        void BuildBridges()
        {
            var acc = new MeshAccumulator();
            float K = RedbaronMap.K;
            foreach (bool south in new[] { false, true })
            {
                // Puente de piedra (línea centro) y de madera (línea este)
                AddBridge(acc, 50, south ? RedbaronMap.MirrorZ(104.6f) : 104.6f, 5.5f, 16 * K, Stone, true);
                AddBridge(acc, 85, south ? RedbaronMap.MirrorZ(104f) : 104f, 3.6f, 15 * K, Plank, false);
                // Piedras del vado (línea oeste)
                for (int i = 0; i < 7; i++)
                {
                    float Zs = 99 + i * 1.8f, X = 15 + (i % 2 == 1 ? 1.2f : -1.2f);
                    Vector3 p = RedbaronMap.ToWorld(X, south ? RedbaronMap.MirrorZ(Zs) : Zs, -0.85f);
                    acc.AddCylinder(p - Vector3.up * 0.25f, p + Vector3.up * 0.25f, 0.85f, 0.7f, 6, Jitter(Stone, 0.06f));
                }
            }
            var go = acc.ToObject("Puentes", Root, vertexColorMaterial, true);
            go.layer = groundLayer; SetGroundTag(go);
        }

        void SetGroundTag(GameObject go)
        {
            if (!string.IsNullOrEmpty(groundTag)) go.tag = groundTag;
        }

        void AddBridge(MeshAccumulator acc, float X, float Zc, float width, float len, Color col, bool stone)
        {
            Vector3 c = RedbaronMap.ToWorld(X, Zc, 0.15f);
            acc.AddBox(c, new Vector3(width, 0.6f, len), col);
            foreach (int s in new[] { -1, 1 })
            {
                if (stone) acc.AddBox(c + new Vector3(s * (width / 2 - 0.25f), 0.55f, 0), new Vector3(0.5f, 0.8f, len), Concrete2);
                else for (int i = 0; i <= 5; i++) acc.AddBox(c + new Vector3(s * (width / 2 - 0.2f), 0.6f, -len / 2 + i * len / 5), new Vector3(0.25f, 1, 0.25f), Wood);
            }
            if (stone) foreach (int s in new[] { -1, 1 }) acc.AddBox(c + new Vector3(0, -1.45f, s * 2.6f), new Vector3(width + 0.8f, 2.6f, 1.4f), Concrete2);
        }

        // ------------------------------------------------------------------ Aeródromos
        void BuildAirfields()
        {
            var g = Group("Aerodromos");
            foreach (Team t in new[] { Team.Centrales, Team.Entente })
            {
                var acc = new MeshAccumulator();
                var rw = RedbaronMap.Runway;
                for (float X = rw.xMin + 2; X <= rw.xMax - 2; X += 7)
                    foreach (float Zb in new[] { rw.yMin + 0.4f, rw.yMax - 0.4f })
                    {
                        float Z = RedbaronMap.ForTeam(t, Zb);
                        acc.AddBox(RedbaronMap.ToWorld(X, Z, RedbaronMap.Height(X, Z) + 0.1f), new Vector3(2.4f, 0.2f, 0.5f), new Color32(0xd8, 0xd2, 0xbf, 255));
                    }
                int n = 0;
                foreach (var hg in RedbaronMap.Hangars)
                {
                    // Each hangar is its own object so it can be destroyed (Hangar).
                    float Z = RedbaronMap.ForTeam(t, hg.y);
                    var ha = new MeshAccumulator();
                    BuildingModels.AddHangar(ha, Vector3.zero, t);
                    var hgo = ha.ToObject("Hangar_" + t + "_" + n++, g, vertexColorMaterial, true);
                    hgo.transform.position = RedbaronMap.ToWorld(hg.x, Z);
                    var hh = GetOrAdd<Health>(hgo);
                    hh.team = t; hh.kind = TargetKind.Hangar; hh.maxHp = hh.hp = 40; hh.aimHeight = 2f;
                    GetOrAdd<Hangar>(hgo);
                }
                float Zs = RedbaronMap.ForTeam(t, rw.yMin - 1.5f);
                Vector3 ws = RedbaronMap.ToWorld(rw.xMin - 2, Zs);
                acc.AddBox(ws + Vector3.up * 2.5f, new Vector3(0.15f, 5, 0.15f), Pole);
                acc.AddBox(ws + new Vector3(1.1f, 4.8f, 0), new Vector3(2.2f, 0.7f, 0.7f), new Color32(0xd2, 0x78, 0x3c, 255));
                var go = acc.ToObject("Aerodromo_" + t, g, vertexColorMaterial, true);
                var af = go.AddComponent<Airfield>();
                af.team = t;
            }
        }

        void AddFlag(MeshAccumulator acc, Vector3 basePos, Team t, float h, float size) => BuildingModels.AddFlag(acc, basePos, t, h, size);

        // ------------------------------------------------------------------ Bases y casamatas
        void BuildBases()
        {
            var g = Group("Bases");
            foreach (Team t in new[] { Team.Centrales, Team.Entente })
            {
                // Puesto de mando
                Vector3 p = RedbaronMap.ToWorld(RedbaronMap.CommandPost.x, RedbaronMap.ForTeam(t, RedbaronMap.CommandPost.y));
                GameObject post;
                if (commandPostPrefab != null) post = Instantiate(commandPostPrefab, p, Quaternion.identity, g);
                else
                {
                    var acc = new MeshAccumulator();
                    BuildingModels.AddCommandPost(acc, t);
                    post = acc.ToObject("PuestoDeMando_" + t, g, vertexColorMaterial, false);
                    post.transform.position = p;
                    var bc = post.AddComponent<BoxCollider>();
                    bc.center = BuildingModels.CommandPostCenter; bc.size = BuildingModels.CommandPostSize;
                }
                var hp = GetOrAdd<Health>(post);
                hp.team = t; hp.kind = TargetKind.CommandPost; hp.maxHp = hp.hp = 150; hp.aimHeight = 1.5f;
                if (post.GetComponent<CommandPost>() == null) post.AddComponent<CommandPost>();
                AddObstacle(post, BuildingModels.CommandPostSize, BuildingModels.CommandPostCenter);

                // Casamatas
                foreach (var b in RedbaronMap.Bunkers)
                {
                    Vector3 bp = RedbaronMap.ToWorld(b.x, RedbaronMap.ForTeam(t, b.y));
                    GameObject bunker;
                    if (bunkerPrefab != null) bunker = Instantiate(bunkerPrefab, bp, Quaternion.identity, g);
                    else
                    {
                        var acc = new MeshAccumulator();
                        BuildingModels.AddBunker(acc, t);
                        bunker = acc.ToObject("Casamata_" + t, g, vertexColorMaterial, false);
                        bunker.transform.position = bp;
                        var bc = bunker.AddComponent<BoxCollider>();
                        bc.center = BuildingModels.BunkerCenter; bc.size = BuildingModels.BunkerSize;
                    }
                    var bh = GetOrAdd<Health>(bunker);
                    bh.team = t; bh.kind = TargetKind.Bunker; bh.maxHp = bh.hp = 20; bh.aimHeight = 1.1f;
                    if (bunker.GetComponent<Bunker>() == null) bunker.AddComponent<Bunker>();
                    AddObstacle(bunker, BuildingModels.BunkerSize, BuildingModels.BunkerCenter);
                }
            }
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        static void AddObstacle(GameObject go, Vector3 size, Vector3 center)
        {
            var ob = GetOrAdd<NavMeshObstacle>(go);
            ob.shape = NavMeshObstacleShape.Box;
            ob.size = size; ob.center = center;
            ob.carving = true;
        }

        // ------------------------------------------------------------------ Árboles
        bool TryTree(float X, float Z, float minD)
        {
            float Zm = Z <= 150 ? Z : 300 - Z;
            if (RedbaronMap.OnAirfield(X, Zm, 3)) return false;
            var s = RedbaronMap.Sample(X, Z);
            if (s.laneDist < 4.5f || s.wet || s.h < -0.4f || s.mountain > 2.5f) return false;
            foreach (var t in treesN) if ((t - new Vector2(X, Z)).sqrMagnitude < minD * minD) return false;
            treesN.Add(new Vector2(X, Z));
            return true;
        }

        void TryGrove(float X, float Z, float minD)
        {
            var s = RedbaronMap.Sample(X, Z);
            if (s.laneDist < 5 || s.wet || s.h < -0.4f || s.mountain > 2.5f) return;
            foreach (var c in groves) if ((c - new Vector2(X, Z)).sqrMagnitude < minD * minD) return;
            groves.Add(new Vector2(X, Z));
            int n = 1 + Mathf.FloorToInt(rng.Next() * 3);
            float a0 = rng.Next() * 6.28f;
            TryTree(X, Z, 2.6f);
            for (int k = 1; k < n; k++)
            {
                float a = a0 + k * 2.1f + (rng.Next() - 0.5f) * 0.6f, r = 2.8f + rng.Next() * 0.8f;
                TryTree(X + Mathf.Cos(a) * r, Z + Mathf.Sin(a) * r, 2.6f);
            }
        }

        void PlaceTrees()
        {
            for (int i = 0; i < 600; i++) TryGrove(3 + rng.Next() * 34, 32 + rng.Next() * 26, 10);
            for (int i = 0; i < 200; i++) TryGrove(3 + rng.Next() * 94, 4 + rng.Next() * 52, 17);
            for (int i = 0; i < 80; i++) TryGrove(3 + rng.Next() * 94, 92 + rng.Next() * 8, 14);
            var g = Group("Arboles");
            var fallback = new MeshAccumulator();
            foreach (var t in treesN)
                foreach (float Z in new[] { t.y, RedbaronMap.MirrorZ(t.y) })
                {
                    Vector3 p = RedbaronMap.ToWorld(t.x, Z, RedbaronMap.Height(t.x, Z) - 0.1f);
                    float hgt = rng.Range(treeHeight.x, treeHeight.y);
                    float yaw = rng.Next() * 360f;
                    GameObject tree;
                    if (treePrefabs != null && treePrefabs.Length > 0)
                    {
                        bool dead = deadTreePrefabs != null && deadTreePrefabs.Length > 0 && t.y > deadTreesFromZ;
                        var pool = dead ? deadTreePrefabs : treePrefabs;
                        var pf = pool[Mathf.FloorToInt(rng.Next() * pool.Length) % pool.Length];
                        tree = Instantiate(pf, p, Quaternion.Euler(0, yaw, 0), g);
                        FitHeight(tree, hgt);
                        if (canopyColliders) AddCanopy(tree, p, hgt, dead);
                    }
                    else
                    {
                        tree = new GameObject("Arbol");
                        tree.transform.SetParent(g, false);
                        tree.transform.position = p;
                        float s = hgt / 13f;
                        fallback.AddCylinder(p, p + Vector3.up * 2.6f, 0.42f, 0.3f, 6, new Color32(0x4f, 0x3d, 0x2a, 255));
                        Color leaf = Jitter(new Color32(0x56, 0x74, 0x37, 255), 0.12f);
                        fallback.AddCone(p + Vector3.up * 2f, 2.3f * s, 6.2f * s, 6, leaf);
                        fallback.AddCone(p + Vector3.up * (2f + 4.2f * s), 1.6f * s, 4.6f * s, 6, leaf);
                    }
                    if (!System.Array.Exists(tree.GetComponentsInChildren<Collider>(), c => c.name != "Copa")) // the crown does not count as a trunk
                    {
                        var cc = tree.AddComponent<CapsuleCollider>();
                        float sc = tree.transform.lossyScale.y;
                        cc.radius = 0.45f / Mathf.Max(0.01f, tree.transform.lossyScale.x);
                        cc.height = hgt / Mathf.Max(0.01f, sc);
                        cc.center = new Vector3(0, cc.height / 2, 0);
                    }
                    var cp = tree.AddComponent<CoverPoint>();
                    cp.type = CoverType.Arbol; cp.back = 0.8f;
                    var td = tree.AddComponent<TreeDestructible>();
                    td.deadLogPrefab = deadLogPrefab;
                    td.material = vertexColorMaterial;
                    td.height = hgt;
                }
            if (fallback.VertexCount > 0) fallback.ToObject("Arboles_Reserva", g, vertexColorMaterial, false);
        }

        /// <summary>Solid crown for a tree: a vertical capsule over the upper part, as wide as the foliage (narrower for bare dead trees).</summary>
        static void AddCanopy(GameObject tree, Vector3 basePos, float height, bool dead)
        {
            var rs = tree.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float radius = Mathf.Max(0.8f, Mathf.Min(b.size.x, b.size.z) * 0.5f * (dead ? 0.45f : 0.8f));
            var crown = new GameObject("Copa");
            crown.transform.SetParent(tree.transform, false);
            crown.transform.position = new Vector3(b.center.x, basePos.y + height * 0.62f, b.center.z);
            crown.transform.rotation = Quaternion.identity;
            Vector3 ls = tree.transform.lossyScale;
            crown.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
            var cc = crown.AddComponent<CapsuleCollider>();
            cc.direction = 1; cc.radius = radius; cc.height = Mathf.Max(height * 0.72f, 2f * radius);
        }

        /// <summary>Escala un prefab para que mida <paramref name="height"/> metros de alto.</summary>
        public static void FitHeight(GameObject go, float height)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.size.y < 0.001f) return;
            go.transform.localScale *= height / b.size.y;
        }

        // ------------------------------------------------------------------ Rocas
        void PlaceRocks()
        {
            var g = Group("Rocas");
            var fallback = new MeshAccumulator();
            foreach (var cl in RedbaronMap.RockClusters)
                for (int i = 0; i < cl.z * 2; i++)
                {
                    float rx = cl.x + (rng.Next() - 0.5f) * 8, rz = cl.y + (rng.Next() - 0.5f) * 8;
                    float w = 0.85f + rng.Next() * 0.7f;
                    if (RedbaronMap.Sample(rx, rz).trench > 0 || RedbaronMap.OnAirfield(rx, rz, 2)) continue;
                    foreach (float Z in new[] { rz, RedbaronMap.MirrorZ(rz) })
                    {
                        Vector3 p = RedbaronMap.ToWorld(rx, Z);
                        GameObject rock;
                        if (rockPrefabs != null && rockPrefabs.Length > 0)
                        {
                            var pf = rockPrefabs[Mathf.FloorToInt(rng.Next() * rockPrefabs.Length) % rockPrefabs.Length];
                            rock = Instantiate(pf, p, Quaternion.Euler(0, rng.Next() * 360, 0), g);
                            FitHeight(rock, rockHeight);
                            rock.transform.localScale = Vector3.Scale(rock.transform.localScale, new Vector3(w * 1.6f, 1, w * 1.3f));
                        }
                        else
                        {
                            rock = new GameObject("Roca");
                            rock.transform.SetParent(g, false);
                            rock.transform.position = p;
                            fallback.AddBox(p + Vector3.up * 0.45f, new Vector3(2 * w * (1 + rng.Next() * 0.5f), rockHeight, 1.8f * w), Quaternion.Euler(rng.Next() * 8, rng.Next() * 360, rng.Next() * 8), Jitter(Rock1, 0.1f));
                            var bc = rock.AddComponent<BoxCollider>();
                            bc.center = new Vector3(0, 0.5f, 0); bc.size = new Vector3(2 * w, 1, 1.8f * w);
                        }
                        if (rock.GetComponentInChildren<Collider>() == null) rock.AddComponent<BoxCollider>();
                        var cp = rock.AddComponent<CoverPoint>();
                        cp.type = CoverType.Roca; cp.back = 1.2f + w * 0.6f;
                    }
                }
            if (fallback.VertexCount > 0) fallback.ToObject("Rocas_Reserva", g, vertexColorMaterial, true);
        }

        // ------------------------------------------------------------------ Alambrada
        void BuildWire()
        {
            // Stretches of up to four spans, each its own object, so a blast cuts the wire where it lands (Breakable).
            var g = Group("Alambrada");
            var acc = new MeshAccumulator();
            int spans = 0, n = 0;
            void Close()
            {
                if (acc.VertexCount > 0) acc.ToObject("Alambrada_" + n++, g, vertexColorMaterial, false).AddComponent<Breakable>();
                acc = new MeshAccumulator(); spans = 0;
            }
            foreach (float Zw in new[] { 145f, RedbaronMap.MirrorZ(145f) })
            {
                Vector3? prev = null;
                for (float X = 2.5f; X <= 97.5f; X += 2.5f)
                {
                    float Zm = Zw <= 150 ? Zw : 300 - Zw;
                    if (RedbaronMap.LaneDist(X, Zm) < 3.5f) { prev = null; Close(); continue; }
                    Vector3 pt = RedbaronMap.ToWorld(X, Zw + (rng.Next() - 0.5f) * 0.8f);
                    if (spans >= 4) { Close(); if (prev.HasValue) acc.AddBox(prev.Value + Vector3.up * 0.6f, new Vector3(0.15f, 1.2f, 0.15f), new Color32(0x4b, 0x3f, 0x30, 255)); }
                    acc.AddBox(pt + Vector3.up * 0.6f, new Vector3(0.15f, 1.2f, 0.15f), new Color32(0x4b, 0x3f, 0x30, 255));
                    if (prev.HasValue)
                    {
                        foreach (float hh in new[] { 0.5f, 0.95f })
                            acc.AddCylinder(prev.Value + Vector3.up * hh, pt + Vector3.up * hh, 0.02f, 0.02f, 3, new Color32(0x3a, 0x36, 0x2e, 255));
                        spans++;
                    }
                    prev = pt;
                }
                Close();
            }
            if (Application.isPlaying) StaticBatchingUtility.Combine(g.gameObject); // one batch, but each stretch can still be hidden
        }

        // ------------------------------------------------------------------ Cráteres y hitos
        void AddCraterAndTrenchCovers()
        {
            var g = Group("Coberturas_Crater");
            foreach (var c in RedbaronMap.Craters)
                foreach (float Z in new[] { c.z, RedbaronMap.MirrorZ(c.z) })
                {
                    if (RedbaronMap.Sample(c.x, Z).wet) continue;
                    var cp = new GameObject("Crater").AddComponent<CoverPoint>();
                    cp.transform.SetParent(g, false);
                    cp.transform.position = RedbaronMap.ToWorld(c.x, Z);
                    cp.type = CoverType.Crater; cp.back = 0;
                }
        }

        void BuildSectionMarkers()
        {
            var acc = new MeshAccumulator();
            for (int k = 1; k < 10; k++)
                foreach (float X in new[] { 0.5f, 99.5f })
                {
                    Vector3 p = RedbaronMap.ToWorld(X, k * 30);
                    float h = k == 5 ? 3 : 1.6f;
                    acc.AddBox(p + Vector3.up * (h / 2 - 0.4f), new Vector3(0.5f, h, 0.5f), k == 5 ? (Color)new Color32(0xc9, 0xa2, 0x4a, 255) : (Color)new Color32(0xd8, 0xd2, 0xbf, 255));
                }
            acc.ToObject("Hitos_Franjas", Root, vertexColorMaterial, false);
        }

        // ------------------------------------------------------------------ NavMesh
        NavMeshDataInstance navInstance;

        /// <summary>Hornea la NavMesh en tiempo de ejecución (no hace falta ningún paquete).</summary>
        public void BakeNavMesh()
        {
            if (Root == null) return;
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            if (waterBlockers == null) waterBlockers = Root.Find("Agua_BloqueoNavMesh");
            if (waterBlockers != null) markups.Add(new NavMeshBuildMarkup { root = waterBlockers, overrideArea = true, area = 1 });
            foreach (var scenery in new[] { "Decorado", "Pared_Rocas" })
            {
                var sr = Root.Find(scenery);
                if (sr != null) markups.Add(new NavMeshBuildMarkup { root = sr, ignoreFromBuild = true });
            }
#if UNITY_6000_6_OR_NEWER
            var units = FindObjectsByType<UnityEngine.AI.NavMeshAgent>();
#else
            var units = FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
#endif
            foreach (var u in units) markups.Add(new NavMeshBuildMarkup { root = u.transform, ignoreFromBuild = true });
            NavMeshBuilder.CollectSources(Root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            sources.RemoveAll(src => src.component != null && src.component.name == "Copa"); // tree crowns are for planes, not for walking
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 1.8f;
            settings.agentSlope = 40;
            settings.agentClimb = 0.45f;
            var bounds = new Bounds(new Vector3(RedbaronMap.WorldW / 2, 5, RedbaronMap.WorldL / 2), new Vector3(RedbaronMap.WorldW + 4, 60, RedbaronMap.WorldL + 4));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (navInstance.valid) navInstance.Remove();
            navInstance = NavMesh.AddNavMeshData(data);
        }

        void OnDestroy()
        {
            if (navInstance.valid) navInstance.Remove();
        }
    }
}
