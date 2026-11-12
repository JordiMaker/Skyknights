using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Redbaron
{
    /// <summary>
    /// IA de infantería del prototipo web: avanza por su línea, busca cobertura al ver enemigos,
    /// salta de cobertura en cobertura, carga si su grupo es superior y ataca casamatas y el puesto de mando.
    ///
    /// Para los soldados del jugador (Entente) obedece las órdenes de Quick RTS:
    /// cualquier destino que Quick RTS ponga en el NavMeshAgent se trata como orden; al llegar,
    /// el soldado se queda en esa zona, se cubre y dispara. También se puede llamar a OrderMove().
    ///
    /// The same AI drives every unit type (UnitType): riflemen, machine gunners, grenadiers, flamethrowers, sappers,
    /// tanks and mechs. Each fires its own weapon. Short-range units close in until their weapon reaches, and vehicles
    /// never take cover. Each weapon has its targets: bullets go for infantry (they barely scratch armour), grenades and
    /// flames for men in cover, cannons for vehicles and buildings. A sapper picks the nearest enemy building, sets a
    /// charge with a fuse against it and runs clear; with no building near it sticks the charge on a vehicle.
    /// The anti-aircraft mech (WeaponKind.Flak) fires at enemy planes in reach before anything on the ground.
    ///
    /// Tactics on top of that:
    /// - Suppression: fire landing around a soldier (machine guns most of all) spoils its aim, sends it to cover and,
    ///   when heavy, pins it there. It wears off, faster in cover.
    /// - Squads (Squad): the men of one wave and lane share a target, walk behind a friendly vehicle, and fall back and regroup when beaten.
    /// - Danger: soldiers try to jump clear of a grenade or bomb about to land next to them.
    /// - Targets: wounded and exposed enemies and dangerous weapons come first; grenadiers and flamethrowers go for men in cover.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class SoldierAI : MonoBehaviour
    {
        public enum State { Advance, ToCover, InCover, Hold, Charge, Order, Dead, Assault, Fallback, Evade }
        public static readonly List<SoldierAI> All = new List<SoldierAI>();

        [Header("Bando")]
        public Team team = Team.Centrales;
        public int lane;
        [Tooltip("Lo controla el jugador con Quick RTS.")]
        public bool playerControlled;
        [Tooltip("Si está activo avanza solo por su línea hacia la base enemiga.")]
        public bool autoAdvance = true;

        public enum Stance { Aggressive, Defensive, HoldPosition }
        [Tooltip("How the player's units react to enemies. Aggressive: goes after what it sees (up to 35 m from the spot it holds) and comes back. " +
                 "Defensive: fights only what its weapon reaches from where it is. Hold position: never moves.")]
        public Stance stance = Stance.Aggressive;

        [Header("Combate (a soldier spawned without a UnitType)")]
        public float walkSpeed = 2.6f;
        public float runSpeed = 3.6f;
        public float rifleRange = 50f;
        public float rifleCooldown = 3.25f; // 20 % slower than the first version's 2.6 s
        public float baseHit = 0.55f;
        public float coverSearchRadius = 16f;
        [Tooltip("Radio alrededor del punto de una orden en el que el soldado busca cobertura.")]
        public float orderCoverRadius = 6f;

        [Header("Animator (Toon Soldiers WW2 demo)")]
        [Tooltip("Parámetro float de velocidad. Déjalo vacío si el controlador no lo tiene.")]
        public string speedParam = "Speed";
        public string crouchParam = "Crouch";
        public string shootTrigger = "Shoot";
        public string dieTrigger = "Die";

        public State CurrentState { get; private set; } = State.Advance;
        public bool Alive => Health != null && Health.Alive;
        public Health Health { get; private set; }
        /// <summary>What this unit is and what it fires (set by Init).</summary>
        public UnitType Type { get; private set; }
        public bool IsVehicle => Type != null && Type.IsVehicle;
        /// <summary>The squad it came with (null for vehicles).</summary>
        public Squad Squad { get; set; }
        /// <summary>0–1: how much fire is landing around it. It spoils its aim and sends it to cover; at 0.8 it is pinned.</summary>
        public float Suppression { get; private set; }
        public bool Pinned => Suppression >= 0.8f;
        public Health Target => target;
        public float Length => lookSize.z;
        /// <summary>Counter for the tests: how often a soldier jumped clear of a grenade or bomb.</summary>
        public static int Evades;
        /// <summary>The player's attack order, if any.</summary>
        public Health ForcedTarget => forcedTarget;
        /// <summary>The order it is carrying out, in words (Spanish, for the screen).</summary>
        public string OrderText
        {
            get
            {
                if (forcedTarget != null) return "atacar un objetivo";
                if (CurrentState == State.Order) return "moverse";
                if (hasObjective) return stayAtObjective ? "avanzar atacando" : toEnemyBase ? "ir al cuartel enemigo" : "buscar al enemigo";
                if (autoAdvance || !playerControlled) return "avanzar por su línea";
                return "defender su posición";
            }
        }
        /// <summary>Where it is heading when it has nothing to fight (a hunt objective, else its lane).</summary>
        public bool Hunting => hasObjective;
        /// <summary>The spot it is running to or holding: cover, a fallback point or where it jumps clear. For test scenes.</summary>
        public Vector3? DebugSpot => CurrentState == State.ToCover || CurrentState == State.InCover || CurrentState == State.Fallback || CurrentState == State.Evade ? spot : (Vector3?)null;
        /// <summary>Where others aim at this unit.</summary>
        public Vector3 HitPoint => IsVehicle ? transform.position + Vector3.up * lookSize.y * 0.5f : transform.position + Vector3.up * (EyeHeight() - 0.2f);

        /// <summary>How far every unit sees an enemy (m).</summary>
        const float Sight = 50f;
        const float BuildingSight = 42f, CommandPostSight = 40f;
        /// <summary>How far a sapper looks for a building to blow up (m).</summary>
        const float SapperSight = 90f;
        /// <summary>The charge's fuse (s) and how far the sapper runs from it (m).</summary>
        const float ChargeFuse = 3f, ChargeRun = 9f;
        /// <summary>The plane the anti-aircraft mech is firing at, if any.</summary>
        public BiplaneAI AirTarget => airTarget;

        FlakGun flak;
        BiplaneAI airTarget;
        float airT, flakCd;

        NavMeshAgent agent;
        Animator anim;
        UnitAim aim;
        Vector3 lookSize = new Vector3(0.7f, 1.8f, 0.5f);
        List<Vector3> path;
        int wi = 1;
        float laneOffset;
        CoverPoint cover;
        Vector3 spot;
        float coverT, charge, cd, thinkT;
        Health target;
        static readonly Vector3 NoDest = new Vector3(float.NaN, 0, 0);
        Vector3 lastAIDest = NoDest, lastAsked = NoDest; // where this AI sent the agent (as the navmesh placed it), and the point it asked for
        bool ownPath, foreignPath; // a destination of this AI's, or someone else's (Quick RTS), is waiting for its path
        Vector3 orderPoint;
        bool hasOrderArea;
        float evadeT;
        bool escorting, waitForEscort, retreated;
        Vector3 objective; bool hasObjective, stayAtObjective, toEnemyBase;
        Health forcedTarget;
        readonly HashSet<int> animParams = new HashSet<int>();

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            anim = GetComponentInChildren<Animator>();
            Health = RedbaronMapBuilder.GetOrAdd<Health>(gameObject);
            Health.kind = TargetKind.Soldier;
            Health.maxHp = Health.hp = 2;
            Health.aimHeight = 1.2f;
            Health.Died += (h, src) => Die();
            if (anim != null) foreach (var p in anim.parameters) animParams.Add(p.nameHash);
            agent.speed = walkSpeed;
            agent.angularSpeed = 540;
            agent.acceleration = 12;
            agent.stoppingDistance = 0.3f;
            agent.radius = 0.35f;
            agent.height = 1.8f;
            cd = Random.Range(0f, 2f);
            thinkT = Random.Range(0f, 0.5f);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); ReleaseCover(); }

        /// <summary>Lo llama WaveSpawner al crear el soldado. type = what it is (null: a rifleman with this component's values).</summary>
        public void Init(Team t, int laneIndex, bool player, bool advance, UnitType type = null)
        {
            Type = type ?? new UnitType { name = "Fusilero", range = rifleRange, cooldown = rifleCooldown, hit = baseHit, walkSpeed = walkSpeed, runSpeed = runSpeed };
            team = t; lane = laneIndex; playerControlled = player; autoAdvance = advance;
            Health.team = t;
            Health.kind = Type.IsVehicle ? TargetKind.Vehicle : TargetKind.Soldier;
            Health.maxHp = Health.hp = Type.hp;
            walkSpeed = Type.walkSpeed; runSpeed = Type.runSpeed;
            agent.speed = walkSpeed;
            if (Type.IsVehicle) { agent.angularSpeed = 110; agent.acceleration = 5; }
            flak = Type.weapon == WeaponKind.Flak ? new FlakGun(t, $"{Type.name} de {t.Name()}") { burstRadius = Mathf.Max(3f, Type.splash + 3f) } : null;
            path = RedbaronMap.LanePath(t, laneIndex);
            laneOffset = Random.Range(-1.6f, 1.6f);
            wi = 1;
            CurrentState = State.Advance;
        }

        /// <summary>The kit model is on (WaveSpawner): a vehicle takes its size for the collider, the agent and where it is hit.</summary>
        public void SetLook(UnitModels.Look look)
        {
            if (look == null) return;
            aim = look.aim;
            lookSize = look.size;
            if (aim != null && flak != null) { aim.maxPitch = 75f; aim.pitchRate = 90f; } // the gun lifts to the planes
            if (!IsVehicle) return;
            Health.aimHeight = lookSize.y * 0.5f;
            agent.radius = Mathf.Clamp(Mathf.Max(lookSize.x, lookSize.z) * 0.35f, 0.5f, 1.8f);
            agent.height = lookSize.y;
            var cc = GetComponent<CapsuleCollider>();
            if (cc == null) return;
            float sc = Mathf.Max(1e-4f, transform.lossyScale.y);
            bool lying = lookSize.z > lookSize.y; // a tank: the capsule lies along the hull
            cc.direction = lying ? 2 : 1;
            cc.radius = (lying ? Mathf.Min(lookSize.x, lookSize.y) : Mathf.Max(lookSize.x, lookSize.z)) * 0.5f / sc;
            cc.height = (lying ? lookSize.z : lookSize.y) / sc;
            cc.center = new Vector3(look.center.x, lookSize.y * 0.5f, look.center.z) / sc;
        }

        // ------------------------------------------------------------ Órdenes del jugador
        /// <summary>Ir a un punto (orden de Quick RTS o de tu propio código).</summary>
        public void OrderMove(Vector3 p)
        {
            if (!Alive) return;
            ReleaseCover();
            orderPoint = p; hasOrderArea = true;
            CurrentState = State.Order;
            SetDest(p, runSpeed);
        }

        /// <summary>Volver a avanzar solo por la línea.</summary>
        public void OrderAdvance()
        {
            autoAdvance = true; hasOrderArea = false;
            ReleaseCover();
            CurrentState = State.Advance;
            wi = ClosestWaypoint();
        }

        /// <summary>Quedarse donde está y cubrirse.</summary>
        public void OrderHold()
        {
            autoAdvance = false;
            orderPoint = transform.position; hasOrderArea = true;
            CurrentState = State.Hold;
            Stop();
        }

        int ClosestWaypoint()
        {
            if (path == null) return 1;
            int best = 1; float bd = float.MaxValue;
            for (int i = 1; i < path.Count; i++)
            {
                float d = (path[i] - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return Mathf.Min(best + 1, path.Count - 1);
        }

        // ------------------------------------------------------------ Bucle
        void Update()
        {
            if (!Alive) return;
            if (Type == null) Init(team, lane, playerControlled, autoAdvance);
            var game = RedbaronGame.Instance;
            if (game != null && game.GameOver) { Stop(); UpdateAnimator(); return; }
            float dt = Time.deltaTime;
            if (CurrentState == State.InCover) coverT += dt;
            if (charge > 0) charge -= dt;
            if (Suppression > 0) Suppression = Mathf.Max(0f, Suppression - dt * (CurrentState == State.InCover ? 0.16f : 0.08f));
            Squad?.Think();

            // A Quick RTS order (its Mover sets the agent's destination) shows up as a path this AI did not ask for. The AI waits
            // until the agent has worked it out, or it would stop the agent or send it elsewhere meanwhile; then the new
            // destination becomes a move order. The AI's own destinations are read back the same way, as the navmesh placed them.
            if (playerControlled)
            {
                if (agent.pathPending && !ownPath) foreignPath = true;
                if (foreignPath)
                {
                    if (agent.pathPending) { UpdateAnimator(); return; }
                    foreignPath = false;
                    if (agent.hasPath && (float.IsNaN(lastAIDest.x) || (agent.destination - lastAIDest).sqrMagnitude > 2.25f))
                    {
                        lastAIDest = agent.destination;
                        OrderMove(lastAIDest);
                    }
                }
                else if (!agent.pathPending)
                {
                    if (ownPath) { ownPath = false; if (agent.hasPath) lastAIDest = agent.destination; }
                    else if (agent.hasPath && (float.IsNaN(lastAIDest.x) || (agent.destination - lastAIDest).sqrMagnitude > 2.25f))
                    {
                        lastAIDest = agent.destination;
                        OrderMove(lastAIDest);
                    }
                }
            }

            thinkT -= dt;
            if (thinkT <= 0) { thinkT = 0.5f; Think(); }

            switch (CurrentState)
            {
                case State.Advance:
                case State.Charge:
                {
                    float speed = CurrentState == State.Charge || charge > 0 ? runSpeed : walkSpeed;
                    // An attack-move to a spot of the player's goes there, not behind the lane's vehicle.
                    bool escort = CurrentState == State.Advance && !IsVehicle && Squad != null && Squad.Escort != null && (!hasObjective || toEnemyBase);
                    if (escort) FollowEscort();
                    else
                    {
                        if (escorting) wi = ClosestWaypoint(); // back on the lane where the vehicle left it
                        FollowLane(IsVehicle && waitForEscort ? speed * 0.35f : speed);
                    }
                    escorting = escort;
                    break;
                }
                case State.Fallback:
                    SetDest(spot, runSpeed);
                    if (Flat(transform.position - spot) < 0.8f) { CurrentState = cover != null ? State.InCover : State.Hold; coverT = 0; Stop(); }
                    break;
                case State.Evade:
                    evadeT -= dt;
                    SetDest(spot, runSpeed);
                    if (evadeT <= 0 || Flat(transform.position - spot) < 0.5f) { CurrentState = State.Hold; Stop(); thinkT = 0; }
                    break;
                case State.ToCover:
                    SetDest(spot, runSpeed);
                    if (Flat(transform.position - spot) < 0.7f) { CurrentState = State.InCover; coverT = 0; Stop(); }
                    break;
                case State.Order:
                    if (!agent.pathPending && agent.remainingDistance < 0.8f) { CurrentState = State.Hold; Stop(); }
                    break;
                case State.Assault:
                {
                    if (target == null || !target.Alive) { CurrentState = State.Advance; break; }
                    float td = target.DistanceFrom(transform.position);
                    if (td <= Reach(target, td) * 0.85f) { CurrentState = State.Hold; Stop(); }
                    else SetDest(ApproachPoint(target), IsVehicle ? walkSpeed : runSpeed);
                    break;
                }
                case State.InCover:
                case State.Hold:
                    Stop();
                    break;
            }

            if (flak != null) UpdateFlak(dt);
            bool engaged = target != null && target.Alive;
            if (aim != null) aim.Target = airTarget != null ? airTarget.Body.Position : engaged ? AimPointOf(target) : (Vector3?)null;
            if (airTarget == null && engaged && (CurrentState == State.InCover || CurrentState == State.Hold || CurrentState == State.Charge || charge > 0 || CurrentState == State.Order))
            {
                if (CurrentState == State.InCover || CurrentState == State.Hold) FaceTowards(target.transform.position);
                cd -= dt;
                if (cd <= 0) Shoot();
            }
            UpdateAnimator();
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
        static Vector3 FlatDir(Vector3 v) { v.y = 0; return v; }

        /// <summary>Go and look for the enemy at p instead of following the lane (test scenes, AITestBench). It still fights
        /// whatever it meets on the way; with nothing in sight it walks on to p.</summary>
        public void Hunt(Vector3 p) { objective = p; hasObjective = true; autoAdvance = true; toEnemyBase = false; }
        public void StopHunt() { hasObjective = false; }

        // ------------------------------------------------------------ Player commands (RTS controls)
        /// <summary>Move there, firing on the way but not stopping, then hold the spot with its stance.</summary>
        public void CommandMove(Vector3 p)
        {
            if (!Alive) return;
            forcedTarget = null; hasObjective = stayAtObjective = toEnemyBase = false;
            autoAdvance = false; // on arrival it holds there instead of going back to its lane
            OrderMove(p);
        }

        /// <summary>Advance to p fighting whatever it meets, then hold the spot.</summary>
        public void CommandAttackMove(Vector3 p)
        {
            if (!Alive) return;
            forcedTarget = null; hasOrderArea = false;
            ReleaseCover();
            Hunt(p); stayAtObjective = true;
            CurrentState = State.Advance; thinkT = 0;
        }

        /// <summary>Go after t until it is destroyed, whatever the stance, then hold where it ends up.</summary>
        public void CommandAttack(Health t)
        {
            if (!Alive || t == null || !t.Alive || t.team == team) return;
            forcedTarget = t; target = t;
            hasObjective = stayAtObjective = false; hasOrderArea = false; autoAdvance = false;
            ReleaseCover();
            CurrentState = State.Assault; thinkT = 0;
        }

        /// <summary>Stop and hold here with its stance.</summary>
        public void CommandStop()
        {
            forcedTarget = null; hasObjective = stayAtObjective = false;
            OrderHold();
        }

        /// <summary>The default order: head for the enemy command post, fighting whatever it meets.</summary>
        public void CommandAdvance()
        {
            if (!Alive) return;
            forcedTarget = null; stayAtObjective = false; hasOrderArea = false;
            ReleaseCover();
            var post = CommandPost.Of(team.Enemy());
            if (post != null) { Hunt(post.transform.position); toEnemyBase = true; }
            else OrderAdvance();
            CurrentState = State.Advance; thinkT = 0;
        }

        /// <summary>Hold position also stops the unit where it is.</summary>
        public void SetStance(Stance s)
        {
            stance = s;
            if (s == Stance.HoldPosition) CommandStop();
            thinkT = 0;
        }

        void FollowLane(float speed)
        {
            if (hasObjective)
            {
                if (Flat(objective - transform.position) < 3f)
                {
                    hasObjective = false;
                    if (stayAtObjective) { stayAtObjective = false; OrderHold(); } // an attack-move ends holding the spot
                    else Stop();
                    return;
                }
                SetDest(objective, speed);
                return;
            }
            if (path == null) return;
            if (!autoAdvance && playerControlled) { CurrentState = State.Hold; Stop(); return; }
            Vector3 a = path[Mathf.Max(0, wi - 1)], b = path[Mathf.Min(wi, path.Count - 1)];
            Vector3 dir = b - a; dir.y = 0;
            Vector3 side = dir.sqrMagnitude > 0.01f ? new Vector3(-dir.z, 0, dir.x).normalized : Vector3.zero;
            Vector3 goal = b + side * laneOffset;
            if (Flat(goal - transform.position) < 2.5f && wi < path.Count - 1) { wi++; return; }
            SetDest(goal, speed);
        }

        void SetDest(Vector3 p, float speed)
        {
            agent.speed = speed;
            agent.isStopped = false;
            agent.updateRotation = true;
            if (!float.IsNaN(lastAsked.x) && (p - lastAsked).sqrMagnitude < 0.25f && agent.hasPath) return;
            lastAsked = p;
            if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
            if (agent.SetDestination(p)) ownPath = true; // read back in Update when the path is ready
        }

        void Stop()
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            agent.updateRotation = false;
            lastAIDest = transform.position; lastAsked = NoDest; ownPath = false;
        }

        void FaceTowards(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.01f) return;
            float turn = IsVehicle ? 1.5f : 8f; // a hull turns slowly
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * turn);
        }

        // ------------------------------------------------------------ Anti-aircraft (the scout mech)
        /// <summary>Picks the nearest enemy plane in reach and fires flak at it once the torso points that way. While a plane is
        /// in reach the mech does not fire at the ground, but it keeps walking or holding as its state says.</summary>
        void UpdateFlak(float dt)
        {
            flak.team = team;
            flak.Update(dt);
            airT -= dt; flakCd -= dt;
            if (airT <= 0) { airT = 0.4f; if (!PlaneInReach(airTarget)) airTarget = NearestPlane(); }
            if (airTarget == null || flakCd > 0) return;
            if (aim != null && !aim.Aimed(6f)) return;
            Vector3 muzzle = Muzzle();
            Vector3 heading = aim != null && aim.Muzzle != null ? aim.Muzzle.forward : transform.forward;
            Vector3 tp = airTarget.Body.Position;
            float dist = new Vector2(tp.x - muzzle.x, tp.z - muzzle.z).magnitude;
            flak.Fire(muzzle, heading, dist, tp.y);
            flakCd = (Type.cooldown + Random.Range(0f, Type.cooldown * 0.3f));
        }

        bool PlaneInReach(BiplaneAI p)
        {
            if (p == null || !p.Alive || p.team == team) return false;
            Vector3 d = p.Body.Position - transform.position; d.y = 0;
            return d.magnitude <= Type.range;
        }

        BiplaneAI NearestPlane()
        {
            BiplaneAI best = null; float bd = float.MaxValue;
            Vector3 muzzle = Muzzle();
            foreach (var p in BiplaneAI.All)
            {
                if (!PlaneInReach(p)) continue;
                float d = Vector3.Distance(p.Body.Position, transform.position);
                if (d < bd && Combat.HasLineOfSight(muzzle, p.Body.Position, p.transform)) { bd = d; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------ Decisión
        /// <summary>The target to fight. A sapper goes for the nearest enemy building within 90 m, then for a vehicle in sight.
        /// Otherwise the nearest enemy unit, where a weapon that barely scratches armour (rifles, machine guns) prefers
        /// soldiers and a cannon prefers vehicles; a cannon also takes a building near over a soldier further away.
        /// With no unit in sight, a bunker, flak tower or hangar within 42 m, and last the command post within 40 m.</summary>
        Health PickTarget(out float dist)
        {
            dist = 0;
            if (Type.weapon == WeaponKind.Demolition)
            {
                var building = NearestStructure(SapperSight, out float db);
                if (building != null) { dist = db; return building; }
                var vehicle = NearestVehicle(Sight, out float dv);
                if (vehicle != null) { dist = dv; return vehicle; }
            }
            Health best = null; float bs = float.MaxValue;
            DamageKind how = Type.Damage;
            foreach (var o in All)
            {
                if (o.team == team || !o.Alive) continue;
                float d = Vector3.Distance(o.transform.position, transform.position);
                if (d > Sight) continue;
                float s = d / Mathf.Max(0.05f, Combat.Factor(how, o.Health.kind)) * Priority(o, d);
                if (s < bs) { bs = s; best = o.Health; dist = d; }
            }
            if (best != null && Type.weapon == WeaponKind.Cannon)
            {
                var building = NearestStructure(BuildingSight, out float db);
                if (building != null && db * 0.7f < bs) { dist = db; return building; }
            }
            if (best != null) return best;

            float bd = BuildingSight;
            foreach (var h in Health.All)
            {
                if (!h.Alive || h.team == team || (h.kind != TargetKind.Bunker && h.kind != TargetKind.Hangar)) continue;
                float d = h.DistanceFrom(transform.position);
                if (d < bd) { bd = d; best = h; dist = d; }
            }
            if (best != null) return best;
            var post = CommandPost.Of(team.Enemy());
            if (post != null && post.Health.Alive)
            {
                float d = post.Health.DistanceFrom(transform.position);
                if (d < CommandPostSight) { dist = d; return post.Health; }
            }
            return null;
        }

        /// <summary>Lower is more urgent. The squad's target, wounded men and dangerous weapons near come first. Bullets go for men
        /// in the open; grenades and flames go for men in cover.</summary>
        float Priority(SoldierAI o, float d)
        {
            float k = 1f;
            if (Squad != null && Squad.Focus == o.Health) k *= 0.6f;
            if (o.Health.hp < o.Health.maxHp) k *= 0.85f;
            var w = o.Type != null ? o.Type.weapon : WeaponKind.Rifle;
            if (d < 30f && (w == WeaponKind.MachineGun || w == WeaponKind.Flamethrower || w == WeaponKind.Demolition)) k *= 0.8f;
            bool covered = o.CurrentState == State.InCover;
            if (Type.weapon == WeaponKind.Grenade || Type.weapon == WeaponKind.Flamethrower) k *= covered ? 0.7f : 1f;
            else if (covered) k *= 1.15f;
            // Cannons are for armour; the anti-aircraft gun's shell does little to it.
            if (o.IsVehicle) k *= Type.weapon == WeaponKind.Cannon ? 0.5f : Type.weapon == WeaponKind.Flak ? 1.5f : 1f;
            return k;
        }

        /// <summary>The nearest enemy building (bunker, flak tower, hangar or the command post) within range.</summary>
        Health NearestStructure(float range, out float dist)
        {
            Health best = null; dist = range;
            foreach (var h in Health.All)
            {
                if (!h.Alive || h.team == team || !h.IsStructure) continue;
                float d = h.DistanceFrom(transform.position);
                if (d < dist) { dist = d; best = h; }
            }
            return best;
        }

        /// <summary>The nearest enemy tank or mech within range.</summary>
        Health NearestVehicle(float range, out float dist)
        {
            Health best = null; dist = range;
            foreach (var o in All)
            {
                if (!o.Alive || o.team == team || !o.IsVehicle) continue;
                float d = o.Health.DistanceFrom(transform.position);
                if (d < dist) { dist = d; best = o.Health; }
            }
            return best;
        }

        /// <summary>How close this unit must be to hit tgt, dist away. Infantry with a close-range weapon fires its rifle
        /// beyond that range, except flamethrowers closing on anything within 25 m and sappers going for buildings and vehicles.</summary>
        float Reach(Health tgt, float dist)
        {
            bool sapper = Type.weapon == WeaponKind.Demolition;
            float primary = sapper && tgt.kind == TargetKind.Soldier ? 0f : Type.range; // charges are for buildings and vehicles
            if (!Type.CarriesRifle) return primary;
            if (Type.weapon == WeaponKind.Flamethrower && dist <= 25f) return primary;
            // Grenadiers and flamethrowers work their way up to men in cover and to bunkers, where rifles do little.
            bool dugIn = tgt.kind == TargetKind.Bunker || (tgt.kind == TargetKind.Soldier && tgt.GetComponent<SoldierAI>() is SoldierAI ts && ts.CurrentState == State.InCover);
            if (dugIn && dist <= 40f && (Type.weapon == WeaponKind.Grenade || Type.weapon == WeaponKind.Flamethrower)) return primary;
            if (sapper && tgt.kind != TargetKind.Soldier) return primary;
            return Mathf.Max(primary, UnitType.Rifleman.range);
        }

        /// <summary>The weapon to use on tgt, dist away: the unit's own if it reaches, else the rifle if it carries one.</summary>
        WeaponKind? WeaponFor(Health tgt, float dist)
        {
            bool sapper = Type.weapon == WeaponKind.Demolition;
            if (dist <= Type.range && !(sapper && tgt.kind == TargetKind.Soldier)) return Type.weapon;
            if (Type.CarriesRifle && dist <= UnitType.Rifleman.range) return WeaponKind.Rifle;
            return null;
        }

        static Vector3 ApproachPoint(Health tgt) =>
            tgt.kind != TargetKind.Soldier && tgt.Body != null ? tgt.Body.bounds.ClosestPoint(tgt.transform.position + Vector3.up * 0.1f) : tgt.transform.position;

        static Vector3 AimPointOf(Health tgt)
        {
            var s = tgt.GetComponent<SoldierAI>();
            return s != null ? s.HitPoint : tgt.AimPoint;
        }

        float LocalOdds()
        {
            int a = 0, e = 0;
            foreach (var o in All)
            {
                if (!o.Alive) continue;
                float d = Vector3.Distance(o.transform.position, transform.position);
                if (o.team == team && d < 30) a++;
                else if (o.team != team && d < 45) e++;
            }
            return a / (float)Mathf.Max(1, e);
        }

        void Think()
        {
            if (CurrentState == State.Evade) return;
            if (forcedTarget != null && !forcedTarget.Alive) { forcedTarget = null; if (playerControlled) OrderHold(); } // target down: hold there
            float dist = 0;
            Health tgt = forcedTarget;
            if (tgt != null) dist = tgt.DistanceFrom(transform.position);
            else tgt = PickTarget(out dist);
            target = tgt;
            if (IsVehicle) waitForEscort = EscortLagging();
            if (charge > 0 && Suppression > 0.7f) charge = 0; // a charge breaks down under heavy fire
            if (charge > 0) return;
            if (CurrentState == State.Order) return; // primero llegar adonde mandó el jugador

            // Hold position: never moves, and fires at whatever its weapon reaches (Update fires from Hold).
            if (playerControlled && stance == Stance.HoldPosition && forcedTarget == null)
            {
                if (CurrentState != State.InCover && CurrentState != State.Hold) { CurrentState = State.Hold; Stop(); }
                return;
            }
            bool followsLane = autoAdvance || !playerControlled || forcedTarget != null;
            bool aggressiveHold = playerControlled && !followsLane && stance == Stance.Aggressive;

            // The squad falls back: run to cover behind, then stay there and fire while it regroups.
            if (followsLane && forcedTarget == null && Squad != null && Squad.CurrentOrder != Squad.Order.Advance)
            {
                if (!retreated)
                {
                    retreated = true;
                    Vector3 threat = tgt != null ? tgt.transform.position : transform.position + Forward() * 30f;
                    if (!PickCoverBehind(threat)) { ReleaseCover(); spot = RetreatPoint(); }
                    CurrentState = State.Fallback;
                }
                return;
            }
            retreated = false;
            if (tgt == null)
            {
                if (followsLane) { ReleaseCover(); CurrentState = State.Advance; }
                else if (aggressiveHold && hasOrderArea && Flat(orderPoint - transform.position) > 4f) OrderMove(orderPoint); // back to its spot
                else if (CurrentState != State.InCover) CurrentState = State.Hold;
                return;
            }
            // Out of reach (a flamethrower, a sapper's charge, a short-range vehicle): close in, unless holding a spot for the player.
            // Under heavy fire a soldier closes in from cover to cover instead.
            if (dist > Reach(tgt, dist))
            {
                // A sapper on its last metres to a building or vehicle sprints on whatever the fire.
                bool lastRush = Type.weapon == WeaponKind.Demolition && tgt.kind != TargetKind.Soldier && dist < 15f;
                if (!IsVehicle && Suppression > 0.7f && forcedTarget == null && !lastRush)
                {
                    if (CurrentState != State.InCover && !PickCover(tgt.transform.position, false, transform.position, coverSearchRadius)) { ReleaseCover(); CurrentState = State.Hold; }
                    return;
                }
                // Who closes in: an attack order always; units on the move unless Defensive; an aggressive unit holding a spot
                // if the enemy is within 35 m of that spot.
                bool defensive = playerControlled && stance == Stance.Defensive && forcedTarget == null;
                bool chase = forcedTarget != null || (!defensive && (followsLane || (aggressiveHold && Flat(tgt.transform.position - orderPoint) < 35f)));
                if (chase) { if (CurrentState != State.Assault) { ReleaseCover(); CurrentState = State.Assault; } }
                else if (CurrentState != State.InCover) CurrentState = State.Hold;
                return;
            }
            bool planting = Type.weapon == WeaponKind.Demolition && tgt.kind != TargetKind.Soldier; // a sapper at its wall
            if (CurrentState == State.Assault) { CurrentState = State.Hold; Stop(); if (planting) cd = Mathf.Min(cd, 0.4f); }
            if (planting) { if (CurrentState != State.Hold) { CurrentState = State.Hold; Stop(); } return; } // no cover: set the charge now
            if (tgt.kind == TargetKind.CommandPost) { CurrentState = State.Hold; return; }
            if (IsVehicle) { CurrentState = State.Hold; return; } // vehicles never take cover

            Vector3 ep = tgt.transform.position;
            // Moving up from cover: not while pinned, and a machine gunner keeps its post longer and never charges.
            bool gunner = Type.weapon == WeaponKind.MachineGun;
            if (CurrentState == State.InCover && tgt.kind == TargetKind.Soldier && dist > 10 && coverT > (gunner ? 14 : 6) + Random.value * 4 && followsLane && Suppression < 0.4f)
            {
                if (PickCover(ep, true, transform.position, 24)) return;
                if (!gunner && LocalOdds() >= 1.15f && (Squad == null || Squad.Strength >= 0.75f))
                {
                    ReleaseCover();
                    CurrentState = State.Charge; charge = 6;
                    return;
                }
                coverT = 0;
            }
            bool keep = cover != null && Flat(cover.transform.position - transform.position) < 18 && cover.ProtectionAgainst(spot, ep) < 0.7f;
            if (keep) return;
            Vector3 center = hasOrderArea && !followsLane ? orderPoint : transform.position;
            float radius = hasOrderArea && !followsLane ? orderCoverRadius : coverSearchRadius;
            if (!PickCover(ep, false, center, radius))
            {
                ReleaseCover();
                CurrentState = State.Hold;
            }
        }

        bool PickCover(Vector3 enemyPos, bool forward, Vector3 center, float radius)
        {
            if (IsVehicle) return false;
            float dMe = Flat(enemyPos - transform.position);
            CoverPoint best = null; float bs = float.MaxValue; Vector3 bestSpot = Vector3.zero;
            foreach (var c in CoverPoint.All)
            {
                if (c == cover || c.occupants >= c.capacity) continue;
                float dc = Flat(c.transform.position - center);
                if (dc > radius) continue;
                Vector3 s = c.SpotAgainst(enemyPos);
                Vector2 sd = RedbaronMap.ToDesign(s);
                if (RedbaronMap.Sample(sd.x, sd.y).wet) continue;
                float dS = Flat(enemyPos - s);
                if (forward && (dS > dMe - 5 || Flat(s - transform.position) > 22)) continue;
                float sc = Flat(s - transform.position) + Mathf.Max(0, dS - dMe) * 1.5f + (c.type == CoverType.Trinchera ? -2 : 0);
                if (sc < bs) { bs = sc; best = c; bestSpot = s; }
            }
            if (best == null) return false;
            ReleaseCover();
            cover = best; cover.occupants++;
            spot = bestSpot; coverT = 0;
            CurrentState = State.ToCover;
            return true;
        }

        void ReleaseCover()
        {
            if (cover != null) { cover.occupants = Mathf.Max(0, cover.occupants - 1); cover = null; }
        }

        // ------------------------------------------------------------ Disparo
        /// <summary>Multiplicador de acierto contra este soldado desde <paramref name="shooter"/>. Vehicles never have cover.</summary>
        public float ProtectionAgainst(Vector3 shooter)
        {
            if (IsVehicle) return 1f;
            switch (CurrentState)
            {
                case State.Advance: case State.Charge: case State.Order: case State.ToCover: case State.Assault: case State.Fallback: case State.Evade: return 1f;
            }
            if (CurrentState == State.InCover && cover != null) return cover.ProtectionAgainst(transform.position, shooter);
            Vector2 d = RedbaronMap.ToDesign(transform.position);
            if (RedbaronMap.Sample(d.x, d.y).trench > 0.6f) return CoverPoint.Protection(CoverType.Trinchera);
            return 0.75f;
        }

        float EyeHeight()
        {
            if (IsVehicle) return lookSize.y * 0.6f;
            if (CurrentState == State.InCover && cover != null && cover.type == CoverType.Trinchera) return 1.55f;
            return CurrentState == State.InCover || CurrentState == State.Hold ? 1.15f : 1.5f;
        }

        Vector3 Muzzle()
        {
            if (aim != null && aim.Muzzle != null) return aim.Muzzle.position + aim.Muzzle.forward * 0.8f;
            if (IsVehicle) return transform.position + Vector3.up * EyeHeight() + transform.forward * lookSize.z * 0.45f;
            return transform.position + Vector3.up * EyeHeight();
        }

        void Shoot()
        {
            if (target == null || !target.Alive) return;
            var w = WeaponFor(target, target.DistanceFrom(transform.position));
            if (w == null) { cd = 0.4f; return; }
            var weapon = w.Value;
            if (aim != null && aim.Muzzle != null && !aim.Aimed(7f)) { cd = 0.15f; return; } // the turret is still turning
            Vector3 eye = Muzzle();
            var ts = target.GetComponent<SoldierAI>();
            Vector3 at = ts != null ? ts.HitPoint : target.AimPoint;
            if (weapon != WeaponKind.Grenade && !Combat.HasLineOfSight(eye, at, target.transform)) { cd = 0.5f; return; }

            bool rifle = weapon == WeaponKind.Rifle && Type.weapon != WeaponKind.Rifle; // the rifle of a grenadier, flamethrower or sapper
            float cool = rifle ? UnitType.Rifleman.cooldown : Type.cooldown;
            cd = (cool + Random.Range(0f, cool * 0.3f)) * (Pinned ? 1.5f : 1f);
            float d = Vector3.Distance(eye, at);
            float p = HitChance(rifle ? UnitType.Rifleman.hit * 0.9f : Type.hit, d, ts, weapon);
            bool hit = Random.value < p;

            switch (weapon)
            {
                case WeaponKind.Rifle:
                    Combat.Tracer(eye, at + (hit ? Vector3.zero : Random.insideUnitSphere * 1.5f));
                    Sfx.Rifle(eye);
                    if (anim != null && !string.IsNullOrEmpty(shootTrigger) && animParams.Contains(Animator.StringToHash(shootTrigger))) anim.SetTrigger(shootTrigger);
                    if (ts != null) ts.Suppress(0.15f);
                    if (hit) target.Damage(rifle ? 1f : Type.damage, gameObject, DamageKind.Bullet);
                    break;

                case WeaponKind.MachineGun:
                    Sfx.BunkerGun(eye);
                    for (int k = 0; k < Mathf.Max(1, Type.shots); k++)
                    {
                        bool h = k == 0 ? hit : Random.value < p;
                        Combat.Tracer(eye, at + (h ? Vector3.zero : Random.insideUnitSphere * 1.8f));
                        if (h && target.Alive) target.Damage(Type.damage, gameObject, DamageKind.Bullet);
                    }
                    if (ts != null) ts.Suppress(0.12f * Mathf.Max(1, Type.shots));
                    Combat.SuppressAround(at, 3f, team, 0.06f * Mathf.Max(1, Type.shots)); // a burst keeps heads down around it
                    break;

                case WeaponKind.Grenade:
                {
                    Vector3 land = target.transform.position + FlatDir(Random.insideUnitSphere) * (hit ? 0.6f : 3.5f);
                    float dmg = Type.damage, rad = Type.splash; Team side = team; GameObject src = gameObject;
                    float flight = 0.55f + d * 0.02f;
                    if (IsVehicle) Combat.MuzzleSmoke(eye); // the mech's launcher
                    Combat.Lob(eye, land, flight, () => { Combat.SmallExplosion(land); Combat.Blast(land, rad, dmg, side, DamageKind.Fragment, src); });
                    Combat.WarnBlast(land, rad, team); // the men there may see it coming
                    break;
                }

                case WeaponKind.Flamethrower:
                {
                    Vector3 burn = at + (hit ? Vector3.zero : FlatDir(Random.insideUnitSphere) * 1.2f);
                    Combat.Flame(eye, burn);
                    Combat.Blast(burn, Mathf.Max(0.8f, Type.splash), Type.damage, team, DamageKind.Fire, gameObject);
                    break;
                }

                case WeaponKind.Cannon:
                case WeaponKind.Flak: // against the ground: a light shell
                {
                    Vector3 burst = at + (hit ? Vector3.zero : FlatDir(Random.insideUnitSphere) * 3f);
                    Combat.MuzzleSmoke(eye);
                    Combat.Tracer(eye, burst, 0.12f, 0.12f);
                    Combat.SmallExplosion(burst);
                    Combat.Blast(burst, Mathf.Max(0.8f, Type.splash), Type.damage, team, DamageKind.Blast, gameObject);
                    break;
                }

                case WeaponKind.Demolition:
                {
                    // The charge goes against the wall (or onto the vehicle) on the sapper's side; then the sapper runs clear.
                    Vector3 plant = target.kind != TargetKind.Soldier && target.Body != null
                        ? target.Body.bounds.ClosestPoint(transform.position + Vector3.up * 0.3f) : target.transform.position;
                    DemolitionCharge.Plant(plant, target, team, ChargeFuse, Type.damage, Type.splash, gameObject);
                    RunClear(plant, ChargeRun, ChargeFuse + 0.6f);
                    break;
                }
            }
        }

        /// <summary>Chance that a shot from here lands, d metres away. A soldier's cover counts in full against bullets, partly against
        /// a grenade, and not against flames. Vehicles are big targets with no cover; buildings are hard to miss.</summary>
        float HitChance(float baseHit, float d, SoldierAI ts, WeaponKind w)
        {
            if (ts == null) return w == WeaponKind.Rifle || w == WeaponKind.MachineGun ? 0.75f : 0.85f;
            float p;
            if (ts.IsVehicle) p = baseHit * 1.3f * Mathf.Max(0.35f, 1 - d / 90f);
            else
            {
                float prot = ts.ProtectionAgainst(transform.position);
                if (w == WeaponKind.Flamethrower) prot = 1f;
                else if (w == WeaponKind.Grenade) prot = Mathf.Sqrt(prot);
                p = baseHit * Mathf.Max(0.15f, 1 - d / 60f) * prot;
            }
            if (CurrentState == State.Charge || charge > 0 || CurrentState == State.Order) p *= 0.6f;
            if (transform.position.y > target.transform.position.y + 3) p *= 1.25f;
            p *= 1f - 0.5f * Suppression; // fire around you spoils your aim
            return Mathf.Clamp01(p);
        }

        // ------------------------------------------------------------ Tactics
        /// <summary>Fire landing around this soldier (0–1 added). Cover takes some of the edge off. Vehicles do not mind.</summary>
        public void Suppress(float amount)
        {
            if (!Alive || IsVehicle || amount <= 0) return;
            Suppression = Mathf.Min(1f, Suppression + amount * (CurrentState == State.InCover ? 0.6f : 1f));
        }

        /// <summary>A grenade or bomb is about to land at danger: jump clear of radius, if the soldier sees it and is not pinned.</summary>
        public void Evade(Vector3 danger, float radius)
        {
            if (!Alive || IsVehicle || CurrentState == State.Evade || CurrentState == State.Dead) return;
            if (Random.value > 0.65f * (1f - 0.5f * Suppression)) return;
            if (RunClear(danger, radius + 1.5f, 1.5f)) Evades++;
        }

        /// <summary>Runs straight away from danger to distance metres from it, for up to time seconds, then holds there.</summary>
        bool RunClear(Vector3 danger, float distance, float time)
        {
            Vector3 away = transform.position - danger; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            Vector3 to = danger + away.normalized * distance;
            if (!NavMesh.SamplePosition(to, out var hit, 4f, NavMesh.AllAreas)) return false;
            ReleaseCover();
            spot = hit.position; evadeT = time;
            CurrentState = State.Evade;
            SetDest(spot, runSpeed);
            return true;
        }

        Vector3 Forward()
        {
            var post = CommandPost.Of(team.Enemy());
            Vector3 d = post != null ? post.transform.position - transform.position : transform.forward;
            d.y = 0;
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        /// <summary>Cover within 25 m that is farther from the threat than this soldier is (a trench is best).</summary>
        bool PickCoverBehind(Vector3 threat)
        {
            float dMe = Flat(threat - transform.position);
            CoverPoint best = null; float bs = float.MaxValue; Vector3 bestSpot = Vector3.zero;
            foreach (var c in CoverPoint.All)
            {
                if (c == cover || c.occupants >= c.capacity) continue;
                float dc = Flat(c.transform.position - transform.position);
                if (dc > 25f) continue;
                Vector3 s = c.SpotAgainst(threat);
                if (Flat(threat - s) < dMe + 6f) continue;
                Vector2 sd = RedbaronMap.ToDesign(s);
                if (RedbaronMap.Sample(sd.x, sd.y).wet) continue;
                float sc = dc + (c.type == CoverType.Trinchera ? -4f : 0f);
                if (sc < bs) { bs = sc; best = c; bestSpot = s; }
            }
            if (best == null) return false;
            ReleaseCover();
            cover = best; cover.occupants++;
            spot = bestSpot; coverT = 0;
            return true;
        }

        /// <summary>With no cover behind: 15 m back down its own lane.</summary>
        Vector3 RetreatPoint()
        {
            Vector3 p = transform.position - Forward() * 15f;
            if (path != null && path.Count > 1)
            {
                int back = Mathf.Max(0, ClosestWaypoint() - 2);
                p = path[back];
            }
            return NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas) ? hit.position : transform.position;
        }

        /// <summary>Walk behind the squad's vehicle, in rows of three, using it as cover.</summary>
        void FollowEscort()
        {
            var v = Squad.Escort;
            Vector3 back = -v.transform.forward; back.y = 0;
            if (back.sqrMagnitude < 0.01f) back = -Forward();
            back.Normalize();
            Vector3 side = new Vector3(-back.z, 0, back.x);
            int rank = Squad.RankOf(this);
            Vector3 goal = v.transform.position + back * (v.Length * 0.5f + 2.5f + (rank / 3) * 2f) + side * ((rank % 3) - 1) * 1.8f;
            float d = Flat(goal - transform.position);
            if (d < 1f) { agent.speed = Mathf.Min(agent.speed, v.walkSpeed); return; }
            SetDest(goal, d > 6f ? runSpeed : walkSpeed);
        }

        /// <summary>A vehicle slows down when the infantry following it falls more than 16 m behind.</summary>
        bool EscortLagging()
        {
            foreach (var sq in Squad.All)
            {
                if (sq.Escort != this) continue;
                float nearest = float.MaxValue;
                foreach (var m in sq.members) if (m != null && m.Alive && m.escorting && m.autoAdvance) nearest = Mathf.Min(nearest, Flat(m.transform.position - transform.position));
                if (nearest < float.MaxValue && nearest > 16f) return true;
            }
            return false;
        }

        /// <summary>The soldier's model was replaced (UnitModels) and has no Animator any more: it walks with AndarMech
        /// and falls flat when it dies.</summary>
        public void DropAnimator()
        {
            anim = null;
            animParams.Clear();
        }

        // ------------------------------------------------------------ Muerte y animación
        void Die()
        {
            CurrentState = State.Dead;
            ReleaseCover();
            Squad?.Leave(this);
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (IsVehicle)
            {
                StartCoroutine(Wreck());
                RedbaronGame.Log($"{Type.name} de {team.Name()} fuera de combate");
            }
            else
            {
                if (anim != null && !string.IsNullOrEmpty(dieTrigger) && animParams.Contains(Animator.StringToHash(dieTrigger))) anim.SetTrigger(dieTrigger);
                else StartCoroutine(FallOver());
                Combat.Puff(transform.position + Vector3.up, new Color(0.54f, 0.5f, 0.42f, 0.6f), 0.8f, 1f, 0.3f);
            }
            if (RedbaronGame.Instance != null) RedbaronGame.Instance.OnSoldierDied(this);
            Destroy(gameObject, IsVehicle ? 12f : 8f);
        }

        /// <summary>Without a death animation the soldier topples onto its back, faster as it goes (0.45 s). It rises by the
        /// depth of its back (backpack included) so that it lies on the ground instead of sinking half into it.</summary>
        System.Collections.IEnumerator FallOver()
        {
            float back = 0f;
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    back = Mathf.Min(back, Vector3.Dot(mf.transform.TransformPoint(corner) - transform.position, transform.forward));
                }
            }
            Quaternion from = transform.rotation, to = Quaternion.Euler(-90, transform.eulerAngles.y, 0);
            Vector3 start = transform.position, end = start + Vector3.up * -back;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.45f)
            {
                float k = t * t;
                transform.SetPositionAndRotation(Vector3.Lerp(start, end, k), Quaternion.Slerp(from, to, k));
                yield return null;
            }
            transform.SetPositionAndRotation(end, to);
        }

        static readonly int TintId = Shader.PropertyToID("_Tint");

        /// <summary>A knocked-out vehicle blows up, goes dark and smokes. A mech slumps; a tank settles where it stopped.</summary>
        System.Collections.IEnumerator Wreck()
        {
            Combat.Explosion(transform.position + Vector3.up * lookSize.y * 0.5f, true);
            if (aim != null) aim.Freeze();
            foreach (var w in GetComponentsInChildren<AndarMech>()) w.enabled = false;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(TintId, new Color(0.3f, 0.28f, 0.26f, 1f));
                r.SetPropertyBlock(mpb);
            }
            bool mech = Type.unitClass == UnitClass.Mech;
            Quaternion from = transform.rotation, to = from * (mech ? Quaternion.Euler(-14f, 0f, 9f) : Quaternion.Euler(0f, 0f, 4f));
            Vector3 start = transform.position, end = start - Vector3.up * (mech ? lookSize.y * 0.18f : 0.2f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.8f)
            {
                float k = t * t;
                transform.SetPositionAndRotation(Vector3.Lerp(start, end, k), Quaternion.Slerp(from, to, k));
                yield return null;
            }
            transform.SetPositionAndRotation(end, to);
            for (int i = 0; i < 8; i++)
            {
                Combat.Puff(transform.position + Vector3.up * lookSize.y * 0.7f + Random.insideUnitSphere * 0.5f, new Color(0.18f, 0.17f, 0.16f, 0.75f), 3f, 3f, 3f);
                yield return new WaitForSeconds(0.9f);
            }
        }

        void UpdateAnimator()
        {
            if (anim == null) return;
            if (!string.IsNullOrEmpty(speedParam) && animParams.Contains(Animator.StringToHash(speedParam)))
                anim.SetFloat(speedParam, agent.enabled ? agent.velocity.magnitude : 0);
            if (!string.IsNullOrEmpty(crouchParam) && animParams.Contains(Animator.StringToHash(crouchParam)))
                anim.SetBool(crouchParam, CurrentState == State.InCover || CurrentState == State.Hold);
        }
    }
}
