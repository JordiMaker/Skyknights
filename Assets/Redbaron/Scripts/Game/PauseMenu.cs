using UnityEngine;
using UnityEngine.UI;

namespace Redbaron
{
    /// <summary>
    /// Pause in the battle: a small button at the top of the screen (and Escape, which is also the phone's back
    /// button) stops the game and opens a panel with Continue and Main menu. While paused, time stands still,
    /// sounds stop, and neither the keys nor the on-screen controls fly the plane.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; internal set; }

        RectTransform panel;
        GameObject pauseButton;
        float savedScale = 1f;

        void Start()
        {
            IsPaused = false;
            var canvas = UiKit.Canvas("Pausa", 150, transform);
            var safe = UiKit.SafeArea(canvas.transform);

            var btn = UiKit.Button(safe, "II", 34, Pause, new Color(0.12f, 0.12f, 0.10f, 0.55f), "Boton_Pausa");
            UiKit.At((RectTransform)btn.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(96, 80));
            pauseButton = btn.gameObject;

            panel = UiKit.Fill(UiKit.Rect("Panel_Pausa", safe));
            var shade = panel.gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, 0.55f); // blocks touches to the game
            var box = UiKit.Panel(panel, UiKit.Ink, "Caja").rectTransform;
            UiKit.At(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 560));
            var title = UiKit.Label(box, "PAUSA", 64, UiKit.Brass, TextAnchor.UpperCenter, true);
            UiKit.At(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(640, 80));
            var mission = UiKit.Label(box, MissionSession.Title, 30, UiKit.Muted, TextAnchor.UpperCenter);
            UiKit.At(mission.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(640, 44));

            var cont = UiKit.Button(box, "CONTINUAR", 40, Resume, new Color(0.50f, 0.38f, 0.14f, 0.97f));
            UiKit.At((RectTransform)cont.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(520, 104));
            var menu = UiKit.Button(box, "MENÚ PRINCIPAL", 36, MissionSession.ReturnToMenu);
            UiKit.At((RectTransform)menu.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -338), new Vector2(520, 104));
            menu.gameObject.SetActive(MissionSession.HasMenu);

            var version = UiKit.Label(box, "v" + Application.version, 24, UiKit.Muted, TextAnchor.LowerCenter);
            UiKit.At(version.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(640, 36));
            panel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { if (IsPaused) Resume(); else Pause(); }
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            savedScale = Time.timeScale > 0 ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            panel.gameObject.SetActive(true);
            pauseButton.SetActive(false);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = savedScale;
            AudioListener.pause = false;
            panel.gameObject.SetActive(false);
            pauseButton.SetActive(true);
        }

        void OnDestroy()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
