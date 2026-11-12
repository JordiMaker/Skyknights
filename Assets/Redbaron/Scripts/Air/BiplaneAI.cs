using System.Collections.Generic;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Lo que mueve físicamente al avión. Por defecto <see cref="KinematicFlight"/>;
    /// para usar la física de Air Battle 3D, implementa esta interfaz en un componente que
    /// traduzca estas órdenes a los controles del avión de la plantilla (ver LEEME).
    /// </summary>
    public interface IFlightBody
    {
        Vector3 Position { get; }
        float Yaw { get; }            // radianes, 0 = +z
        float VerticalSpeed { get; }
        float Speed { get; }
        /// <summary>Rumbo deseado (rad) y velocidad vertical deseada (m/s) para este paso.</summary>
        void Steer(float desiredYaw, float desiredVerticalSpeed, float dt);
        void Teleport(Vector3 pos, float yaw, float verticalSpeed);
    }

    /// <summary>
    /// Piloto de IA del prototipo web: vuela a ras de suelo esquivando montañas, árboles, edificios y la pared,
    /// persigue a los aviones enemigos (objetivo principal), dispara con cadencia lenta, suelta una bomba
    /// sobre el aeródromo enemigo y la recarga al pasar por el suyo. Reaparece en su aeródromo al caer.
    /// </summary>
    public class BiplaneAI : MonoBehaviour
    {
        public static readonly List<BiplaneAI> All = new List<BiplaneAI>();

        [Header("Piloto")]
        public string pilotName = "Fokker";
        public Team team = Team.Centrales;
        public PlaneModel model = PlaneModel.FokkerTriplano;
        [Tooltip("Altura mínima que intenta dejar sobre los obstáculos (m). Más bajo = más temerario.")]
        public float clearance = 2.4f;
        [Tooltip("Segundos que mira hacia delante para subir a tiempo.")]
        public float lookAhead = 1.0f;
        public float maxClimb = 9.5f;

        [Header("Combate")]
        public float maxHp = 6;
        public float gunRange = 55f;
        [Tooltip("AI pilots: time between bursts (s). 2.75 = 20 % slower than the first version's 2.2.")]
        public float fireCooldown = 2.75f;
        [Tooltip("Muzzle speed of the rounds, added to the plane's own speed (m/s). 96 = 20 % slower than the first version's 120.")]
        public float bulletSpeed = 96f;
        public float respawnDelay = 6f;
        public Material material;
        [Tooltip("Rounds carried (each burst fires two). Refilled at your airfield's floating icon.")]
        public int maxAmmo = 80;
        [Tooltip("Bombs the player carries (key X). AI pilots carry one, as in the prototype.")]
        public int maxBombs = 4;
        public float playerBombCooldown = 0.8f;
        [Tooltip("Which side of its own airfield the plane always respawns on (0 = west, 1 = east). -1 = alternate.")]
        public int homeSlot = -1;

        [Header("Model")]
        [Tooltip("3D model flown instead of the kit's built biplane (e.g. a plane from Air Battle 3D or Zero Fighter). Its own scripts, colliders, cameras, trails and effects are removed.")]
        public GameObject visualPrefab;
        [Tooltip("Wingspan the model is scaled to (m). 0 = use Visual Scale instead.")]
        public float visualSpan = 7.5f;
        [Tooltip("Scale for the model when Visual Span is 0 (the Redbaron hangar ships use 0.6, their game scale).")]
        public float visualScale = 0.6f;
        [Tooltip("How much of the team colour is mixed into the model (0 = its own colours).")]
        [Range(0, 1)] public float teamTint = 0.3f;

        [Header("Player control (the plane you fly)")]
        [Tooltip("You fly this plane: left/right roll it, up/down change speed, Z or Space fire. Altitude stays automatic.")]
        public bool playerControlled;
        [Tooltip("Turn rate at 90° of bank (degrees per second). Turn = max turn × sin(bank): none with level wings or upside down.")]
        public float maxTurnRate = 65f;
        [Tooltip("How fast the plane rolls while you hold left or right (degrees per second).")]
        public float rollRate = 150f;
        [Tooltip("When you let go below this bank angle (degrees), the wings level themselves.")]
        public float autoLevelBelow = 3f;
        public float minSpeed = 11f, maxSpeed = 26f;
        [Tooltip("Speed change per second while you hold up or down (m/s²).")]
        public float throttleRate = 7f;
        [Tooltip("Time between bursts while the trigger is held (s). 0.5 = 20 % slower than the first version's 0.4. There is no aim assist: rounds fly where the nose points.")]
        public float playerFireCooldown = 0.5f;

        [Header("Collisions")]
        [Tooltip("Energy a collision costs: with a tree crown, a mountain, a building, the rock wall or another plane. The plane only goes down when its energy runs out.")]
        public float collisionDamage = 2f;
        [Tooltip("After a collision the plane cannot be hurt by another collision for this long (s), so one impact never counts twice while it gets clear.")]
        public float collisionGrace = 1.5f;
        [Tooltip("Speed of the push that throws the plane clear of what it hit (m/s). It fades out over Push Time.")]
        public float pushSpeed = 16f;
        public float pushTime = 0.6f;
        [Tooltip("Models picked at random every time the plane spawns. Empty = Visual Prefab, or the built biplane.")]
        public GameObject[] modelPool;
        [Tooltip("Also pick among the kit's built biplanes (Camel, Fokker triplane, Albatros).")]
        public bool poolIncludesBuilt;

        /// <summary>The single horizontal plane that all aircraft and air-to-air bullets share (m).
        /// NaN = terrain following, as in the web prototype. Set by AirWar.</summary>
        public static float FlightAltitude = float.NaN;
        static bool OnFlightPlane => !float.IsNaN(FlightAltitude);
        public string ModelName { get; private set; }
        public int Ammo { get; private set; }
        public int Bombs { get; private set; }
        /// <summary>Size of the plane for collisions and hits (m), from its model.</summary>
        public float BodyRadius { get; private set; } = 2.25f;
        public bool NeedsRefill => Hp < maxHp || Ammo < maxAmmo || Bombs < maxBombs || !HasBomb;
        float refilledAt = -10f, bombCd;

        /// <summary>The plane the player flies, if any.</summary>
        public static BiplaneAI Player { get; private set; }
        /// <summary>Bank angle in degrees, -180..180; positive = right wing down.</summary>
        public float Bank { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float RespawnIn => Alive ? 0 : Mathf.Max(0, wait);
        /// <summary>True for a moment after a collision: another collision does no damage (the plane is still pushed clear).</summary>
        public bool CollisionImmune => Time.time < graceUntil;
        float graceUntil = -10f;

        public bool Alive { get; private set; }
        public bool HasBomb { get; private set; }
        public IFlightBody Body { get; private set; }
        public float Hp { get; private set; }
        public Transform Visual => visual;

        Transform visual, prop;
        Color bodyColor;
        float cd, wait, smokeT, t, turnRate, lastYaw, thinkT, plan, enemyDist;
        BiplaneAI enemy;
        bool hunting;
        static int spawnCounter;
        static LayerMask obstacleMask = ~(1 << 2);

        public static BiplaneAI Spawn(string name, Team team, PlaneModel model, float clearance, float lookAhead, float climb, Material mat)
        {
            var go = new GameObject("Piloto_" + name);
            var ai = go.AddComponent<BiplaneAI>();
            ai.pilotName = name; ai.team = team; ai.model = model; ai.clearance = clearance; ai.lookAhead = lookAhead; ai.maxClimb = climb; ai.material = mat;
            return ai;
        }

        void Start()
        {
            Body = GetComponent<IFlightBody>();
            if (Body == null) Body = gameObject.AddComponent<KinematicFlight>();
            if (playerControlled) Player = this;
            t = Random.value * 10;
            if (!HasPool) BuildModel(visualPrefab, model);
            Respawn();
        }

        bool HasPool => (modelPool != null && modelPool.Length > 0) || poolIncludesBuilt;
        static readonly PlaneModel[] BuiltModels = { PlaneModel.Camel, PlaneModel.FokkerTriplano, PlaneModel.Albatros };

        /// <summary>A random model from the pool: one of the prefabs or, if allowed, one of the built biplanes.</summary>
        void PickRandomModel()
        {
            int nPrefabs = modelPool != null ? modelPool.Length : 0, n = nPrefabs + (poolIncludesBuilt ? BuiltModels.Length : 0);
            int i = Random.Range(0, n);
            if (i < nPrefabs && modelPool[i] != null) BuildModel(modelPool[i], model);
            else BuildModel(null, BuiltModels[(i - nPrefabs + BuiltModels.Length) % BuiltModels.Length]);
        }

        /// <summary>Replaces the plane's visual: a prefab model if given (falls back to the built biplane if it fails), otherwise the built biplane.</summary>
        void BuildModel(GameObject prefab, PlaneModel builtModel)
        {
            if (visual != null) Destroy(visual.gameObject);
            GameObject built = null;
            if (prefab != null)
            {
                try { built = BuildVisual(prefab, out prop, out bodyColor); ModelName = prefab.name; }
                catch (System.Exception e) { Debug.LogWarning($"[Redbaron] The model {prefab.name} could not be used for {pilotName}; using the built biplane. {e.Message}"); built = null; }
            }
            if (built == null)
            {
                built = BiplaneBuilder.Build(builtModel, material, out prop, out bodyColor);
                ModelName = builtModel.ToString();
                BodyRadius = CollisionRadius / 2f;
                if (HasPool)
                {
                    var mpb = new MaterialPropertyBlock();
                    Color tint = Color.Lerp(Color.white, team.FlagColor(), teamTint);
                    foreach (var r in built.GetComponentsInChildren<Renderer>()) { r.GetPropertyBlock(mpb); mpb.SetColor("_Tint", tint); r.SetPropertyBlock(mpb); }
                }
            }
            visual = built.transform;
            visual.SetParent(transform, false);
            visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // ---------------------------------------------------------------- Obstáculos
        /// <summary>Altura del obstáculo más alto bajo un punto (terreno, árbol, edificio, pared).</summary>
        public static float ObstacleHeight(float x, float z, out Collider col)
        {
            col = null;
            if (x < 0 || z < 0 || x > RedbaronMap.WorldW || z > RedbaronMap.WorldL) return RedbaronMap.WallHeight + 4;
            if (Physics.Raycast(new Vector3(x, 250, z), Vector3.down, out var hit, 400, obstacleMask, QueryTriggerInteraction.Ignore))
            { col = hit.collider; return hit.point.y; }
            return -3;
        }
        static float ObstacleHeight(float x, float z) => ObstacleHeight(x, z, out _);

        /// <summary>Like ObstacleHeight but with a 1.5 m sphere, so look-ahead samples do not slip between thin tree trunks.</summary>
        static float ObstacleHeightWide(float x, float z)
        {
            if (x < 0 || z < 0 || x > RedbaronMap.WorldW || z > RedbaronMap.WorldL) return RedbaronMap.WallHeight + 4;
            if (Physics.SphereCast(new Vector3(x, 250, z), 1.5f, Vector3.down, out var hit, 400, obstacleMask, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return -3;
        }

        float MaxObs(Vector3 p, float yaw, float d0, float d1, int steps)
        {
            float m = -3, s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            for (int k = 0; k <= steps; k++)
            {
                float d = d0 + (d1 - d0) * k / steps;
                float cx = p.x + s * d, cz = p.z + c * d;
                m = Mathf.Max(m, ObstacleHeightWide(cx, cz));
                m = Mathf.Max(m, ObstacleHeightWide(cx + c * 3, cz - s * 3));
                m = Mathf.Max(m, ObstacleHeightWide(cx - c * 3, cz + s * 3));
            }
            return m;
        }

        // ---------------------------------------------------------------- Ciclo
        Vector3 SpawnPoint(out float yaw)
        {
            Rect r = RedbaronMap.AirfieldWorldRect(team);
            bool right = homeSlot >= 0 ? (homeSlot % 2) == 1 : (spawnCounter % 2) == 1;
            yaw = right ? -Mathf.PI / 2 : Mathf.PI / 2;
            Vector3 p = new Vector3(right ? r.xMax - 8 : r.xMin + 8, 0, r.center.y);
            p.y = OnFlightPlane ? FlightAltitude : ObstacleHeight(p.x, p.z) + 1.5f;
            return p;
        }

        void Respawn()
        {
            Vector3 p = SpawnPoint(out float yaw);
            if (homeSlot < 0) spawnCounter++;
            Body.Teleport(p, yaw, OnFlightPlane ? 0f : 3f);
            if (HasPool) PickRandomModel();
            Hp = maxHp; HasBomb = true; cd = 1.5f; Alive = true; smokeT = 0;
            Ammo = maxAmmo; Bombs = maxBombs; bombCd = 0;
            Bank = 0; graceUntil = -10f;
            CurrentSpeed = Body.Speed;
            visual.gameObject.SetActive(true);
            lastYaw = Body.Yaw;
        }

        /// <summary>Back to full health, ammunition and bombs (the floating icon over your airfield).</summary>
        public void Refill()
        {
            Hp = maxHp; Ammo = maxAmmo; Bombs = maxBombs; HasBomb = true; smokeT = 0;
            refilledAt = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!Alive)
            {
                wait -= dt;
                if (wait <= 0)
                {
                    // Wait until the spawn point is clear, so nobody collides on arrival.
                    Vector3 sp = SpawnPoint(out _);
                    bool clear = true;
                    foreach (var o in All) if (o != this && o.Alive && (o.Body.Position - sp).magnitude < 14f) clear = false;
                    if (clear) Respawn(); else wait = 0.5f;
                }
                return;
            }
            t += dt;
            if (playerControlled) { UpdatePlayer(dt); return; }
            thinkT -= dt;
            if (thinkT <= 0) { thinkT = 0.1f; plan = Think(out enemy, out enemyDist, out hunting); }
            if (enemy != null && !enemy.Alive) enemy = null;
            float dist = enemy != null ? Vector3.Distance(enemy.Body.Position, Body.Position) : float.MaxValue;
            Vector3 pos = Body.Position;

            float wantVy = 0; // on the flight plane nobody climbs or dives
            if (!OnFlightPlane)
            {
                float need = Mathf.Max(MaxObs(pos, Body.Yaw, 0, Body.Speed * lookAhead, 6), MaxObs(pos, plan, 0, Body.Speed * lookAhead * 0.7f, 4));
                float targetY = Mathf.Max(need + clearance, 4.5f) + 1.2f * Mathf.Sin(t * 0.8f + GetHashCode());
                if (enemy != null && hunting && dist < 60) targetY = Mathf.Max(targetY, Mathf.Min(enemy.Body.Position.y, need + 14));
                wantVy = Mathf.Clamp((targetY - pos.y) * 1.6f, -6, maxClimb);
            }
            Body.Steer(plan, wantVy, dt);
            pos = Body.Position;

            // Visual
            turnRate = Mathf.DeltaAngle(lastYaw * Mathf.Rad2Deg, Body.Yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad / Mathf.Max(dt, 1e-4f);
            lastYaw = Body.Yaw;
            transform.position = pos;
            Vector3 fwd = new Vector3(Mathf.Sin(Body.Yaw), Body.VerticalSpeed / Mathf.Max(1, Body.Speed), Mathf.Cos(Body.Yaw));
            transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 0, Mathf.Clamp(-turnRate * 0.7f, -0.8f, 0.8f) * Mathf.Rad2Deg);
            if (prop != null) prop.Rotate(0, 0, dt * 2600f, Space.Self);

            // Collisions cost energy and push the plane clear
            if (CheckCollisions(pos)) return;

            // Bomb: reloaded over its own airfield, dropped over the enemy's. No computed release point:
            // the pilot lets go as the airfield comes under the nose, so the bomb may land long.
            var own = RedbaronMap.AirfieldWorldRect(team);
            var foe = RedbaronMap.AirfieldWorldRect(team.Enemy());
            if (!HasBomb && own.Contains(new Vector2(pos.x, pos.z))) HasBomb = true;
            Vector3 ahead = pos + new Vector3(fwd.x, 0, fwd.z).normalized * 6f;
            if (HasBomb && foe.Contains(new Vector2(ahead.x, ahead.z))) DropBomb(pos, fwd);

            // Ametralladora
            cd -= dt;
            if (enemy != null && dist < gunRange && cd <= 0 && Ammo > 0)
            {
                Vector3 to = (enemy.Body.Position - pos).normalized;
                if (Vector3.Dot(to, fwd.normalized) > Mathf.Cos(0.15f)) { Fire(pos, fwd.normalized); cd = fireCooldown; }
            }

            Smoke(pos, dt);
        }

        /// <summary>Humo si está tocado.</summary>
        void Smoke(Vector3 pos, float dt)
        {
            if (Hp >= maxHp) return;
            smokeT -= dt;
            if (smokeT <= 0)
            {
                float f = Hp / Mathf.Max(1f, maxHp);
                smokeT = f <= 0.25f ? 0.12f : f <= 0.5f ? 0.22f : 0.4f;
                Combat.Puff(pos, new Color(0.24f, 0.23f, 0.2f, 0.7f), 1.6f, 1.2f, 0.5f);
            }
        }

        // ---------------------------------------------------------------- Model from a prefab
        static readonly string[] DropNames = { "Sphere", "Main Camera", "Wheels" };
        static readonly string[] PropNames = { "propeller", "prop", "roter", "rotor", "helice" };

        /// <summary>Instantiates a plane model as a pure visual: nothing in it can move, collide, film or think on its own.
        /// It is scaled to visualSpan, centred on the pilot, tinted with the team colour, and its propeller is found to spin.</summary>
        GameObject BuildVisual(GameObject prefab, out Transform propeller, out Color color)
        {
            var stash = new GameObject("Redbaron_ModelStash"); stash.SetActive(false); // no Awake runs while stripping
            var m = Instantiate(prefab, stash.transform);
            foreach (var t in m.GetComponentsInChildren<Transform>(true))
                if (t != null && t != m.transform && System.Array.IndexOf(DropNames, t.name) >= 0) DestroyImmediate(t.gameObject); // children go with it
            foreach (var mb in m.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
            foreach (var c in m.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                if (c is Camera || c is AudioListener || c is TrailRenderer || c is ParticleSystem) { DestroyImmediate(c.gameObject); continue; }
                if (c is Collider || c is Animator || c is AudioSource) DestroyImmediate(c);
            }
            foreach (var rb in m.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);

            var holder = new GameObject("Modelo_" + prefab.name);
            m.transform.SetParent(holder.transform, false);
            m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
            Destroy(stash);

            var rs = m.GetComponentsInChildren<Renderer>(true);
            propeller = null; color = team.FlagColor();
            if (rs.Length == 0) return holder;
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float k = visualSpan > 0 ? visualSpan / Mathf.Max(0.01f, b.size.x) : visualScale;
            m.transform.localScale = m.transform.localScale * k;
            m.transform.localPosition = -b.center * k;
            BodyRadius = Mathf.Max(1.2f, 0.3f * Mathf.Max(b.size.x, b.size.z) * k);

            bool teamParts = PaintTeam(m, team, playerControlled, teamTint, out Color main);

            // Moving parts (Helice_*, Rotor*, Ala_I/Ala_D) get their own pivot and spin with GiroPiezas.
            if (RepivotMovingParts(m.transform)) holder.AddComponent<GiroPiezas>();
            else
                foreach (var t in m.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    foreach (var pn in PropNames) if (n.Contains(pn) && t.GetComponent<Renderer>() != null) { propeller = t; break; }
                    if (propeller != null) break;
                }
            color = teamParts ? main : Color.Lerp(rs[0].sharedMaterial != null && rs[0].sharedMaterial.HasProperty("_Color") ? rs[0].sharedMaterial.color : Color.gray, team.FlagColor(), 0.5f);
            return holder;
        }

        /// <summary>Side colours. Redbaron hangar ships (materials named by colour zone: "Equipo", "Lona", "EscA"…) are painted
        /// with their side's theme and insignia (TeamLivery); the player's plane gets the Red Baron theme. Other models get
        /// a light team tint instead. Returns whether the model has colour zones.</summary>
        public static bool PaintTeam(GameObject model, Team team, bool player, float teamTint, out Color main)
        {
            var rs = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (TeamLivery.HasZones(rs)) { main = TeamLivery.Apply(model, team, player); return true; }

            main = team.FlagColor();
            Color tint = Color.Lerp(Color.white, team.FlagColor(), teamTint);
            var mpb = new MaterialPropertyBlock();
            foreach (var r in rs)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mm = mats[i];
                    if (mm == null || !mm.HasProperty("_Color")) continue;
                    r.GetPropertyBlock(mpb, i); mpb.SetColor("_Color", mm.color * tint); r.SetPropertyBlock(mpb, i);
                }
            }
            return false;
        }

        /// <summary>A hangar ship as a display model (for the menu's cards): painted in the side's colours,
        /// with its propellers, rotors or wings moving. Centred on its bounds; nothing in it collides or thinks.</summary>
        public static GameObject Showcase(GameObject prefab, Team team, Transform parent)
        {
            var holder = new GameObject("Vitrina_" + prefab.name);
            holder.transform.SetParent(parent, false);
            var m = Instantiate(prefab, holder.transform);
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            var rs = m.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                m.transform.position -= b.center - holder.transform.position;
            }
            PaintTeam(m, team, false, 0.35f, out _);
            if (RepivotMovingParts(m.transform)) holder.AddComponent<GiroPiezas>();
            return holder;
        }

        /// <summary>OBJ parts share the model origin, so a propeller would spin around the ground. Each moving part gets
        /// a parent at its centre (a wing at its root), named as GiroPiezas expects; the mesh becomes "<name>_Malla".</summary>
        public static bool RepivotMovingParts(Transform root)
        {
            bool any = false;
            var parts = new List<Transform>(root.GetComponentsInChildren<Transform>(true));
            foreach (var t in parts)
            {
                if (t.name.EndsWith("_Malla")) continue;
                bool wing = t.name == "Ala_I" || t.name == "Ala_D";
                if (!wing && !t.name.StartsWith("Helice_") && !t.name.StartsWith("Rotor")) continue;
                any = true;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue; // already has its own pivot (e.g. a glTF node)
                Bounds pb = r.bounds;
                Vector3 pivot = pb.center;
                if (wing) pivot.x = Mathf.Abs(pb.min.x - root.parent.position.x) < Mathf.Abs(pb.max.x - root.parent.position.x) ? pb.min.x : pb.max.x;
                var pv = new GameObject(t.name).transform;
                pv.SetParent(t.parent, false);
                pv.position = pivot; pv.rotation = t.parent.rotation;
                t.SetParent(pv, true);
                t.name += "_Malla";
            }
            return any;
        }

        // ---------------------------------------------------------------- Player flight
        void UpdatePlayer(float dt)
        {
            if (PauseMenu.IsPaused) return; // no flying, firing or bombing while the pause panel is open
            // Arrow keys also pan the Quick RTS camera, so in the free camera (R) only WASD fly the plane.
            var cams = RedbaronCameras.Instance;
            bool arrows = cams == null || cams.mode != RedbaronCameras.Mode.Free;
            float h = 0, v = 0;
            if (Input.GetKey(KeyCode.D) || (arrows && Input.GetKey(KeyCode.RightArrow))) h += 1;
            if (Input.GetKey(KeyCode.A) || (arrows && Input.GetKey(KeyCode.LeftArrow))) h -= 1;
            if (Input.GetKey(KeyCode.W) || (arrows && Input.GetKey(KeyCode.UpArrow))) v += 1;
            if (Input.GetKey(KeyCode.S) || (arrows && Input.GetKey(KeyCode.DownArrow))) v -= 1;
            // On-screen touch controls (Lean Touch) add to the keyboard.
            h = Mathf.Clamp(h + MobileFlightControls.Roll, -1, 1);
            v = Mathf.Clamp(v + MobileFlightControls.Throttle, -1, 1);
            bool trigger = Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.Space) || MobileFlightControls.Fire;

            Vector3 pos = Body.Position;
            CurrentSpeed = Mathf.Clamp(CurrentSpeed + v * throttleRate * dt, minSpeed, maxSpeed);

            // Only your keys change the bank; no assist and no automatic course change.
            // (As specified: when you let go below autoLevelBelow degrees, the wings level themselves.)
            if (h != 0) Bank = Mathf.Repeat(Bank + h * rollRate * dt + 180f, 360f) - 180f;
            else if (Mathf.Abs(Bank) < autoLevelBelow) Bank = Mathf.MoveTowards(Bank, 0, 20f * dt);

            float yawRate = maxTurnRate * Mathf.Deg2Rad * Mathf.Sin(Bank * Mathf.Deg2Rad);
            if (Body is KinematicFlight kf) { kf.speed = CurrentSpeed; kf.turnRate = Mathf.Max(kf.turnRate, maxTurnRate * Mathf.Deg2Rad * 1.05f); }

            // The altitude never changes on the flight plane. Without one, the AI pilots' terrain following is used.
            float wantVy = 0;
            if (!OnFlightPlane)
            {
                float look = CurrentSpeed * lookAhead;
                float need = Mathf.Max(MaxObs(pos, Body.Yaw, 0, look, 6), MaxObs(pos, Body.Yaw + yawRate * lookAhead * 0.6f, 0, look * 0.7f, 4));
                wantVy = Mathf.Clamp((Mathf.Max(need + clearance, 4.5f) - pos.y) * 1.6f, -6, maxClimb);
            }
            Body.Steer(Body.Yaw + yawRate * dt, wantVy, dt);
            pos = Body.Position;

            transform.position = pos;
            Vector3 fwd = new Vector3(Mathf.Sin(Body.Yaw), Body.VerticalSpeed / Mathf.Max(1, Body.Speed), Mathf.Cos(Body.Yaw));
            transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 0, -Bank);
            if (prop != null) prop.Rotate(0, 0, dt * 2600f, Space.Self);

            if (CheckCollisions(pos)) return;

            cd -= dt;
            bombCd -= dt;
            if ((Input.GetKeyDown(KeyCode.X) || MobileFlightControls.BombPressed) && Bombs > 0 && bombCd <= 0)
            {
                DropBomb(pos, fwd);
                Bombs--; bombCd = playerBombCooldown;
            }
            // No aim assist: the rounds leave along the nose.
            if (trigger && cd <= 0 && Ammo > 0)
            {
                Fire(pos, fwd.normalized, 0.018f);
                cd = playerFireCooldown;
            }
            Smoke(pos, dt);
        }

        float Think(out BiplaneAI enemy, out float best, out bool hunting)
        {
            enemy = null; best = float.MaxValue; hunting = false;
            Vector3 pos = Body.Position;
            foreach (var e in All)
            {
                if (e == this || !e.Alive || e.team == team) continue;
                float d = Vector3.Distance(e.Body.Position, pos);
                if (d < best) { best = d; enemy = e; }
            }
            Vector3 tgt;
            var pickup = AirPickup.For(team);
            if (pickup != null && (Ammo <= 0 || Hp < maxHp * 0.35f)) tgt = pickup.transform.position; // refuel first
            else if (enemy != null && (best < 80 || !HasBomb))
            {
                tgt = enemy.Body.Position; // no computed lead: the pilot points at where the enemy is now
                hunting = true;
            }
            else if (HasBomb) { var r = RedbaronMap.AirfieldWorldRect(team.Enemy()); tgt = new Vector3(r.center.x, 0, r.center.y); }
            else { var r = RedbaronMap.AirfieldWorldRect(team); tgt = new Vector3(r.center.x, 0, r.center.y); }

            float want = Mathf.Atan2(tgt.x - pos.x, tgt.z - pos.z);
            // Empuje para no salirse del cañón
            float m = 16, W = RedbaronMap.WorldW, L = RedbaronMap.WorldL, px = 0, pz = 0;
            if (pos.x > W - m) px -= (pos.x - W + m) / m; if (pos.x < m) px += (m - pos.x) / m;
            if (pos.z > L - m) pz -= (pos.z - L + m) / m; if (pos.z < m) pz += (m - pos.z) / m;
            if (px != 0 || pz != 0) want = Mathf.Atan2(Mathf.Sin(want) + px * 2.5f, Mathf.Cos(want) + pz * 2.5f);

            if (OnFlightPlane) return SafestHeading(pos, want);

            // Elegir el rumbo más directo que no lleve contra algo demasiado alto
            float bestYaw = want, bestScore = float.MaxValue;
            float reach = OnFlightPlane ? pos.y : pos.y + maxClimb * (35f / Mathf.Max(1, Body.Speed)) * 0.8f;
            foreach (float off in new[] { 0f, 0.4f, -0.4f, 0.8f, -0.8f, 1.3f, -1.3f, 2.0f, -2.0f })
            {
                float yaw = want + off;
                float blocked = Mathf.Max(0, MaxObs(pos, yaw, 8, 35, 6) + 1.5f - reach);
                float turnCost = Mathf.Abs(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, Body.Yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad) * 0.3f;
                float sc = Mathf.Abs(off) + blocked * 1.2f + turnCost;
                if (sc < bestScore) { bestScore = sc; bestYaw = yaw; }
            }
            return bestYaw;
        }

        // ---------------------------------------------------------------- Player HUD
        GUIStyle hud;
        static Texture2D triangleTex;
        /// <summary>White downward-pointing triangle, tinted when drawn.</summary>
        public static Texture2D TriangleTex
        {
            get
            {
                if (triangleTex != null) return triangleTex;
                const int W = 36, H = 28;
                triangleTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float half = (W / 2f) * y / (H - 1f); // texture row 0 is the bottom (the tip)
                        float a = Mathf.Clamp01(half + 0.5f - Mathf.Abs(x + 0.5f - W / 2f));
                        triangleTex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                triangleTex.Apply();
                return triangleTex;
            }
        }
        void OnGUI()
        {
            if (!playerControlled || PauseMenu.IsPaused) return; // IMGUI draws over uGUI, so hide bars and sights behind the pause panel
            float k = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float w = Screen.width / k, hgt = Screen.height / k;
            if (hud == null) hud = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.95f, 0.92f, 0.84f) } };
            string line = !Alive ? $"{pilotName} derribado · reapareces en {RespawnIn:0} s"
                : $"{pilotName} · {(ModelName ?? "").Replace('_', ' ')} · {CurrentSpeed:0} m/s · inclinación {Bank:0}° · vida {Hp:0}/{maxHp:0}"
                  + (Time.time - refilledAt < 1.5f ? " · RECARGADO" : "");
            GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(w / 2 - 330, hgt - 58, 660, 44), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(w / 2 - 330, hgt - 58, 660, 24), line, hud);
            if (MobileFlightControls.Active) GUI.Label(new Rect(w / 2 - 330, hgt - 38, 660, 20), "Joystick: ↔ alabear · ↕ velocidad · FUEGO · BOMBA · CÁM cámaras", hud);
            else GUI.Label(new Rect(w / 2 - 330, hgt - 38, 660, 20), "← → alabear · ↑ ↓ velocidad · Z / Espacio disparar · X bomba · " + (RedbaronCameras.Instance != null ? RedbaronCameras.Instance.KeysText : "") + " cámaras", hud);
            // Over every aircraft: an energy bar; over the others, a green (friend) or red (foe) triangle.
            if (Camera.main != null)
                foreach (var o in All)
                {
                    if (!o.Alive) continue;
                    bool cockpit = RedbaronCameras.Instance != null && RedbaronCameras.Instance.mode == RedbaronCameras.Mode.Cockpit;
                    if (o == this && cockpit)
                    {
                        // From the cockpit: a small sight where the nose points, instead of your own bar.
                        Vector3 nose = Camera.main.WorldToScreenPoint(Body.Position + transform.forward * 40f);
                        if (nose.z > 0)
                        {
                            float nx = nose.x / k, ny = hgt - nose.y / k;
                            GUI.color = new Color(1f, 1f, 1f, 0.8f);
                            GUI.DrawTexture(new Rect(nx - 12, ny - 1, 8, 2), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(nx + 4, ny - 1, 8, 2), Texture2D.whiteTexture);
                            GUI.DrawTexture(new Rect(nx - 1, ny - 12, 2, 8), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(nx - 1, ny + 4, 2, 8), Texture2D.whiteTexture);
                            GUI.color = Color.white;
                        }
                        continue;
                    }
                    Vector3 sp = Camera.main.WorldToScreenPoint(o.Body.Position + Vector3.up * (o.BodyRadius * 0.6f + 2.2f));
                    if (sp.z <= 0) continue;
                    float x = sp.x / k, y = hgt - sp.y / k;
                    float f = Mathf.Clamp01(o.Hp / Mathf.Max(1f, o.maxHp));
                    GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(x - 21, y - 3, 42, 6), Texture2D.whiteTexture);
                    GUI.color = f > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 0.3f), (f - 0.5f) * 2f) : Color.Lerp(new Color(0.95f, 0.2f, 0.15f), new Color(1f, 0.85f, 0.2f), f * 2f);
                    GUI.DrawTexture(new Rect(x - 20, y - 2, 40 * f, 4), Texture2D.whiteTexture);
                    if (o != this)
                    {
                        GUI.color = o.team == team ? new Color(0.25f, 0.95f, 0.35f, 0.95f) : new Color(1f, 0.18f, 0.12f, 0.95f);
                        GUI.DrawTexture(new Rect(x - 9, y - 19, 18, 14), TriangleTex);
                    }
                    GUI.color = Color.white;
                }
            if (Alive) DrawAmmoCounter(w, hgt);
            // No target box and no bomb sight: there is no aim assist of any kind.
        }

        // ---------------------------------------------------------------- Not crashing comes first
        /// <summary>Size used for collisions by the built biplanes (m): two planes closer than their radii collide, whatever their side.</summary>
        public static float CollisionRadius = 4.5f;
        static readonly float[] HeadingOffsets = { 0f, 0.25f, -0.25f, 0.5f, -0.5f, 0.8f, -0.8f, 1.1f, -1.1f, 1.5f, -1.5f, 2.0f, -2.0f, 2.6f, -2.6f };
        static readonly float[] HardTurns = { 0f, 0.7f, -0.7f, 1.4f, -1.4f, 2.2f, -2.2f, 3.1f, -3.1f };

        // Obstacle heights on a 1 m grid, so the AI can try many paths cheaply. Refreshed around a tree when it falls.
        static float[] obstacleGrid; static int gridW, gridL;
        public static void ResetObstacleGrid() { obstacleGrid = null; }

        static void EnsureObstacleGrid()
        {
            if (obstacleGrid != null) return;
            gridW = Mathf.CeilToInt(RedbaronMap.WorldW) + 1; gridL = Mathf.CeilToInt(RedbaronMap.WorldL) + 1;
            obstacleGrid = new float[gridW * gridL];
            for (int i = 0; i < gridW; i++) for (int j = 0; j < gridL; j++) obstacleGrid[i * gridL + j] = ObstacleHeightWide(i, j);
        }

        public static void RefreshObstacleGrid(Vector3 c, float radius)
        {
            if (obstacleGrid == null) return;
            int r = Mathf.CeilToInt(radius);
            for (int i = Mathf.Max(0, (int)c.x - r); i <= Mathf.Min(gridW - 1, (int)c.x + r); i++)
                for (int j = Mathf.Max(0, (int)c.z - r); j <= Mathf.Min(gridL - 1, (int)c.z + r); j++)
                    obstacleGrid[i * gridL + j] = ObstacleHeightWide(i, j);
        }

        static float GridHeight(float x, float z)
        {
            if (x < 0 || z < 0 || x > RedbaronMap.WorldW || z > RedbaronMap.WorldL) return RedbaronMap.WallHeight + 4;
            EnsureObstacleGrid();
            return obstacleGrid[Mathf.Clamp(Mathf.RoundToInt(x), 0, gridW - 1) * gridL + Mathf.Clamp(Mathf.RoundToInt(z), 0, gridL - 1)];
        }

        // ---------------------------------------------------------------- Collisions
        Vector3 Forward => new Vector3(Mathf.Sin(Body.Yaw), 0, Mathf.Cos(Body.Yaw));

        /// <summary>Touching an obstacle taller than the plane (tree crown, mountain, building, the rock wall) or another
        /// plane costs energy, pushes the plane clear and gives it a moment of immunity to further collisions.
        /// While immune it is still pushed out of anything it touches, so it never passes through. Returns true if it went down.</summary>
        bool CheckCollisions(Vector3 pos)
        {
            float ground = ObstacleHeight(pos.x, pos.z, out Collider under);
            if (pos.y - 0.42f < ground)
            {
                Push(AwayFromObstacle(pos));
                if (!CollisionImmune && Collide(Describe(under, pos))) return true;
            }
            foreach (var o in All)
            {
                if (o == this || !o.Alive) continue;
                Vector3 d = pos - o.Body.Position; d.y = 0;
                if (d.magnitude > BodyRadius + o.BodyRadius) continue;
                Vector3 n = d.sqrMagnitude > 1e-4f ? d.normalized : -Forward;
                Push(n); o.Push(-n); // both planes are thrown apart
                if (!o.CollisionImmune) o.Collide("el " + pilotName);
                if (!CollisionImmune && Collide("el " + o.pilotName)) return true;
            }
            return false;
        }

        void Push(Vector3 away)
        {
            if (Body is KinematicFlight kf) kf.Push(away, pushSpeed, pushTime);
        }

        /// <summary>One collision: energy lost and the immunity starts. Returns true if the plane went down.</summary>
        bool Collide(string what)
        {
            graceUntil = Time.time + collisionGrace;
            Hp = Mathf.Max(0, Hp - collisionDamage);
            Vector3 p = Body.Position;
            Sfx.PlaneHit(p); Sfx.Thud(p, 0.5f);
            Combat.Puff(p, new Color(0.45f, 0.4f, 0.32f, 0.85f), 0.6f, 2.2f, 0.4f);
            if (Hp > 0) { RedbaronGame.Log($"{pilotName} ({team.Name()}) choca contra {what} y pierde {collisionDamage:0} de energía"); return false; }
            if (RedbaronGame.Instance != null) RedbaronGame.Instance.OnPlaneCrash(pilotName, team, what);
            GoDown();
            return true;
        }

        /// <summary>Horizontal direction out of the obstacle: towards the free samples around the plane and away from
        /// the blocked ones. Boxed in on every side, it backs out the way it came.</summary>
        Vector3 AwayFromObstacle(Vector3 pos)
        {
            float clear = pos.y - 0.42f, r = BodyRadius + 1.5f;
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI / 6f;
                Vector3 d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                sum += ObstacleHeight(pos.x + d.x * r, pos.z + d.z * r) < clear ? d : -d;
            }
            return sum.sqrMagnitude > 0.04f ? sum.normalized : -Forward;
        }

        /// <summary>AI heading on the flight plane. Each candidate is flown ahead for 2.1 s at the real turn rate.
        /// Obstacles taller than the flight plane (tree crowns, mountains, walls) and other planes weigh far more than
        /// getting to the target, so the pilot only chases or attacks along paths that do not crash.</summary>
        float SafestHeading(Vector3 pos, float want)
        {
            float turn = Body is KinematicFlight kf ? kf.turnRate : 1.15f;
            float W = RedbaronMap.WorldW, L = RedbaronMap.WorldL;
            float bestYaw = want, bestScore = float.MaxValue;
            // Candidates around the target direction, plus hard turns from the current heading (up to a U-turn).
            for (int c = 0; c < HeadingOffsets.Length + HardTurns.Length; c++)
            {
                bool aroundTarget = c < HeadingOffsets.Length;
                float target = aroundTarget ? want + HeadingOffsets[c] : Body.Yaw + HardTurns[c - HeadingOffsets.Length];
                float blocked = 0, traffic = 0, edge = 0, yaw = Body.Yaw, step = 0.15f;
                Vector3 p = pos;
                for (int k = 1; k <= 20; k++) // 3 s ahead: long enough to complete a U-turn
                {
                    yaw += Mathf.Clamp(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad, -turn * step, turn * step);
                    float sn = Mathf.Sin(yaw), cs = Mathf.Cos(yaw);
                    p += new Vector3(sn, 0, cs) * Body.Speed * step;
                    float h = Mathf.Max(GridHeight(p.x, p.z), Mathf.Max(GridHeight(p.x + cs * 2.5f, p.z - sn * 2.5f), GridHeight(p.x - cs * 2.5f, p.z + sn * 2.5f)));
                    float over = h + 1.5f - pos.y;
                    if (over > 0) blocked += over * (3f - k * 0.1f); // the nearer, the worse
                    float dEdge = Mathf.Min(Mathf.Min(p.x, W - p.x), Mathf.Min(p.z, L - p.z));
                    if (dEdge < 12f) edge += 12f - dEdge; // keep clear of the canyon walls
                    foreach (var o in All)
                    {
                        if (o == this || !o.Alive) continue;
                        Vector3 ov = new Vector3(Mathf.Sin(o.Body.Yaw), 0, Mathf.Cos(o.Body.Yaw)) * o.Body.Speed;
                        float d = (o.Body.Position + ov * (k * step) - p).magnitude;
                        if (d < 11f) traffic += 11f - d;
                    }
                }
                float offTarget = Mathf.Abs(Mathf.DeltaAngle(target * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                float turnCost = Mathf.Abs(Mathf.DeltaAngle(target * Mathf.Rad2Deg, Body.Yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad) * 0.3f;
                float score = offTarget + turnCost + blocked * 60f + traffic * 25f + edge * 6f;
                if (score < bestScore) { bestScore = score; bestYaw = target; }
            }
            return bestYaw;
        }

        // ---------------------------------------------------------------- Ammunition counter
        GUIStyle ammoSmall, ammoBig;

        /// <summary>Right side of the screen: rounds left (number and a 20-segment gauge) and bombs left (dots).</summary>
        void DrawAmmoCounter(float w, float h)
        {
            if (ammoSmall == null)
            {
                ammoSmall = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.85f, 0.82f, 0.74f) } };
                ammoBig = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            }
            float pw = 150, ph = 92, x = w - pw - 16, y = h * 0.34f;
            float f = maxAmmo > 0 ? (float)Ammo / maxAmmo : 0;
            Color c = Ammo == 0 ? new Color(1f, 0.25f, 0.2f, Mathf.PingPong(Time.unscaledTime * 2f, 1f) * 0.7f + 0.3f)
                    : f <= 0.25f ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.88f, 0.45f);
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(x, y, pw, ph), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(x + 8, y + 4, pw, 16), "MUNICIÓN", ammoSmall);
            ammoBig.normal.textColor = c;
            GUI.Label(new Rect(x + 8, y + 16, 80, 34), Ammo.ToString(), ammoBig);
            GUI.Label(new Rect(x + 72, y + 30, 70, 16), "/ " + maxAmmo, ammoSmall);
            const int segs = 20;
            float sw = (pw - 16) / segs;
            int lit = Mathf.CeilToInt(f * segs);
            for (int i = 0; i < segs; i++)
            {
                GUI.color = i < lit ? c : new Color(1, 1, 1, 0.15f);
                GUI.DrawTexture(new Rect(x + 8 + i * sw, y + 52, sw - 1.5f, 8), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8, y + 66, 60, 16), "BOMBAS", ammoSmall);
            for (int i = 0; i < maxBombs; i++)
            {
                GUI.color = i < Bombs ? new Color(1f, 0.75f, 0.3f) : new Color(1, 1, 1, 0.18f);
                GUI.DrawTexture(new Rect(x + 64 + i * 16, y + 69, 11, 11), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            if (Ammo == 0)
            {
                ammoSmall.normal.textColor = new Color(1f, 0.45f, 0.4f);
                GUI.Label(new Rect(x - 40, y + ph + 2, pw + 40, 16), "RECARGA EN TU AERÓDROMO", ammoSmall);
                ammoSmall.normal.textColor = new Color(0.85f, 0.82f, 0.74f);
            }
        }

        // ---------------------------------------------------------------- Daño
        public void TakeHit(BiplaneAI shooter) => TakeHit(shooter.pilotName, shooter.team);

        /// <summary>A round or a flak burst: one energy point. by/byTeam are credited if the plane goes down.</summary>
        public void TakeHit(string by, Team byTeam)
        {
            if (!Alive) return;
            Hp--;
            Sfx.PlaneHit(Body.Position);
            Combat.Puff(Body.Position, new Color(0.94f, 0.75f, 0.38f, 0.9f), 0.35f, 1.5f, 0);
            if (Hp <= 0)
            {
                if (RedbaronGame.Instance != null) RedbaronGame.Instance.OnPlaneKill(by, byTeam, pilotName, team);
                GoDown();
            }
        }

        void GoDown()
        {
            Combat.Explosion(Body.Position, false);
            Debris.Spawn(Body.Position, bodyColor, material);
            Alive = false; wait = respawnDelay;
            visual.gameObject.SetActive(false);
        }

        /// <summary>What the plane ran into, for the battle log. A tree it hits falls.</summary>
        string Describe(Collider under, Vector3 pos)
        {
            string what = "el suelo";
            bool outside = pos.x < 1 || pos.z < 1 || pos.x > RedbaronMap.WorldW - 1 || pos.z > RedbaronMap.WorldL - 1;
            if (under == null && outside) what = "la pared de roca";
            if (under != null)
            {
                var tree = under.GetComponentInParent<TreeDestructible>();
                if (tree != null) { what = "un árbol"; tree.Fell(); }
                else if (under.GetComponentInParent<CommandPost>() != null || under.GetComponentInParent<Bunker>() != null || under.GetComponentInParent<Airfield>() != null) what = "un edificio";
                else if (under.name == "Pared" || pos.x < 1 || pos.z < 1 || pos.x > RedbaronMap.WorldW - 1 || pos.z > RedbaronMap.WorldL - 1) what = "la pared de roca";
                else
                {
                    Vector2 d = RedbaronMap.ToDesign(pos);
                    if (RedbaronMap.Sample(d.x, d.y).mountain > 2) what = "la montaña";
                }
            }
            return what;
        }

        void Fire(Vector3 pos, Vector3 dir, float spread = 0.045f)
        {
            int n = Mathf.Min(2, Ammo);
            Ammo -= n;
            if (n > 0) Sfx.PlaneGun(pos, playerControlled);
            for (int k = 0; k < n; k++)
            {
                Vector3 d = dir + Random.insideUnitSphere * spread;
                if (OnFlightPlane) d.y = 0; // air-to-air rounds stay on the flight plane
                d.Normalize();
                Projectiles.Bullet(this, pos + d * (3 + k * 1.2f), d * (bulletSpeed + Body.Speed));
            }
        }

        void DropBomb(Vector3 pos, Vector3 fwd)
        {
            HasBomb = false;
            Vector3 v = new Vector3(fwd.x, 0, fwd.z).normalized * Body.Speed * 0.9f + Vector3.up * Body.VerticalSpeed;
            Projectiles.Bomb(this, pos - Vector3.up * 0.6f, v);
        }
    }

    /// <summary>Modelo de vuelo sencillo del prototipo: giro y ascenso limitados, velocidad constante.</summary>
    public class KinematicFlight : MonoBehaviour, IFlightBody
    {
        public float speed = 24f * 0.7f;
        public float turnRate = 1.15f;     // rad/s
        public float vyAccel = 14f;        // m/s²
        Vector3 pos; float yaw, vy;
        Vector3 pushDir; float pushSpeed, pushT, pushT0;

        public Vector3 Position => pos;
        public float Yaw => yaw;
        public float VerticalSpeed => vy;
        public float Speed => speed;

        public void Teleport(Vector3 p, float y, float v) { pos = p; yaw = y; vy = v; pushT = 0; }

        /// <summary>A collision push: for a moment the plane is thrown along dir (horizontal) and cannot move against it.
        /// The heading is not changed; the pilot has to turn away.</summary>
        public void Push(Vector3 dir, float speed, float time)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f || time <= 0) return;
            pushDir = dir.normalized; pushSpeed = speed; pushT = pushT0 = time;
        }

        public void Steer(float desiredYaw, float desiredVy, float dt)
        {
            float dy = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, desiredYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            yaw += Mathf.Clamp(dy, -turnRate * dt, turnRate * dt);
            if (!float.IsNaN(BiplaneAI.FlightAltitude)) { vy = 0; pos.y = BiplaneAI.FlightAltitude; }
            else vy += Mathf.Clamp(desiredVy - vy, -vyAccel * dt, vyAccel * dt);
            Vector3 v = new Vector3(Mathf.Sin(yaw) * speed, vy, Mathf.Cos(yaw) * speed);
            if (pushT > 0)
            {
                float into = Vector3.Dot(v, pushDir);
                if (into < 0) v -= pushDir * into; // no motion back into what was hit while the push lasts
                v += pushDir * pushSpeed * (pushT / pushT0);
                pushT -= dt;
            }
            pos += v * dt;
        }
    }

    /// <summary>Balas trazadoras y bombas de los aviones.</summary>
    public class Projectiles : MonoBehaviour
    {
        class Shot { public BiplaneAI from; public Vector3 p, v; public float life; }
        class BombShot { public BiplaneAI from; public Transform t; public Vector3 v; }
        static Projectiles inst;
        readonly List<Shot> shots = new List<Shot>();
        readonly List<BombShot> bombs = new List<BombShot>();
        static Material bombMat;

        static Projectiles I
        {
            get
            {
                if (inst == null) inst = new GameObject("Redbaron_Proyectiles").AddComponent<Projectiles>();
                return inst;
            }
        }

        public static void Bullet(BiplaneAI from, Vector3 p, Vector3 v) => I.shots.Add(new Shot { from = from, p = p, v = v, life = 1.2f });

        [Tooltip("Air-to-air rounds: hit radius around a plane (m), tracer width and tracer length (m).")]
        public static float BulletHitRadius = 2.4f, BulletWidth = 0.22f, BulletTrail = 3.5f;

        public static void Bomb(BiplaneAI from, Vector3 p, Vector3 v)
        {
            var acc = new MeshAccumulator();
            acc.AddCylinder(new Vector3(0, 0, -0.45f), new Vector3(0, 0, 0.45f), 0.16f, 0.16f, 6, new Color32(0x2e, 0x2a, 0x24, 255));
            var go = acc.ToObject("Bomba", I.transform, from.material, false);
            go.transform.position = p;
            I.bombs.Add(new BombShot { from = from, t = go.transform, v = v });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                Vector3 p0 = s.p;
                s.p += s.v * dt; s.life -= dt;
                Combat.Tracer(s.p - s.v.normalized * BulletTrail, s.p, 0.035f, BulletWidth);
                bool ground = s.p.y < BiplaneAI.ObstacleHeight(s.p.x, s.p.z, out _);
                bool gone = s.life <= 0 || ground;
                if (ground) Sfx.Thud(s.p, 0.22f); // a round striking a tree, a rock or the ground
                if (!gone)
                    foreach (var e in BiplaneAI.All)
                    {
                        if (!e.Alive || e.team == s.from.team) continue;
                        Vector3 ep = e.Body.Position;
                        Vector3 seg = s.p - p0;
                        float tt = Mathf.Clamp01(Vector3.Dot(ep - p0, seg) / Mathf.Max(1e-4f, seg.sqrMagnitude));
                        if ((p0 + seg * tt - ep).magnitude < Mathf.Max(BulletHitRadius, e.BodyRadius)) { e.TakeHit(s.from); gone = true; break; }
                    }
                // The player's guns also strafe enemy infantry; own troops are never hit.
                if (!gone && s.from.playerControlled && float.IsNaN(BiplaneAI.FlightAltitude))
                {
                    var hs = Health.All;
                    for (int k = 0; k < hs.Count; k++)
                    {
                        var hh = hs[k];
                        if (!hh.Alive || hh.kind != TargetKind.Soldier || hh.team == s.from.team) continue;
                        Vector3 ap = hh.AimPoint, seg = s.p - p0;
                        float tt = Mathf.Clamp01(Vector3.Dot(ap - p0, seg) / Mathf.Max(1e-4f, seg.sqrMagnitude));
                        if ((p0 + seg * tt - ap).magnitude < 0.9f) { hh.Damage(1, s.from.gameObject); gone = true; break; }
                    }
                }
                if (gone) shots.RemoveAt(i);
            }
            for (int i = bombs.Count - 1; i >= 0; i--)
            {
                var b = bombs[i];
                b.v += Physics.gravity * dt;
                b.t.position += b.v * dt;
                if (b.v.sqrMagnitude > 0.01f) b.t.rotation = Quaternion.LookRotation(b.v);
                float g = BiplaneAI.ObstacleHeight(b.t.position.x, b.t.position.z, out _);
                if (b.t.position.y <= g)
                {
                    Vector3 p = new Vector3(b.t.position.x, g, b.t.position.z);
                    Combat.Explosion(p, true);
                    if (RedbaronMap.AirfieldWorldRect(b.from.team.Enemy()).Contains(new Vector2(p.x, p.z)) && RedbaronGame.Instance != null)
                        RedbaronGame.Instance.OnBombHit(b.from.pilotName, b.from.team);
                    foreach (var h in new List<Health>(Health.All))
                    {
                        if (!h.Alive || h.team == b.from.team) continue; // no friendly fire
                        float d = Vector3.Distance(h.transform.position, p);
                        if (h.kind == TargetKind.Soldier && d < 6) h.Damage(99, b.from.gameObject);
                        else if (h.kind == TargetKind.Bunker && d < 8) h.Damage(6, b.from.gameObject);
                        else if (h.kind == TargetKind.CommandPost && d < 9) h.Damage(12, b.from.gameObject);
                    }
                    TreeDestructible.FellAround(p, 4f);
                    Destroy(b.t.gameObject);
                    bombs.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>Restos de un avión derribado que caen y se quedan unos segundos.</summary>
    public class Debris : MonoBehaviour
    {
        Vector3 v, spin; float life = 14f; bool landed;

        public static void Spawn(Vector3 p, Color c, Material mat)
        {
            for (int i = 0; i < 9; i++)
            {
                var acc = new MeshAccumulator();
                acc.AddBox(Vector3.zero, new Vector3(0.6f, 0.15f, 0.9f), i % 3 == 0 ? (Color)new Color32(0x3b, 0x2f, 0x22, 255) : c);
                var go = acc.ToObject("Restos", null, mat, false);
                go.transform.position = p;
                var d = go.AddComponent<Debris>();
                d.v = new Vector3(Random.Range(-7f, 7f), Random.Range(5f, 12f), Random.Range(-7f, 7f));
                d.spin = Random.insideUnitSphere * 460f;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0) { Destroy(gameObject); return; }
            if (landed) return;
            v += Physics.gravity * dt;
            transform.position += v * dt;
            transform.Rotate(spin * dt);
            float g = BiplaneAI.ObstacleHeight(transform.position.x, transform.position.z, out _);
            if (transform.position.y <= g) { landed = true; transform.position = new Vector3(transform.position.x, g + 0.05f, transform.position.z); }
        }
    }
}
