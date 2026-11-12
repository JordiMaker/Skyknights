using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Dresses the infantry in the Redbaron colour kit's big-headed soldiers instead of the Toon Soldiers model the prefabs carry.
    /// Each kit model is baked once per side into vertex-coloured meshes (body and both legs), painted with the side's theme
    /// (TeamLivery), and every soldier of that model and side shares them. That keeps a soldier at three draw calls, which matters
    /// with up to 120 of them on a phone. The legs swing with the kit's AndarMech; a soldier with no Animator falls flat when it dies.
    /// </summary>
    public static class SoldierModels
    {
        class Baked { public Mesh body, legL, legR; public Vector3 hipL, hipR; }
        static readonly Dictionary<(GameObject, Team), Baked> Cache = new Dictionary<(GameObject, Team), Baked>();
        static readonly FieldInfo OutlineRenderers = typeof(Outline).GetField("renderers", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo OutlineNormals = typeof(Outline).GetMethod("LoadSmoothNormals", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Replaces the soldier's look with model (a kit OBJ with "Cuerpo", "Pierna_I" and "Pierna_D").
        /// Returns false, leaving the soldier untouched, if the model cannot be used.</summary>
        public static bool Apply(GameObject soldier, GameObject model, Team team, Material material)
        {
            if (model == null || material == null) return false;
            var baked = Bake(model, team);
            if (baked == null) return false;
            var root = soldier.transform;

            // Quick Outline keeps a list of the renderers it outlines: take its materials off the old ones first.
            var outline = soldier.GetComponent<Outline>();
            bool outlined = outline != null && outline.enabled;
            if (outlined) outline.enabled = false;

            // The Toon Soldiers look goes: animator, skinned meshes and their bones.
            var ai = soldier.GetComponent<SoldierAI>();
            if (ai != null) ai.DropAnimator();
            foreach (var a in soldier.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            var bones = new HashSet<Transform>();
            foreach (var smr in soldier.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.rootBone != null) bones.Add(TopChild(root, smr.rootBone));
                if (smr.gameObject == soldier) Object.DestroyImmediate(smr);
                else bones.Add(TopChild(root, smr.transform));
            }
            foreach (var b in bones) if (b != null && b != root) Object.DestroyImmediate(b.gameObject);

            // The kit soldier, at 1:1 in metres whatever the prefab's own scale (WaveSpawner fits the height afterwards).
            var look = new GameObject("Modelo_" + model.name).transform;
            look.SetParent(root, false);
            Vector3 s = root.lossyScale;
            look.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
            var renderers = new List<Renderer> { Part("Cuerpo", look, Vector3.zero, baked.body, material) };
            if (baked.legL != null) renderers.Add(Part("Pierna_I", look, baked.hipL, baked.legL, material));
            if (baked.legR != null) renderers.Add(Part("Pierna_D", look, baked.hipR, baked.legR, material));
            var walk = look.gameObject.AddComponent<AndarMech>(); // finds Pierna_I / Pierna_D in Awake
            walk.velocidadPlena = 2.6f; walk.cadencia = 1.5f; walk.cadera = 28f; walk.brazo = 0f;

            if (outline != null && OutlineRenderers != null)
            {
                var keep = new List<Renderer>();
                if (OutlineRenderers.GetValue(outline) is Renderer[] old) foreach (var r in old) if (r != null) keep.Add(r);
                keep.AddRange(renderers);
                OutlineRenderers.SetValue(outline, keep.ToArray());
                OutlineNormals?.Invoke(outline, null);
                if (outlined) outline.enabled = true;
            }
            return true;
        }

        static Renderer Part(string name, Transform parent, Vector3 at, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return mr;
        }

        static Transform TopChild(Transform root, Transform t)
        {
            while (t != null && t.parent != null && t.parent != root) t = t.parent;
            return t != null && t.parent == root ? t : null;
        }

        // ------------------------------------------------------------ Baking
        /// <summary>One vertex-coloured mesh per part, coloured by the side's theme. Legs are moved so that their hip is the origin.
        /// The model's meshes must be Read/Write enabled (the import settings of Assets/Redbaron/Soldados do that).</summary>
        static Baked Bake(GameObject model, Team team)
        {
            if (Cache.TryGetValue((model, team), out var cached)) return cached;
            var theme = TeamLivery.For(team, false);
            Baked b = new Baked();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mesh == null || mr == null) continue;
                if (!mesh.isReadable) { Debug.LogWarning($"[Redbaron] {model.name}: turn on Read/Write in its import settings to use it as a soldier."); continue; }
                Bounds bb = mesh.bounds;
                Vector3 hip = new Vector3(bb.center.x, bb.max.y, bb.center.z);
                if (mf.name == "Cuerpo") b.body = BakePart(mesh, mr.sharedMaterials, theme, Vector3.zero, model.name + "_Cuerpo_" + team);
                else if (mf.name == "Pierna_I") { b.hipL = hip; b.legL = BakePart(mesh, mr.sharedMaterials, theme, hip, model.name + "_PiernaI_" + team); }
                else if (mf.name == "Pierna_D") { b.hipR = hip; b.legR = BakePart(mesh, mr.sharedMaterials, theme, hip, model.name + "_PiernaD_" + team); }
            }
            if (b.body == null) b = null;
            Cache[(model, team)] = b;
            return b;
        }

        static Mesh BakePart(Mesh src, Material[] mats, TemaDatos theme, Vector3 origin, string name)
        {
            var sv = src.vertices; var sn = src.normals;
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color32>(); var tris = new List<int>();
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
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetNormals(normals); m.SetColors(colors); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
