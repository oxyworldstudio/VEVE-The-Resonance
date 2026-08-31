using System;
using UnityEngine;

namespace VEVE.Scoring
{
    /// <summary>Raw mission tallies fed to the calculator (design doc §9).</summary>
    public struct MissionScoreInputs
    {
        public int shotsFired;
        public int shotsOnTarget;
        public float missionSeconds;
        public float parSeconds;
        public int contactsHeld;
        public int enemyKills;
        public int civilianHarmEvents;
        public int intelObjectsRecovered;
        public int squadMembersLost;
        public int squadMembersTotal;
        public int malfunctionCount;
    }

    public enum MissionRank { Failed, Grunt, Operator, Ghost }

    /// <summary>Additive score components (design doc §9 scoring formula).</summary>
    public struct MissionScoreBreakdown
    {
        public int baseScore;
        public int accuracyBonus;
        public int timeBonus;
        public int stealthBonus;
        public int intelBonus;
        public int crewBonus;
        public int collateralPenalty;
        public int frictionPenalty;
        public int total;
        public MissionRank rank;
        public int intelPoints;
        public int experienceReward;
    }

    /// <summary>
    /// Pure deterministic mission scoring: no side effects, fully unit-testable.
    /// Accuracy bonus scales hit rate 0..100; time bonus rewards beating par without
    /// rewarding reckless speed; stealth/intel/crew bonuses each clamped by design;
    /// collateral and malfunction events subtract; totals clamped non-negative.
    /// Rank thresholds map total>=1400 Ghost, >=900 Operator, >=350 Grunt, else Failed
    /// (any civilian harm demotes Ghost eligibility, per Ghost = flawless doctrine).
    /// </summary>
    public static class MissionScoreCalculator
    {
        public const int BaseScore = 500;
        public const int MaxAccuracyBonus = 300;
        public const int MaxTimeBonus = 200;
        public const int StealthBonus = 250;
        public const int IntelPointsPerObject = 40;
        public const int CrrBonusPerCrewKept = 60;
        public const int CollateralPenaltyPerEvent = 250;
        public const int FrictionPenaltyPerMalfunction = 25;

        public static MissionScoreBreakdown Score(in MissionScoreInputs i)
        {
            var b = new MissionScoreBreakdown { baseScore = BaseScore };

            float hitRate = ClampRate(i.shotsFired > 0
                ? (float)Mathf.Max(0, i.shotsOnTarget) / Mathf.Max(1, i.shotsFired)
                : 0f);
            b.accuracyBonus = (int)Math.Round(hitRate * MaxAccuracyBonus);

            if (i.parSeconds > 0f && i.missionSeconds > 0f && i.missionSeconds <= i.parSeconds)
            {
                // Linear inside the par window; no extra credit for reckless speed.
                float t = 1f - (i.missionSeconds / i.parSeconds);
                b.timeBonus = (int)Math.Round(Mathf.Clamp01(t) * MaxTimeBonus * 0.5f);
            }

            if (i.contactsHeld == i.squadMembersTotal && i.squadMembersTotal > 0 && i.civilianHarmEvents == 0
                && i.squadMembersLost == 0)
            {
                b.stealthBonus = StealthBonus;
            }

            b.intelBonus = Mathf.Max(0, i.intelObjectsRecovered) * IntelPointsPerObject;
            b.crewBonus = Mathf.Max(0, i.squadMembersTotal - i.squadMembersLost) * CrrBonusPerCrewKept
                        + (i.squadMembersTotal > 0 ? CrrBonusPerCrewKept : 0);
            b.collateralPenalty = Mathf.Max(0, i.civilianHarmEvents) * CollateralPenaltyPerEvent;
            b.frictionPenalty = Mathf.Max(0, i.malfunctionCount) * FrictionPenaltyPerMalfunction;

            b.total = Math.Max(0,
                b.baseScore + b.accuracyBonus + b.timeBonus + b.stealthBonus + b.intelBonus
                + b.crewBonus - b.collateralPenalty - b.frictionPenalty);

            int ghostFloor = 1400;
            b.rank = b.total >= ghostFloor && i.civilianHarmEvents == 0 ? MissionRank.Ghost
                   : b.total >= 900 ? MissionRank.Operator
                   : b.total >= 350 ? MissionRank.Grunt
                   : MissionRank.Failed;

            // Rewards: IntelPoints = f(outcome) monotonic in score & intel; XP likewise with
            // a rank multiplier. Both never negative, XP also never exceeds 4500.
            b.intelPoints = Mathf.Max(0,
                (b.total / 100) + Mathf.Max(0, i.intelObjectsRecovered) * 2);
            b.experienceReward = Mathf.Clamp(
                b.total * (b.rank == MissionRank.Ghost ? 3 : b.rank == MissionRank.Operator ? 2 : 1),
                0, 4500);
            return b;
        }

        private static float ClampRate(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }

    /// <summary>Published when a mission is finalized (consumers: XP grant, intel, legacy).</summary>
    public sealed class MissionScoredEvent : VEVE.IEvent
    {
        public MissionScoreBreakdown breakdown;
        public string missionTemplateId;
    }

    /// <summary>
    /// Live tally collector for the scoring model. Callers report shot hits, intel pickups,
    /// collateral and crew losses during play; FinalizeMission computes/records and publishes
    /// once per mission. Null-safe, zero GC per event beyond counters.
    /// </summary>
    public sealed class MissionScoreBoard : MonoBehaviour
    {
        private int shotsFired;
        private int shotsOnTarget;
        private int intelObjects;
        private int civilianHarm;
        private int malfunctions;
        private int contactsHeld;
        private int squadTotal = 1;
        private float missionStart;
        private MissionScoreBreakdown lastBreakdown;
        private bool finalized;

        public MissionScoreBreakdown LastBreakdown => lastBreakdown;
        public bool Finalized => finalized;

        private void OnEnable()
        {
            missionStart = Time.time;
            finalized = false;
        }

        public void ReportShot(bool onTarget)
        {
            if (finalized) return;
            shotsFired++;
            if (onTarget) shotsOnTarget++;
        }

        public void ReportIntelObject() { if (!finalized) intelObjects++; }
        public void ReportCivilianHarm() { if (!finalized) civilianHarm++; }
        public void ReportMalfunction() { if (!finalized) malfunctions++; }
        public void ReportContactHeld() { if (!finalized) contactsHeld++; }
        public void ReportSquadTotal(int total) { squadTotal = Math.Max(1, total); }

        /// <summary>Computes the final breakdown, caches it and publishes MissionScoredEvent.</summary>
        public MissionScoreBreakdown FinalizeMission(float parSeconds, int squadLost, string missionTemplateId = "")
        {
            var inputs = new MissionScoreInputs
            {
                shotsFired = shotsFired,
                shotsOnTarget = shotsOnTarget,
                missionSeconds = Time.time - missionStart,
                parSeconds = parSeconds,
                contactsHeld = contactsHeld,
                enemyKills = 0,
                civilianHarmEvents = civilianHarm,
                intelObjectsRecovered = intelObjects,
                squadMembersLost = squadLost,
                squadMembersTotal = squadTotal,
                malfunctionCount = malfunctions
            };
            lastBreakdown = MissionScoreCalculator.Score(in inputs);
            finalized = true;
            VEVE.EventBus.PublishGlobal(new MissionScoredEvent
            {
                breakdown = lastBreakdown,
                missionTemplateId = missionTemplateId
            });
            return lastBreakdown;
        }

        public void Reset()
        {
            shotsFired = shotsOnTarget = intelObjects = civilianHarm = malfunctions = contactsHeld = 0;
            finalized = false;
            lastBreakdown = default;
            missionStart = Time.time;
        }
    }
}
