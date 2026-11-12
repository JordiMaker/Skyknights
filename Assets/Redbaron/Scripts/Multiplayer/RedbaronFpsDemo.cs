using Alteruna.Multiplayer.Unity;
using AlterunaFPS;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// The multiplayer FPS demo on the Redbaron map (scene Redbaron_Multijugador), built on the Alteruna Multiplayer FPS Template.
    /// The template's Multiplayer object connects to Alteruna's service and spawns an avatar per player in a room; this panel adds
    /// what the demo needs around it:
    /// - a status line (connected, in a room, or no Alteruna licence registered);
    /// - "Jugar solo": the template's offline mode, a local player with its Avatar switched off, at the first spawn point;
    /// - LAN: create a room, look for rooms on the LAN, or connect straight to an address (the SDK's LAN sample);
    /// - Escape opens the template's menu and frees the cursor.
    /// </summary>
    public class RedbaronFpsDemo : MonoBehaviour
    {
        public MultiplayerManager multiplayer;
        [Tooltip("The template's Player prefab (the Multiplayer object's avatar).")]
        public GameObject playerPrefab;
        [Tooltip("Parent of the spawn points (PuntosAparicion).")]
        public Transform spawnPoints;
        [Tooltip("The SDK's room menu, hidden while playing offline.")]
        public GameObject roomMenu;
        public bool showPanel = true;

        GameObject localPlayer;
        string address = "localhost";
        string status = "";
        GUIStyle box, label, small, button;

        void Start()
        {
            if (multiplayer == null) multiplayer = FindAnyObjectByType<MultiplayerManager>();
            if (spawnPoints == null) { var sp = GameObject.Find("PuntosAparicion"); if (sp != null) spawnPoints = sp.transform; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public bool Online => multiplayer != null && multiplayer.IsConnected;
        public bool InRoom => multiplayer != null && multiplayer.InRoom;
        public bool Offline => localPlayer != null;

        /// <summary>The template's offline mode: the player without networking, as in its Offline scene.</summary>
        public GameObject PlayOffline()
        {
            if (localPlayer != null) return localPlayer;
            if (playerPrefab == null) { status = "Falta el prefab del jugador"; return null; }
            Transform at = spawnPoints != null && spawnPoints.childCount > 0 ? spawnPoints.GetChild(Random.Range(0, spawnPoints.childCount)) : transform;
            // Built inactive, so the networking components never wake up.
            var holder = new GameObject("JugadorLocal");
            holder.SetActive(false);
            var go = Instantiate(playerPrefab, at.position, at.rotation, holder.transform);
            go.name = "Jugador_Offline";
            var avatar = go.GetComponent<Alteruna.Multiplayer.Unity.Avatar>();
            if (avatar != null) avatar.enabled = false;
            var pc = go.GetComponent<PlayerController>();
            if (pc != null) pc.Avatar = null;
            go.transform.SetParent(null, true);
            Destroy(holder);
            go.SetActive(true);
            localPlayer = go;
            if (roomMenu != null) roomMenu.SetActive(false);
            if (multiplayer != null) multiplayer.enabled = false; // no connection attempts behind the offline game
            status = "Modo offline: un solo jugador en el mapa";
            RedbaronGame.Log(status);
            return go;
        }

        public void CreateLanRoom()
        {
            if (multiplayer == null) return;
            try { multiplayer.CreateRoom(); status = "Creando sala…"; }
            catch (System.Exception e) { status = "No se pudo crear la sala: " + e.Message; }
        }

        public void JoinLan()
        {
            if (multiplayer == null) return;
            try { multiplayer.JoinLan(); status = "Buscando salas en la red local…"; }
            catch (System.Exception e) { status = "LAN: " + e.Message; }
        }

        public void Connect(string addr)
        {
            if (multiplayer == null) return;
            try { multiplayer.DirectConnect(addr); status = "Conectando a " + addr + "…"; }
            catch (System.Exception e) { status = "Conexión: " + e.Message; }
        }

        void Styles()
        {
            if (box != null) return;
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.12f, 0.11f, 0.09f, 0.88f)); bg.Apply();
            box = new GUIStyle(GUI.skin.box) { normal = { background = bg }, padding = new RectOffset(10, 10, 8, 8) };
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.93f, 0.9f, 0.82f) } };
            small = new GUIStyle(label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.78f, 0.75f, 0.66f) } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        }

        void OnGUI()
        {
            if (!showPanel) return;
            if (Offline && Cursor.lockState == CursorLockMode.Locked) return; // playing: the panel would only get in the way
            Styles();
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(12, 12, 330, 250), box);
            GUILayout.Label("REDBARON · DEMO MULTIJUGADOR (FPS)", label);
            string state = multiplayer == null ? "sin objeto Multiplayer"
                : Offline ? "offline"
                : InRoom ? "en sala «" + multiplayer.CurrentRoom + "»"
                : Online ? "conectado, sin sala"
                : multiplayer.IsConnecting ? "conectando…"
                : "sin conexión";
            GUILayout.Label("Estado: " + state, small);
            if (multiplayer != null && !Online && !Offline && Time.timeSinceLevelLoad > 6f)
                GUILayout.Label("No conecta con Alteruna. Si el proyecto no tiene licencia registrada, hazlo en Window → Alteruna → Register license (clave de portal.mp2.alteruna.com).", small);
            GUILayout.Space(4);
            GUI.enabled = !Offline;
            if (GUILayout.Button("Jugar solo (offline)", button)) PlayOffline();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Crear sala", button)) CreateLanRoom();
            if (GUILayout.Button("Buscar en LAN", button)) JoinLan();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            address = GUILayout.TextField(address, GUILayout.Width(170));
            if (GUILayout.Button("Conectar a IP", button)) Connect(address);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status, small);
            GUILayout.Label("WASD mover · ratón apuntar · clic disparar · R recargar · V 1ª/3ª persona · Esc menú", small);
            GUILayout.EndArea();
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
