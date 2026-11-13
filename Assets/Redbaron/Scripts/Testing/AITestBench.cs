using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Redbaron
{
    /// <summary>
    /// RTS test screen (Redbaron_PruebaIA.unity): the battle map with no planes, where you command one side with the mouse.
    /// - Select: click a unit, drag a box, Shift adds, double-click takes every unit of that type in view, Ctrl+1–9 stores a group and 1–9 recalls it.
    /// - Orders: right-click the ground to move, right-click an enemy or building to attack it, A + click to advance fighting,
    ///   S to stop, H to hold position, G to head for the enemy command post (what every unit does by default).
    /// - Stances: aggressive, defensive, hold position.
    /// - Recruit any of the 11 unit types at your base; the waves can be turned off.
    /// - Camera: the arrows, the screen edges or the middle mouse button move it, the wheel zooms, Q/E turn it, Space centres it on the selection.
    /// The selected units' paths, targets and cover are drawn, labels say what each unit is doing, and with one unit selected
    /// the panel shows what it is thinking and a log of its decisions.
    /// </summary>
    public class AITestBench : MonoBehaviour
    {
        public Team playerTeam = Team.Entente;
        [Tooltip("Waves from both bases, as in the battle. Your units from the waves head for the enemy command post until you give them orders.")]
        public bool waves = true;
        [Tooltip("Infantry per recruited squad.")]
        public int squadSize = 4;

        [Header("Camera")]
        public float panSpeed = 45f, pitch = 55f, minDistance = 15f, maxDistance = 150f;
        [Tooltip("Pixels from the edge of the screen that move the camera.")]
        public float edgePan = 10f;

        public enum LabelMode { Selection, All, None }
        public LabelMode labels = LabelMode.Selection;
        public bool lines = true;

        enum Pending { None, Move, AttackMove }

        WaveSpawner spawner;
        RedbaronGame game;
        Camera cam;
        readonly List<SoldierAI> selected = new List<SoldierAI>();
        readonly List<SoldierAI>[] groups = new List<SoldierAI>[10];
        Health inspected;
        Pending pending;
        Vector3 pivot; float yaw, distance = 70f;
        bool dragging; Vector2 dragStart;
        float lastClickT; SoldierAI lastClicked;
        bool help;
        string status = "Selecciona tus unidades y dales órdenes con el ratón";

        // Decision log of the one selected unit
        SoldierAI watched; SoldierAI.State lastState; Health lastTarget;
        readonly List<string> log = new List<string>();

        // Drawings
        Material lineMat;
        readonly List<LineRenderer> rings = new List<LineRenderer>(), paths = new List<LineRenderer>(), aims = new List<LineRenderer>();
        class Mark { public LineRenderer lr; public Vector3 at; public float t; }
        readonly List<Mark> marks = new List<Mark>();
        Transform spotMarker;

        // Screen areas of the panels (GUI units), so clicks there do not reach the map
        Rect topRect, selRect, cardRect;
        float guiScale = 1f;

        static readonly string[] UnitNames =
        {
            "Fusilero", "Ametrallador", "Granadero", "Lanzallamas", "Zapador",
            "Tanque ligero", "Tanqueta lanzallamas", "Tanque pesado", "Mech ligero", "Mech explorador", "Mech de asalto",
            "Mech blindado", "Mech táctico",
        };

        // ------------------------------------------------------------ Setup
        void Start()
        {
            spawner = FindAnyObjectByType<WaveSpawner>();
            game = RedbaronGame.Instance != null ? RedbaronGame.Instance : FindAnyObjectByType<RedbaronGame>();
            var air = FindAnyObjectByType<AirWar>();
            if (air != null) air.enabled = false;
            if (game != null)
            {
                game.showPanel = false;
                game.globalOrderKeys = false; // G and H are unit orders here
                game.speedKeys = new[] { KeyCode.F5, KeyCode.F6, KeyCode.F7 }; // 1–9 are control groups
            }
            // Quick RTS keeps its unit registry, but its own selection, orders and camera are switched off: this screen does that.
            foreach (var b in FindObjectsByType<SelectorRTS>()) b.enabled = false;
            foreach (var b in FindObjectsByType<ShortcutsSelectable>()) b.enabled = false;
            var cams = FindAnyObjectByType<RedbaronCameras>();
            if (cams != null)
            {
                if (cams.rtsCameraScripts != null) foreach (var b in cams.rtsCameraScripts) if (b != null) b.enabled = false;
                cam = cams.cam;
                cams.enabled = false;
            }
            foreach (var b in FindObjectsByType<RTSCameraController>()) b.enabled = false;
            if (cam == null) cam = Camera.main;
            if (cam != null) { cam.orthographic = false; cam.fieldOfView = 50f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 1500f; }
            var mobile = GameObject.Find("Controles_Movil");
            if (mobile != null) mobile.SetActive(false);
            if (spawner != null)
            {
                spawner.playerTeam = playerTeam;
                spawner.playerAutoAdvance = true; // by default your units head for the enemy, fighting on the way
                spawner.enabled = waves;
            }
            lineMat = new Material(Shader.Find("Sprites/Default"));
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(s.GetComponent<Collider>());
            s.name = "Cobertura";
            s.transform.localScale = new Vector3(1.4f, 0.05f, 1.4f);
            s.GetComponent<Renderer>().sharedMaterial = new Material(lineMat) { color = new Color(0.3f, 1f, 0.4f, 0.8f) };
            spotMarker = s.transform;
            LookAtBase();
        }

        void LookAtBase()
        {
            var post = CommandPost.Of(playerTeam);
            pivot = post != null ? post.transform.position : new Vector3(RedbaronMap.WorldW / 2, 0, RedbaronMap.WorldL / 2);
            pivot += Forward(playerTeam) * 35f;
            yaw = playerTeam == Team.Centrales ? 180f : 0f;
        }

        static Vector3 Forward(Team t) => t == Team.Centrales ? Vector3.back : Vector3.forward; // the Central Powers hold the north end

        // ------------------------------------------------------------ Loop
        void Update()
        {
            selected.RemoveAll(u => u == null || !u.Alive);
            if (inspected != null && !inspected.Alive) inspected = null;
            CameraInput();
            if (!PauseMenu.IsPaused) { KeyOrders(); MouseInput(); }
            Watch();
        }

        Vector2 GuiMouse => new Vector2(Input.mousePosition.x / guiScale, (Screen.height - Input.mousePosition.y) / guiScale);
        bool OverPanel() { var m = GuiMouse; return topRect.Contains(m) || selRect.Contains(m) || cardRect.Contains(m); }

        void CameraInput()
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 f = Quaternion.Euler(0, yaw, 0) * Vector3.forward, r = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            Vector3 move = Vector3.zero;
            // Arrows and screen edges (letters are orders: A, S, M, H, G).
            if (Input.GetKey(KeyCode.UpArrow)) move += f;
            if (Input.GetKey(KeyCode.DownArrow)) move -= f;
            if (Input.GetKey(KeyCode.LeftArrow)) move -= r;
            if (Input.GetKey(KeyCode.RightArrow)) move += r;
            if (Application.isFocused && !dragging)
            {
                var m = Input.mousePosition;
                if (m.x >= 0 && m.x <= Screen.width && m.y >= 0 && m.y <= Screen.height)
                {
                    if (m.x < edgePan) move -= r; else if (m.x > Screen.width - edgePan) move += r;
                    if (m.y < edgePan) move -= f; else if (m.y > Screen.height - edgePan) move += f;
                }
            }
            if (Input.GetMouseButton(2)) move += -r * Input.GetAxis("Mouse X") * 2f - f * Input.GetAxis("Mouse Y") * 2f;
            pivot += move * panSpeed * (distance / 70f) * dt;
            pivot.x = Mathf.Clamp(pivot.x, 0, RedbaronMap.WorldW);
            pivot.z = Mathf.Clamp(pivot.z, 0, RedbaronMap.WorldL);
            if (!OverPanel()) distance = Mathf.Clamp(distance * (1f - Input.mouseScrollDelta.y * 0.1f), minDistance, maxDistance);
            if (Input.GetKey(KeyCode.Q)) yaw -= 80f * dt;
            if (Input.GetKey(KeyCode.E)) yaw += 80f * dt;
            if (Input.GetKeyDown(KeyCode.Space) && selected.Count > 0) pivot = Center(selected);
            pivot.y = RedbaronMap.Sample(RedbaronMap.ToDesign(pivot).x, RedbaronMap.ToDesign(pivot).y).h;
        }

        void LateUpdate()
        {
            if (cam != null)
            {
                Quaternion q = Quaternion.Euler(pitch, yaw, 0);
                cam.transform.SetPositionAndRotation(pivot + q * (Vector3.back * distance), q);
            }
            Draw();
        }

        // ------------------------------------------------------------ Keys
        void KeyOrders()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand);
            for (int i = 1; i <= 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + i)) continue;
                if (ctrl) { groups[i] = new List<SoldierAI>(selected); Say($"Grupo {i}: {selected.Count} unidades"); }
                else if (groups[i] != null)
                {
                    groups[i].RemoveAll(u => u == null || !u.Alive);
                    bool again = selected.Count == groups[i].Count && selected.TrueForAll(groups[i].Contains);
                    selected.Clear(); selected.AddRange(groups[i]);
                    if (again && selected.Count > 0) pivot = Center(selected); // second press: go there
                }
            }
            if (selected.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.A)) { pending = Pending.AttackMove; Say("Avanzar atacando: haz clic en el destino"); }
            if (Input.GetKeyDown(KeyCode.M)) { pending = Pending.Move; Say("Mover: haz clic en el destino"); }
            if (Input.GetKeyDown(KeyCode.S)) Order("Detenerse", u => u.CommandStop());
            if (Input.GetKeyDown(KeyCode.H)) Order("Mantener la posición", u => u.SetStance(SoldierAI.Stance.HoldPosition));
            if (Input.GetKeyDown(KeyCode.G)) Order("Al cuartel enemigo", u => u.CommandAdvance());
            if (Input.GetKeyDown(KeyCode.Escape)) pending = Pending.None;
        }

        void Order(string what, System.Action<SoldierAI> act)
        {
            foreach (var u in selected) if (u.Alive) act(u);
            Say($"{what} ({selected.Count})");
        }

        // ------------------------------------------------------------ Mouse
        void MouseInput()
        {
            if (Input.GetMouseButtonDown(0) && !OverPanel())
            {
                if (pending != Pending.None) { Command(pending == Pending.AttackMove, Input.mousePosition); pending = Pending.None; return; }
                dragging = true; dragStart = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                bool add = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (((Vector2)Input.mousePosition - dragStart).magnitude < 6f) Click(Input.mousePosition, add);
                else BoxSelect(dragStart, Input.mousePosition, add);
            }
            if (Input.GetMouseButtonDown(1) && !OverPanel() && selected.Count > 0)
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand);
                Command(ctrl, Input.mousePosition);
                pending = Pending.None;
            }
        }

        /// <summary>A unit, a building or a point on the ground under the cursor.</summary>
        bool Pick(Vector2 screen, out SoldierAI unit, out Health building, out Vector3 ground)
        {
            unit = null; building = null; ground = Vector3.zero;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(screen), 1500f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (var h in hits)
            {
                var u = h.collider.GetComponentInParent<SoldierAI>();
                if (u != null && u.Alive) { unit = u; ground = u.transform.position; return true; }
                var hp = h.collider.GetComponentInParent<Health>();
                if (hp != null && hp.Alive && hp.IsStructure) { building = hp; ground = h.point; return true; }
                if (!found && h.collider.name != "Copa") { ground = h.point; found = true; } // through tree crowns to the ground
            }
            return found;
        }

        /// <summary>A click on the ground with nothing selected moves the flag of the lane under the cursor (LaneFlags).</summary>
        void MoveFlag(Vector3 ground)
        {
            var flags = LaneFlags.Instance;
            if (flags == null) return;
            int lane = flags.NearestLane(ground);
            flags.Set(lane, ground);
            AddMark(flags.Position(lane), flags.team.FlagColor());
            Say(flags.AtPost(lane) ? $"Bandera del carril {lane + 1} en el cuartel enemigo: avanzar y destruirlo" : $"Bandera del carril {lane + 1}: ocupar ese tramo");
        }

        void Click(Vector2 screen, bool add)
        {
            if (!Pick(screen, out var unit, out var building, out var ground)) { if (!add) selected.Clear(); return; }
            if (unit == null && building == null && selected.Count == 0 && !add) { MoveFlag(ground); return; }
            if (unit != null && unit.team == playerTeam)
            {
                bool doubleClick = unit == lastClicked && Time.unscaledTime - lastClickT < 0.35f;
                lastClicked = unit; lastClickT = Time.unscaledTime;
                inspected = null;
                if (doubleClick) { SelectAllOfType(unit, add); return; }
                if (!add) selected.Clear();
                if (add && selected.Contains(unit)) selected.Remove(unit); else selected.Add(unit);
                return;
            }
            // An enemy unit or a building: shown in the panel, no orders.
            inspected = unit != null ? unit.Health : building;
            if (!add) selected.Clear();
        }

        void SelectAllOfType(SoldierAI like, bool add)
        {
            if (!add) selected.Clear();
            foreach (var u in SoldierAI.All)
                if (u.Alive && u.team == playerTeam && u.Type != null && like.Type != null && u.Type.name == like.Type.name && OnScreen(u.transform.position) && !selected.Contains(u))
                    selected.Add(u);
            Say($"{selected.Count} × {like.Type?.name}");
        }

        bool OnScreen(Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1;
        }

        void BoxSelect(Vector2 a, Vector2 b, bool add)
        {
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            if (!add) selected.Clear();
            inspected = null;
            foreach (var u in SoldierAI.All)
            {
                if (!u.Alive || u.team != playerTeam || selected.Contains(u)) continue;
                Vector3 sp = cam.WorldToScreenPoint(u.transform.position + Vector3.up);
                if (sp.z > 0 && r.Contains(new Vector2(sp.x, sp.y))) selected.Add(u);
            }
        }

        /// <summary>Right click (or a pending order): attack what is under the cursor, or move / advance to the ground there.</summary>
        void Command(bool attackMove, Vector2 screen)
        {
            if (selected.Count == 0 || !Pick(screen, out var unit, out var building, out var ground)) return;
            Health enemy = unit != null && unit.team != playerTeam ? unit.Health : building != null && building.team != playerTeam ? building : null;
            if (enemy != null)
            {
                Order("Atacar", u => u.CommandAttack(enemy));
                AddMark(enemy.transform.position, new Color(1f, 0.25f, 0.2f));
                return;
            }
            if (unit != null) ground = unit.transform.position; // a friendly unit: go next to it
            var spots = Formation(ground, selected);
            for (int i = 0; i < selected.Count; i++)
            {
                if (attackMove) selected[i].CommandAttackMove(spots[i]); else selected[i].CommandMove(spots[i]);
            }
            Say((attackMove ? "Avanzar atacando" : "Mover") + $" ({selected.Count})");
            AddMark(ground, attackMove ? new Color(1f, 0.6f, 0.15f) : new Color(0.3f, 1f, 0.4f));
        }

        /// <summary>Spots in a block around p facing the way the group is going, wider apart for vehicles.</summary>
        static List<Vector3> Formation(Vector3 p, List<SoldierAI> units)
        {
            var list = new List<Vector3>();
            bool vehicles = units.Exists(u => u.IsVehicle);
            float gap = vehicles ? 7f : 2.4f;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(units.Count));
            Vector3 dir = p - Center(units); dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 side = new Vector3(dir.z, 0, -dir.x);
            for (int i = 0; i < units.Count; i++)
            {
                int row = i / cols, col = i % cols;
                Vector3 q = p + side * (col - (cols - 1) * 0.5f) * gap - dir * row * gap;
                if (NavMesh.SamplePosition(q, out var hit, 4f, NavMesh.AllAreas)) q = hit.position;
                list.Add(q);
            }
            return list;
        }

        static Vector3 Center(List<SoldierAI> units)
        {
            Vector3 c = Vector3.zero; int n = 0;
            foreach (var u in units) if (u != null) { c += u.transform.position; n++; }
            return n > 0 ? c / n : Vector3.zero;
        }

        /// <summary>The troop buttons share TroopBar's cooldowns with the battle; the bar itself stays hidden here.</summary>
        TroopBar Troops
        {
            get
            {
                if (bar != null) return bar;
                bar = FindAnyObjectByType<TroopBar>();
                if (bar == null) bar = gameObject.AddComponent<TroopBar>();
                bar.showBar = false;
                bar.spawner = spawner;
                return bar;
            }
        }
        TroopBar bar;

        void Recruit(string typeName)
        {
            var type = spawner != null ? spawner.TypeByName(typeName) : null;
            if (type == null) { Say("No hay tipo «" + typeName + "»"); return; }
            if (!type.IsVehicle && squadSize > 0) type.recruitCount = squadSize;
            var units = Troops.Recruit(type);
            if (units == null) { Say($"{type.name}: listo en {Troops.Remaining(type):0} s"); return; }
            Say($"Reclutado: {units.Count} × {type.name} (línea {Troops.LastLane + 1})");
        }

        void Say(string s) { status = s; }

        // ------------------------------------------------------------ Decision log of the single selected unit
        void Watch()
        {
            var u = selected.Count == 1 ? selected[0] : null;
            if (u != watched)
            {
                watched = u; log.Clear();
                if (u != null) { lastState = u.CurrentState; lastTarget = u.Target; Note("Seleccionado: " + u.Type?.name); }
            }
            if (u == null || !u.Alive) return;
            if (u.CurrentState != lastState) { Note($"{StateName(lastState)} → {StateName(u.CurrentState)}"); lastState = u.CurrentState; }
            if (u.Target != lastTarget)
            {
                if (lastTarget != null && !lastTarget.Alive) Note("Abatido: " + Describe(lastTarget));
                if (u.Target != null) Note($"Objetivo: {Describe(u.Target)} a {u.Target.DistanceFrom(u.transform.position):0} m");
                lastTarget = u.Target;
            }
        }

        void Note(string s)
        {
            log.Insert(0, $"{Time.time:0}s  {s}");
            if (log.Count > 6) log.RemoveAt(log.Count - 1);
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
                case SoldierAI.State.Order: return "se mueve";
                case SoldierAI.State.Assault: return "se acerca";
                case SoldierAI.State.Fallback: return "se repliega";
                case SoldierAI.State.Evade: return "esquiva";
                default: return "muerto";
            }
        }

        static string StanceName(SoldierAI.Stance s) => s == SoldierAI.Stance.Aggressive ? "agresiva" : s == SoldierAI.Stance.Defensive ? "defensiva" : "mantener posición";

        // ------------------------------------------------------------ Drawings
        LineRenderer NewLine(List<LineRenderer> pool, int i, Color c, float width, bool loop)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject("Linea");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<LineRenderer>();
                l.sharedMaterial = lineMat;
                l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                l.receiveShadows = false;
                pool.Add(l);
            }
            var lr = pool[i];
            lr.enabled = true; lr.loop = loop; lr.widthMultiplier = width; lr.startColor = lr.endColor = c;
            return lr;
        }

        static void HideFrom(List<LineRenderer> pool, int i) { for (; i < pool.Count; i++) pool[i].enabled = false; }

        void Ring(LineRenderer lr, Vector3 c, float radius)
        {
            const int n = 24;
            lr.positionCount = n;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                lr.SetPosition(k, c + new Vector3(Mathf.Cos(a) * radius, 0.15f, Mathf.Sin(a) * radius));
            }
        }

        static float Radius(SoldierAI u) => u.IsVehicle ? Mathf.Max(1.5f, u.Length * 0.55f) : 0.7f;

        void Draw()
        {
            int ri = 0, pi = 0, ai = 0;
            foreach (var u in selected)
            {
                Ring(NewLine(rings, ri++, new Color(0.35f, 1f, 0.45f, 0.95f), 0.09f, true), u.transform.position, Radius(u));
                if (!lines) continue;
                var agent = u.GetComponent<NavMeshAgent>();
                if (pi < 24 && agent != null && agent.enabled && agent.hasPath)
                {
                    var c = agent.path.corners;
                    var lr = NewLine(paths, pi++, new Color(1f, 0.85f, 0.2f, 0.8f), 0.12f, false);
                    lr.positionCount = c.Length;
                    for (int k = 0; k < c.Length; k++) lr.SetPosition(k, c[k] + Vector3.up * 0.3f);
                }
                if (ai < 24 && u.Target != null && u.Target.Alive)
                {
                    var lr = NewLine(aims, ai++, new Color(1f, 0.25f, 0.2f, 0.85f), 0.06f, false);
                    lr.positionCount = 2;
                    lr.SetPosition(0, u.transform.position + Vector3.up * 1.4f);
                    var ts = u.Target.GetComponent<SoldierAI>();
                    lr.SetPosition(1, ts != null ? ts.HitPoint : u.Target.AimPoint);
                }
            }
            if (inspected != null)
            {
                var iu = inspected.GetComponent<SoldierAI>();
                float rad = iu != null ? Radius(iu) : inspected.Body != null ? Mathf.Max(inspected.Body.bounds.extents.x, inspected.Body.bounds.extents.z) + 0.5f : 2f;
                Ring(NewLine(rings, ri++, new Color(1f, 0.3f, 0.25f, 0.95f), 0.1f, true), inspected.transform.position, rad);
            }
            HideFrom(rings, ri); HideFrom(paths, pi); HideFrom(aims, ai);

            var spot = lines && watched != null && watched.Alive ? watched.DebugSpot : null;
            spotMarker.gameObject.SetActive(spot.HasValue);
            if (spot.HasValue) spotMarker.position = spot.Value + Vector3.up * 0.05f;

            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                m.t += Time.unscaledDeltaTime;
                if (m.t > 0.7f) { Destroy(m.lr.gameObject); marks.RemoveAt(i); continue; }
                Ring(m.lr, m.at, Mathf.Lerp(2.2f, 0.4f, m.t / 0.7f));
            }
        }

        void AddMark(Vector3 at, Color c)
        {
            var go = new GameObject("Orden");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMat; lr.loop = true; lr.widthMultiplier = 0.15f; lr.startColor = lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marks.Add(new Mark { lr = lr, at = at });
        }

        // ------------------------------------------------------------ Panels
        GUIStyle box, label, small, tiny, oneLine, button, on;
        Texture2D white;

        void Styles()
        {
            if (box != null) return;
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.12f, 0.11f, 0.09f, 0.9f)); bg.Apply();
            white = Texture2D.whiteTexture;
            box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.93f, 0.9f, 0.82f) } };
            small = new GUIStyle(label) { fontSize = 11, normal = { textColor = new Color(0.78f, 0.75f, 0.66f) } };
            tiny = new GUIStyle(small) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            oneLine = new GUIStyle(small) { wordWrap = false, clipping = TextClipping.Clip };
            button = new GUIStyle(GUI.skin.button) { fontSize = 11 };
            on = new GUIStyle(button) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
        }

        void OnGUI()
        {
            guiScale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));
            Styles();
            float W = Screen.width / guiScale, H = Screen.height / guiScale;
            Labels();
            if (dragging)
            {
                Vector2 a = new Vector2(dragStart.x / guiScale, (Screen.height - dragStart.y) / guiScale), b = GuiMouse;
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                GUI.color = new Color(0.4f, 1f, 0.5f, 0.15f); GUI.DrawTexture(r, white);
                GUI.color = new Color(0.4f, 1f, 0.5f, 0.8f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), white); GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), white);
                GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), white); GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), white);
                GUI.color = Color.white;
            }
            TopBar(W);
            SelectionPanel(H);
            CommandCard(W, H);
            if (help) Help(W, H);
        }

        void TopBar(float W)
        {
            topRect = new Rect(10, 8, W - 20, 30);
            GUILayout.BeginArea(topRect, box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("PRUEBA RTS", small, GUILayout.Width(80));
            foreach (var t in new[] { Team.Centrales, Team.Entente })
            {
                var p = CommandPost.Of(t);
                int n = 0; foreach (var u in SoldierAI.All) if (u.Alive && u.team == t) n++;
                GUILayout.Label($"{(t == playerTeam ? "Tú" : "Enemigo")} ({t.Name()}): cuartel {(p != null ? p.Health.hp : 0):0} · {n} uds", oneLine, GUILayout.Width(285));
            }
            GUILayout.FlexibleSpace();
            foreach (float s in new[] { 0.5f, 1f, 2f, 4f })
                if (GUILayout.Button("x" + s, Mathf.Approximately(Time.timeScale, s) ? on : button, GUILayout.Width(38))) Time.timeScale = s;
            if (GUILayout.Button(waves ? "Oleadas: sí" : "Oleadas: no", button, GUILayout.Width(86))) { waves = !waves; if (spawner != null) spawner.enabled = waves; }
            if (GUILayout.Button("Etiquetas: " + (labels == LabelMode.Selection ? "selección" : labels == LabelMode.All ? "todas" : "no"), button, GUILayout.Width(120)))
                labels = (LabelMode)(((int)labels + 1) % 3);
            if (GUILayout.Button(lines ? "Líneas: sí" : "Líneas: no", button, GUILayout.Width(76))) lines = !lines;
            if (GUILayout.Button("Banderas al cuartel", button, GUILayout.Width(120)) && LaneFlags.Instance != null) { LaneFlags.Instance.ResetAll(); Say("Las tres banderas vuelven al cuartel enemigo: avanzar"); }
            if (GUILayout.Button("Bando", button, GUILayout.Width(56))) SwitchSide();
            if (GUILayout.Button(help ? "Ayuda ▲" : "Ayuda", button, GUILayout.Width(62))) help = !help;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void SwitchSide()
        {
            playerTeam = playerTeam.Enemy();
            if (LaneFlags.Instance != null) LaneFlags.Instance.SetTeam(playerTeam);
            selected.Clear(); inspected = null;
            if (spawner != null) spawner.playerTeam = playerTeam;
            if (game != null) game.ResetBattle(); // a fresh battle on the other side
            LookAtBase();
            Say("Ahora mandas a " + playerTeam.Name());
        }

        void SelectionPanel(float H)
        {
            selRect = new Rect(10, H - 250, 360, 240);
            GUILayout.BeginArea(selRect, box);
            if (selected.Count == 1) UnitDetails(selected[0], true);
            else if (selected.Count > 1)
            {
                GUILayout.Label($"{selected.Count} unidades seleccionadas", label);
                var counts = new Dictionary<string, int>();
                float hp = 0, max = 0;
                foreach (var u in selected) { string k = u.Type != null ? u.Type.name : "?"; counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1; hp += u.Health.hp; max += u.Health.maxHp; }
                foreach (var kv in counts) GUILayout.Label($"  {kv.Value} × {kv.Key}", small);
                GUILayout.Label($"Vida total: {hp:0}/{max:0}", small);
            }
            else if (inspected != null)
            {
                var iu = inspected.GetComponent<SoldierAI>();
                if (iu != null) UnitDetails(iu, false);
                else { GUILayout.Label(Describe(inspected), label); GUILayout.Label($"Resistencia: {inspected.hp:0}/{inspected.maxHp:0}", small); GUILayout.Label("Clic derecho con tus unidades seleccionadas para atacarlo", small); }
            }
            else
            {
                GUILayout.Label("Nada seleccionado", label);
                GUILayout.Label("Clic: seleccionar · arrastrar: recuadro · doble clic: todos los de ese tipo", small);
                GUILayout.Label("Clic derecho: mover o atacar · A + clic: avanzar atacando", small);
                GUILayout.Label("Clic en el suelo sin selección: mover la bandera de ese carril (su orden)", small);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(status, small);
            GUILayout.EndArea();
        }

        void UnitDetails(SoldierAI u, bool mine)
        {
            GUILayout.Label($"{u.Type?.name} de {u.team.Name()} · vida {u.Health.hp:0.#}/{u.Health.maxHp:0}", label);
            GUILayout.Label($"Estado: {StateName(u.CurrentState)} · orden: {u.OrderText}" + (u.playerControlled ? $" · actitud {StanceName(u.stance)}" : ""), small);
            var t = u.Target;
            GUILayout.Label(t != null ? $"Objetivo: {Describe(t)} a {t.DistanceFrom(u.transform.position):0} m" : "Objetivo: ninguno a la vista", small);
            if (u.Type != null) GUILayout.Label($"Arma: {(u.IsVehicle && u.Type.weapon == WeaponKind.Grenade ? "lanzagranadas" : Weapon(u.Type.weapon))}, {u.Type.range:0} m{(u.Type.CarriesRifle ? " (y fusil a 50 m)" : "")}", small);
            if (u.AirTarget != null) GUILayout.Label($"Fuego antiaéreo contra {u.AirTarget.pilotName}", small);
            if (u.IsVehicle) GUILayout.Label("Blindaje: balas ×0,1 · granadas ×0,25 · fuego ×0,5 · obuses, cargas y bombas ×1", small);
            if (!u.IsVehicle) GUILayout.Label($"Supresión {Bar(u.Suppression)} {u.Suppression:0.00}{(u.Pinned ? " · CLAVADO" : "")}" + (u.Squad != null ? $" · escuadra {u.Squad.AliveCount}/{u.Squad.Size} ({SquadOrder(u.Squad)})" : ""), small);
            if (mine) foreach (var l in log) GUILayout.Label(l, tiny);
        }

        static string SquadOrder(Squad s) => s.CurrentOrder == Squad.Order.Advance ? (s.Escort != null ? "sigue a un blindado" : "en marcha") : s.CurrentOrder == Squad.Order.Fallback ? "se repliega" : "se reagrupa";

        void CommandCard(float W, float H)
        {
            cardRect = new Rect(W - 380, H - 250, 370, 240);
            GUILayout.BeginArea(cardRect, box);
            bool any = selected.Count > 0;
            GUI.enabled = any;
            GUILayout.Label("Órdenes", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Mover (M)", pending == Pending.Move ? on : button)) { pending = Pending.Move; Say("Mover: haz clic en el destino"); }
            if (GUILayout.Button("Atacar (A)", pending == Pending.AttackMove ? on : button)) { pending = Pending.AttackMove; Say("Avanzar atacando: haz clic en el destino o en un enemigo"); }
            if (GUILayout.Button("Detener (S)", button)) Order("Detenerse", u => u.CommandStop());
            if (GUILayout.Button("Al cuartel (G)", button)) Order("Al cuartel enemigo", u => u.CommandAdvance());
            GUILayout.EndHorizontal();
            GUILayout.Label("Actitud", small);
            GUILayout.BeginHorizontal();
            foreach (var s in new[] { SoldierAI.Stance.Aggressive, SoldierAI.Stance.Defensive, SoldierAI.Stance.HoldPosition })
            {
                bool all = any && selected.TrueForAll(u => u.stance == s);
                string name = s == SoldierAI.Stance.HoldPosition ? "Mantener (H)" : s == SoldierAI.Stance.Aggressive ? "Agresiva" : "Defensiva";
                if (GUILayout.Button(name, all ? on : button)) Order("Actitud " + StanceName(s), u => u.SetStance(s));
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Reclutar en tu base (salen hacia el cuartel enemigo; cada tipo tiene su tiempo de espera)", small);
            for (int i = 0; i < UnitNames.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 3, UnitNames.Length); j++)
                {
                    var t = spawner != null ? spawner.TypeByName(UnitNames[j]) : null;
                    float rem = t != null ? Troops.Remaining(t) : 0f;
                    GUI.enabled = t != null && rem <= 0f;
                    if (GUILayout.Button(rem > 0f ? $"{UnitNames[j]} · {rem:0} s" : UnitNames[j], button)) Recruit(UnitNames[j]);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        void Help(float W, float H)
        {
            var r = new Rect(W / 2 - 230, 50, 460, 300);
            GUILayout.BeginArea(r, box);
            GUILayout.Label("Controles", label);
            foreach (var l in new[]
            {
                "Clic izquierdo: seleccionar · arrastrar: recuadro · Mayús: añadir · doble clic: todos los de ese tipo a la vista",
                "Ctrl + 1–9: guardar grupo · 1–9: seleccionarlo (dos veces: ir a él)",
                "Clic derecho en el suelo: mover · en un enemigo o edificio: atacarlo · Ctrl + clic derecho: avanzar atacando",
                "A + clic: avanzar atacando · M + clic: mover · S: detenerse · H: mantener la posición · G: al cuartel enemigo",
                "Banderas: clic en el suelo sin nada seleccionado mueve la bandera del carril más cercano; en el cuartel enemigo = avanzar y destruirlo, en otro sitio = ocupar ese tramo",
                "Actitudes: agresiva (persigue a lo que ve y vuelve), defensiva (solo lo que alcanza su arma), mantener (no se mueve)",
                "Cámara: flechas o bordes de la pantalla · botón central: arrastrar · rueda: zoom · Q/E: girar · Espacio: ir a la selección",
                "Velocidad: botones de arriba o F5/F6/F7 · Escape: pausa",
                "Amarillo: camino · rojo: a quién apunta · verde: cobertura del soldado seleccionado",
            }) GUILayout.Label(l, small);
            GUILayout.EndArea();
        }

        void Labels()
        {
            if (labels == LabelMode.None || cam == null) return;
            foreach (var u in SoldierAI.All)
            {
                if (!u.Alive) continue;
                bool sel = selected.Contains(u) || (inspected != null && inspected == u.Health);
                if (labels == LabelMode.Selection && !sel) continue;
                Vector3 sp = cam.WorldToScreenPoint(u.HitPoint + Vector3.up * (u.IsVehicle ? 2.5f : 1.2f));
                if (sp.z <= 0) continue;
                float x = sp.x / guiScale, y = (Screen.height - sp.y) / guiScale;
                // life bar
                float f = u.Health.maxHp > 0 ? u.Health.hp / u.Health.maxHp : 0;
                GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(x - 16, y + 2, 32, 4), white);
                GUI.color = Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(0.4f, 1f, 0.4f), f); GUI.DrawTexture(new Rect(x - 16, y + 2, 32 * f, 4), white);
                GUI.color = Color.white;
                var c = u.team == Team.Centrales ? new Color(0.65f, 0.9f, 0.55f) : new Color(0.6f, 0.75f, 1f);
                var st = new GUIStyle(tiny) { normal = { textColor = c } };
                GUI.Label(new Rect(x - 80, y - 26, 160, 28), $"{u.Type?.name}\n{StateName(u.CurrentState)}{(u.Pinned ? " · clavado" : "")}", st);
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
                case WeaponKind.Demolition: return "cargas de demolición (mecha de 3 s)";
                case WeaponKind.Flak: return "cañón antiaéreo";
                default: return "cañón";
            }
        }
    }
}
