using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// The player's troop buttons: one per unit type, each with its own cooldown (UnitType.recruitEvery). A press puts
    /// a squad of infantry or one vehicle at the player's base, in the next lane, heading for the enemy command post.
    /// It draws its own bar (showBar) in the battle, with the mouse or a finger; the RTS test screen (AITestBench) draws
    /// its own buttons and only uses the logic here.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class TroopBar : MonoBehaviour
    {
        public static TroopBar Instance { get; private set; }

        public WaveSpawner spawner;
        [Tooltip("Draw the bar on screen. Off in scenes with their own buttons.")]
        public bool showBar = true;
        [Tooltip("Multiplies every cooldown. 0.5 = twice as fast.")]
        public float cooldownScale = 1f;
        [Tooltip("Left edge of the bar in GUI units (720 px high screen), to the right of the game panel.")]
        public float left = 345f;
        [Tooltip("Width kept free on the right for the camera button of the phone controls.")]
        public float rightMargin = 170f;

        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        int lane;
        /// <summary>The lane the last recruits went down.</summary>
        public int LastLane { get; private set; }
        /// <summary>Recruits so far this battle, for the tests.</summary>
        public int Recruited { get; private set; }

        void Awake()
        {
            Instance = this;
            if (spawner == null) spawner = FindAnyObjectByType<WaveSpawner>();
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public float Remaining(UnitType t) => t != null && readyAt.TryGetValue(t.name, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;
        public bool Ready(UnitType t) => Remaining(t) <= 0f;
        /// <summary>How much of the cooldown has passed, 0–1 (1 = ready).</summary>
        public float Fraction(UnitType t)
        {
            float every = Mathf.Max(0.01f, (t != null ? t.recruitEvery : 1f) * cooldownScale);
            return 1f - Mathf.Clamp01(Remaining(t) / every);
        }

        /// <summary>The player's recruits of type t at the base: null if the type is cooling down or the battle is over.</summary>
        public List<SoldierAI> Recruit(UnitType t)
        {
            if (t == null || spawner == null || !Ready(t)) return null;
            var g = RedbaronGame.Instance;
            if (g != null && g.GameOver) return null;
            Team team = spawner.playerTeam;
            LastLane = lane; lane = (lane + 1) % 3;
            var path = RedbaronMap.LanePath(team, LastLane);
            int n = Mathf.Max(1, t.IsVehicle ? 1 : t.recruitCount);
            Squad squad = t.IsVehicle ? null : new Squad(team, LastLane);
            var units = new List<SoldierAI>();
            for (int i = 0; i < n; i++)
            {
                Vector3 p = path[0] + new Vector3((i - (n - 1) * 0.5f) * 2.2f, 0, 0);
                var u = spawner.SpawnAt(team, t, p, Quaternion.LookRotation(path[1] - path[0]), LastLane, true, true);
                squad?.Add(u);
                units.Add(u);
            }
            readyAt[t.name] = Time.time + t.recruitEvery * cooldownScale;
            Recruited += n;
            return units;
        }

        public void ResetAll() { readyAt.Clear(); lane = 0; Recruited = 0; }

        // ------------------------------------------------------------ Bar
        GUIStyle box, button, small, title;
        Texture2D white;

        void OnGUI()
        {
            if (!showBar || spawner == null || spawner.unitTypes.Count == 0) return;
            var g = RedbaronGame.Instance;
            if (g != null && !g.showPanel) return; // the game's own screens (menus) hide the HUD
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float W = Screen.width / scale;
            if (box == null)
            {
                white = Texture2D.whiteTexture;
                var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.12f, 0.11f, 0.09f, 0.82f)); bg.Apply();
                box = new GUIStyle(GUI.skin.box) { normal = { background = bg }, padding = new RectOffset(6, 6, 4, 4) };
                button = new GUIStyle(GUI.skin.button) { fontSize = 11, wordWrap = true, padding = new RectOffset(2, 2, 2, 2) };
                small = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.78f, 0.75f, 0.66f) } };
                title = new GUIStyle(small) { fontStyle = FontStyle.Bold };
            }

            var types = new List<UnitType>();
            foreach (var t in spawner.unitTypes) if (t != null && !string.IsNullOrEmpty(t.name)) types.Add(t);
            int perRow = Mathf.Max(1, Mathf.CeilToInt(types.Count / 2f));
            float width = Mathf.Clamp((W - left - rightMargin - 12) / perRow, 60f, 110f);
            float h = 34f, rows = Mathf.Ceil(types.Count / (float)perRow);
            GUILayout.BeginArea(new Rect(left, 8, perRow * width + 12, rows * (h + 4) + 24), box);
            GUILayout.Label("Tropas · toca para enviarlas hacia el cuartel enemigo", title);
            for (int i = 0; i < types.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, types.Count); j++) Button(types[j], width - 4, h);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
            GUI.matrix = Matrix4x4.identity;
        }

        void Button(UnitType t, float width, float h)
        {
            float rem = Remaining(t);
            GUI.enabled = rem <= 0f;
            string label = rem > 0f ? $"{t.name}\n{rem:0} s" : t.IsVehicle ? t.name : $"{t.name} ×{Mathf.Max(1, t.recruitCount)}";
            if (GUILayout.Button(label, button, GUILayout.Width(width), GUILayout.Height(h))) Recruit(t);
            GUI.enabled = true;
            if (rem <= 0f) return;
            // The cooldown fills the bottom edge of the button.
            Rect r = GUILayoutUtility.GetLastRect();
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            GUI.DrawTexture(new Rect(r.x + 2, r.yMax - 4, (r.width - 4) * Fraction(t), 3), white);
            GUI.color = Color.white;
        }
    }
}
