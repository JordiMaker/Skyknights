using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Redbaron
{
    /// <summary>
    /// Small helpers that build the game's uGUI screens from code, in one look: dark olive panels with rounded
    /// corners, parchment text and brass highlights. Used by the main menu and the pause menu.
    /// Layout is in a 1920 × 1080 reference that scales with the screen height (wider phones get more width).
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.10f, 0.11f, 0.09f, 0.92f);
        public static readonly Color InkLight = new Color(0.17f, 0.18f, 0.14f, 0.95f);
        public static readonly Color Paper = new Color(0.94f, 0.90f, 0.80f);
        public static readonly Color Muted = new Color(0.72f, 0.69f, 0.60f);
        public static readonly Color Brass = new Color(0.87f, 0.70f, 0.33f);
        public static readonly Color ButtonColor = new Color(0.23f, 0.24f, 0.18f, 0.96f);
        public static readonly Color Centrales = new Color32(0xA3, 0x27, 0x1F, 255);
        public static readonly Color Entente = new Color32(0x2F, 0x4F, 0x8A, 255);

        public static Color SideColor(Team t) => t == Team.Centrales ? Centrales : Entente;
        public static string SideName(Team t) => t == Team.Centrales ? "Potencias Centrales" : "Entente";

        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        // ------------------------------------------------------------ Canvas
        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        /// <summary>A full-screen panel kept inside the phone's safe area (away from notches and rounded corners).</summary>
        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = Rect("ZonaSegura", canvas);
            Fill(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        // ------------------------------------------------------------ Layout
        public static RectTransform Rect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Fill(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors the rect between two relative points of its parent (0..1), with optional pixel margins.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin = default, Vector2 offMax = default)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        /// <summary>A fixed-size rect at an anchor point of its parent.</summary>
        public static RectTransform At(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        // ------------------------------------------------------------ Widgets
        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft, bool bold = false, string name = "Texto")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string text, int size, UnityAction onClick, Color? color = null, string name = "Boton")
        {
            var img = Panel(parent, color ?? ButtonColor, name);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.2f, 1.05f); cb.pressedColor = new Color(0.75f, 0.72f, 0.62f);
            cb.selectedColor = Color.white; cb.colorMultiplier = 1.2f; cb.fadeDuration = 0.08f;
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);
            var t = Label(img.transform, text, size, Paper, TextAnchor.MiddleCenter, true);
            Fill(t.rectTransform, 16, 0, 16, 0);
            return b;
        }

        /// <summary>A thin coloured bar, for the side colour of a list entry or a title underline.</summary>
        public static Image Bar(Transform parent, Color color, string name = "Barra")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return img;
        }

        /// <summary>A vertical scrolling list. Add rows to the returned content; each row sets its own height.</summary>
        public static RectTransform ScrollList(Transform parent, float spacing = 10)
        {
            var view = Rect("Lista", parent);
            var mask = view.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            var hit = view.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); // catches drags between rows
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 40; sr.inertia = true;
            var content = Rect("Contenido", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var lg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            lg.spacing = spacing; lg.childControlWidth = true; lg.childForceExpandWidth = true;
            lg.childControlHeight = true; lg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.viewport = view;
            return content;
        }

        /// <summary>Gives a rect a fixed height inside a layout group.</summary>
        public static void Height(Component c, float h)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>(); // not ??: in the Editor a missing component is a fake null
            le.preferredHeight = h; le.minHeight = h;
        }

        // ------------------------------------------------------------ Sprites
        static Sprite rounded;
        /// <summary>A white rounded rectangle with 9-slice borders, drawn once at start-up.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int N = 64, R = 18;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, R, N - R), cy = Mathf.Clamp(y + 0.5f, R, N - R);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(R - d + 0.5f)));
                    }
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R + 2, R + 2, R + 2, R + 2));
                return rounded;
            }
        }
    }

    /// <summary>Keeps a full-screen rect inside Screen.safeArea, updated when the phone rotates.</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect last;
        RectTransform rt;

        void Awake() { rt = (RectTransform)transform; Apply(); }
        void Update() { if (Screen.safeArea != last) Apply(); }

        void Apply()
        {
            last = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(last.xMin / Screen.width, last.yMin / Screen.height);
            rt.anchorMax = new Vector2(last.xMax / Screen.width, last.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
