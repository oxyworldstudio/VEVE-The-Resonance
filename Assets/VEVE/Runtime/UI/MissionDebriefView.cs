using System;
using UnityEngine;
using VEVE.Content;
using VEVE.Net;
using VEVE.Scoring;

namespace VEVE.UI
{
    /// <summary>
    /// Debrief symmetry: the host fills it from the authoritative publish
    /// (MissionScoredEvent); clients build it from the replayed mirror breakdown.
    /// Same data struct either way - produced by the exact same scoring math,
    /// proved by the C4 protocol parity test.
    /// </summary>
    public sealed class MissionDebriefView : MonoBehaviour
    {
        [Serializable]
        public struct Data
        {
            public string templateId;
            public int total;
            public int experience;
            public int intelPoints;
            public MissionRank rank;
            public bool fromAuthoritativePublish;
            public string headline;
        }

        public Data? Last { get; private set; }
        public bool Visible { get; private set; }

        private void OnEnable()
        {
            VEVE.EventBus.SubscribeGlobal<MissionScoredEvent>(OnScored);
        }

        private void OnDisable()
        {
            VEVE.EventBus.UnsubscribeGlobal<MissionScoredEvent>(OnScored);
        }

        private void OnScored(MissionScoredEvent e)
        {
            if (e == null) return;
            Show(From(e.breakdown, e.missionTemplateId, true));
        }

        public void Show(Data d)
        {
            Last = d;
            Visible = true;
        }

        public void Hide()
        {
            Visible = false;
        }

        public static Data From(MissionScoreBreakdown b, string templateId, bool authoritative)
        {
            return new Data
            {
                templateId = string.IsNullOrEmpty(templateId) ? "?" : templateId,
                total = Math.Max(0, b.total),
                experience = Math.Max(0, b.experienceReward),
                intelPoints = Math.Max(0, b.intelPoints),
                rank = b.rank,
                fromAuthoritativePublish = authoritative,
                headline = authoritative ? "MISSION DEBRIEF" : "MISSION DEBRIEF - RELAYED"
            };
        }

        public static Data? FromMirror(NetMissionMirror mirror, string templateId)
        {
            if (mirror == null || !mirror.Finished || !mirror.FinalBreakdown.HasValue) return null;
            return From(mirror.FinalBreakdown.Value, templateId, false);
        }

        /// <summary>Editor/runtime inspector helper (debug).</summary>
        public static Data? BuildPreview(CampaignLoopController loop)
        {
            if (loop == null || loop.CurrentSession == null) return null;
            MissionScoreBreakdown b = loop.CurrentSession.LastBreakdown;
            if (loop.CurrentSession.Phase != MissionPhase.Debrief) return null;
            return From(b, loop.CurrentSession.Template.id, true);
        }
    }
}
