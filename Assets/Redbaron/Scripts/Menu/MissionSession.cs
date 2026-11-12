using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redbaron
{
    /// <summary>
    /// Carries the chosen mission from the menu to the battle, and goes back to the menu.
    /// A battle scene opened directly (without the menu) runs with no mission chosen.
    /// </summary>
    public static class MissionSession
    {
        public const string MenuScene = "Redbaron_Menu";
        const string DefaultTitle = "Frente en diez franjas";

        /// <summary>The mission being played, or null when the battle scene was opened directly.</summary>
        public static MissionDefinition Current { get; private set; }

        public static string Title => Current != null && !string.IsNullOrEmpty(Current.title) ? Current.title : DefaultTitle;

        /// <summary>Whether the menu scene is in the build, so there is somewhere to go back to.</summary>
        public static bool HasMenu => Application.CanStreamedLevelBeLoaded(MenuScene);

        public static AsyncOperation Begin(MissionDefinition mission)
        {
            Current = mission;
            ResetGlobals();
            return SceneManager.LoadSceneAsync(mission != null ? mission.sceneName : "Redbaron_QuickRTS");
        }

        public static void ReturnToMenu()
        {
            ResetGlobals();
            SceneManager.LoadScene(MenuScene);
        }

        /// <summary>Game speed and paused sound would otherwise carry over from one scene to the next.</summary>
        static void ResetGlobals()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            PauseMenu.IsPaused = false;
        }
    }
}
