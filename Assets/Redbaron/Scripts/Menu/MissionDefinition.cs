using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// One mission of the campaign, as a data asset (Assets > Create > Redbaron > Mission).
    /// For now every mission loads the same battle scene with the same rules; fields for what will make them
    /// different (map, sides, waves, objectives) are added here when a mission needs them.
    /// </summary>
    [CreateAssetMenu(menuName = "Redbaron/Mission", fileName = "Mision")]
    public class MissionDefinition : ScriptableObject
    {
        [Tooltip("Shown in the mission list and at the top of the battle panel.")]
        public string title = "Nueva misión";
        [Tooltip("Place and date, shown under the title.")]
        public string setting = "";
        [TextArea(3, 8)] public string briefing = "";
        public string objective = "Destruye el puesto de mando enemigo.";
        [Tooltip("Name of the battle shown on the card (several missions can share one).")]
        public string scenario = "Frente en diez franjas";
        [Tooltip("Scene to load. It must be in the build's scene list.")]
        public string sceneName = "Redbaron_QuickRTS";
    }
}
