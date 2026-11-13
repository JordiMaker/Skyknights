using UnityEngine;

namespace Redbaron
{
    public enum UnitClass { Infantry, Tank, Mech }
    /// <summary>Grenade is also the mech's grenade launcher. Demolition is the sapper's charge with a fuse.
    /// Flak is the anti-aircraft gun: shells that burst at a plane's distance, and a weak shell against ground targets.</summary>
    public enum WeaponKind { Rifle, MachineGun, Grenade, Flamethrower, Demolition, Cannon, Flak }
    /// <summary>How a hit does its damage. Armour takes little from bullets and a quarter from grenade fragments; only shells,
    /// charges, bombs (Blast) and fire get through (see Combat.Factor).</summary>
    public enum DamageKind { Bullet, Blast, Fire, Fragment }

    /// <summary>
    /// One kind of unit that WaveSpawner sends down the lanes: its look, its toughness and its weapon.
    /// Infantry with a short-range weapon (grenades, flamethrower, demolition charges) also carries a rifle
    /// for anything beyond that range. Vehicles never take cover; they close in until their weapon reaches.
    /// </summary>
    [System.Serializable]
    public class UnitType
    {
        [Tooltip("Name shown in the game (Spanish) and used by WaveSpawner's rotation.")]
        public string name = "Fusilero";
        public UnitClass unitClass = UnitClass.Infantry;
        [Tooltip("Entente looks (Adrian helmet for infantry). One is picked at random for each unit.")]
        public GameObject[] modelsEntente;
        [Tooltip("Central Powers looks (steel helmet for infantry).")]
        public GameObject[] modelsCentrales;
        [Tooltip("Units per lane when this type comes up. 0 = WaveSpawner's minPerLane / maxPerLane.")]
        public int minCount, maxCount;

        [Header("Body")]
        public float hp = 2f;
        public float walkSpeed = 2.6f, runSpeed = 3.6f;
        [Tooltip("Vehicles taller than this are scaled down (m), so they stay below the planes' flight plane.")]
        public float maxHeight = 6f;
        [Tooltip("For rigged models (Asset Store mechs): the Animator controller with a Speed float (0 standing, 1 walking). Kit models leave it empty.")]
        public RuntimeAnimatorController animator;
        [Tooltip("Rigged models only: degrees to turn the model so that it faces the unit's forward (+Z).")]
        public float lookYaw;

        [Header("Weapon")]
        public WeaponKind weapon = WeaponKind.Rifle;
        public float range = 50f;
        [Tooltip("Seconds between shots or bursts; up to 30 % more is added at random.")]
        public float cooldown = 3.25f;
        [Tooltip("Rounds per burst (machine guns).")]
        public int shots = 1;
        [Tooltip("Chance to hit a soldier in the open at close range.")]
        public float hit = 0.55f;
        public float damage = 1f;
        [Tooltip("Radius of the blast or the flames (m). 0 = only the target.")]
        public float splash;

        [Header("Recruiting (TroopBar)")]
        [Tooltip("Units the player gets per press of the button: a squad of infantry, one vehicle.")]
        public int recruitCount = 4;
        [Tooltip("Seconds before the player can summon this type again.")]
        public float recruitEvery = 18f;

        public bool IsVehicle => unitClass != UnitClass.Infantry;
        /// <summary>Infantry whose own weapon is for close work keeps a rifle for the rest.</summary>
        public bool CarriesRifle => unitClass == UnitClass.Infantry && (weapon == WeaponKind.Grenade || weapon == WeaponKind.Flamethrower || weapon == WeaponKind.Demolition);
        public DamageKind Damage => DamageOf(weapon);

        public static DamageKind DamageOf(WeaponKind w) =>
            w == WeaponKind.Rifle || w == WeaponKind.MachineGun ? DamageKind.Bullet : w == WeaponKind.Flamethrower ? DamageKind.Fire
            : w == WeaponKind.Grenade ? DamageKind.Fragment : DamageKind.Blast;

        /// <summary>Copies the numbers (body, weapon, recruiting) of another type, keeping this one's name and models.
        /// For scenes that hold an older roster: UnitType.SyncStats(spawner.unitTypes).</summary>
        public void CopyStatsFrom(UnitType o)
        {
            unitClass = o.unitClass; minCount = o.minCount; maxCount = o.maxCount; lookYaw = o.lookYaw;
            hp = o.hp; walkSpeed = o.walkSpeed; runSpeed = o.runSpeed; maxHeight = o.maxHeight;
            weapon = o.weapon; range = o.range; cooldown = o.cooldown; shots = o.shots; hit = o.hit; damage = o.damage; splash = o.splash;
            recruitCount = o.recruitCount; recruitEvery = o.recruitEvery;
        }

        /// <summary>Brings every type of the list up to the current defaults, by name. Returns how many were updated.</summary>
        public static int SyncStats(System.Collections.Generic.List<UnitType> types)
        {
            int n = 0;
            foreach (var d in Defaults())
                foreach (var t in types)
                    if (t != null && string.Equals(t.name, d.name, System.StringComparison.OrdinalIgnoreCase)) { t.CopyStatsFrom(d); n++; }
            return n;
        }

        // ------------------------------------------------------------ Defaults
        /// <summary>The rifle every infantryman carries, and the type of a soldier spawned without one.</summary>
        public static readonly UnitType Rifleman = Infantry("Fusilero", WeaponKind.Rifle, 50, 3.25f, 1, 0.55f, 1, 0);

        static UnitType Infantry(string name, WeaponKind w, float range, float cooldown, int shots, float hit, float damage, float splash, float walk = 2.6f, float recruit = 18f)
            => new UnitType { name = name, weapon = w, range = range, cooldown = cooldown, shots = shots, hit = hit, damage = damage, splash = splash, walkSpeed = walk, runSpeed = walk + 1f,
                              recruitCount = 4, recruitEvery = recruit };

        static UnitType Vehicle(string name, UnitClass c, float hp, float speed, WeaponKind w, float range, float cooldown, int shots, float hit, float damage, float splash, float recruit = 40f)
            => new UnitType { name = name, unitClass = c, hp = hp, walkSpeed = speed, runSpeed = speed * 1.2f, minCount = 1, maxCount = 1,
                              weapon = w, range = range, cooldown = cooldown, shots = shots, hit = hit, damage = damage, splash = splash,
                              recruitCount = 1, recruitEvery = recruit };

        /// <summary>The unit roster, without models (the scene's WaveSpawner holds them; UnitType.SyncStats updates its numbers).
        /// The kit file each type uses is in the comment. Each type has its own job:
        /// riflemen and machine guns against infantry; grenadiers and the grenade-launcher mech against men in cover;
        /// flamethrowers against cover and bunkers; sappers against buildings (charges, also the only infantry that kills tanks);
        /// tanks and the assault mech against vehicles and buildings; the scout mech against planes.</summary>
        public static UnitType[] Defaults() => new[]
        {
            Infantry("Fusilero", WeaponKind.Rifle, 50, 3.25f, 1, 0.55f, 1, 0),                          // Fusilero
            Infantry("Ametrallador", WeaponKind.MachineGun, 45, 4.5f, 4, 0.22f, 1, 0, 2.3f, 22f),        // Ametrallador
            Infantry("Granadero", WeaponKind.Grenade, 26, 5f, 1, 0.6f, 2, 2.5f, 2.6f, 22f),              // Granadero
            Infantry("Lanzallamas", WeaponKind.Flamethrower, 12, 2.4f, 1, 0.85f, 2, 2f, 2.4f, 26f),      // Lanzallamas
            Infantry("Zapador", WeaponKind.Demolition, 4.5f, 8f, 1, 1f, 14, 4f, 2.6f, 26f),              // Zapador: charge with a 3 s fuse
            Vehicle("Tanque ligero", UnitClass.Tank, 28, 2.2f, WeaponKind.Cannon, 45, 4.5f, 1, 0.5f, 3, 2.5f, 40f),          // Lince
            Vehicle("Tanqueta lanzallamas", UnitClass.Tank, 20, 2.6f, WeaponKind.Flamethrower, 14, 1.8f, 1, 0.9f, 3, 2.5f, 36f), // Escarabajo
            Vehicle("Tanque pesado", UnitClass.Tank, 50, 1.6f, WeaponKind.Cannon, 50, 5f, 1, 0.5f, 4, 3f, 60f),              // Mastodonte
            Vehicle("Mech ligero", UnitClass.Mech, 16, 2.4f, WeaponKind.Grenade, 34, 3.2f, 1, 0.55f, 2, 3f, 40f),            // Tetera: grenade launcher
            Vehicle("Mech explorador", UnitClass.Mech, 16, 3.4f, WeaponKind.Flak, 60, 2.2f, 1, 0.4f, 1, 2f, 40f),            // Zancudo: anti-aircraft
            Vehicle("Mech de asalto", UnitClass.Mech, 34, 1.8f, WeaponKind.Cannon, 45, 4f, 1, 0.5f, 3, 3f, 50f),             // Caldera
            // Asset Store mechs (rigged FBX, animated by their own controllers: see the scene's roster for models and controller).
            Vehicle("Mech blindado", UnitClass.Mech, 44, 1.5f, WeaponKind.Cannon, 48, 4.5f, 1, 0.5f, 4, 3f, 60f),            // The Assault Mech (Triplebrick)
            Vehicle("Mech táctico", UnitClass.Mech, 18, 3f, WeaponKind.MachineGun, 45, 2.4f, 4, 0.3f, 1, 0, 40f),             // Stylized Sci Fi Mech (RetroStyle)
        };

        /// <summary>Kit model file for each default type, per side (soldiers change helmet, vehicles are only repainted).</summary>
        public static string ModelPath(string typeName, Team side)
        {
            string helmet = side == Team.Centrales ? "_CascoAcero" : "_CascoAdrian";
            switch (typeName)
            {
                case "Fusilero": case "Ametrallador": case "Granadero": case "Lanzallamas": case "Zapador":
                    return "Assets/Redbaron/Soldados/" + typeName + helmet + ".obj";
                case "Tanque ligero": return "Assets/Redbaron/Tanques/Lince.obj";
                case "Tanqueta lanzallamas": return "Assets/Redbaron/Tanques/Escarabajo.obj";
                case "Tanque pesado": return "Assets/Redbaron/Tanques/Mastodonte.obj";
                case "Mech ligero": return "Assets/Redbaron/Mechs/Tetera.obj";
                case "Mech explorador": return "Assets/Redbaron/Mechs/Zancudo.obj";
                case "Mech de asalto": return "Assets/Redbaron/Mechs/Caldera.obj";
            }
            return null;
        }

        /// <summary>Each lane takes the next entry in turn: an infantry squad, then a vehicle, and so on,
        /// with tanks and mechs taking turns among the vehicles.</summary>
        public static readonly string[] DefaultRotation =
        {
            "Fusilero", "Tanque ligero", "Granadero", "Mech ligero", "Ametrallador", "Tanqueta lanzallamas",
            "Lanzallamas", "Mech explorador", "Zapador", "Tanque pesado", "Fusilero", "Mech de asalto",
            "Granadero", "Mech blindado", "Ametrallador", "Mech táctico",
        };
    }
}
