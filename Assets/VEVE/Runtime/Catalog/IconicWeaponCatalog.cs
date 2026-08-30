using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VEVE;

namespace VEVE.Catalog
{
    /// <summary>
    /// High level tactical role used for catalog searching and loadout generation.
    /// </summary>
    public enum WeaponRole
    {
        Pistol,
        Shotgun,
        SubmachineGun,
        PersonalDefenseWeapon,
        Carbine,
        AssaultRifle,
        BattleRifle,
        DesignatedMarksman,
        SniperRifle,
        LightMachineGun,
        GeneralPurposeMachineGun,
        AntiMateriel
    }

    /// <summary>
    /// Data-driven, plain serializable specification for a single iconic weapon.
    /// Deliberately NOT a <see cref="UnityEngine.ScriptableObject"/> so the catalog can be
    /// constructed, searched and unit tested without touching the asset database.
    ///
    /// All ballistics use SI units: velocity m/s, mass kg, ranges m, muzzle energy J (kilograms
    /// metres per second squared). Barrel length and twist rate are millimetres to stay consistent
    /// with <see cref="RealisticWeaponDefinition"/>.
    /// </summary>
    [Serializable]
    public struct WeaponSpec
    {
        public string id;
        public string displayName;
        public string manufacturer;
        public string caliber;
        public string description;

        public WeaponRole role;
        public ProjectileType projectileType;

        [Header("Ballistics")]
        public float muzzleVelocity;
        public float bulletMass;
        public float ballisticCoefficient;
        public float twistRate;
        public float barrelLength;
        public bool smoothbore;

        [Header("Ammunition / Fire")]
        public int magazineCapacity;
        /// <summary>Cyclic rate in rounds/min. Zero for manually operated / semi-only platforms.</summary>
        public float fireRate;
        public float fireInterval;
        public float muzzleEnergy;
        /// <summary>Manufacturer / published nominal muzzle energy, kept for cross validation.</summary>
        public float publishedMuzzleEnergy;
        public float damage;
        public float effectiveRange;
        public float maximumRange;
        public float weaponMass;

        [Header("Recoil profile")]
        public float recoilImpulse;
        public float recoilVertical;
        public float recoilHorizontal;
        public string recoilProfile;

        /// <summary>Free recoil momentum of the projectile in N*s (m * v).</summary>
        public float ProjectileMomentum => bulletMass * muzzleVelocity;

        /// <summary>True kinetic energy 0.5 * m * v^2 derived from the listed mass and velocity.</summary>
        public double KineticEnergy => 0.5 * (double)bulletMass * (double)muzzleVelocity * (double)muzzleVelocity;

        /// <summary>Fractional error between derived kinetic energy and the listed muzzle energy.</summary>
        public double EnergyErrorRatio
        {
            get
            {
                if (muzzleEnergy <= 0f) return double.PositiveInfinity;
                return Math.Abs(KineticEnergy - muzzleEnergy) / muzzleEnergy;
            }
        }
    }

    /// <summary>
    /// Static registry of the iconic arsenal together with a small in-memory query database.
    /// Provides the 18+ reference weapons with real-world specification data and exposes consistency
    /// validation used by <c>BallisticConsistencyTests</c>.
    /// </summary>
    public static class IconicWeaponCatalog
    {
        /// <summary>Energy tolerance (8 %) required between 0.5*m*v^2 and the listed muzzle energy.</summary>
        public const float EnergyTolerance = 0.08f;

        /// <summary>Realistic bounds applied by catalog validation and the consistency test suite.</summary>
        public const float MinPlausibleVelocity = 100f;
        public const float MaxPlausibleVelocity = 1200f;
        public const float MinPlausibleBulletMass = 0.001f;
        public const float MaxPlausibleBulletMass = 0.060f;

        private static readonly WeaponSpec[] specs = BuildSpecs();
        private static readonly WeaponSpecDatabase database = new WeaponSpecDatabase(specs);

        /// <summary>Every catalog entry, in declaration order.</summary>
        public static IReadOnlyList<WeaponSpec> All => specs;

        /// <summary>Query database (Get by id, search by caliber/role).</summary>
        public static WeaponSpecDatabase Database => database;

        public static int Count => specs.Length;

        /// <summary>Look up a weapon by stable id. Returns true when found.</summary>
        public static bool TryGet(string id, out WeaponSpec spec) => database.TryGet(id, out spec);

        public static WeaponSpec Get(string id) => database.Get(id);

        public static IEnumerable<WeaponSpec> ByCaliber(string caliber) => database.SearchByCaliber(caliber);

        public static IEnumerable<WeaponSpec> ByRole(WeaponRole role) => database.SearchByRole(role);

        private static WeaponSpec[] BuildSpecs()
        {
            return new[]
            {
                new WeaponSpec
                {
                    id = "ak74m", displayName = "AK-74M", manufacturer = "Kalashnikov Concern",
                    caliber = "5.45x39mm", role = WeaponRole.AssaultRifle, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 880f, bulletMass = 0.0034f, ballisticCoefficient = 0.18f,
                    twistRate = 200f, barrelLength = 415f, smoothbore = false,
                    magazineCapacity = 30, fireRate = 650f, fireInterval = 0.0923f,
                    muzzleEnergy = 1316f, publishedMuzzleEnergy = 1327f, damage = 34f,
                    effectiveRange = 500f, maximumRange = 3600f, weaponMass = 3.4f,
                    recoilImpulse = 0.62f, recoilVertical = 1.7f, recoilHorizontal = 1.2f,
                    recoilProfile = "Light, sharp crack; mild 5.45 impulse",
                    description = "Russian 5.45mm service rifle, high-velocity small-calibre.",
                },
                new WeaponSpec
                {
                    id = "ak103", displayName = "AK-103", manufacturer = "Kalashnikov Concern",
                    caliber = "7.62x39mm", role = WeaponRole.AssaultRifle, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 715f, bulletMass = 0.00797f, ballisticCoefficient = 0.27f,
                    twistRate = 240f, barrelLength = 415f, smoothbore = false,
                    magazineCapacity = 30, fireRate = 600f, fireInterval = 0.1000f,
                    muzzleEnergy = 2037f, publishedMuzzleEnergy = 2100f, damage = 44f,
                    effectiveRange = 400f, maximumRange = 3500f, weaponMass = 3.3f,
                    recoilImpulse = 0.98f, recoilVertical = 2.3f, recoilHorizontal = 1.5f,
                    recoilProfile = "Moderate, rolling 7.62x39 kick",
                    description = "Kalashnikov-pattern carbine firing the intermediate M43 cartridge.",
                },
                new WeaponSpec
                {
                    id = "m4a1", displayName = "M4A1", manufacturer = "Colt / Remington",
                    caliber = "5.56x45mm NATO", role = WeaponRole.Carbine, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 870f, bulletMass = 0.00402f, ballisticCoefficient = 0.30f,
                    twistRate = 178f, barrelLength = 368f, smoothbore = false,
                    magazineCapacity = 30, fireRate = 800f, fireInterval = 0.0750f,
                    muzzleEnergy = 1521f, publishedMuzzleEnergy = 1560f, damage = 33f,
                    effectiveRange = 500f, maximumRange = 3600f, weaponMass = 3.4f,
                    recoilImpulse = 0.66f, recoilVertical = 1.5f, recoilHorizontal = 1.0f,
                    recoilProfile = "Manageable straight-line recoil",
                    description = "US 14.5\" service carbine, M855 from a Picatinny platform.",
                },
                new WeaponSpec
                {
                    id = "hk416", displayName = "HK416", manufacturer = "Heckler & Koch",
                    caliber = "5.56x45mm NATO", role = WeaponRole.Carbine, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 880f, bulletMass = 0.00402f, ballisticCoefficient = 0.30f,
                    twistRate = 178f, barrelLength = 368f, smoothbore = false,
                    magazineCapacity = 30, fireRate = 800f, fireInterval = 0.0750f,
                    muzzleEnergy = 1557f, publishedMuzzleEnergy = 1600f, damage = 33f,
                    effectiveRange = 500f, maximumRange = 4000f, weaponMass = 3.2f,
                    recoilImpulse = 0.65f, recoilVertical = 1.5f, recoilHorizontal = 1.0f,
                    recoilProfile = "Manageable, short-stroke gas piston",
                    description = "Gas-piston AR derivative with a modfree/free-float rail system.",
                },
                new WeaponSpec
                {
                    id = "scar-l", displayName = "FN SCAR-L (Mk 16)", manufacturer = "FN Herstal",
                    caliber = "5.56x45mm NATO", role = WeaponRole.AssaultRifle, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 860f, bulletMass = 0.00400f, ballisticCoefficient = 0.30f,
                    twistRate = 178f, barrelLength = 356f, smoothbore = false,
                    magazineCapacity = 30, fireRate = 600f, fireInterval = 0.1000f,
                    muzzleEnergy = 1479f, publishedMuzzleEnergy = 1480f, damage = 34f,
                    effectiveRange = 500f, maximumRange = 4000f, weaponMass = 3.56f,
                    recoilImpulse = 0.68f, recoilVertical = 1.6f, recoilHorizontal = 1.0f,
                    recoilProfile = "Smooth, low impulse",
                    description = "Light variant of the SCAR family in 5.56 NATO.",
                },
                new WeaponSpec
                {
                    id = "scar-h", displayName = "FN SCAR-H (Mk 17)", manufacturer = "FN Herstal",
                    caliber = "7.62x51mm NATO", role = WeaponRole.BattleRifle, projectileType = ProjectileType.HollowPoint,
                    muzzleVelocity = 790f, bulletMass = 0.01134f, ballisticCoefficient = 0.49f,
                    twistRate = 254f, barrelLength = 330f, smoothbore = false,
                    magazineCapacity = 20, fireRate = 600f, fireInterval = 0.1000f,
                    muzzleEnergy = 3539f, publishedMuzzleEnergy = 3550f, damage = 58f,
                    effectiveRange = 800f, maximumRange = 4600f, weaponMass = 3.7f,
                    recoilImpulse = 1.65f, recoilVertical = 3.2f, recoilHorizontal = 1.9f,
                    recoilProfile = "Heavy, snappy full-power",
                    description = "Heavy SCAR (175gr M118LR load) chambered in 7.62 NATO.",
                },
                new WeaponSpec
                {
                    id = "mp5a5", displayName = "MP5A5", manufacturer = "Heckler & Koch",
                    caliber = "9x19mm Parabellum", role = WeaponRole.SubmachineGun, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 360f, bulletMass = 0.00800f, ballisticCoefficient = 0.12f,
                    twistRate = 250f, barrelLength = 150f, smoothbore = false,
                    magazineCapacity = 30, fireRate = 800f, fireInterval = 0.0750f,
                    muzzleEnergy = 518f, publishedMuzzleEnergy = 500f, damage = 24f,
                    effectiveRange = 100f, maximumRange = 1200f, weaponMass = 2.54f,
                    recoilImpulse = 0.48f, recoilVertical = 1.0f, recoilHorizontal = 0.7f,
                    recoilProfile = "Low, soft roller-delayed recoil",
                    description = "Roller-delayed 9mm SMG firing 124gr loadings.",
                },
                new WeaponSpec
                {
                    id = "mp7a1", displayName = "MP7A1", manufacturer = "Heckler & Koch",
                    caliber = "4.6x30mm", role = WeaponRole.PersonalDefenseWeapon, projectileType = ProjectileType.ArmorPiercing,
                    muzzleVelocity = 720f, bulletMass = 0.00200f, ballisticCoefficient = 0.15f,
                    twistRate = 230f, barrelLength = 180f, smoothbore = false,
                    magazineCapacity = 20, fireRate = 950f, fireInterval = 0.0632f,
                    muzzleEnergy = 518f, publishedMuzzleEnergy = 520f, damage = 22f,
                    effectiveRange = 100f, maximumRange = 1000f, weaponMass = 1.9f,
                    recoilImpulse = 0.33f, recoilVertical = 0.9f, recoilHorizontal = 0.6f,
                    recoilProfile = "Very light, high rate",
                    description = "PDW firing the low-recoil 4.6x30mm armour-piercing round.",
                },
                new WeaponSpec
                {
                    id = "p90", displayName = "FN P90", manufacturer = "FN Herstal",
                    caliber = "5.7x28mm", role = WeaponRole.PersonalDefenseWeapon, projectileType = ProjectileType.ArmorPiercing,
                    muzzleVelocity = 715f, bulletMass = 0.00200f, ballisticCoefficient = 0.15f,
                    twistRate = 225f, barrelLength = 407f, smoothbore = false,
                    magazineCapacity = 50, fireRate = 900f, fireInterval = 0.0667f,
                    muzzleEnergy = 511f, publishedMuzzleEnergy = 500f, damage = 22f,
                    effectiveRange = 150f, maximumRange = 1500f, weaponMass = 2.6f,
                    recoilImpulse = 0.34f, recoilVertical = 0.9f, recoilHorizontal = 0.6f,
                    recoilProfile = "Very light, top-mounted 50-round magazine",
                    description = "Bullpup PDW feeding the 5.7x28mm round via a half-magazine.",
                },
                new WeaponSpec
                {
                    id = "m249", displayName = "M249 SAW", manufacturer = "FN Herstal",
                    caliber = "5.56x45mm NATO", role = WeaponRole.LightMachineGun, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 950f, bulletMass = 0.00400f, ballisticCoefficient = 0.30f,
                    twistRate = 178f, barrelLength = 465f, smoothbore = false,
                    magazineCapacity = 200, fireRate = 800f, fireInterval = 0.0750f,
                    muzzleEnergy = 1805f, publishedMuzzleEnergy = 1850f, damage = 35f,
                    effectiveRange = 800f, maximumRange = 4000f, weaponMass = 7.5f,
                    recoilImpulse = 0.72f, recoilVertical = 1.8f, recoilHorizontal = 1.3f,
                    recoilProfile = "Sustained, controllable for a belt-fed",
                    description = "Squad automatic weapon, belt/drum fed, long heavy barrel.",
                },
                new WeaponSpec
                {
                    id = "m240b", displayName = "M240B", manufacturer = "FN Herstal",
                    caliber = "7.62x51mm NATO", role = WeaponRole.GeneralPurposeMachineGun, projectileType = ProjectileType.ArmorPiercing,
                    muzzleVelocity = 840f, bulletMass = 0.00950f, ballisticCoefficient = 0.31f,
                    twistRate = 279f, barrelLength = 560f, smoothbore = false,
                    magazineCapacity = 100, fireRate = 700f, fireInterval = 0.0857f,
                    muzzleEnergy = 3352f, publishedMuzzleEnergy = 3330f, damage = 60f,
                    effectiveRange = 1100f, maximumRange = 3700f, weaponMass = 12.5f,
                    recoilImpulse = 1.55f, recoilVertical = 3.0f, recoilHorizontal = 1.8f,
                    recoilProfile = "Heavy cyclic, belt-fed GPMG",
                    description = "7.62 NATO GPMG with a quick-detach flash hider.",
                },
                new WeaponSpec
                {
                    id = "m82a1", displayName = "Barrett M82A1", manufacturer = "Barrett / Desk Tech",
                    caliber = "12.7x99mm (.50 BMG)", role = WeaponRole.AntiMateriel, projectileType = ProjectileType.ArmorPiercing,
                    muzzleVelocity = 853f, bulletMass = 0.04260f, ballisticCoefficient = 0.50f,
                    twistRate = 356f, barrelLength = 737f, smoothbore = false,
                    magazineCapacity = 10, fireRate = 0f, fireInterval = 1.0f,
                    muzzleEnergy = 15498f, publishedMuzzleEnergy = 15500f, damage = 150f,
                    effectiveRange = 1800f, maximumRange = 6800f, weaponMass = 13.8f,
                    recoilImpulse = 5.0f, recoilVertical = 8.0f, recoilHorizontal = 4.0f,
                    recoilProfile = "Severe even with a muzzle brake",
                    description = "Recoil-operated .50 BMG anti-materiel rifle.",
                },
                new WeaponSpec
                {
                    id = "m110-sass", displayName = "M110 SASS", manufacturer = "KSR / Remington",
                    caliber = "7.62x51mm NATO", role = WeaponRole.DesignatedMarksman, projectileType = ProjectileType.HollowPoint,
                    muzzleVelocity = 800f, bulletMass = 0.01134f, ballisticCoefficient = 0.49f,
                    twistRate = 279f, barrelLength = 508f, smoothbore = false,
                    magazineCapacity = 20, fireRate = 0f, fireInterval = 0.15f,
                    muzzleEnergy = 3629f, publishedMuzzleEnergy = 3600f, damage = 62f,
                    effectiveRange = 800f, maximumRange = 4000f, weaponMass = 6.0f,
                    recoilImpulse = 1.7f, recoilVertical = 3.4f, recoilHorizontal = 2.0f,
                    recoilProfile = "Sharp single-shot 7.62 report",
                    description = "Semi-automatic DMR, 175gr match in a free-float chassis.",
                },
                new WeaponSpec
                {
                    id = "glock-17", displayName = "Glock 17", manufacturer = "Glock Ges.m.b.H.",
                    caliber = "9x19mm Parabellum", role = WeaponRole.Pistol, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 375f, bulletMass = 0.00800f, ballisticCoefficient = 0.12f,
                    twistRate = 254f, barrelLength = 114f, smoothbore = false,
                    magazineCapacity = 17, fireRate = 0f, fireInterval = 0.15f,
                    muzzleEnergy = 563f, publishedMuzzleEnergy = 570f, damage = 26f,
                    effectiveRange = 50f, maximumRange = 1500f, weaponMass = 0.71f,
                    recoilImpulse = 0.55f, recoilVertical = 1.5f, recoilHorizontal = 1.0f,
                    recoilProfile = "Snappy, light blaster",
                    description = "Striker-fired 9mm service pistol, 17-round magazine.",
                },
                new WeaponSpec
                {
                    id = "m1911a1", displayName = "M1911A1", manufacturer = "Colt / Military Contract",
                    caliber = "11.43x23mm (.45 ACP)", role = WeaponRole.Pistol, projectileType = ProjectileType.HollowPoint,
                    muzzleVelocity = 255f, bulletMass = 0.01490f, ballisticCoefficient = 0.16f,
                    twistRate = 406f, barrelLength = 127f, smoothbore = false,
                    magazineCapacity = 7, fireRate = 0f, fireInterval = 0.15f,
                    muzzleEnergy = 484f, publishedMuzzleEnergy = 480f, damage = 42f,
                    effectiveRange = 50f, maximumRange = 1300f, weaponMass = 1.10f,
                    recoilImpulse = 0.70f, recoilVertical = 2.0f, recoilHorizontal = 1.3f,
                    recoilProfile = "Heavy push, slower muzzle rise",
                    description = ".45 ACP service pistol, 230gr ball, 7-round single-stack.",
                },
                new WeaponSpec
                {
                    id = "remington-870", displayName = "Remington 870", manufacturer = "Remington",
                    caliber = "12 Gauge (2 3/4 in)", role = WeaponRole.Shotgun, projectileType = ProjectileType.FullMetalJacket,
                    muzzleVelocity = 425f, bulletMass = 0.02835f, ballisticCoefficient = 0.11f,
                    twistRate = 0f, barrelLength = 470f, smoothbore = true,
                    magazineCapacity = 5, fireRate = 0f, fireInterval = 0.75f,
                    muzzleEnergy = 2560f, publishedMuzzleEnergy = 2600f, damage = 90f,
                    effectiveRange = 50f, maximumRange = 150f, weaponMass = 3.3f,
                    recoilImpulse = 3.5f, recoilVertical = 5.5f, recoilHorizontal = 2.5f,
                    recoilProfile = "Hard, heavy pump recoil",
                    description = "Pump shotgun modelled with a nominal 1 oz slug projectile.",
                },
                new WeaponSpec
                {
                    id = "mk14-ebr", displayName = "Mk 14 EBR", manufacturer = "Springfield / USFFN",
                    caliber = "7.62x51mm NATO", role = WeaponRole.DesignatedMarksman, projectileType = ProjectileType.HollowPoint,
                    muzzleVelocity = 830f, bulletMass = 0.01134f, ballisticCoefficient = 0.49f,
                    twistRate = 279f, barrelLength = 559f, smoothbore = false,
                    magazineCapacity = 20, fireRate = 0f, fireInterval = 0.20f,
                    muzzleEnergy = 3906f, publishedMuzzleEnergy = 3900f, damage = 62f,
                    effectiveRange = 800f, maximumRange = 4600f, weaponMass = 4.4f,
                    recoilImpulse = 1.7f, recoilVertical = 3.3f, recoilHorizontal = 2.0f,
                    recoilProfile = "Sharp, free-float chassis",
                    description = "Enhanced Battle Rifle in an adjustable Sage chassis.",
                },
                new WeaponSpec
                {
                    id = "svd-dragunov", displayName = "SVD Dragunov", manufacturer = "Izhmash / Kalashnikov Concern",
                    caliber = "7.62x54mmR", role = WeaponRole.SniperRifle, projectileType = ProjectileType.HollowPoint,
                    muzzleVelocity = 830f, bulletMass = 0.00984f, ballisticCoefficient = 0.43f,
                    twistRate = 240f, barrelLength = 620f, smoothbore = false,
                    magazineCapacity = 10, fireRate = 0f, fireInterval = 0.20f,
                    muzzleEnergy = 3389f, publishedMuzzleEnergy = 3300f, damage = 80f,
                    effectiveRange = 800f, maximumRange = 3800f, weaponMass = 4.3f,
                    recoilImpulse = 1.5f, recoilVertical = 3.2f, recoilHorizontal = 1.8f,
                    recoilProfile = "Full-power, steady",
                    description = "Semi-automatic 7.62x54R marksman rifle.",
                },
            };
        }
    }

    /// <summary>
    /// In-memory queryable database plus consistency invariants over a set of <see cref="WeaponSpec"/>.
    /// Designed for direct unit testing (no ScriptableObject / asset loading required).
    /// </summary>
    public sealed class WeaponSpecDatabase
    {
        private readonly List<WeaponSpec> entries;
        private readonly Dictionary<string, WeaponSpec> byId;

        public WeaponSpecDatabase(IEnumerable<WeaponSpec> specs)
        {
            entries = specs.ToList();
            byId = new Dictionary<string, WeaponSpec>(StringComparer.OrdinalIgnoreCase);
            foreach (WeaponSpec spec in entries)
            {
                byId[spec.id] = spec;
            }
        }

        public IReadOnlyList<WeaponSpec> All => entries;
        public int Count => entries.Count;

        public bool TryGet(string id, out WeaponSpec spec) => byId.TryGetValue(id, out spec);

        public WeaponSpec Get(string id)
        {
            if (byId.TryGetValue(id, out WeaponSpec spec)) return spec;
            throw new KeyNotFoundException($"No weapon spec with id '{id}'.");
        }

        public bool Contains(string id) => byId.ContainsKey(id);

        /// <summary>Case-insensitive substring search over the caliber reference string.</summary>
        public IEnumerable<WeaponSpec> SearchByCaliber(string caliber)
        {
            if (string.IsNullOrEmpty(caliber)) return entries;
            return entries.Where(e => e.caliber != null &&
                e.caliber.IndexOf(caliber, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public IEnumerable<WeaponSpec> SearchByRole(WeaponRole role) => entries.Where(e => e.role == role);
    }

    /// <summary>
    /// Reusable consistency invariant checks. The BallisticConsistencyTests and any runtime QA
    /// tooling share these so the rules are defined exactly once.
    /// </summary>
    public static class WeaponSpecValidator
    {
        /// <summary>
        /// Returns a list of human-readable invariant violations for a single spec. An empty list
        /// means the entry passes every consistency rule.
        /// </summary>
        public static List<string> Validate(WeaponSpec spec)
        {
            var problems = new List<string>();
            string who = string.IsNullOrEmpty(spec.id) ? spec.displayName : spec.id;

            double ke = spec.KineticEnergy;
            if (spec.muzzleEnergy <= 0f)
                problems.Add($"{who}: muzzleEnergy must be > 0");
            else if (spec.EnergyErrorRatio > IconicWeaponCatalog.EnergyTolerance)
                problems.Add($"{who}: 0.5*m*v^2 ({ke:F1} J) is not within {IconicWeaponCatalog.EnergyTolerance:P0} of listed muzzleEnergy ({spec.muzzleEnergy} J)");

            if (spec.publishedMuzzleEnergy > 0f)
            {
                double pubErr = Math.Abs(ke - spec.publishedMuzzleEnergy) / spec.publishedMuzzleEnergy;
                if (pubErr > IconicWeaponCatalog.EnergyTolerance)
                    problems.Add($"{who}: derived energy deviates more than {IconicWeaponCatalog.EnergyTolerance:P0} from publishedMuzzleEnergy ({spec.publishedMuzzleEnergy} J)");
            }

            if (spec.ballisticCoefficient <= 0f)
                problems.Add($"{who}: ballisticCoefficient must be > 0");

            if (spec.bulletMass < IconicWeaponCatalog.MinPlausibleBulletMass || spec.bulletMass > IconicWeaponCatalog.MaxPlausibleBulletMass)
                problems.Add($"{who}: bulletMass {spec.bulletMass} kg outside plausible [{IconicWeaponCatalog.MinPlausibleBulletMass}..{IconicWeaponCatalog.MaxPlausibleBulletMass}]");

            if (spec.muzzleVelocity < IconicWeaponCatalog.MinPlausibleVelocity || spec.muzzleVelocity > IconicWeaponCatalog.MaxPlausibleVelocity)
                problems.Add($"{who}: muzzleVelocity {spec.muzzleVelocity} m/s outside [{IconicWeaponCatalog.MinPlausibleVelocity}..{IconicWeaponCatalog.MaxPlausibleVelocity}]");

            if (spec.effectiveRange <= 0f || spec.maximumRange <= 0f || spec.maximumRange < spec.effectiveRange)
                problems.Add($"{who}: ranges must be positive and ordered (effective {spec.effectiveRange} <= max {spec.maximumRange})");

            if (spec.magazineCapacity <= 0)
                problems.Add($"{who}: magazineCapacity must be > 0");

            if (spec.fireInterval <= 0f)
                problems.Add($"{who}: fireInterval must be > 0");

            if (!spec.smoothbore && spec.twistRate <= 0f)
                problems.Add($"{who}: rifled weapon must have a positive twistRate (or flag smoothbore)");

            return problems;
        }

        public static List<string> ValidateAll(IEnumerable<WeaponSpec> specs) =>
            specs.SelectMany(Validate).ToList();
    }
}
