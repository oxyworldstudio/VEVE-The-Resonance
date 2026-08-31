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

        /// <summary>
        /// Connected by MissionTransportAdapter (C4b): every tally fact this loop
        /// produces is ALSO emitted as an ordered journal command, so the client
        /// mirror sees exactly what the authoritative session saw.
        /// </summary>
        public System.Action<VEVE.Net.NetCommand> CommandSink;

        /// <summary>
        /// False on pure clients: every gameplay fact becomes a command ONLY (the
        /// host journal owns sequence and the mirror owns replay); local session,
        /// scoreboard and XP stay untouched by definition (client has none).
        /// </summary>
        public bool Authoritative = true;

        /// <summary>W5 reconciler telemetry (confirmed/reverted/late) for debrief + debug overlay.</summary>
        public readonly VEVE.Net.PredictionReconciler Reconciler = new VEVE.Net.PredictionReconciler();

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

        private bool busSubscribed;

        private void Awake()
        {
            EnsureBus(true);
        }

        private void OnEnable()
        {
            EnsureBus(true);
        }

        private void OnDisable()
        {
            EnsureBus(false);
        }

        private void EnsureBus(bool on)
        {
            if (on == busSubscribed) return;
            busSubscribed = on;
            if (on) VEVE.EventBus.SubscribeGlobal<ShotResolvedEvent>(OnShot);
            else VEVE.EventBus.UnsubscribeGlobal<ShotResolvedEvent>(OnShot);
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
            if (!Authoritative) return default; // drafts are a host-only prerogative

            string region = Normalize(string.IsNullOrEmpty(regionKey) ? defaultRegionKey : regionKey);
            int cycle = completedInRegion.TryGetValue(region, out int c) ? c : 0;
            // C7: designer Resources pool preferred, code catalog is the guaranteed fallback
            MissionTemplate template = MissionScheduler.Draft(region, cycle, MissionCatalogSource.Resolve());

            session = new MissionSession(template, difficulty);
            int alert = PostureToIntensity(region);
            session.SetAlertAtInsert(alert);
            session.Deploy();
            publish(MissionPhase.Deployed, template.id);

            CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.MissionStart,
                VEVE.Net.MissionNetMap.IndexOfTemplate(template.id), 0, (float)difficulty));
            CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.AlertSet, alert));

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
            CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.MissionEnd,
                success ? 1 : 0, 0, elapsedSeconds));
            return breakdown;
        }

        // ------------------------------------------------------------------ events

        private void OnShot(ShotResolvedEvent e)
        {
            if (e == null) return;
            ApplyShotFact(e);
        }

        /// <summary>Direct fact injection (host integrators / headless tests): one shot resolved.</summary>
        public void NotifyShot(bool onTarget, bool civilianHarm = false)
        {
            ApplyShotFact(new ShotResolvedEvent
            {
                onTarget = onTarget,
                civilianHarm = civilianHarm,
                predictedTick = UnityEngine.Time.frameCount
            });
        }

        private void ApplyShotFact(ShotResolvedEvent e)
        {
            // W5: retroactive authority - the journal truth revokes optimistic prediction XP
            // grants. Session tallies stay the server's own raycast (never prediction-driven).
            Reconciler.Reconcile(Weapon.Predictions, e.predictedOwner, e.predictedTick, e.onTarget,
                VEVE.Catalog.FamilyXpLedger.Default, string.IsNullOrEmpty(e.family) ? "generic" : e.family,
                VEVE.Catalog.FamilyXpLedger.XpPerHitOnTarget);
            bool onTarget = e.onTarget;
            bool civilianHarm = e.civilianHarm;
            if (Authoritative)
            {
                if (session != null && session.Phase == MissionPhase.Deployed) session.RecordShot(onTarget, civilianHarm);
                if (scoreboard != null)
                {
                    scoreboard.ReportShot(onTarget);
                    if (civilianHarm) scoreboard.ReportCivilianHarm();
                }
            }
            CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.ShotFired,
                onTarget ? 1 : 0, civilianHarm ? 1 : 0));
        }

        public void ReportIntelObject()
        {
            if (Authoritative)
            {
                if (session != null && session.Phase == MissionPhase.Deployed) session.ReportIntelObject();
                if (scoreboard != null) scoreboard.ReportIntelObject();
            }
            CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.IntelObject));
        }

        public void ReportContactHeld()
        {
            if (Authoritative)
            {
                if (session != null && session.Phase == MissionPhase.Deployed) session.ReportContactHeld();
                if (scoreboard != null) scoreboard.ReportContactHeld();
            }
            CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.ContactHeld));
        }

        public void ReportMalfunction()
        {
            if (Authoritative)
            {
                if (session != null && session.Phase == MissionPhase.Deployed) session.ReportMalfunction();
            }
            CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.Malfunction));
        }

        public void SetSquadSize(int members)
        {
            int m = Math.Max(1, members);
            if (Authoritative)
            {
                if (session != null) session.SetSquadTotal(m);
                if (scoreboard != null) scoreboard.ReportSquadTotal(m);
                CommandSink?.Invoke(VEVE.Net.MissionNetMap.Command(VEVE.Net.NetCommandType.SquadTotalSet, m));
            }
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

