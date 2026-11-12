using AlterunaFPS;
using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Joins the two damage systems of the multiplayer demo. The Redbaron AI fights with Redbaron.Health (points, teams,
    /// armour); the FPS template's players and bullets use AlterunaFPS.Health (points, no teams). Ten FPS points are one
    /// Redbaron point: a rifle hit takes 10 of a player's 50, and a pistol hit kills a soldier (2 points) outright
    /// but barely scratches a tank (bullets do a tenth to armour).
    /// </summary>
    public static class FpsDamage
    {
        public const float PointsPerRedbaronPoint = 10f;
        /// <summary>The map's middle line (world z): spawn points north of it belong to the Central Powers.</summary>
        public static float MiddleZ => RedbaronMap.ToWorld(50f, 150f).z;
        public static Team TeamAt(Vector3 p) => p.z > MiddleZ ? Team.Centrales : Team.Entente;
    }

    /// <summary>
    /// On an FPS player: a Redbaron.Health the AI can see and shoot (registered in Health.Fighters while the player is on the
    /// field), whose hits are passed on to the player's FPS health. The player's side is the half of the map it spawned on.
    /// </summary>
    public class FpsPlayerBridge : MonoBehaviour
    {
        public Health Redbaron { get; private set; }
        AlterunaFPS.Health fps;
        PlayerController controller;

        void Awake()
        {
            fps = GetComponent<AlterunaFPS.Health>();
            controller = GetComponent<PlayerController>();
            Redbaron = GetComponent<Health>();
            if (Redbaron == null) Redbaron = gameObject.AddComponent<Health>();
            Redbaron.kind = TargetKind.Soldier;
            Redbaron.aimHeight = 1.3f;
            float max = controller != null ? controller.MaxHealth : 50f;
            Redbaron.maxHp = Redbaron.hp = max / FpsDamage.PointsPerRedbaronPoint;
            Redbaron.Damaged += OnHit;
        }

        void OnEnable()
        {
            Redbaron.team = FpsDamage.TeamAt(transform.position); // a respawn may land on the other half
            Redbaron.Restore();
            if (!Health.Fighters.Contains(Redbaron)) Health.Fighters.Add(Redbaron);
        }

        void OnDisable() { Health.Fighters.Remove(Redbaron); }

        void OnHit(Health h, float amount, GameObject source)
        {
            if (fps == null || amount <= 0) return;
            fps.TakeDamage(0, amount * FpsDamage.PointsPerRedbaronPoint);
        }

        void LateUpdate()
        {
            // The FPS health is the truth (other players' bullets, respawns): mirror it for the AI.
            if (fps != null) Redbaron.hp = Mathf.Clamp(fps.HealthPoints / FpsDamage.PointsPerRedbaronPoint, 0f, Redbaron.maxHp);
        }
    }

    /// <summary>
    /// On an AI unit or a building: an FPS health the players' bullets can hit, whose losses are passed on to the Redbaron
    /// health with its armour rules. Added by RedbaronFpsDemo to everything with a Redbaron.Health.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class FpsTargetBridge : MonoBehaviour
    {
        Health redbaron;
        AlterunaFPS.Health fps;
        float last;

        void Awake()
        {
            redbaron = GetComponent<Health>();
            fps = GetComponent<AlterunaFPS.Health>();
            if (fps == null) fps = gameObject.AddComponent<AlterunaFPS.Health>();
            fps.MaterialType = redbaron.kind == TargetKind.Soldier ? EfxManager.ImpactType.Flesh
                : redbaron.kind == TargetKind.Vehicle ? EfxManager.ImpactType.Metal : EfxManager.ImpactType.Stone;
            fps.PenetrationResistance = redbaron.kind == TargetKind.Soldier ? 0.5f : 50f;
            Mirror();
        }

        void Mirror()
        {
            last = fps.HealthPoints = redbaron.Alive ? Mathf.Max(0.01f, redbaron.hp * FpsDamage.PointsPerRedbaronPoint) : 0f;
        }

        void Update()
        {
            if (fps.HealthPoints < last - 0.001f)
            {
                float lost = (last - fps.HealthPoints) / FpsDamage.PointsPerRedbaronPoint;
                redbaron.Damage(lost, null, DamageKind.Bullet); // armour applies: a tenth to vehicles
            }
            Mirror();
        }
    }
}
