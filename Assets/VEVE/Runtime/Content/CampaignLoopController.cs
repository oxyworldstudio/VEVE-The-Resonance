using System;
using System.Collections.Generic;
using UnityEngine;
using VEVE.Scoring;

namespace VEVE.Content
{
    /// <summary>
    /// Scene orchestrator for the campaign loop: drafts operations from the B8 catalog,
    /// feeds the live session from weapon/telemetry events, finalizes scoring through the
    /// B7 model, grants XP through the progression manager and carries forward the B4
    /// escalation posture per region. Everything is null-safe; with no events the loop
    /// is inert (matches the existing diegetic HUD/legacy tranches).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignLoopController : MonoBehaviour
    {
        [Header("Campaign config (M4-1 region of the milestone demo)")]
        [SerializeField] private string defaultRegionKey = MissionContentCatalog.Regions[0];
        [SerializeField] private CampaignDifficulty difficulty = CampaignDifficulty.Regular;
        [SerializeField] private int draftSeedSalt = 0;

        private MissionSession session;
        private MissionScoreBoard scoreboard;
        private CampaignState campaign;
        private VEVE.Progression.ProgressionManager progressionManager;
        private readonly Dictionary<string, int> completedInRegion = new Dictionary<string, int>();
        private readonly Dictionary<string, VEVE.Tactics.PostureDelta> nextPosture = new Dictionary<string, VEVE.Tactics.PostureDelta>();

        public MissionPhase Phase => session != null ? session.Phase : MissionPhase.Briefing;
        public MissionTemplate CurrentTemplate => session != null ? session.Template : default;
        public MissionSession CurrentSession => session;
        public CampaignDifficulty Difficulty { get => difficulty; set => difficulty = value; }
        public string DefaultRegionKey { get => defaultRegionKey; set => defaultRegionKey = value; }
        public VEVE.Progression.ProgressionManager Manager => progressionManager;

        /// <summary>Posture the enemy will show in a region on the next draft (B4 escalation).</summary>
        public VEVE.Tactics.PostureDelta? PostureForNext(string regionKey)
        {
            if (string.IsNullOrEmpty(regionKey)) return null;
            return nextPosture.TryGetValue(Normalize(regionKey), out VEVE.Tactics.PostureDelta d) ? d : (VEVE.Tactics.PostureDelta?)null;
        }

        private static string Normalize(string k) => k.Trim().ToUpperInvariant();

        private void OnEnable()
        {
            VEVE.EventBus.SubscribeGlobal<ShotResolvedEvent>(OnShot);
        }

        private void OnDisable()
        {
            VEVE.EventBus.UnsubscribeGlobal<ShotResolvedEvent>(OnShot);
        }

        private void Start()
        {
            campaign = UnityEngine.Object.FindFirstObjectByType<CampaignState>();
            scoreboard = UnityEngine.Object.FindFirstObjectByType<MissionScoreBoard>();
            if (progressionManager == null)
            {
                string callsign = campaign != null && campaign.ActiveOperator != null ? campaign.ActiveOperator.callsign : "VEVE-01";
                progressionManager = new VEVE.Progression.ProgressionManager(callsign, callsign);
            }
            SetSquadSize(1);
        }

        // ------------------------------------------------------------------ flow

        /// <summary>Drafts the next operation for a region (falls back to the default region).</summary>
        public MissionTemplate BeginNextMission(string regionKey = null)
        {
            string region = Normalize(string.IsNullOrEmpty(regionKey) ? defaultRegionKey : regionKey);
            int cycle = completedInRegion.TryGetValue(region, out int c) ? c : 0;
            MissionTemplate template = MissionScheduler.Draft(region, cycle);

            session = new MissionSession(template, difficulty);
            session.SetAlertAtInsert(PostureToIntensity(region));
            session.Deploy();
            publish(MissionPhase.Deployed, template.id);

            if (scoreboard != null)
            {
                scoreboard.ReportSquadTotal(1);
            }
            return template;
        }

        /// <summary>
        /// Ends the current operation end-to-end. squadLost may include KIA operators (B3 legacy
        /// hooks consume the same number). XP is granted through the progression manager, the
        /// next posture is stored per region, and MissionScoredEvent fires for UI/scoring.
        /// </summary>
        public MissionScoreBreakdown EndCurrentMission(bool success, float elapsedSeconds, int squadLost = 0)
        {
            if (session == null || session.Phase != MissionPhase.Deployed)
            {
                Debug.LogWarning("[CampaignLoop] EndCurrentMission called without an active operation.");
                return default;
            }

            for (int i = 0; i < squadLost; i++) session.ReportSquadLoss();
            session.SetElapsedForEscalation(elapsedSeconds);
            MissionScoreBreakdown breakdown = session.Complete(elapsedSeconds, success);

            string region = Normalize(session.Template.regionKey);
            completedInRegion[region] = (completedInRegion.TryGetValue(region, out int c) ? c : 0) + 1;
            nextPosture[region] = session.EscalateToNextMission();

            if (progressionManager != null && breakdown.experienceReward > 0)
            {
                progressionManager.AddExperience(breakdown.experienceReward);
            }

            publish(MissionPhase.Debrief, session.Template.id);
            VEVE.EventBus.PublishGlobal(new MissionScoredEvent
            {
                breakdown = breakdown,
                missionTemplateId = session.Template.id
            });
            return breakdown;
        }

        // ------------------------------------------------------------------ events

        private void OnShot(ShotResolvedEvent e)
        {
            if (e == null) return;
            if (session != null && session.Phase == MissionPhase.Deployed)
                session.RecordShot(e.onTarget, e.civilianHarm);
            if (scoreboard != null)
            {
                scoreboard.ReportShot(e.onTarget);
                if (e.civilianHarm) scoreboard.ReportCivilianHarm();
            }
        }

        public void ReportIntelObject()
        {
            if (session != null && session.Phase == MissionPhase.Deployed) session.ReportIntelObject();
            if (scoreboard != null) scoreboard.ReportIntelObject();
        }

        public void ReportContactHeld()
        {
            if (session != null && session.Phase == MissionPhase.Deployed) session.ReportContactHeld();
            if (scoreboard != null) scoreboard.ReportContactHeld();
        }

        public void ReportMalfunction()
        {
            if (session != null && session.Phase == MissionPhase.Deployed) session.ReportMalfunction();
        }

        public void SetSquadSize(int members)
        {
            int m = Math.Max(1, members);
            if (session != null) session.SetSquadTotal(m);
            if (scoreboard != null) scoreboard.ReportSquadTotal(m);
        }

        private int PostureToIntensity(string region)
        {
            if (!nextPosture.TryGetValue(region, out VEVE.Tactics.PostureDelta d)) return 0;
            float severity = d.patrolDensity01 * 0.5f + (1f - Mathf.Min(1f, d.reactionTimeMult / 3f)) * 0.5f;
            if (severity > 0.75f) return 4;
            if (severity > 0.5f) return 3;
            if (severity > 0.25f) return 2;
            return 1;
        }

        private void publish(MissionPhase phase, string id)
        {
            VEVE.EventBus.PublishGlobal(new MissionPhaseChangedEvent { phase = phase, templateId = id });
        }
    }
}
