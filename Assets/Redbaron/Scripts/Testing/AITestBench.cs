using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Redbaron
{
    /// <summary>
    /// Test scene for the unit AI (Redbaron_PruebaIA.unity): one hunter crosses the battle map looking for enemies of every kind,
    /// while the camera follows it. Its NavMesh path, its target and the cover it runs to are drawn, labels over every unit say
    /// what each one is doing, and a panel shows what the hunter is thinking, with a log of its decisions.
    /// There are no waves and no planes. Buttons pick the hunter's type and side, reset the enemies and set the game speed.
    /// </summary>
    public class AITestBench : MonoBehaviour
    {
        public enum Mode { Hold, Patrol, Hunt }

        [System.Serializable]
        public class Enemy
        {
            public string type = "Fusilero";
            public int count = 1;
            [Tooltip("Design coordinates: x 0–100 across the map, z 0–150 measured from this side's own end of the map.")]
            public Vector2 at = new Vector2(50, 137);
            [Tooltip("Hold = dug in around its spot; Patrol = walks between at and to; Hunt = comes after the hunter.")]
            public Mode mode = Mode.Hold;
            public Vector2 to = new Vector2(50, 137);
            public Enemy() { }
            public Enemy(string t, int n, Vector2 a, Mode m, Vector2 b) { type = t; count = n; at = a; mode = m; to = b; }
        }

        [Header("Hunter")]
        public string hunterType = "Fusilero";
        public Team hunterTeam = Team.Entente;
        [Tooltip("Where the hunter starts: design x 0–100, and z measured from its own side's end of the map.")]
        public Vector2 hunterStart = new Vector2(50, 38);
        public float respawnDelay = 4f;

        [Header("Enemies (the other side)")]
        public List<Enemy> enemies = new List<Enemy>
        {
            new Enemy("Fusilero", 3, new Vector2(42, 137), Mode.Hold, Vector2.zero),
            new Enemy("Ametrallador", 1, new Vector2(46, 137), Mode.Hold, Vector2.zero),
            new Enemy("Tanque ligero", 1, new Vector2(72, 118), Mode.Hold, Vector2.zero),
            new Enemy("Mech explorador", 1, new Vector2(20, 112), Mode.Patrol, new Vector2(30, 150)),
            new Enemy("Granadero", 2, new Vector2(85, 100), Mode.Hunt, Vector2.zero),
        };

        [Header("View")]
        public float followDistance = 16f, followHeight = 10f;
        public bool labels = true, lines = true;

        WaveSpawner spawner;
        SoldierAI hunter, followed;
        readonly List<SoldierAI> foes = new List<SoldierAI>();
        readonly Dictionary<SoldierAI, Enemy> plans = new Dictionary<SoldierAI, Enemy>();
        readonly Dictionary<SoldierAI, bool> patrolBack = new Dictionary<SoldierAI, bool>();
        readonly List<string> log = new List<string>();
        Camera cam;
        LineRenderer pathLine, aimLine;
        Transform spotMarker;
        float respawnT = -1, thinkT, yaw, zoom = 1f;
        SoldierAI.State lastState;
        Health lastTarget;
        int kills;
        bool ready;

        static readonly string[] HunterTypes =
        {
            "Fusilero", "Ametrallador", "Granadero", "Lanzallamas", "Zapador",
            "Tanque ligero", "Tanqueta lanzallamas", "Tanque pesado", "Mech ligero", "Mech explorador", "Mech de asalto",
        };

        // ------------------------------------------------------------ Setup
        IEnumerator Start()
        {
            spawner = FindAnyObjectByType<WaveSpawner>();
            if (spawner != null) spawner.enabled = false; // no waves in the test
            var air = FindAnyObjectByType<AirWar>();
            if (air != null) air.enabled = false;
            var game = RedbaronGame.Instance;
            if (game != null) game.showPanel = false;
            var cams = FindAnyObjectByType<RedbaronCameras>();
            if (cams != null)
            {
                if (cams.rtsCameraScripts != null) foreach (var b in cams.rtsCameraScripts) if (b != null) b.enabled = false;
                cam = cams.cam;
                cams.enabled = false;
            }
            if (cam == null) cam = Camera.main;
            if (cam != null) { cam.orthographic = false; cam.fieldOfView = 55f; cam.nearClipPlane = 0.3f; } // the Quick RTS camera is orthographic
            var mobile = GameObject.Find("Controles_Movil"); // the phone's flight controls have nothing to do here
            if (mobile != null) mobile.SetActive(false);
            MakeMarkers();
            yield return null; // the map and its NavMesh are built in Awake
            yield return null;
            if (spawner == null || spawner.unitTypes.Count == 0) { Note("Falta el WaveSpawner con los tipos de unidad"); yield break; }
            SpawnEnemies();
            SpawnHunter();
            ready = true;
        }

        void MakeMarkers()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            pathLine = Line("Camino", mat, new Color(1f, 0.85f, 0.2f, 0.9f), 0.18f);
            aimLine = Line("Objetivo", mat, new Color(1f, 0.25f, 0.2f, 0.9f), 0.08f);
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(s.GetComponent<Collider>());
            s.name = "Cobertura";
            s.transform.localScale = new Vector3(1.4f, 0.05f, 1.4f);
            s.GetComponent<Renderer>().sharedMaterial = new Material(mat) { color = new Color(0.3f, 1f, 0.4f, 0.8f) };
            spotMarker = s.transform;
        }

        LineRenderer Line(string name, Material mat, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat; lr.startColor = lr.endColor = c;
            lr.widthMultiplier = width; lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        Team EnemyTeam => hunterTeam.Enemy();
        /// <summary>Design point measured from side's own end (the Central Powers hold the north end, z = 0).</summary>
        Vector3 World(Vector2 design, Team side) => RedbaronMap.ToWorld(design.x, side == Team.Centrales ? design.y : RedbaronMap.MirrorZ(design.y));

        void SpawnHunter()
        {
            var type = spawner.TypeByName(hunterType);
            if (type == null) { Note("No hay tipo «" + hunterType + "»"); return; }
            Vector3 p = World(hunterStart, hunterTeam);
            var post = CommandPost.Of(EnemyTeam);
            Vector3 face = post != null ? post.transform.position - p : Vector3.forward; face.y = 0;
            hunter = spawner.SpawnAt(hunterTeam, type, p, Quaternion.LookRotation(face), 1, false, true);
            hunter.name = "Cazador_" + type.name;
            followed = hunter;
            lastState = hunter.CurrentState; lastTarget = null;
            Note($"Sale el cazador: {type.name} ({hunterTeam.Name()})");
        }

        void SpawnEnemies()
        {
            foreach (var f in foes) if (f != null) Destroy(f.gameObject);
            foes.Clear(); plans.Clear(); patrolBack.Clear();
            Team side = EnemyTeam;
            foreach (var e in enemies)
            {
                var type = spawner.TypeByName(e.type);
                if (type == null) continue;
                Squad squad = type.IsVehicle || e.count < 2 ? null : new Squad(side, 1);
                for (int i = 0; i < Mathf.Max(1, e.count); i++)
                {
                    Vector3 p = World(e.at, side) + new Vector3((i - (e.count - 1) * 0.5f) * 2.2f, 0, 0);
                    var post = CommandPost.Of(side.Enemy());
                    Vector3 face = post != null ? post.transform.position - p : Vector3.back; face.y = 0;
                    bool hold = e.mode == Mode.Hold;
                    var u = spawner.SpawnAt(side, type, p, Quaternion.LookRotation(face), 1, hold, !hold);
                    if (hold) u.OrderHold();
                    squad?.Add(u);
                    foes.Add(u); plans[u] = e; patrolBack[u] = false;
                }
            }
            Note($"Enemigos en posición: {foes.Count}");
        }

        // ------------------------------------------------------------ Loop
        void Update()
        {
            if (spawner == null || !ready) return;
            foes.RemoveAll(f => f == null);

            if (hunter == null || !hunter.Alive)
            {
                if (respawnT < 0) { respawnT = respawnDelay; if (hunter != null) Note("El cazador ha caído"); }
                respawnT -= Time.deltaTime;
                if (respawnT <= 0) { respawnT = -1; SpawnHunter(); }
            }
            int alive = 0; foreach (var f in foes) if (f.Alive) alive++;
            if (alive == 0) { Note("Todos los enemigos han caído: vuelven a su posición"); SpawnEnemies(); }

            thinkT -= Time.deltaTime;
            if (thinkT <= 0) { thinkT = 1f; Direct(); }
            Watch();
            if (Input.GetKeyDown(KeyCode.Tab)) FollowNext();
        }

        /// <summary>Once a second: a hunter with nothing in sight heads for the nearest enemy anywhere on the map; patrols turn
        /// at their ends; hunters of the other side go after the hunter.</summary>
        void Direct()
        {
            if (hunter != null && hunter.Alive && hunter.Target == null)
            {
                Vector3? goal = Nearest(hunter.transform.position, EnemyTeam);
                if (goal.HasValue) hunter.Hunt(goal.Value);
            }
            foreach (var f in foes)
            {
                if (!f.Alive || !plans.TryGetValue(f, out var e)) continue;
                if (e.mode == Mode.Hunt && hunter != null && hunter.Alive && f.Target == null) f.Hunt(hunter.transform.position);
                else if (e.mode == Mode.Patrol && f.Target == null && !f.Hunting)
                {
                    bool back = patrolBack[f] = !patrolBack[f];
                    f.Hunt(World(back ? e.to : e.at, f.team));
                }
            }
        }

        /// <summary>The nearest enemy unit of side, or failing that its nearest building.</summary>
        static Vector3? Nearest(Vector3 from, Team side)
        {
            float bd = float.MaxValue; Vector3? best = null;
            foreach (var u in SoldierAI.All)
                if (u.Alive && u.team == side) { float d = (u.transform.position - from).sqrMagnitude; if (d < bd) { bd = d; best = u.transform.position; } }
            if (best.HasValue) return best;
            foreach (var h in Health.All)
                if (h.Alive && h.team == side && h.IsStructure) { float d = (h.transform.position - from).sqrMagnitude; if (d < bd) { bd = d; best = h.transform.position; } }
            return best;
        }

        /// <summary>Logs the hunter's decisions: a change of state or of target.</summary>
        void Watch()
        {
            if (hunter == null || !hunter.Alive) return;
            if (hunter.CurrentState != lastState) { Note($"{StateName(lastState)} → {StateName(hunter.CurrentState)}"); lastState = hunter.CurrentState; }
            var t = hunter.Target;
            if (t != lastTarget)
            {
                if (lastTarget != null && !lastTarget.Alive) { kills++; Note($"Abatido: {Describe(lastTarget)}"); }
                if (t != null) Note($"Objetivo: {Describe(t)} a {t.DistanceFrom(hunter.transform.position):0} m");
                else if (lastTarget != null) Note("Sin enemigos a la vista: sigue buscando");
                lastTarget = t;
            }
        }

        void FollowNext()
        {
            var all = new List<SoldierAI>();
            if (hunter != null && hunter.Alive) all.Add(hunter);
            foreach (var f in foes) if (f.Alive) all.Add(f);
            if (all.Count == 0) return;
            int i = all.IndexOf(followed);
            followed = all[(i + 1) % all.Count];
        }

        void Note(string s)
        {
            log.Insert(0, $"{Time.time:0}s  {s}");
            if (log.Count > 9) log.RemoveAt(log.Count - 1);
        }

        static string Describe(Health h)
        {
            var u = h.GetComponent<SoldierAI>();
            string what = u != null && u.Type != null ? u.Type.name : h.kind == TargetKind.CommandPost ? "puesto de mando" : h.kind == TargetKind.Hangar ? "hangar" : h.GetComponent<FlakTower>() != null ? "torre antiaérea" : "casamata";
            return what + " de " + h.team.Name();
        }

        static string StateName(SoldierAI.State s)
        {
            switch (s)
            {
                case SoldierAI.State.Advance: return "avanza";
                case SoldierAI.State.ToCover: return "corre a cubrirse";
                case SoldierAI.State.InCover: return "a cubierto";
                case SoldierAI.State.Hold: return "quieto";
                case SoldierAI.State.Charge: return "carga";
                case SoldierAI.State.Order: return "cumple una orden";
                case SoldierAI.State.Assault: return "se acerca";
                case SoldierAI.State.Fallback: return "se repliega";
                case SoldierAI.State.Evade: return "esquiva";
                default: return "muerto";
            }
        }

        // ------------------------------------------------------------ Camera and drawings
        void LateUpdate()
        {
            if (followed == null || !followed.Alive) followed = hunter != null && hunter.Alive ? hunter : followed;
            if (cam != null && followed != null)
            {
                if (Input.GetMouseButton(1)) yaw += Input.GetAxis("Mouse X") * 4f;
                if (Input.GetKey(KeyCode.Q)) yaw -= 90f * Time.unscaledDeltaTime;
                if (Input.GetKey(KeyCode.E)) yaw += 90f * Time.unscaledDeltaTime;
                zoom = Mathf.Clamp(zoom - Input.mouseScrollDelta.y * 0.1f, 0.4f, 3f);
                Vector3 t = followed.transform.position + Vector3.up * 1.5f;
                Vector3 fwd = followed.transform.forward; fwd.y = 0;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                Vector3 back = Quaternion.Euler(0, yaw, 0) * -fwd.normalized;
                Vector3 want = t + back * followDistance * zoom + Vector3.up * followHeight * zoom;
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
                cam.transform.rotation = Quaternion.LookRotation(t - cam.transform.position);
            }
            DrawLines();
        }

        void DrawLines()
        {
            var u = followed;
            bool show = lines && u != null && u.Alive;
            var agent = show ? u.GetComponent<NavMeshAgent>() : null;
            if (agent != null && agent.enabled && agent.hasPath)
            {
                var c = agent.path.corners;
                pathLine.positionCount = c.Length;
                for (int i = 0; i < c.Length; i++) pathLine.SetPosition(i, c[i] + Vector3.up * 0.3f);
            }
            else pathLine.positionCount = 0;
            if (show && u.Target != null && u.Target.Alive)
            {
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, u.transform.position + Vector3.up * 1.4f);
                var ts = u.Target.GetComponent<SoldierAI>();
                aimLine.SetPosition(1, ts != null ? ts.HitPoint : u.Target.AimPoint);
            }
            else aimLine.positionCount = 0;
            var spot = show ? u.DebugSpot : null;
            spotMarker.gameObject.SetActive(spot.HasValue);
            if (spot.HasValue) spotMarker.position = spot.Value + Vector3.up * 0.05f;
        }

        // ------------------------------------------------------------ Panel
        GUIStyle box, label, small, button;

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (box == null)
            {
                var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.12f, 0.11f, 0.09f, 0.88f)); bg.Apply();
                box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.93f, 0.9f, 0.82f) } };
                small = new GUIStyle(label) { fontSize = 11, normal = { textColor = new Color(0.75f, 0.72f, 0.63f) } };
                button = new GUIStyle(GUI.skin.button) { fontSize = 11 };
            }
            if (labels) Labels(scale);

            GUILayout.BeginArea(new Rect(10, 10, 330, 700), box);
            GUILayout.Label("PRUEBA DE IA · cazador contra todo tipo de enemigos", small);
            var h = hunter;
            if (h != null && h.Alive)
            {
                GUILayout.Label($"Cazador: {h.Type.name} de {h.team.Name()} · vida {h.Health.hp:0.#}/{h.Health.maxHp:0}", label);
                GUILayout.Label($"Estado: {StateName(h.CurrentState)}{(h.Hunting && h.Target == null ? " (buscando enemigos)" : "")}", label);
                var t = h.Target;
                GUILayout.Label(t != null ? $"Objetivo: {Describe(t)} a {t.DistanceFrom(h.transform.position):0} m" : "Objetivo: ninguno a la vista", label);
                GUILayout.Label($"Arma: {Weapon(h.Type.weapon)}, alcance {h.Type.range:0} m{(h.Type.CarriesRifle ? " (y fusil a 50 m)" : "")}", small);
                GUILayout.Label($"Supresión: {Bar(h.Suppression)} {h.Suppression:0.00}{(h.Pinned ? " · CLAVADO" : "")}", small);
                GUILayout.Label($"Bajas causadas: {kills}", small);
            }
            else GUILayout.Label(respawnT >= 0 ? $"El cazador ha caído; vuelve en {respawnT:0} s" : "Esperando al cazador…", label);

            GUILayout.Space(4);
            GUILayout.Label("Tipo de cazador", small);
            int cols = 2;
            for (int i = 0; i < HunterTypes.Length; i += cols)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + cols, HunterTypes.Length); j++)
                {
                    bool on = HunterTypes[j] == hunterType;
                    if (GUILayout.Button((on ? "● " : "") + HunterTypes[j], button)) { hunterType = HunterTypes[j]; Respawn(); }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bando: " + hunterTeam.Name(), button)) { hunterTeam = hunterTeam.Enemy(); SpawnEnemies(); Respawn(); }
            if (GUILayout.Button("Reiniciar enemigos", button)) SpawnEnemies();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (float s in new[] { 0.5f, 1f, 2f, 4f })
                if (GUILayout.Button((Mathf.Approximately(Time.timeScale, s) ? "● " : "") + "x" + s, button)) Time.timeScale = s;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Seguir siguiente (Tab)", button)) FollowNext();
            if (GUILayout.Button(labels ? "Etiquetas: sí" : "Etiquetas: no", button)) labels = !labels;
            if (GUILayout.Button(lines ? "Líneas: sí" : "Líneas: no", button)) lines = !lines;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Decisiones del cazador", small);
            foreach (var l in log) GUILayout.Label(l, small);
            GUILayout.Space(2);
            GUILayout.Label("Amarillo: su camino · rojo: a quién apunta · verde: la cobertura a la que va. Ratón derecho o Q/E giran la cámara, la rueda acerca.", small);
            GUILayout.EndArea();
        }

        void Respawn()
        {
            if (hunter != null) Destroy(hunter.gameObject);
            hunter = null; respawnT = 0.01f;
        }

        void Labels(float scale)
        {
            if (cam == null || followed == null) return;
            foreach (var u in SoldierAI.All)
            {
                if (!u.Alive || (u.transform.position - followed.transform.position).sqrMagnitude > 80f * 80f) continue;
                Vector3 sp = cam.WorldToScreenPoint(u.HitPoint + Vector3.up * (u.IsVehicle ? 2.5f : 1.2f));
                if (sp.z <= 0) continue;
                float x = sp.x / scale, y = (Screen.height - sp.y) / scale;
                var c = u.team == Team.Centrales ? new Color(0.65f, 0.9f, 0.55f) : new Color(0.6f, 0.75f, 1f);
                var st = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = c } };
                string txt = $"{(u == hunter ? "CAZADOR · " : "")}{(u.Type != null ? u.Type.name : "?")}\n{StateName(u.CurrentState)}{(u.Pinned ? " · clavado" : "")}";
                GUI.Label(new Rect(x - 80, y - 28, 160, 30), txt, st);
            }
        }

        static string Bar(float v) { int n = Mathf.RoundToInt(Mathf.Clamp01(v) * 10); return new string('█', n) + new string('·', 10 - n); }

        static string Weapon(WeaponKind w)
        {
            switch (w)
            {
                case WeaponKind.Rifle: return "fusil";
                case WeaponKind.MachineGun: return "ametralladora";
                case WeaponKind.Grenade: return "granadas";
                case WeaponKind.Flamethrower: return "lanzallamas";
                case WeaponKind.Demolition: return "cargas de demolición";
                default: return "cañón";
            }
        }
    }
}
