using System;
using System.Collections.Generic;
using VEVE.Scoring;
using VEVE.Tactics;

namespace VEVE.Content
{
    /// <summary>Published when the campaign loop advances a mission phase.</summary>
    public sealed class MissionPhaseChangedEvent : VEVE.IEvent
    {
        public MissionPhase phase;
        public string templateId;
    }

    /// <summary>One shot resolved (hit or miss); optional civilian harm on the same round.</summary>
    public sealed class ShotResolvedEvent : VEVE.IEvent
    {
        public bool onTarget;
        public bool civilianHarm;
        /// <summary>Local prediction metadata for reconciliation; -1 when server-side (no prediction to check).</summary>
        public int predictedTick = -1;
        public ulong predictedOwner;
    }

    public enum MissionPhase { Briefing, Deployed, Debrief }

    /// <summary>
    /// Pure, deterministic mission session: the whole loop
    /// draft (B8) &rarr; par scaled by difficulty track &rarr; live tally events &rarr; score (B7)
    /// &rarr; next-mission enemy posture (B4). No Unity singletons on the hot path so the full
    /// chain is unit-testable headlessly.
    /// </summary>
    public sealed class MissionSession
    {
        public MissionTemplate Template { get; private set; }
        public CampaignDifficulty Difficulty { get; private set; }
        /// <summary>Authored par for the template scaled by the difficulty track factor.</summary>
        public float ParSeconds { get; private set; }
        public MissionPhase Phase { get; private set; } = MissionPhase.Briefing;

        private int shotsFired;
        private int shotsOnTarget;
        private int civilianHarm;
        private int intelObjects;
        private int contactsHeld;
        private int squadTotal = 1;
        private int squadLost;
        private int malfunctions;
        private int alertAtInsert;
        private MissionScoreBreakdown lastBreakdown;

        public int ShotsFired => shotsFired;
        public MissionScoreBreakdown LastBreakdown => lastBreakdown;

        public MissionSession(MissionTemplate template, CampaignDifficulty difficulty)
        {
            Template = template;
            Difficulty = difficulty;
            ParSeconds = Math.Max(60f, template.parSeconds * CampaignDifficultyProfile.ParSecondsFactor(difficulty));
        }

        public void Deploy() { Phase = MissionPhase.Deployed; }
        public void SetSquadTotal(int members) { squadTotal = Math.Max(1, members); }
        public void ReportSquadLoss() { squadLost++; }
        public void ReportIntelObject() { intelObjects++; }
        public void ReportContactHeld() { contactsHeld++; }
        public void ReportMalfunction() { malfunctions++; }
        public void SetAlertAtInsert(int level) { alertAtInsert = level < 0 ? 0 : level > 4 ? 4 : level; }

        public void RecordShot(bool onTarget, bool hitCivilian)
        {
            if (Phase != MissionPhase.Deployed) return;
            shotsFired++;
            if (onTarget) shotsOnTarget++;
            if (hitCivilian) civilianHarm++;
        }

        /// <summary>
        /// Completes the mission: computes the B7 breakdown (mission failure zeroes stealth/intel
        /// credit), applies the difficulty XP multiplier, advances to Debrief.
        /// </summary>
        public MissionScoreBreakdown Complete(float elapsedSeconds, bool success)
        {
            var inputs = new MissionScoreInputs
            {
                shotsFired = shotsFired,
                shotsOnTarget = shotsOnTarget,
                missionSeconds = Math.Max(0f, elapsedSeconds),
                parSeconds = ParSeconds,
                contactsHeld = contactsHeld,
                enemyKills = 0,
                civilianHarmEvents = civilianHarm,
                intelObjectsRecovered = success ? intelObjects : 0,
                squadMembersLost = squadLost,
                squadMembersTotal = squadTotal,
                malfunctionCount = malfunctions
            };

            MissionScoreBreakdown b = MissionScoreCalculator.Score(in inputs);

            if (!success)
            {
                // Failed operation: no stealth or intel credit, base halved, rank capped.
                int total = Math.Max(0,
                    (b.baseScore / 2) + b.accuracyBonus + b.timeBonus / 2 + b.crewBonus
                    - b.collateralPenalty - b.frictionPenalty);
                b.baseScore = b.baseScore / 2;
                b.stealthBonus = 0;
                b.intelBonus = 0;
                b.total = total;
                b.rank = total >= 350 ? MissionRank.Grunt : MissionRank.Failed;
                b.intelPoints = Math.Max(0, total / 200);
            }

            // Difficulty track XP uplift, re-clamped to the score contract ceiling.
            b.experienceReward = Clamp.ToInt(Math.Round(
                b.experienceReward * CampaignDifficultyProfile.ExperienceMultiplier(Difficulty)));

            lastBreakdown = b;
            Phase = MissionPhase.Debrief;
            return b;
        }

        /// <summary>Converts the session outcome into the B4 escalation model input.</summary>
        public PostureDelta EscalateToNextMission()
        {
            float lossesPct = squadTotal > 0 ? 100f * squadLost / squadTotal : 0f;
            var inputs = new VEVE.Tactics.MissionOutcomeInput
            {
                squadLossesPct = lossesPct,
                intelCaptured = Math.Max(0, intelObjects),
                missionTimeSeconds = lastDurationSeconds,
                alertLevelDuringInsert = alertAtInsert,
                collateralEvents = Math.Max(0, civilianHarm)
            };
            return VEVE.Tactics.CampaignEscalationModel.Compute(inputs);
        }

        private float lastDurationSeconds;
        public void SetElapsedForEscalation(float seconds) { lastDurationSeconds = Math.Max(0f, seconds); }

        private static class Clamp
        {
            public static int ToInt(double v)
            {
                double r = Math.Round(v, MidpointRounding.AwayFromZero);
                if (r < 0) return 0;
                if (r > 4500) return 4500;
                return (int)r;
            }
        }
    }
}
