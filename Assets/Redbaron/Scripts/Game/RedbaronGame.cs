using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Reglas de la partida: victoria al destruir el puesto de mando enemigo, reinicio, marcador,
    /// velocidad de simulación y órdenes globales del jugador.
    /// Teclas: 1/2/3 velocidad x1/x2/x4 · G todos los tuyos avanzan · H todos los tuyos se quedan.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RedbaronGame : MonoBehaviour
    {
        public static RedbaronGame Instance { get; private set; }

        public WaveSpawner spawner;
        public Team playerTeam = Team.Entente;
        public float restartDelay = 12f;
        public bool showPanel = true;
        [Tooltip("Keys for game speed x1, x2 and x4. Quick RTS uses 0-9 for control groups, so its scenes use F5/F6/F7.")]
        public KeyCode[] speedKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        [Tooltip("G sends all your troops forward and H makes them all hold. Off in scenes with their own RTS controls (AITestBench).")]
        public bool globalOrderKeys = true;
        static readonly float[] Speeds = { 1, 2, 4 };

        public bool GameOver => gameOverT > 0;
        float gameOverT;

        public readonly Dictionary<Team, int> wins = new Dictionary<Team, int> { { Team.Centrales, 0 }, { Team.Entente, 0 } };
        public readonly Dictionary<Team, int> airKills = new Dictionary<Team, int> { { Team.Centrales, 0 }, { Team.Entente, 0 } };
        public readonly Dictionary<Team, int> bombHits = new Dictionary<Team, int> { { Team.Centrales, 0 }, { Team.Entente, 0 } };
        public readonly Dictionary<Team, int> fallen = new Dictionary<Team, int> { { Team.Centrales, 0 }, { Team.Entente, 0 } };
        public readonly Dictionary<string, int> crashes = new Dictionary<string, int>();
        public string lastEvent = "Las oleadas salen de las bases";

        void Awake()
        {
            Instance = this;
            if (spawner == null) spawner = FindAnyObjectByType<WaveSpawner>();
            LaneFlags.Ensure(spawner != null ? spawner.playerTeam : playerTeam); // the player's lane orders, one flag per lane
        }

        public static void Log(string msg)
        {
            if (Instance != null) Instance.lastEvent = msg;
            Debug.Log("[Redbaron] " + msg);
        }

        public void OnSoldierDied(SoldierAI s) { fallen[s.team]++; }

        public void OnCommandPostDestroyed(CommandPost post)
        {
            if (GameOver) return;
            Team win = post.Health.team.Enemy();
            wins[win]++;
            Log($"Victoria de {win.Name()}: puesto de mando enemigo destruido");
            gameOverT = restartDelay;
        }

        public void OnPlaneCrash(string pilot, Team team, string what)
        {
            crashes[what] = crashes.TryGetValue(what, out int n) ? n + 1 : 1;
            Log($"{pilot} ({team.Name()}) se estrella contra {what}");
        }

        public void OnPlaneKill(string killer, Team killerTeam, string victim, Team victimTeam)
        {
            airKills[killerTeam]++;
            Log($"{killer} ({killerTeam.Name()}) derriba al {victim} ({victimTeam.Name()})");
        }

        public void OnBombHit(string pilot, Team team)
        {
            bombHits[team]++;
            Log($"{pilot} ({team.Name()}) bombardea el aeródromo enemigo");
        }

        void Update()
        {
            if (gameOverT > 0)
            {
                gameOverT -= Time.deltaTime;
                if (gameOverT <= 0) { gameOverT = 0; ResetBattle(); }
            }
            for (int i = 0; i < speedKeys.Length && i < Speeds.Length; i++)
                if (Input.GetKeyDown(speedKeys[i])) Time.timeScale = Speeds[i];
            if (globalOrderKeys && Input.GetKeyDown(KeyCode.G))
            {
                foreach (var s in SoldierAI.All) if (s.team == playerTeam && s.Alive) s.OrderAdvance();
                if (spawner != null) spawner.playerAutoAdvance = true;
                Log("Tus tropas avanzan por sus líneas");
            }
            if (globalOrderKeys && Input.GetKeyDown(KeyCode.H))
            {
                foreach (var s in SoldierAI.All) if (s.team == playerTeam && s.Alive) s.OrderHold();
                if (spawner != null) spawner.playerAutoAdvance = false;
                Log("Tus tropas se quedan y se cubren");
            }
        }

        public void ResetBattle()
        {
            foreach (var s in new List<SoldierAI>(SoldierAI.All)) Destroy(s.gameObject);
            Squad.ResetAll();
            foreach (var c in CoverPoint.All) c.occupants = 0;
            foreach (var b in Bunker.All) b.ResetBunker();
            foreach (var f in FlakTower.All) f.ResetTower();
            foreach (var h in Hangar.All) h.ResetHangar();
            Breakable.RestoreAll();
            foreach (var p in CommandPost.All) p.ResetPost();
            if (spawner != null) spawner.ResetWaves();
            if (TroopBar.Instance != null) TroopBar.Instance.ResetAll();
            if (LaneFlags.Instance != null) LaneFlags.Instance.ResetAll();
            Log("Nueva batalla: las oleadas salen de las bases");
        }

        // ---------------------------------------------------------------- Panel
        GUIStyle box, label, small;
        Texture2D white;

        void OnGUI()
        {
            if (!showPanel) return;
            // Keep the panel readable on large and high-DPI screens (designed for 720 px high).
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (box == null)
            {
                white = Texture2D.whiteTexture;
                box = new GUIStyle(GUI.skin.box) { normal = { background = MakeTex(new Color(0.12f, 0.11f, 0.09f, 0.86f)) } };
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.93f, 0.9f, 0.82f) } };
                small = new GUIStyle(label) { fontSize = 11, normal = { textColor = new Color(0.71f, 0.68f, 0.59f) } };
            }
            GUILayout.BeginArea(new Rect(12, 12, 320, 250), box);
            GUILayout.Label("REDBARON · " + MissionSession.Title.ToUpperInvariant(), small);
            Bar("Mando Centrales", CommandPost.Of(Team.Centrales), Team.Centrales.FlagColor());
            Bar("Mando Entente", CommandPost.Of(Team.Entente), Team.Entente.FlagColor());
            int nc = 0, ne = 0;
            foreach (var s in SoldierAI.All) if (s.Alive) { if (s.team == Team.Centrales) nc++; else ne++; }
            GUILayout.Label($"Tropas · Centrales {nc} · Entente {ne} · Victorias {wins[Team.Centrales]}–{wins[Team.Entente]}", label);
            GUILayout.Label($"Derribos aéreos · Centrales {airKills[Team.Centrales]} · Entente {airKills[Team.Entente]}", label);
            GUILayout.Label($"Bombas en aeródromo · Centrales {bombHits[Team.Centrales]} · Entente {bombHits[Team.Entente]}", label);
            GUILayout.Label(lastEvent, label);
            if (MobileFlightControls.Active) GUILayout.Label("Joystick: ↔ alabear · ↕ velocidad · FUEGO · BOMBA · CÁM cámaras", small);
            else GUILayout.Label($"Velocidad x{Time.timeScale:0} ({SpeedKeysText()}) · G avanzar · H quedarse · {(RedbaronCameras.Instance != null ? RedbaronCameras.Instance.KeysText : "")} cámaras", small);
            GUILayout.EndArea();
        }

        string SpeedKeysText()
        {
            var names = new string[speedKeys.Length];
            for (int i = 0; i < speedKeys.Length; i++) names[i] = speedKeys[i].ToString().Replace("Alpha", "");
            return string.Join("/", names);
        }

        void Bar(string name, CommandPost p, Color c)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, small, GUILayout.Width(110));
            Rect r = GUILayoutUtility.GetRect(180, 10);
            r.y += 5; r.height = 6;
            GUI.color = new Color(1, 1, 1, 0.15f); GUI.DrawTexture(r, white);
            float f = p != null ? p.Health.hp / p.Health.maxHp : 0;
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * f, r.height), white);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }
    }
}
