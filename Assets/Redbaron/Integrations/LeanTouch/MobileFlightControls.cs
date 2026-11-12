using Lean.Touch;
using UnityEngine;
using UnityEngine.UI;

namespace Redbaron
{
    /// <summary>
    /// Mobile flight controls: a virtual joystick on the left and round buttons on the right, in the style of
    /// Air Battle 3D's touch UI, with fingers read through Lean Touch (several fingers at once).
    /// Joystick: left/right rolls, up/down speeds up or slows down (proportional to how far you push).
    /// FUEGO (hold), BOMBA (tap), CÁM (tap, top right) cycles the cameras.
    /// The plane reads the static values below together with the keyboard, so both work at once.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MobileFlightControls : MonoBehaviour
    {
        public enum Show { Auto, Always, Never }
        [Tooltip("Auto = only on phones and tablets. Always = also on desktop (the mouse acts as a finger), handy for testing.")]
        public Show show = Show.Auto;

        [Header("Look (Air Battle 3D sprites; circles are drawn if left empty)")]
        public Sprite joystickBase;
        public Sprite joystickKnob;
        public Sprite buttonSprite;
        public Sprite smallButtonSprite;
        [Range(0.6f, 1.6f)] public float uiScale = 1f;

        [Header("Joystick")]
        [Tooltip("Part of the stick's travel that does nothing, so a resting thumb does not roll the plane.")]
        [Range(0f, 0.5f)] public float deadZone = 0.08f;
        [Tooltip("Response curve. 1 = linear; 2 = gentle near the centre for fine corrections, still full at the edge.")]
        [Range(1f, 3f)] public float response = 2f;
        [Tooltip("Roll at full deflection, compared with the arrow keys (1 = the same).")]
        [Range(0.2f, 1f)] public float rollSensitivity = 1f;
        [Tooltip("Share of the screen width (from the left) where a touch grabs the joystick.")]
        [Range(0.2f, 0.6f)] public float joystickZone = 0.45f;

        /// <summary>-1 = roll left, 1 = roll right.</summary>
        public static float Roll { get; private set; }
        /// <summary>-1 = slow down, 1 = speed up.</summary>
        public static float Throttle { get; private set; }
        public static bool Fire { get; private set; }
        public static bool BombPressed { get; private set; }
        public static bool CameraPressed { get; private set; }
        public static bool Active { get; private set; }
        /// <summary>Optional external joystick (e.g. Ultimate Joystick Lite) giving -1..1 on both axes.
        /// When set, the built-in joystick is hidden and this one drives roll and speed.</summary>
        public static System.Func<Vector2> ExternalJoystick;
        /// <summary>The safe-area panel the controls live in, for adding other on-screen controls.</summary>
        public RectTransform SafeArea => safe;

        Canvas canvas;
        RectTransform safe, joyBase, joyKnob, fireBtn, bombBtn, camBtn;
        Vector2 joyRest;
        int joyFinger = int.MinValue;
        Vector2 joyCenter;
        Rect lastSafe;

        void Awake()
        {
            if (LeanTouch.Instances.Count == 0) gameObject.AddComponent<LeanTouch>();
            BuildUI();
        }

        // ------------------------------------------------------------ UI
        void BuildUI()
        {
            var go = new GameObject("Controles_Movil");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            safe = new GameObject("ZonaSegura", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(go.transform, false);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;

            float s = uiScale;
            joyRest = new Vector2(270, 250) * s;
            joyBase = MakeImage("Joystick_Base", joystickBase, new Color(1, 1, 1, 0.25f), Vector2.zero, joyRest, 340 * s);
            joyKnob = MakeImage("Joystick_Mando", joystickKnob, new Color(1, 1, 1, 0.8f), Vector2.zero, joyRest, 140 * s);
            fireBtn = MakeImage("Boton_Fuego", buttonSprite, new Color(1, 1, 1, 0.62f), new Vector2(1, 0), new Vector2(-210, 220) * s, 250 * s);
            bombBtn = MakeImage("Boton_Bomba", buttonSprite, new Color(1f, 0.82f, 0.4f, 0.62f), new Vector2(1, 0), new Vector2(-470, 150) * s, 175 * s);
            camBtn = MakeImage("Boton_Camara", smallButtonSprite, new Color(1, 1, 1, 0.5f), new Vector2(1, 1), new Vector2(-110, -105) * s, 130 * s);
            MakeLabel(fireBtn, "FUEGO", 40 * s);
            MakeLabel(bombBtn, "BOMBA", 32 * s);
            MakeLabel(camBtn, "CÁM", 28 * s);
        }

        RectTransform MakeImage(string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, float size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(safe, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : CircleSprite;
            img.color = color; img.raycastTarget = false; img.preserveAspect = true;
            return rt;
        }

        static void MakeLabel(RectTransform parent, string text, float size)
        {
            var rt = new GameObject("Texto", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = Mathf.RoundToInt(size); t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = new Color(1f, 0.98f, 0.92f); t.raycastTarget = false;
            var o = rt.gameObject.AddComponent<UnityEngine.UI.Outline>(); // not Quick Outline's Outline
            o.effectColor = new Color(0, 0, 0, 0.7f); o.effectDistance = new Vector2(2, -2);
        }

        static Sprite circle;
        /// <summary>A soft white disc, used when no sprite is assigned.</summary>
        static Sprite CircleSprite
        {
            get
            {
                if (circle != null) return circle;
                const int N = 128;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 24f)));
                    }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
                return circle;
            }
        }

        void ApplySafeArea()
        {
            Rect a = Screen.safeArea;
            if (a == lastSafe || Screen.width == 0 || Screen.height == 0) return;
            lastSafe = a;
            safe.anchorMin = new Vector2(a.xMin / Screen.width, a.yMin / Screen.height);
            safe.anchorMax = new Vector2(a.xMax / Screen.width, a.yMax / Screen.height);
        }

        // ------------------------------------------------------------ Input
        void Update()
        {
            Roll = Throttle = 0; Fire = BombPressed = CameraPressed = false;
            Active = show == Show.Always || (show == Show.Auto && Application.isMobilePlatform);
            canvas.enabled = Active && !PauseMenu.IsPaused;
            if (!Active || PauseMenu.IsPaused) { joyFinger = int.MinValue; return; }
            ApplySafeArea();

            var fingers = LeanTouch.GetFingers(false, false);
            float radius = joyBase.rect.width * 0.5f * canvas.scaleFactor;

            bool external = ExternalJoystick != null;
            joyBase.gameObject.SetActive(!external); joyKnob.gameObject.SetActive(!external);
            LeanFinger jf = null;
            if (external)
            {
                Vector2 e = ExternalJoystick();
                Roll = Shape(Mathf.Clamp(e.x, -1, 1)) * rollSensitivity; Throttle = Shape(Mathf.Clamp(e.y, -1, 1));
                joyFinger = int.MinValue;
            }
            // Joystick: the first finger that lands in the left zone owns it until it lifts; the base jumps under it.
            else
            {
            foreach (var f in fingers) if (f.Index == joyFinger) jf = f;
            if (jf == null)
            {
                joyFinger = int.MinValue;
                foreach (var f in fingers)
                    if (f.Down && f.ScreenPosition.x < Screen.width * joystickZone && f.ScreenPosition.y < Screen.height * 0.75f && !Over(camBtn, f.ScreenPosition))
                    { joyFinger = f.Index; joyCenter = f.ScreenPosition; jf = f; break; }
            }
            if (jf != null)
            {
                Vector2 v = Vector2.ClampMagnitude((jf.ScreenPosition - joyCenter) / Mathf.Max(1f, radius * 0.75f), 1f);
                joyBase.position = joyCenter;
                joyKnob.position = joyCenter + v * radius * 0.75f;
                Roll = Shape(v.x) * rollSensitivity; Throttle = Shape(v.y);
            }
            else
            {
                joyBase.anchoredPosition = joyRest; joyKnob.anchoredPosition = joyRest;
            }
            }

            // Buttons: any other finger inside the circle.
            foreach (var f in fingers)
            {
                if (f.Index == joyFinger) continue;
                if (Over(fireBtn, f.ScreenPosition)) Fire = true;
                if (f.Down && Over(bombBtn, f.ScreenPosition)) BombPressed = true;
                if (f.Down && Over(camBtn, f.ScreenPosition)) CameraPressed = true;
            }
            Pressed(fireBtn, Fire);
            Pressed(bombBtn, BombPressed);
            Pressed(camBtn, CameraPressed);
            Pressed(joyKnob, jf != null);
        }

        /// <summary>Dead zone, then the response curve: small deflections give fine control, full deflection the maximum.</summary>
        float Shape(float x)
        {
            float d = Dead(x);
            return Mathf.Sign(d) * Mathf.Pow(Mathf.Abs(d), response);
        }

        float Dead(float x)
        {
            float a = Mathf.Abs(x);
            return a < deadZone ? 0f : Mathf.Sign(x) * (a - deadZone) / (1f - deadZone);
        }

        bool Over(RectTransform rt, Vector2 screenPos)
        {
            float r = rt.rect.width * 0.5f * canvas.scaleFactor * 1.1f; // a little generous for thumbs
            return ((Vector2)rt.position - screenPos).magnitude <= r;
        }

        static void Pressed(RectTransform rt, bool on)
        {
            float target = on ? 0.9f : 1f;
            rt.localScale = Vector3.one * Mathf.MoveTowards(rt.localScale.x, target, Time.unscaledDeltaTime * 3f);
        }
    }
}
