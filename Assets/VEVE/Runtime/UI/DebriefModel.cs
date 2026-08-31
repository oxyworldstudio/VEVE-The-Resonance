using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using VEVE.Catalog;
using VEVE.Scoring;
using VEVE.Tactics;

namespace VEVE.UI
{
    /// <summary>Debrief content shaped at mission end: authority truth, per-owner XP, reconciled telemetry.</summary>
    public struct DebriefData
    {
        public string headline;
        public MissionScoreBreakdown score;
        public IReadOnlyList<string> ownerLines;
        public string reconcileTelemetry;
        public string biomeBiomeLightingKey;
    }

    /// <summary>
    /// Debrief composition is pure-logic where possible (Format/OwnerLines) so the
    /// panel is presentation-only and EditMode-verifiable across play sessions.
    /// </summary>
    public static class DebriefModel
    {
        public const double XpVisibleThreshold = 1d;

        public static string RankLabel(MissionRank r)
        {
            switch (r)
            {
                case MissionRank.Ghost: return "GHOST - flawless extraction";
                case MissionRank.Operator: return "OPERATOR";
                case MissionRank.Grunt: return "GRUNT WORK";
                case MissionRank.Failed: return "MISSION FAILED";
                default: return "AFTER ACTION";
            }
        }

        public static List<string> OwnerLines(FamilyXpLedger ledger, IReadOnlyList<ulong> owners,
            Func<ulong, int> pingLookup)
        {
            var lines = new List<string>();
            if (ledger == null || owners == null) return lines;
            foreach (ulong id in owners)
            {
                if (id == 0 || id == Net.LagCompRules.OfflineOwner) continue;
                double xp = 0;
                foreach (var fam in FamilyOf(ledger, id))
                {
                    xp += ledger.Xp(id, fam);
                }
                if (xp < XpVisibleThreshold) continue;
                int ping = pingLookup != null ? pingLookup(id) : 0;
                lines.Add($"client {id}: {xp:F0} xp | ping {ping}ms");
            }
            return lines;
        }

        /// <summary>Families the ledger holds for a client (probe with known catalog keys).</summary>
        static readonly string[] ProbedFamilies =
        {
            "m4a1", "mk18", "glock17", "m1911a1", "mp5", "uiz", "svd", "rem870", "hk416",
            "ak74m", "ak103", "scar-l", "scar-h", "m110", "mk14", "m249", "m240b", "m82a1"
        };

        public static IEnumerable<string> FamilyOf(FamilyXpLedger ledger, ulong owner)
        {
            foreach (string f in ProbedFamilies) yield return f;
        }

        public static void Apply(DebriefData d)
        {
            DebriefSnapshot = d;
        }

        public static DebriefData? DebriefSnapshot { get; private set; }

        public static string Format(DebriefData d)
        {
            string s = RankLabel(d.score.rank) + "\n";
            s += $"intel pts {d.score.intelPoints} | xp +{d.score.experienceReward} | total {d.score.total}";
            if (d.ownerLines != null && d.ownerLines.Count > 0)
            {
                s += "\nSquad:\n" + string.Join("\n", d.ownerLines);
            }
            if (!string.IsNullOrEmpty(d.reconcileTelemetry)) s += "\n" + d.reconcileTelemetry;
            return s;
        }
    }

    /// <summary>
    /// Panel: renders the latest debrief into a UiFactory modal, cleared on new ops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebriefPanel : MonoBehaviour
    {
        private Canvas canvas;
        private UnityEngine.UI.Text body;

        private void Start()
        {
            canvas = UiFactory.CreateCanvas("Debrief", 260);
            var root = UiFactory.CreatePanel(canvas.transform as RectTransform, "Root",
                new Color(0.04f, 0.05f, 0.048f, 0.94f));
            UiFactory.StretchFull(root.rectTransform);
            body = UiFactory.CreateText(root.rectTransform, "Body", "-", 20,
                HudThemeLibrary.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.8f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            canvas.gameObject.SetActive(true); // debug overlay until a mission ends: keep visible on debrief only
            canvas.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            VEVE.EventBus.SubscribeGlobal<VEVE.Content.MissionPhaseChangedEvent>(OnPhase);
        }

        private void OnDisable()
        {
            VEVE.EventBus.UnsubscribeGlobal<VEVE.Content.MissionPhaseChangedEvent>(OnPhase);
        }

        private void OnPhase(VEVE.Content.MissionPhaseChangedEvent e)
        {
            if (e == null) return;
            if (e.phase == VEVE.Content.MissionPhase.Debrief) ShowLatest();
            else if (canvas != null) canvas.gameObject.SetActive(false);
        }

        void ShowLatest()
        {
            var snap = DebriefModel.DebriefSnapshot;
            if (!snap.HasValue || canvas == null) return;
            if (body != null) body.text = DebriefModel.Format(snap.Value);
            canvas.gameObject.SetActive(true);
        }

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
    }
}
