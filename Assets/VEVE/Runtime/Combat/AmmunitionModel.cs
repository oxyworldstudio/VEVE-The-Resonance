namespace VEVE.Combat
{
    /// <summary>
    /// Pure magazine/reserve math extracted from Weapon so reload behaviour is unit-testable.
    /// </summary>
    public static class AmmunitionModel
    {
        /// <summary>Starting fielded reserve loadout: three reloads.</summary>
        public const int ReserveMultiplier = 3;

        /// <summary>Tactical (magazine retained) reload is faster than dry reload (+ bolt release).</summary>
        public const float TacticalTimeFactor = 0.75f;
        public const float DryBoltReleaseSeconds = 0.4f;

        public static int StartReserve(int magazineSize)
        {
            return magazineSize > 0 ? magazineSize * ReserveMultiplier : 0;
        }

        /// <summary>Seconds for a full/dry reload scaled by an optional operator speed multiplier (1 neutral).</summary>
        public static float FullReloadSeconds(float baseReloadTime, float operatorSpeedMultiplier)
        {
            float mult = operatorSpeedMultiplier > 0f ? operatorSpeedMultiplier : 1f;
            return (baseReloadTime > 0f ? baseReloadTime : 2.5f) / mult;
        }

        public static float TacticalReloadSeconds(float baseReloadTime, float operatorSpeedMultiplier)
        {
            return FullReloadSeconds(baseReloadTime, operatorSpeedMultiplier) * TacticalTimeFactor;
        }

        public static float DryReloadSeconds(float baseReloadTime, float operatorSpeedMultiplier)
        {
            return FullReloadSeconds(baseReloadTime, operatorSpeedMultiplier) + DryBoltReleaseSeconds;
        }

        /// <summary>
        /// Transfer rounds from reserve into the magazine up to capacity.
        /// Returns rounds actually transferred; inputs are not mutated (caller owns state).
        /// </summary>
        public static int TransferForReload(int roundsInMagazine, int magazineSize, int reserveRounds, out int newReserve)
        {
            int needed = magazineSize - (roundsInMagazine > 0 ? roundsInMagazine : 0);
            if (needed <= 0 || reserveRounds <= 0)
            {
                newReserve = reserveRounds > 0 ? reserveRounds : 0;
                return 0;
            }
            int transferred = needed < reserveRounds ? needed : reserveRounds;
            newReserve = reserveRounds - transferred;
            return transferred;
        }

        /// <summary>Tactical swap keeps the spent partial magazine conceptually (no rounds back).</summary>
        public static void TacticalTransfer(int roundsInMagazine, int magazineSize, int reserveRounds,
            out int roundsAfter, out int newReserve)
        {
            int localRounds = roundsInMagazine > 0 ? roundsInMagazine : 0;
            int localReserve = reserveRounds > 0 ? reserveRounds : 0;
            if (localRounds >= magazineSize || localReserve <= 0 || magazineSize <= 0)
            {
                roundsAfter = localRounds;
                newReserve = localReserve;
                return;
            }
            roundsAfter = magazineSize;
            newReserve = localReserve - 1;
        }
    }
}
