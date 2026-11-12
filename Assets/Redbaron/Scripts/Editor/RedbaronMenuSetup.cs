#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Redbaron.EditorTools
{
    /// <summary>
    /// Redbaron > Crear menú principal: writes the default catalog (hangar ships and buildings), the missions and the
    /// menu scene, adds the pause menu to the battle scene, and puts the menu first in the build's scene list.
    /// Safe to run again: existing catalog and mission assets are overwritten with the defaults below.
    /// </summary>
    public static class RedbaronMenuSetup
    {
        const string Root = "Assets/Redbaron";
        const string CatalogPath = Root + "/Data/Redbaron_Catalogo.asset";
        const string MissionsDir = Root + "/Missions";
        const string MenuScenePath = Root + "/Scenes/Redbaron_Menu.unity";
        const string BattleScenePath = Root + "/Scenes/Redbaron_QuickRTS.unity";
        const string MatPath = Root + "/Materials/Redbaron_VertexColor.mat";
        const string ShipsDir = Root + "/Naves/";

        [MenuItem("Redbaron/Crear menú principal")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Leave Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var catalog = WriteCatalog();
            var missions = WriteMissions();
            WriteMenuScene(catalog, missions);
            AddPauseToBattle();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuScenePath, true), new EditorBuildSettingsScene(BattleScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Redbaron: menu, catalog and missions created; build scenes = menu, battle.");
        }

        // ------------------------------------------------------------ Catalog
        static RedbaronCatalog WriteCatalog()
        {
            Folder(Root + "/Data");
            var c = AssetDatabase.LoadAssetAtPath<RedbaronCatalog>(CatalogPath);
            if (c == null) { c = ScriptableObject.CreateInstance<RedbaronCatalog>(); AssetDatabase.CreateAsset(c, CatalogPath); }

            var ships = new List<RedbaronCatalog.Ship>();
            void Ship(string file, string name, Team side, string role, string size, string description)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ShipsDir + file + ".obj");
                if (model == null) { Debug.LogWarning("Redbaron: missing ship model " + file); return; }
                ships.Add(new RedbaronCatalog.Ship { name = name, model = model, side = side, role = role, size = size, description = description });
            }
            const Team C = Team.Centrales, E = Team.Entente;
            Ship("Halcon_de_Laton", "Halcón de Latón", C, "Caza triplano", "7,5 × 2,7 × 6,8 m", "Triplano de caza de las Potencias Centrales. Sus tres alas apiladas dan mucha sustentación en poco espacio.");
            Ship("Halcon_de_Laton_Curva", "Halcón de Latón (curvo)", C, "Caza triplano", "7,5 × 2,7 × 6,8 m", "La versión curva del Halcón: fuselaje torneado y alas de puntas redondeadas.");
            Ship("Vibora_de_Vapor", "Víbora de Vapor", E, "Caza biplano", "8,6 × 3,1 × 7,7 m", "Biplano de caza de la Entente, con los tubos de vapor a la vista.");
            Ship("Vibora_de_Vapor_Curva", "Víbora de Vapor (curva)", E, "Caza biplano", "8,6 × 3,1 × 7,7 m", "La versión curva de la Víbora: fuselaje torneado, timones curvos y tubos de vapor doblados.");
            Ship("Aguja", "Aguja", C, "Caza monoplano", "9,6 × 2,8 × 7,0 m", "Un solo par de alas y un fuselaje afilado, pensado para la velocidad.");
            Ship("Mantis", "Mantis", E, "Caza empujador", "8,6 × 2,9 × 8,7 m", "La hélice va detrás de la cabina y empuja el avión, así que el morro queda libre para la ametralladora.");
            Ship("Lechuza", "Lechuza", C, "Caza nocturno", "8,2 × 3,7 × 6,9 m", "Caza nocturno con un reflector para buscar blancos en la oscuridad.");
            Ship("Corneja", "Corneja", E, "Biplaza de reconocimiento", "11,4 × 3,1 × 8,2 m", "Biplaza: el piloto delante y un observador detrás para vigilar las líneas enemigas.");
            Ship("Garza", "Garza", C, "Hidroavión de patrulla", "12,0 × 4,1 × 8,1 m", "Hidroavión con flotadores, para patrullar ríos y costas.");
            Ship("Libelula", "Libélula", E, "Girocóptero de observación", "9,1 × 3,6 × 10,1 m", "Un rotor de 10 m sobre la cabina y una hélice de empuje: vigila el frente desde el aire.");
            Ship("Libelula_Curva", "Libélula (curva)", E, "Girocóptero de observación", "9,1 × 3,6 × 10,1 m", "La versión curva de la Libélula, con cúpula y fuselaje torneado.");
            Ship("Murcielago", "Murciélago", C, "Ornitóptero experimental", "13,1 × 3,3 × 6,7 m", "Un prototipo que vuela batiendo las alas, como un murciélago.");
            Ship("Coloso", "Coloso", E, "Bombardero trimotor", "22,0 × 5,4 × 14,9 m", "El bombardero pesado de la Entente: tres motores y 22 m de envergadura.");
            Ship("Leviatan", "Leviatán", C, "Bombardero cuatrimotor", "24,0 × 6,9 × 16,4 m", "El bombardero pesado de las Potencias Centrales: cuatro motores y 24 m de envergadura.");
            Ship("Leviatan_Curva", "Leviatán (curvo)", C, "Bombardero cuatrimotor", "24,0 × 6,9 × 16,4 m", "La versión curva del Leviatán, con góndolas en forma de gota.");
            c.ships = ships.ToArray();

            // Mirrors BiplaneAI's values for the player's plane (checked in the battle on 2026-11-12).
            c.shipStats = new[]
            {
                S("Energía", "6 impactos"), S("Munición", "80 balas"), S("Bombas", "4"), S("Velocidad", "11 a 26 m/s"),
                S("Alabeo", "150°/s"), S("Giro máximo", "65°/s"), S("Cadencia", "una ráfaga cada 0,5 s"),
                S("Bala", "96 m/s más la velocidad del avión"), S("Choque", "−2 de energía, y 1,5 s sin más daño por choques"),
            };
            c.shipStatsNote = "En la batalla todas las naves vuelan igual por ahora: lo que cambia es el modelo.";

            c.buildings = new[]
            {
                new RedbaronCatalog.Building
                {
                    name = "Puesto de mando", model = RedbaronCatalog.BuildingModel.CommandPost, count = "Uno por bando",
                    stats = new[] { S("Resistencia", "150"), S("Daño por bomba", "12 (a menos de 9 m)"), S("Tamaño", "15 × 5 × 10 m"), S("Si cae", "gana el otro bando") },
                    description = "El corazón de cada base. Cuando su barra llega a cero, el bando contrario gana y la batalla vuelve a empezar.",
                },
                new RedbaronCatalog.Building
                {
                    name = "Casamata", model = RedbaronCatalog.BuildingModel.Bunker, count = "Seis por bando",
                    stats = new[] { S("Resistencia", "20"), S("Daño por bomba", "6 (a menos de 8 m)"), S("Arma", "ametralladora"), S("Alcance", "42 m"), S("Ráfaga", "2 disparos cada 1,75 s") },
                    description = "Nido de ametralladora de hormigón con sacos terreros. Dispara a la infantería y los blindados enemigos que se acercan. Cuatro bombas bien puestas lo destruyen, y también caen ante granadas, cañonazos y cargas de zapador.",
                },
                new RedbaronCatalog.Building
                {
                    name = "Torre antiaérea", model = RedbaronCatalog.BuildingModel.FlakTower, count = "Una por bando",
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Edificios/Torre_Flak.obj"),
                    stats = new[] { S("Resistencia", "18"), S("Daño por bomba", "6 (a menos de 8 m)"), S("Arma", "cañón antiaéreo"), S("Alcance", "60 m"), S("Cadencia", "un proyectil cada 2,5 s"), S("Explosión", "5 m alrededor del avión, −1 de energía") },
                    description = "Junto al puesto de mando. Dispara a los aviones enemigos que se acercan, sin adelantarse a su movimiento: el proyectil estalla donde estaba el avión, así que un avión rápido escapa y uno lento o que gira cae en la explosión. Los aviones pasan por encima. Tres bombas la destruyen, y la infantería, los tanques y los mechs también la atacan.",
                },
                new RedbaronCatalog.Building
                {
                    name = "Hangar", model = RedbaronCatalog.BuildingModel.Hangar, count = "Dos por bando",
                    stats = new[] { S("Resistencia", "40"), S("Daño por bomba", "10 (a menos de 3 m)"), S("Función", "despegue y reaparición"), S("Tamaño", "8,6 × 5 × 12 m") },
                    description = "Aquí despegan y reaparecen los aviones de su bando. Cuatro bombas lo derriban, igual que los cañonazos y las cargas de los zapadores; aunque caiga, los aviones siguen despegando de la pista. Las bombas que caen sobre el aeródromo enemigo suman en el marcador «Bombas en aeródromo».",
                },
                new RedbaronCatalog.Building
                {
                    name = "Punto de recarga", model = RedbaronCatalog.BuildingModel.RefillPoint, count = "Uno por aeródromo",
                    stats = new[] { S("Recupera", "energía, munición y bombas"), S("Alcance", "4 m más el tamaño del avión"), S("Espera", "6 s entre usos") },
                    description = "Una caja de suministros que flota sobre la pista, a la altura de vuelo. Atraviésala con tu avión para recargar; solo sirve a los aviones de su bando.",
                },
            };
            EditorUtility.SetDirty(c);
            return c;
        }

        static RedbaronCatalog.Stat S(string label, string value) => new RedbaronCatalog.Stat(label, value);

        // ------------------------------------------------------------ Missions
        static MissionDefinition[] WriteMissions()
        {
            Folder(MissionsDir);
            MissionDefinition M(string file, string title, string setting, string briefing)
            {
                string path = MissionsDir + "/" + file + ".asset";
                var m = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);
                if (m == null) { m = ScriptableObject.CreateInstance<MissionDefinition>(); AssetDatabase.CreateAsset(m, path); }
                m.title = title; m.setting = setting; m.briefing = briefing;
                m.objective = "Destruye el puesto de mando enemigo.";
                m.scenario = "Frente en diez franjas";
                m.sceneName = "Redbaron_QuickRTS";
                EditorUtility.SetDirty(m);
                return m;
            }
            return new[]
            {
                M("Mision_01", "Frente en diez franjas", "Sector de las diez franjas · 1917",
                  "Dos líneas de trincheras se enfrentan en un valle cerrado por acantilados. Tu infantería avanza sola desde el principio: cúbrela desde el aire, derriba los aviones enemigos y castiga sus trincheras con bombas."),
                M("Mision_02", "Ofensiva de primavera", "Sector de las diez franjas · abril de 1917",
                  "El mando prepara una gran ofensiva. Mantén el cielo libre de cazas enemigos para que la infantería pueda cruzar la tierra de nadie."),
                M("Mision_03", "Última luz", "Sector de las diez franjas · otoño de 1918",
                  "Tras meses de combate, las dos líneas están agotadas. Un último empujón puede decidir el sector: apoya el asalto final."),
            };
        }

        // ------------------------------------------------------------ Scenes
        static void WriteMenuScene(RedbaronCatalog catalog, MissionDefinition[] missions)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.09f, 0.07f);
            cam.gameObject.AddComponent<AudioListener>();
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.48f);
            var menu = new GameObject("Redbaron_Menu").AddComponent<RedbaronMainMenu>();
            menu.catalog = catalog;
            menu.missions = missions;
            menu.buildingMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        static void AddPauseToBattle()
        {
            var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            var game = Object.FindAnyObjectByType<RedbaronGame>();
            if (game != null && game.GetComponent<PauseMenu>() == null) game.gameObject.AddComponent<PauseMenu>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
#endif
