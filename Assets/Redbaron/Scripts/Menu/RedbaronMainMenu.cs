using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Redbaron
{
    /// <summary>
    /// The game's front end, built from code at start-up:
    /// - Main screen: title, Missions / Aircraft / Buildings / Quit, a ship turning on the right, the version.
    /// - Missions: the list and the selected mission's briefing, with a take-off button.
    /// - Aircraft and Buildings: one card per entry of the catalog, with a 3D model you can turn and its values.
    /// The phone's back button (Escape) returns to the main screen.
    /// </summary>
    public class RedbaronMainMenu : MonoBehaviour
    {
        [Header("Content")]
        public string gameTitle = "SKYKNIGHTS";
        public string tagline = "Combate aéreo en la Gran Guerra";
        public RedbaronCatalog catalog;
        public MissionDefinition[] missions;
        [Tooltip("Material of the buildings' vertex-coloured meshes (Redbaron_VertexColor).")]
        public Material buildingMaterial;
        [Tooltip("Side whose colours the building cards use.")]
        public Team buildingSide = Team.Entente;

        RectTransform safe, current, mainScreen, missionScreen, shipScreen, buildingScreen, loading;
        ModelPreview mainPreview;
        Text mainCaption, loadingText;

        void Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            var canvas = UiKit.Canvas("Menu_Principal", 10, transform);
            Background(canvas.transform);
            safe = UiKit.SafeArea(canvas.transform);
            BuildMain();
            BuildMissions();
            if (catalog != null)
            {
                shipScreen = BuildCardScreen("NAVES", ShipCards());
                buildingScreen = BuildCardScreen("EDIFICIOS", BuildingCards());
            }
            BuildLoading();
            Show(mainScreen);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && current != mainScreen && current != loading) Show(mainScreen);
        }

        void Show(RectTransform screen)
        {
            foreach (var s in new[] { mainScreen, missionScreen, shipScreen, buildingScreen, loading })
                if (s != null) s.gameObject.SetActive(s == screen);
            current = screen;
            if (screen == mainScreen) ShowRandomShip();
        }

        // ================================================================ Main screen
        void BuildMain()
        {
            mainScreen = UiKit.Fill(UiKit.Rect("Pantalla_Principal", safe));
            var col = UiKit.Place(UiKit.Rect("Columna", mainScreen), new Vector2(0.04f, 0), new Vector2(0.46f, 1));

            var title = UiKit.Label(col, gameTitle, 118, UiKit.Brass, TextAnchor.UpperLeft, true, "Titulo");
            UiKit.At(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -90), new Vector2(900, 140));
            var line = UiKit.Bar(col, UiKit.Brass, "Raya");
            UiKit.At(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -236), new Vector2(420, 4));
            var sub = UiKit.Label(col, tagline, 36, UiKit.Muted, TextAnchor.UpperLeft, false, "Lema");
            UiKit.At(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -258), new Vector2(900, 50));

            var buttons = new (string, UnityAction, Color)[]
            {
                ("MISIONES", () => Show(missionScreen), new Color(0.50f, 0.38f, 0.14f, 0.97f)),
                ("NAVES", () => Show(shipScreen), UiKit.ButtonColor),
                ("EDIFICIOS", () => Show(buildingScreen), UiKit.ButtonColor),
                ("SALIR", Quit, new Color(0.18f, 0.17f, 0.14f, 0.92f)),
            };
            float y = -380;
            foreach (var (label, action, color) in buttons)
            {
                if (label == "SALIR" && Application.isEditor) continue; // quitting does nothing in the Editor
                var b = UiKit.Button(col, label, 44, action, color, "Boton_" + label);
                UiKit.At((RectTransform)b.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(560, 112));
                y -= 136;
            }

            var stage = UiKit.Place(UiKit.Rect("Nave_Giratoria", mainScreen), new Vector2(0.46f, 0.14f), new Vector2(0.98f, 0.94f));
            stage.gameObject.AddComponent<RawImage>().color = new Color(1, 1, 1, 0);
            mainPreview = stage.gameObject.AddComponent<ModelPreview>();
            mainPreview.spinSpeed = 14f;
            mainCaption = UiKit.Label(mainScreen, "", 30, UiKit.Muted, TextAnchor.MiddleCenter, false, "Nombre_Nave");
            UiKit.Place(mainCaption.rectTransform, new Vector2(0.46f, 0.07f), new Vector2(0.98f, 0.14f));

            var version = UiKit.Label(mainScreen, "v" + Application.version, 26, UiKit.Muted, TextAnchor.LowerRight, false, "Version");
            UiKit.At(version.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 22), new Vector2(300, 40));
        }

        void ShowRandomShip()
        {
            if (mainPreview == null || catalog == null || catalog.ships == null || catalog.ships.Length == 0) return;
            var s = catalog.ships[Random.Range(0, catalog.ships.Length)];
            if (s.model == null) return;
            mainPreview.Show(BiplaneAI.Showcase(s.model, s.side, null));
            mainCaption.text = $"{s.name} · {s.role}";
        }

        static void Quit()
        {
            Application.Quit();
        }

        // ================================================================ Missions
        Text mTitle, mSetting, mBriefing, mObjective, mScenario;
        MissionDefinition selected;
        readonly List<Image> missionRows = new List<Image>();

        void BuildMissions()
        {
            missionScreen = MakeScreen("MISIONES", out var list, out var detail);
            missionRows.Clear();
            if (missions != null)
                for (int i = 0; i < missions.Length; i++)
                {
                    var m = missions[i];
                    if (m == null) continue;
                    int index = missionRows.Count;
                    missionRows.Add(Row(list, $"{index + 1:00}  {m.title}", m.setting, UiKit.Brass, () => SelectMission(m, index)));
                }

            var info = Column(detail, new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.95f));
            mTitle = Add(info, UiKit.Label(info, "", 58, UiKit.Paper, TextAnchor.UpperLeft, true));
            mSetting = Add(info, UiKit.Label(info, "", 30, UiKit.Muted));
            Add(info, UiKit.Bar(info, UiKit.Brass), 4);
            mBriefing = Add(info, UiKit.Label(info, "", 34, UiKit.Paper));
            mObjective = Add(info, UiKit.Label(info, "", 34, UiKit.Brass, TextAnchor.UpperLeft, true));
            mScenario = Add(info, UiKit.Label(info, "", 26, UiKit.Muted));

            var go = UiKit.Button(detail, "DESPEGAR", 46, TakeOff, new Color(0.50f, 0.38f, 0.14f, 0.97f), "Boton_Despegar");
            UiKit.At((RectTransform)go.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(440, 120));

            if (missions != null && missions.Length > 0 && missions[0] != null) SelectMission(missions[0], 0);
        }

        void SelectMission(MissionDefinition m, int index)
        {
            selected = m;
            for (int i = 0; i < missionRows.Count; i++) Highlight(missionRows[i], i == index);
            mTitle.text = m.title;
            mSetting.text = m.setting;
            mBriefing.text = m.briefing;
            mObjective.text = "Objetivo: " + m.objective;
            mScenario.text = "Escenario: " + m.scenario;
        }

        void TakeOff()
        {
            if (selected == null) return;
            Show(loading);
            loadingText.text = $"Cargando «{selected.title}»…";
            MissionSession.Begin(selected);
        }

        void BuildLoading()
        {
            loading = UiKit.Fill(UiKit.Rect("Pantalla_Carga", safe));
            loadingText = UiKit.Label(loading, "", 44, UiKit.Paper, TextAnchor.MiddleCenter, true);
            UiKit.Fill(loadingText.rectTransform);
        }

        // ================================================================ Cards (aircraft and buildings)
        class Card
        {
            public string title, subtitle, line, note, description;
            public Color accent;
            public RedbaronCatalog.Stat[] stats;
            public System.Func<GameObject> model;
        }

        List<Card> ShipCards()
        {
            var cards = new List<Card>();
            if (catalog.ships == null) return cards;
            foreach (var s in catalog.ships)
            {
                if (s == null || s.model == null) continue;
                var ship = s;
                cards.Add(new Card
                {
                    title = s.name,
                    subtitle = s.role,
                    line = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(UiKit.SideColor(s.side), Color.white, 0.35f))}>{UiKit.SideName(s.side)}</color>  ·  {s.size}",
                    accent = UiKit.SideColor(s.side),
                    stats = catalog.shipStats,
                    note = catalog.shipStatsNote,
                    description = s.description,
                    model = () => BiplaneAI.Showcase(ship.model, ship.side, null),
                });
            }
            return cards;
        }

        List<Card> BuildingCards()
        {
            var cards = new List<Card>();
            if (catalog.buildings == null) return cards;
            foreach (var b in catalog.buildings)
            {
                if (b == null) continue;
                var building = b;
                cards.Add(new Card
                {
                    title = b.name,
                    subtitle = b.count,
                    line = b.count,
                    accent = UiKit.Brass,
                    stats = b.stats,
                    description = b.description,
                    model = () => BuildingModel(building),
                });
            }
            return cards;
        }

        GameObject BuildingModel(RedbaronCatalog.Building building)
        {
            var kind = building.model;
            if (kind == RedbaronCatalog.BuildingModel.FlakTower && building.prefab != null)
            {
                var holder = new GameObject("Torre_antiaerea");
                FlakTower.BuildDisplay(building.prefab, buildingSide, holder.transform, out _, out _, out _, out _);
                return holder;
            }
            if (kind == RedbaronCatalog.BuildingModel.RefillPoint)
            {
                var holder = new GameObject("Punto_de_recarga");
                AirPickup.BuildIcon(holder.transform, buildingMaterial, out _);
                return holder;
            }
            var acc = new MeshAccumulator();
            switch (kind)
            {
                case RedbaronCatalog.BuildingModel.CommandPost: BuildingModels.AddCommandPost(acc, buildingSide); break;
                case RedbaronCatalog.BuildingModel.Bunker: BuildingModels.AddBunker(acc, buildingSide); break;
                default: BuildingModels.AddHangar(acc, Vector3.zero, buildingSide); break;
            }
            return acc.ToObject(kind.ToString(), null, buildingMaterial, false);
        }

        RectTransform BuildCardScreen(string heading, List<Card> cards)
        {
            var screen = MakeScreen(heading, out var list, out var detail, 0.30f);
            var rows = new List<Image>();

            var stage = UiKit.Place(UiKit.Rect("Vista_3D", detail), new Vector2(0, 0), new Vector2(0.52f, 1), new Vector2(20, 20), new Vector2(0, -20));
            UiKit.Fill(UiKit.Panel(stage, new Color(1, 1, 1, 0.05f), "Fondo_Vista").rectTransform);
            var raw = UiKit.Rect("Modelo", stage);
            UiKit.Fill(raw);
            raw.gameObject.AddComponent<RawImage>().color = new Color(1, 1, 1, 0);
            var preview = raw.gameObject.AddComponent<ModelPreview>();
            var hint = UiKit.Label(stage, "Arrastra para girar", 24, UiKit.Muted, TextAnchor.LowerCenter);
            UiKit.Fill(hint.rectTransform, 0, 14, 0, 0);

            var info = Column(detail, new Vector2(0.55f, 0.03f), new Vector2(0.97f, 0.96f));
            var title = Add(info, UiKit.Label(info, "", 54, UiKit.Paper, TextAnchor.UpperLeft, true));
            var sub = Add(info, UiKit.Label(info, "", 32, UiKit.Brass));
            var line = Add(info, UiKit.Label(info, "", 28, UiKit.Muted));
            Add(info, UiKit.Bar(info, UiKit.Brass), 4);
            var stats = Add(info, UiKit.Label(info, "", 30, UiKit.Paper));
            stats.lineSpacing = 1.15f;
            var note = Add(info, UiKit.Label(info, "", 24, UiKit.Muted));
            var desc = Add(info, UiKit.Label(info, "", 30, UiKit.Paper));

            void Select(int i)
            {
                var c = cards[i];
                for (int k = 0; k < rows.Count; k++) Highlight(rows[k], k == i);
                title.text = c.title;
                sub.text = c.subtitle;
                line.text = c.line == c.subtitle ? "" : c.line;
                line.gameObject.SetActive(!string.IsNullOrEmpty(line.text));
                var sb = new System.Text.StringBuilder();
                if (c.stats != null)
                    foreach (var st in c.stats) sb.Append($"<color=#B8AF97>{st.label}</color>   <b>{st.value}</b>\n");
                stats.text = sb.ToString().TrimEnd('\n');
                note.text = c.note ?? "";
                note.gameObject.SetActive(!string.IsNullOrEmpty(note.text));
                desc.text = c.description;
                preview.Show(c.model());
            }

            for (int i = 0; i < cards.Count; i++)
            {
                int index = i;
                rows.Add(Row(list, cards[i].title, cards[i].subtitle, cards[i].accent, () => Select(index)));
            }
            if (cards.Count > 0) Select(0);
            return screen;
        }

        // ================================================================ Shared layout
        /// <summary>A screen with a heading, a back button, a list on the left and a detail panel on the right.</summary>
        RectTransform MakeScreen(string heading, out RectTransform list, out RectTransform detail, float split = 0.36f)
        {
            var screen = UiKit.Fill(UiKit.Rect("Pantalla_" + heading, safe));
            var back = UiKit.Button(screen, "VOLVER", 34, () => Show(mainScreen), null, "Boton_Volver");
            UiKit.At((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -36), new Vector2(240, 96));
            var h = UiKit.Label(screen, heading, 66, UiKit.Brass, TextAnchor.MiddleLeft, true, "Encabezado");
            UiKit.At(h.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(310, -36), new Vector2(1000, 96));

            var listPanel = UiKit.Panel(screen, UiKit.Ink, "Panel_Lista").rectTransform;
            UiKit.Place(listPanel, new Vector2(0.025f, 0.05f), new Vector2(split, 0.83f));
            list = UiKit.ScrollList(listPanel, 10);
            UiKit.Fill((RectTransform)list.parent, 16, 16, 16, 16);
            var lg = list.GetComponent<VerticalLayoutGroup>(); lg.padding = new RectOffset(0, 0, 0, 0);

            detail = UiKit.Panel(screen, UiKit.Ink, "Panel_Detalle").rectTransform;
            UiKit.Place(detail, new Vector2(split + 0.015f, 0.05f), new Vector2(0.975f, 0.83f));
            return screen;
        }

        /// <summary>A list entry: a coloured bar on the left, a title and a smaller line under it.</summary>
        Image Row(RectTransform list, string title, string subtitle, Color accent, UnityAction onClick)
        {
            var bg = UiKit.Panel(list, UiKit.InkLight, "Fila");
            UiKit.Height(bg, string.IsNullOrEmpty(subtitle) ? 84 : 112);
            var b = bg.gameObject.AddComponent<Button>();
            b.onClick.AddListener(onClick);
            var bar = UiKit.Bar(bg.transform, accent);
            UiKit.Place(bar.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 10), new Vector2(10, -10));
            var t = UiKit.Label(bg.transform, title, 34, UiKit.Paper, TextAnchor.UpperLeft, true);
            UiKit.Fill(t.rectTransform, 30, 0, 14, 16);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = UiKit.Label(bg.transform, subtitle, 24, UiKit.Muted, TextAnchor.LowerLeft);
                UiKit.Fill(s.rectTransform, 30, 16, 14, 0);
                s.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return bg;
        }

        static void Highlight(Image row, bool on) => row.color = on ? new Color(0.34f, 0.32f, 0.22f, 0.98f) : UiKit.InkLight;

        /// <summary>A vertical stack inside a panel: each added element takes its preferred height.</summary>
        static RectTransform Column(RectTransform parent, Vector2 min, Vector2 max)
        {
            var col = UiKit.Place(UiKit.Rect("Columna", parent), min, max);
            var lg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            lg.spacing = 18; lg.childAlignment = TextAnchor.UpperLeft;
            lg.childControlWidth = true; lg.childForceExpandWidth = true;
            lg.childControlHeight = true; lg.childForceExpandHeight = false;
            return col;
        }

        static T Add<T>(RectTransform column, T element, float height = -1) where T : Component
        {
            if (height > 0) UiKit.Height(element, height);
            return element;
        }

        /// <summary>A soft vertical gradient behind everything: lighter haze at the top, dark olive at the bottom.</summary>
        static void Background(Transform canvas)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                tex.SetPixel(0, y, Color.Lerp(new Color(0.07f, 0.08f, 0.06f), new Color(0.27f, 0.29f, 0.25f), Mathf.Pow(y / 63f, 1.4f)));
            tex.Apply();
            var rt = UiKit.Rect("Fondo", canvas);
            UiKit.Fill(rt);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = tex; raw.raycastTarget = false;
        }
    }
}
