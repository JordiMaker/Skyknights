using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Dresses a unit in a Redbaron colour kit model (soldier, tank or mech) instead of the Toon Soldiers model the prefabs carry.
    /// Each kit model is baked once per side into vertex-coloured meshes, one per moving part, painted with the side's theme
    /// (TeamLivery), and every unit of that model and side shares them. A soldier costs three draw calls and a vehicle three to nine,
    /// which matters with over a hundred units on a phone.
    ///
    /// The kit's OBJ files put every part at the model origin, so the joints come from the kit's GLB files (Joints below):
    /// hips, knees, arms, torso, turret and gun. Soldiers get their hips from the leg meshes. Legs swing with the kit's AndarMech,
    /// and turrets and torsos turn with UnitAim. Wheels and insignias are merged into the part they sit on.
    /// </summary>
    public static class UnitModels
    {
        public class Look
        {
            /// <summary>Size of the model as placed (m): width, height, length, and its centre from the unit's feet.</summary>
            public Vector3 size, center;
            public UnitAim aim;
        }

        class Part { public string name, parent; public Vector3 pivot; public Mesh mesh; }
        class Baked { public List<Part> parts = new List<Part>(); public Bounds bounds; public bool legs; }

        static readonly Dictionary<(GameObject, Team), Baked> Cache = new Dictionary<(GameObject, Team), Baked>();
        static readonly FieldInfo OutlineRenderers = typeof(Outline).GetField("renderers", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo OutlineNormals = typeof(Outline).GetMethod("LoadSmoothNormals", BindingFlags.NonPublic | BindingFlags.Instance);

        // ------------------------------------------------------------ Joints from the kit's GLB files
        struct Joint { public string name, parent; public Vector3 pivot; public Joint(string n, string p, float x, float y, float z) { name = n; parent = p; pivot = new Vector3(x, y, z); } }

        /// <summary>Joint pivots in model space (m), copied from the kit's .glb node matrices (parents first).
        /// The x sign is the GLB's; Bake flips it if the OBJ import mirrored the model.</summary>
        static readonly Dictionary<string, Joint[]> Joints = new Dictionary<string, Joint[]>
        {
            { "Lince", new[] { new Joint("Torreta", "", 0, 1.5f, 0.4f), new Joint("Elevacion", "Torreta", 0, 1.82f, 0.8f) } },
            { "Escarabajo", new[] { new Joint("Torreta", "", 0, 1.45f, -0.05f), new Joint("Elevacion", "Torreta", 0, 1.65f, 0.27f) } },
            { "Mastodonte", new Joint[0] },
            { "Tetera", new[] {
                new Joint("Pierna_I", "", -0.32f, 1.3f, 0), new Joint("Rodilla_I", "Pierna_I", -0.32f, 0.7f, 0.192f),
                new Joint("Pierna_D", "", 0.32f, 1.3f, 0), new Joint("Rodilla_D", "Pierna_D", 0.32f, 0.7f, 0.192f),
                new Joint("Torso", "", 0, 1.5f, 0), new Joint("Brazo_D", "Torso", 0.5f, 2.32f, 0),
                new Joint("Canones", "Brazo_D", 0.5f, 1.816f, 0.512f), new Joint("Brazo_I", "Torso", -0.5f, 2.32f, 0) } },
            { "Zancudo", new[] {
                new Joint("Pierna_I", "", -0.75f, 3.6f, 0), new Joint("Rodilla_I", "Pierna_I", -0.75f, 1.7f, -0.722f),
                new Joint("Pierna_D", "", 0.75f, 3.6f, 0), new Joint("Rodilla_D", "Pierna_D", 0.75f, 1.7f, -0.722f),
                new Joint("Torso", "", 0, 3.9f, 0) } },
            { "Caldera", new[] {
                new Joint("Pierna_I", "", -1.3f, 3.2f, 0), new Joint("Rodilla_I", "Pierna_I", -1.3f, 1.6f, 0.512f),
                new Joint("Pierna_D", "", 1.3f, 3.2f, 0), new Joint("Rodilla_D", "Pierna_D", 1.3f, 1.6f, 0.512f),
                new Joint("Torso", "", 0, 3.7f, 0), new Joint("Brazo_D", "Torso", 2.05f, 6.05f, 0), new Joint("Brazo_I", "Torso", -2.05f, 6.05f, 0) } },
        };

        // ------------------------------------------------------------ Apply
        /// <summary>Replaces the unit's look with model. Vehicles taller than maxHeight are scaled down (0 = keep the size).
        /// Returns null, leaving the unit untouched, if the model cannot be used.</summary>
        public static Look Apply(GameObject unit, GameObject model, Team team, Material material, float maxHeight, bool vehicle)
        {
            if (model == null || material == null) return null;
            var baked = Bake(model, team);
            if (baked == null) return null;
            var root = unit.transform;

            // Quick Outline keeps a list of the renderers it outlines: take its materials off the old ones first.
            var outline = unit.GetComponent<Outline>();
            bool outlined = outline != null && outline.enabled;
            if (outlined) outline.enabled = false;

            // The Toon Soldiers look goes: animator, skinned meshes and their bones.
            var ai = unit.GetComponent<SoldierAI>();
            if (ai != null) ai.DropAnimator();
            foreach (var a in unit.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            var bones = new HashSet<Transform>();
            foreach (var smr in unit.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.rootBone != null) bones.Add(TopChild(root, smr.rootBone));
                if (smr.gameObject == unit) Object.DestroyImmediate(smr);
                else bones.Add(TopChild(root, smr.transform));
            }
            foreach (var b in bones) if (b != null && b != root) Object.DestroyImmediate(b.gameObject);

            // The kit model, in metres whatever the prefab's own scale (WaveSpawner fits soldiers to their height afterwards).
            float k = vehicle && maxHeight > 0 && baked.bounds.size.y > maxHeight ? maxHeight / baked.bounds.size.y : 1f;
            var look = new GameObject("Modelo_" + model.name).transform;
            look.SetParent(root, false);
            Vector3 s = root.lossyScale;
            look.localScale = new Vector3(k / Mathf.Max(1e-4f, s.x), k / Mathf.Max(1e-4f, s.y), k / Mathf.Max(1e-4f, s.z));

            var made = new Dictionary<string, Transform>();
            var renderers = new List<Renderer>();
            foreach (var p in baked.parts)
            {
                Transform parent = p.parent != null && made.TryGetValue(p.parent, out var pt) ? pt : look;
                Vector3 parentPivot = p.parent != null && made.ContainsKey(p.parent) ? Pivot(baked, p.parent) : Vector3.zero;
                var go = new GameObject(p.name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = p.pivot - parentPivot;
                made[p.name] = go.transform;
                if (p.mesh == null) continue;
                go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers.Add(mr);
            }

            var result = new Look { size = baked.bounds.size * k, center = baked.bounds.center * k };
            if (baked.legs)
            {
                var walk = look.gameObject.AddComponent<AndarMech>(); // finds Pierna_ / Rodilla_ / Brazo_ in Awake
                if (!vehicle) { walk.velocidadPlena = 2.6f; walk.cadencia = 1.5f; walk.cadera = 28f; walk.brazo = 0f; }
                else walk.velocidadPlena = ai != null && ai.Type != null ? ai.Type.walkSpeed : 2f; // the kit's mech gait otherwise
            }
            if (made.ContainsKey("Torreta") || made.ContainsKey("Torso")) result.aim = look.gameObject.AddComponent<UnitAim>();

            if (outline != null && OutlineRenderers != null)
            {
                var keep = new List<Renderer>();
                if (OutlineRenderers.GetValue(outline) is Renderer[] old) foreach (var r in old) if (r != null) keep.Add(r);
                keep.AddRange(renderers);
                OutlineRenderers.SetValue(outline, keep.ToArray());
                OutlineNormals?.Invoke(outline, null);
                if (outlined) outline.enabled = true;
            }
            return result;
        }

        static Vector3 Pivot(Baked b, string name)
        {
            foreach (var p in b.parts) if (p.name == name) return p.pivot;
            return Vector3.zero;
        }

        static Transform TopChild(Transform root, Transform t)
        {
            while (t != null && t.parent != null && t.parent != root) t = t.parent;
            return t != null && t.parent == root ? t : null;
        }

        // ------------------------------------------------------------ Baking
        /// <summary>One vertex-coloured mesh per moving part, coloured by the side's theme and moved so that its joint is the origin.
        /// The model's meshes must be Read/Write enabled (the import settings of the kit folders do that).</summary>
        static Baked Bake(GameObject model, Team team)
        {
            if (Cache.TryGetValue((model, team), out var cached)) return cached;
            var theme = TeamLivery.For(team, false);
            var filters = new List<MeshFilter>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                if (!mf.sharedMesh.isReadable) { Debug.LogWarning($"[Redbaron] {model.name}: turn on Read/Write in its import settings to use it as a unit."); Cache[(model, team)] = null; return null; }
                filters.Add(mf);
            }

            // The joints: from the table, or for soldiers the top of each leg.
            var joints = new List<Joint>();
            if (Joints.TryGetValue(model.name, out var table)) joints.AddRange(table);
            else
                foreach (var mf in filters)
                    if (mf.name == "Pierna_I" || mf.name == "Pierna_D")
                    {
                        Bounds lb = mf.sharedMesh.bounds;
                        joints.Add(new Joint(mf.name, "", lb.center.x, lb.max.y, lb.center.z));
                    }
            // Unity's OBJ import may mirror x: line the side joints up with their meshes.
            bool mirrored = false;
            foreach (var j in joints)
            {
                if (Mathf.Abs(j.pivot.x) < 0.1f) continue;
                var mf = filters.Find(f => f.name == j.name);
                if (mf == null) continue;
                mirrored = Mathf.Sign(mf.sharedMesh.bounds.center.x) != Mathf.Sign(j.pivot.x);
                break;
            }
            if (mirrored)
                for (int i = 0; i < joints.Count; i++) { var jj = joints[i]; jj.pivot.x = -jj.pivot.x; joints[i] = jj; }

            // Which part each mesh belongs to. The side's insignia goes on the torso or turret it sits on, else on the body.
            string mine = team == Team.Centrales ? "Insignia_Cruz" : "Insignia_Escarapela";
            var byPart = new Dictionary<string, List<MeshFilter>> { { "Cuerpo", new List<MeshFilter>() } };
            foreach (var j in joints) byPart[j.name] = new List<MeshFilter>();
            var b = new Baked();
            bool first = true;
            foreach (var mf in filters)
            {
                string part = "Cuerpo";
                if (mf.name.StartsWith("Insignia"))
                {
                    if (!mf.name.StartsWith(mine)) continue;
                    Vector3 c = mf.sharedMesh.bounds.center;
                    foreach (var host in filters)
                        if ((host.name == "Torso" || host.name == "Torreta") && byPart.ContainsKey(host.name))
                        {
                            Bounds hb = host.sharedMesh.bounds; hb.Expand(0.3f);
                            if (hb.Contains(c)) { part = host.name; break; }
                        }
                }
                else if (byPart.ContainsKey(mf.name)) part = mf.name;
                byPart[part].Add(mf);
                if (first) { b.bounds = mf.sharedMesh.bounds; first = false; } else b.bounds.Encapsulate(mf.sharedMesh.bounds);
            }
            if (first || byPart["Cuerpo"].Count == 0) { Cache[(model, team)] = null; return null; }

            b.parts.Add(new Part { name = "Cuerpo", parent = null, pivot = Vector3.zero, mesh = BakePart(byPart["Cuerpo"], theme, Vector3.zero, model.name + "_Cuerpo_" + team) });
            foreach (var j in joints)
            {
                var list = byPart[j.name];
                b.parts.Add(new Part
                {
                    name = j.name, parent = string.IsNullOrEmpty(j.parent) ? null : j.parent, pivot = j.pivot,
                    mesh = list.Count > 0 ? BakePart(list, theme, j.pivot, model.name + "_" + j.name + "_" + team) : null
                });
                if (j.name.StartsWith("Pierna_") || j.name.StartsWith("Pata_")) b.legs = true;
            }
            Cache[(model, team)] = b;
            return b;
        }

        static Mesh BakePart(List<MeshFilter> sources, TemaDatos theme, Vector3 origin, string name)
        {
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color32>(); var tris = new List<int>();
            foreach (var mf in sources)
            {
                var src = mf.sharedMesh; var mats = mf.GetComponent<MeshRenderer>().sharedMaterials;
                var sv = src.vertices; var sn = src.normals;
                for (int sub = 0; sub < src.subMeshCount; sub++)
                {
                    Color32 c = PaletaColor.ColorFor(sub < mats.Length ? mats[sub] : null, theme);
                    var idx = src.GetTriangles(sub);
                    for (int i = 0; i < idx.Length; i++) // every corner its own vertex, so each face keeps its material's colour
                    {
                        tris.Add(verts.Count);
                        verts.Add(sv[idx[i]] - origin);
                        normals.Add(sn.Length > 0 ? sn[idx[i]] : Vector3.up);
                        colors.Add(c);
                    }
                }
            }
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetNormals(normals); m.SetColors(colors); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
