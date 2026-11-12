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
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class SoldierAI : MonoBehaviour
    {
        public enum State { Advance, ToCover, InCover, Hold, Charge, Order, Dead }
        public static readonly List<SoldierAI> All = new List<SoldierAI>();

        [Header("Bando")]
        public Team team = Team.Centrales;
        public int lane;
        [Tooltip("Lo controla el jugador con Quick RTS.")]
        public bool playerControlled;
        [Tooltip("Si está activo avanza solo por su línea hacia la base enemiga.")]
        public bool autoAdvance = true;

        [Header("Combate")]
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

        NavMeshAgent agent;
        Animator anim;
        List<Vector3> path;
        int wi = 1;
        float laneOffset;
        CoverPoint cover;
        Vector3 spot;
        float coverT, charge, cd, thinkT;
        Health target;
        Vector3 lastAIDest = new Vector3(float.NaN, 0, 0);
        Vector3 orderPoint;
        bool hasOrderArea;
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

        /// <summary>Lo llama WaveSpawner al crear el soldado.</summary>
        public void Init(Team t, int laneIndex, bool player, bool advance)
        {
            team = t; lane = laneIndex; playerControlled = player; autoAdvance = advance;
            Health.team = t;
            path = RedbaronMap.LanePath(t, laneIndex);
            laneOffset = Random.Range(-1.6f, 1.6f);
            wi = 1;
            CurrentState = State.Advance;
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
            var game = RedbaronGame.Instance;
            if (game != null && game.GameOver) { Stop(); UpdateAnimator(); return; }
            float dt = Time.deltaTime;
            if (CurrentState == State.InCover) coverT += dt;
            if (charge > 0) charge -= dt;

            // Una orden de Quick RTS aparece como un destino nuevo que no ha puesto esta IA.
            if (playerControlled && agent.hasPath && !agent.pathPending && !float.IsNaN(lastAIDest.x)
                && (agent.destination - lastAIDest).sqrMagnitude > 2.25f)
                OrderMove(agent.destination);
            else if (playerControlled && float.IsNaN(lastAIDest.x) && agent.hasPath)
                OrderMove(agent.destination);

            thinkT -= dt;
            if (thinkT <= 0) { thinkT = 0.5f; Think(); }

            switch (CurrentState)
            {
                case State.Advance:
                case State.Charge:
                    FollowLane(CurrentState == State.Charge || charge > 0 ? runSpeed : walkSpeed);
                    break;
                case State.ToCover:
                    SetDest(spot, runSpeed);
                    if (Flat(transform.position - spot) < 0.7f) { CurrentState = State.InCover; coverT = 0; Stop(); }
                    break;
                case State.Order:
                    if (!agent.pathPending && agent.remainingDistance < 0.8f) { CurrentState = State.Hold; Stop(); }
                    break;
                case State.InCover:
                case State.Hold:
                    Stop();
                    break;
            }

            if (target != null && target.Alive && (CurrentState == State.InCover || CurrentState == State.Hold || CurrentState == State.Charge || charge > 0 || CurrentState == State.Order))
            {
                if (CurrentState == State.InCover || CurrentState == State.Hold) FaceTowards(target.transform.position);
                cd -= dt;
                if (cd <= 0) Shoot();
            }
            UpdateAnimator();
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        void FollowLane(float speed)
        {
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
            if (!float.IsNaN(lastAIDest.x) && (p - lastAIDest).sqrMagnitude < 0.25f && agent.hasPath) return;
            if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
            agent.SetDestination(p);
            lastAIDest = p;
        }

        void Stop()
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            agent.updateRotation = false;
            lastAIDest = transform.position;
        }

        void FaceTowards(Vector3 p)
        {
            Vector3 d = p - transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 8f);
        }

        // ------------------------------------------------------------ Decisión
        SoldierAI NearestEnemy(float range, out float dist)
        {
            SoldierAI best = null; dist = range;
            foreach (var o in All)
            {
                if (o.team == team || !o.Alive) continue;
                float d = Vector3.Distance(o.transform.position, transform.position);
                if (d < dist) { dist = d; best = o; }
            }
            return best;
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
            var en = NearestEnemy(rifleRange, out float dist);
            Health tgt = null;
            if (en != null) tgt = en.Health;
            else
            {
                float bd = 42;
                foreach (var b in Bunker.All)
                    if (b.Health.Alive && b.Health.team != team)
                    {
                        float d = Vector3.Distance(b.transform.position, transform.position);
                        if (d < bd) { bd = d; tgt = b.Health; dist = d; }
                    }
                if (tgt == null)
                {
                    var post = CommandPost.Of(team.Enemy());
                    if (post != null && post.Health.Alive)
                    {
                        float d = Vector3.Distance(post.transform.position, transform.position);
                        if (d < 40) { tgt = post.Health; dist = d; }
                    }
                }
            }
            target = tgt;
            if (charge > 0) return;
            if (CurrentState == State.Order) return; // primero llegar adonde mandó el jugador

            bool followsLane = autoAdvance || !playerControlled;
            if (tgt == null)
            {
                if (followsLane) { ReleaseCover(); CurrentState = State.Advance; }
                else if (CurrentState != State.InCover) CurrentState = State.Hold;
                return;
            }
            if (tgt.kind == TargetKind.CommandPost) { CurrentState = State.Hold; return; }

            Vector3 ep = tgt.transform.position;
            if (CurrentState == State.InCover && tgt.kind == TargetKind.Soldier && dist > 10 && coverT > 6 + Random.value * 4 && followsLane)
            {
                if (PickCover(ep, true, transform.position, 24)) return;
                if (LocalOdds() >= 1.15f)
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
        /// <summary>Multiplicador de acierto contra este soldado desde <paramref name="shooter"/>.</summary>
        public float ProtectionAgainst(Vector3 shooter)
        {
            switch (CurrentState)
            {
                case State.Advance: case State.Charge: case State.Order: case State.ToCover: return 1f;
            }
            if (CurrentState == State.InCover && cover != null) return cover.ProtectionAgainst(transform.position, shooter);
            Vector2 d = RedbaronMap.ToDesign(transform.position);
            if (RedbaronMap.Sample(d.x, d.y).trench > 0.6f) return CoverPoint.Protection(CoverType.Trinchera);
            return 0.75f;
        }

        float EyeHeight()
        {
            if (CurrentState == State.InCover && cover != null && cover.type == CoverType.Trinchera) return 1.55f;
            return CurrentState == State.InCover || CurrentState == State.Hold ? 1.15f : 1.5f;
        }

        void Shoot()
        {
            if (target == null || !target.Alive) return;
            Vector3 eye = transform.position + Vector3.up * EyeHeight();
            var ts = target.GetComponent<SoldierAI>();
            Vector3 aim = ts != null ? target.transform.position + Vector3.up * (ts.EyeHeight() - 0.2f) : target.AimPoint;
            if (!Combat.HasLineOfSight(eye, aim, target.transform)) { cd = 0.5f; return; }
            cd = rifleCooldown + Random.Range(0f, 1f);
            float d = Vector3.Distance(eye, aim);
            float p;
            if (ts != null)
            {
                p = baseHit * Mathf.Max(0.15f, 1 - d / 60f) * ts.ProtectionAgainst(transform.position);
                if (CurrentState == State.Charge || charge > 0 || CurrentState == State.Order) p *= 0.6f;
                if (transform.position.y > target.transform.position.y + 3) p *= 1.25f;
            }
            else p = 0.75f;
            bool hit = Random.value < p;
            Combat.Tracer(eye, aim + (hit ? Vector3.zero : Random.insideUnitSphere * 1.5f));
            Sfx.Rifle(eye);
            if (anim != null && !string.IsNullOrEmpty(shootTrigger) && animParams.Contains(Animator.StringToHash(shootTrigger))) anim.SetTrigger(shootTrigger);
            if (hit) target.Damage(1, gameObject);
        }

        /// <summary>The soldier's model was replaced (SoldierModels) and has no Animator any more: it walks with AndarMech
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
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (anim != null && !string.IsNullOrEmpty(dieTrigger) && animParams.Contains(Animator.StringToHash(dieTrigger))) anim.SetTrigger(dieTrigger);
            else StartCoroutine(FallOver());
            Combat.Puff(transform.position + Vector3.up, new Color(0.54f, 0.5f, 0.42f, 0.6f), 0.8f, 1f, 0.3f);
            if (RedbaronGame.Instance != null) RedbaronGame.Instance.OnSoldierDied(this);
            Destroy(gameObject, 8f);
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
