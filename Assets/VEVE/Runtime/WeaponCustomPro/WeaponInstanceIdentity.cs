using System;
using System.Globalization;
using UnityEngine;
using VEVE.Catalog;
using VEVE.Customization;

namespace VEVE.WeaponCustomPro
{
    /// <summary>
    /// Serialised per-identity record for one physical weapon: factory serial, barrel life,
    /// fouling/erosion accumulation, committed turret position, cosmetic finish and rail kit.
    /// Mirrors the wear/fouling state <c>Weapon</c> keeps at runtime, but as plain data so it
    /// survives saves and unit tests without a component. All behavioural hooks are pure
    /// statics that return new values instead of hidden mutation.
    /// </summary>
    [Serializable]
    public sealed class WeaponInstanceIdentity
    {
        /// <summary>Long-run throat-erosion zero creep, clicks/shot (≈1 MOA per 1000 rounds).</summary>
        public const double ErosionDriftClicksPerShot = 0.001;

        /// <summary>FNV-1a 32-bit of the factory plate seed; canonical persistent serial.</summary>
        [SerializeField] private uint sn;
        /// <summary>Mirrors the seed text kept alongside the hash for audit / re-derivation.</summary>
        public string seed = string.Empty;
        public string weaponId = string.Empty;
        /// <summary>Shots down the current barrel (drives erosion and eventually a barrel swap).</summary>
        public int barrelLifeShots;
        /// <summary>Carbon fouling accumulation 0..1 (same band as <c>Weapon</c>'s runtime value).</summary>
        public float fouling;
        /// <summary>Bore erosion 0..1 from heat/velocity wear.</summary>
        public float wear;
        /// <summary>Committed elevation turret position in signed clicks about the saved zero.</summary>
        public int zeroClicksElevation;
        public int zeroClicksWindage;
        /// <summary>Fractional clicks that have not yet rounded into a detent.</summary>
        public float zeroDriftClickAccumulator;
        /// <summary>Catalog id resolved by <c>CosmeticFinishSystem</c>; empty = bare metal.</summary>
        public string finishId = string.Empty;
        /// <summary>Optic rail/adapter kit label; timing/compatibility is owned by the matrix.</summary>
        public string railKitId = string.Empty;
        public DateTime lastZeroDateUtc;

        public uint SerialNumber => sn;

        /// <summary>
        /// FNV-1a (32-bit, offset 2166136261, prime 16777619) over the UTF-8-ish byte pattern of
        /// the seed — the canonical deterministic sn used for persistence and spawn de-dup.
        /// </summary>
        public static uint ComputeSerial(string seedString)
        {
            const uint fnvOffsetBasis = 2166136261;
            const uint fnvPrime = 16777619;
            uint hash = fnvOffsetBasis;
            if (seedString == null) seedString = string.Empty;
            for (int i = 0; i < seedString.Length; i++)
            {
                hash ^= seedString[i];
                hash *= fnvPrime;
            }
            return hash;
        }

        /// <summary>Creates an identity and derives its serial from the seed in one step.</summary>
        public static WeaponInstanceIdentity Create(string seedString, string weaponId)
        {
            var id = new WeaponInstanceIdentity
            {
                seed = seedString ?? string.Empty,
                weaponId = weaponId ?? string.Empty,
            };
            id.sn = ComputeSerial(id.seed);
            id.lastZeroDateUtc = DateTime.UtcNow;
            id.EnsureValid();
            return id;
        }

        /// <summary>Re-derives the serial from the stored seed (anti-tamper on load).</summary>
        public bool Reseal()
        {
            uint recomputed = ComputeSerial(seed);
            bool ok = recomputed == sn;
            if (!ok) sn = recomputed;
            return ok;
        }

        /// <summary>Clamps every field back into its physical range after load/import.</summary>
        public void EnsureValid()
        {
            fouling = Mathf.Clamp01(fouling);
            wear = Mathf.Clamp01(wear);
            barrelLifeShots = Math.Max(0, barrelLifeShots);
            zeroClicksElevation = ZeroingSystem.AdjustClicks(1.0, zeroClicksElevation, 0);
            zeroClicksWindage = ZeroingSystem.WrapClickIndex(zeroClicksWindage);
            zeroDriftClickAccumulator = Math.Max(0f, zeroDriftClickAccumulator);
            seed ??= string.Empty;
            weaponId ??= string.Empty;
            finishId ??= string.Empty;
            railKitId ??= string.Empty;
        }

        /// <summary>Rail kit swap cost, seconds — delegated to the compatibility matrix (single source of truth).</summary>
        public float RailKitSwapSeconds() =>
            AttachmentCompatibilityMatrix.GetQuickDetachSwapTime(weaponId, AttachmentSlot.Optic);

        /// <summary>
        /// First-order zero drift, clicks, for a fire allowance: a small positive creep per
        /// 100 rounds (barrel heat/lockupSettle), multiplied by 1 + weatherGain * severity.
        /// Monotone increasing in both arguments and clamped at zero.
        /// </summary>
        public static double DriftClicksFor(int shotsFired, double weatherSeverity = 0.0,
            double baseDriftPer100Shots = 0.035, double weatherGain = 1.5)
        {
            if (shotsFired <= 0) return 0.0;
            double sev = Math.Max(0.0, weatherSeverity);
            double perHundred = Math.Max(0.0, baseDriftPer100Shots) * (1.0 + Math.Max(0.0, weatherGain) * sev);
            return shotsFired * 0.01 * perHundred;
        }

        /// <summary>
        /// Applies a firing session: accumulates barrel wear and fouling (fouling decays with
        /// cleaning handled by the caller), rolls the pure drift model into the fractional
        /// click accumulator and detents whole clicks into the elevation turret.
        /// </summary>
        public void ApplyFiringAndWeather(int shotsFired, double weatherSeverity = 0.0,
            float foulingRatePerShot = 0.0015f, float erosionRatePerShot = 0.00002f)
        {
            if (shotsFired <= 0) return;
            barrelLifeShots += shotsFired;
            fouling = Mathf.Clamp01(fouling + shotsFired * Math.Max(0f, foulingRatePerShot));
            wear = Mathf.Clamp01(wear + shotsFired * Math.Max(0f, erosionRatePerShot));
            zeroDriftClickAccumulator += (float)DriftClicksFor(shotsFired, weatherSeverity);
            int whole = (int)zeroDriftClickAccumulator;
            if (whole != 0)
            {
                zeroClicksElevation = ZeroingSystem.AdjustClicks(1.0, zeroClicksElevation, whole);
                zeroDriftClickAccumulator -= whole;
            }
            EnsureValid();
        }

        /// <summary>True once the accumulated drift is large enough to justify a fresh live zero.</summary>
        public bool ShouldReZero(double driftClicksTolerance = 1.5, double foulingTolerance = 0.75)
        {
            double totalDriftClicks = Math.Abs(zeroClicksElevation) + zeroDriftClickAccumulator
                + barrelLifeShots * ErosionDriftClicksPerShot;
            return totalDriftClicks > driftClicksTolerance
                || fouling >= foulingTolerance
                || wear >= foulingTolerance;
        }

        /// <summary>JSON-serialisable field mirror (JsonUtility does not ship uint support).</summary>
        [Serializable]
        private struct IdentitySnapshot
        {
            public int sn;
            public string seed;
            public string weaponId;
            public int barrelLifeShots;
            public float fouling;
            public float wear;
            public int zeroClicksElevation;
            public int zeroClicksWindage;
            public float zeroDriftClickAccumulator;
            public string finishId;
            public string railKitId;
            public string lastZeroDateUtc;
        }

        /// <summary>Stable JSON persistence form of the whole identity record.</summary>
        public string IdentitySnapshotJson()
        {
            var s = new IdentitySnapshot
            {
                sn = unchecked((int)sn),
                seed = seed,
                weaponId = weaponId,
                barrelLifeShots = barrelLifeShots,
                fouling = fouling,
                wear = wear,
                zeroClicksElevation = zeroClicksElevation,
                zeroClicksWindage = zeroClicksWindage,
                zeroDriftClickAccumulator = zeroDriftClickAccumulator,
                finishId = finishId,
                railKitId = railKitId,
                lastZeroDateUtc = lastZeroDateUtc.ToString("o", CultureInfo.InvariantCulture),
            };
            return JsonUtility.ToJson(s);
        }

        /// <summary>Parses a snapshot produced by <see cref="IdentitySnapshotJson"/>. False on garbage input.</summary>
        public static bool TryFromSnapshotJson(string json, out WeaponInstanceIdentity identity)
        {
            identity = null;
            if (string.IsNullOrEmpty(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
            try
            {
                IdentitySnapshot s = JsonUtility.FromJson<IdentitySnapshot>(json);
                identity = new WeaponInstanceIdentity
                {
                    seed = s.seed,
                    weaponId = s.weaponId,
                    barrelLifeShots = s.barrelLifeShots,
                    fouling = s.fouling,
                    wear = s.wear,
                    zeroClicksElevation = s.zeroClicksElevation,
                    zeroClicksWindage = s.zeroClicksWindage,
                    zeroDriftClickAccumulator = s.zeroDriftClickAccumulator,
                    finishId = s.finishId,
                    railKitId = s.railKitId,
                };
                identity.sn = unchecked((uint)s.sn);
                if (DateTime.TryParse(s.lastZeroDateUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out DateTime dt))
                {
                    identity.lastZeroDateUtc = dt;
                }
                identity.EnsureValid();
                identity.Reseal();
                return true;
            }
            catch (Exception)
            {
                identity = null;
                return false;
            }
        }
    }
}
