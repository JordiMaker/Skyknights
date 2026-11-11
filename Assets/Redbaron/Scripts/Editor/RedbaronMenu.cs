#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Redbaron.EditorTools
{
    public static class RedbaronMenu
    {
        const string MatPath = "Assets/Redbaron/Materials/Redbaron_VertexColor.mat";

        static Material VertexMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null) return mat;
            if (!AssetDatabase.IsValidFolder("Assets/Redbaron/Materials")) AssetDatabase.CreateFolder("Assets/Redbaron", "Materials");
            mat = new Material(Shader.Find("Redbaron/LowPolyVertexColor"));
            AssetDatabase.CreateAsset(mat, MatPath);
            return mat;
        }

        [MenuItem("Redbaron/Crear escena de partida")]
        public static void CreateScene()
        {
            CreateSceneObjects();
            EditorUtility.DisplayDialog("Redbaron",
                "Escena creada. Ahora asigna en el objeto Redbaron:\n\n" +
                "• Árboles y rocas de Low Poly Environment (RedbaronMapBuilder)\n" +
                "• Prefab de soldado de Toon Soldiers para cada bando (WaveSpawner)\n" +
                "• En la cámara, el script de cámara de Quick RTS en 'Rts Camera Scripts'\n\n" +
                "Usa Redbaron > Vista previa del mapa para verlo sin darle a Play.", "Vale");
        }

        /// <summary>Creates the Redbaron objects, light, fog and camera setup without showing any dialog (usable from scripts).</summary>
        public static GameObject CreateSceneObjects()
        {
            var root = new GameObject("Redbaron");
            Undo.RegisterCreatedObjectUndo(root, "Crear Redbaron");
            var builder = root.AddComponent<RedbaronMapBuilder>();
            builder.vertexColorMaterial = VertexMaterial();
            var game = root.AddComponent<RedbaronGame>();
            var spawner = root.AddComponent<WaveSpawner>();
            game.spawner = spawner;
            var air = root.AddComponent<AirWar>();
            air.planeMaterial = builder.vertexColorMaterial;

            var cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(RedbaronMap.WorldW / 2 + 60, 120, RedbaronMap.WorldL / 2 - 140);
            cam.transform.LookAt(new Vector3(RedbaronMap.WorldW / 2, 0, RedbaronMap.WorldL / 2));
            cam.farClipPlane = 1500;
            var cams = cam.gameObject.GetComponent<RedbaronCameras>();
            if (cams == null) cams = cam.gameObject.AddComponent<RedbaronCameras>();
            cams.airWar = air;

            var sun = Object.FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            sun.color = new Color(1f, 0.94f, 0.83f);
            sun.shadows = LightShadows.Soft;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 450);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0xb9, 0xbc, 0xae, 255);
            RenderSettings.fogStartDistance = 400;
            RenderSettings.fogEndDistance = 880;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0xdc, 0xdf, 0xd0, 255);
            RenderSettings.ambientEquatorColor = new Color32(0x9a, 0x9a, 0x88, 255);
            RenderSettings.ambientGroundColor = new Color32(0x4a, 0x46, 0x36, 255);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;

            Selection.activeGameObject = root;
            return root;
        }

        [MenuItem("Redbaron/Vista previa del mapa")]
        public static void Preview()
        {
            var builder = Object.FindAnyObjectByType<RedbaronMapBuilder>();
            if (builder == null) { EditorUtility.DisplayDialog("Redbaron", "No hay ningún RedbaronMapBuilder en la escena. Usa primero Redbaron > Crear escena de partida.", "Vale"); return; }
            if (builder.vertexColorMaterial == null) builder.vertexColorMaterial = VertexMaterial();
            builder.Build();
            // La vista previa no se guarda en la escena: al darle a Play el mapa se vuelve a construir.
            foreach (var t in builder.Root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            SceneView.RepaintAll();
        }

        [MenuItem("Redbaron/Borrar vista previa")]
        public static void ClearPreview()
        {
            var builder = Object.FindAnyObjectByType<RedbaronMapBuilder>();
            if (builder == null) return;
            var r = builder.transform.Find("Redbaron_Mapa");
            if (r != null) Object.DestroyImmediate(r.gameObject);
        }
    }
}
#endif
