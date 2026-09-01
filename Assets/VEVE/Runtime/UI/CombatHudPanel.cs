using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using VEVE.Catalog;
using VEVE.Content;

namespace VEVE.UI
{
    /// <summary>
    /// W-H8 combat HUD: assembles a <see cref="CombatHudState"/> from optional sources
    /// (campaign loop session, live pawn counts, proficiency ledger) and renders the pure
    /// presenter string. Builds its canvas once in Start and samples on a 0.25s cadence -
    /// never per frame. Null-safe: with no loop/session the presenter shows its placeholder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHudPanel : MonoBehaviour
    {
        public const string DefaultModeLabel = "STANDBY";
        public const string SkillPlaceholder = "SKILL --";
        private const string PrimaryFamily = "m4a1";

        /// <summary>Optional campaign source; null (or session-less) keeps the placeholder.</summary>
        public CampaignLoopController loop;

        [SerializeField] private float interval = 0.25f;

        private Canvas canvas;
        private Text body;
        private Text skillLabel;
        private string modeLabel = DefaultModeLabel;
        private float t;

        public string CurrentText => body != null ? body.text : string.Empty;
        public string CurrentSkillLabel => skillLabel != null ? skillLabel.text : string.Empty;

        /// <summary>Bind the (optional) campaign loop; safe to call with null.</summary>
        public void BindLoop(CampaignLoopController source)
        {
            loop = source;
            RefreshNow();
        }

        public void SetModeLabel(string label)
        {
            modeLabel = string.IsNullOrEmpty(label) ? DefaultModeLabel : label;
            RefreshNow();
        }

        private void Start()
        {
            BuildHud();
            RefreshNow();
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t < interval) return;
            t = 0f;
            RefreshNow();
        }

        /// <summary>Samples every bound source once and rewrites the readout (null-safe).</summary>
        public void RefreshNow()
        {
            var state = new CombatHudState
            {
                modeLabel = modeLabel,
                health01 = CombatHudState.UnknownHealth01
            };

            MissionSession session = loop != null ? loop.CurrentSession : null;
            if (session != null)
            {
                state.posture01 = ReadPosture01(session);
                ReadSquadCounts(ref state);
                var snap = DebriefModel.DebriefSnapshot;
                if (snap.HasValue)
                    state.missionRankLabel = DebriefModel.RankLabel(snap.Value.score.rank);
            }

            if (body != null) body.text = CombatHudPresenter.Format(state);
            if (skillLabel != null) skillLabel.text = SkillLine();
        }

        private static void ReadSquadCounts(ref CombatHudState state)
        {
            var pawns = Net.NetworkedPlayerPawn.Active;
            if (pawns.Count <= 0)
            {
                // Offline host: the loop deploys a one-man squad (SetSquadSize(1)).
                state.squadAlive = 1;
                state.squadTotal = 1;
                return;
            }
            int alive = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] != null) alive++;
            }
            state.squadAlive = alive;
            state.squadTotal = pawns.Count;
        }

        private static float ReadPosture01(MissionSession session)
        {
            string region = session.Template.regionKey;
            if (!string.IsNullOrEmpty(region) && BiomeSceneProfiles.TryAlertFloor(region, out int alert))
                return Mathf.Clamp01(alert / 4f);
            return 0f;
        }

        private static string SkillLine()
        {
            FamilyXpLedger ledger = FamilyXpLedger.Default;
            if (ledger == null) return SkillPlaceholder;
            ulong owner = 0;
            var pawns = Net.NetworkedPlayerPawn.Active;
            for (int i = 0; i < pawns.Count; i++)
            {
                Net.NetworkedPlayerPawn pawn = pawns[i];
                if (pawn != null && pawn.IsMine)
                {
                    owner = pawn.OwnerClientId;
                    break;
                }
            }
            return "SKILL " + ledger.Skill(owner, PrimaryFamily).ToString(CultureInfo.InvariantCulture);
        }

        private void BuildHud()
        {
            canvas = UiFactory.CreateCanvas("CombatHud", 230);
            var root = UiFactory.CreatePanel(canvas.transform as RectTransform, "Root",
                new Color(0f, 0f, 0f, 0.35f));
            root.rectTransform.anchorMin = new Vector2(0f, 1f);
            root.rectTransform.anchorMax = new Vector2(0f, 1f);
            root.rectTransform.pivot = new Vector2(0f, 1f);
            root.rectTransform.sizeDelta = new Vector2(260f, 168f);
            root.rectTransform.anchoredPosition = new Vector2(24f, -24f);

            body = UiFactory.CreateText(root.rectTransform, "State",
                CombatHudPresenter.Format(default), HudThemeLibrary.FontSubhead,
                HudThemeLibrary.TextOnDark, TextAnchor.UpperLeft,
                new Vector2(0f, 0.24f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            skillLabel = UiFactory.CreateText(root.rectTransform, "Skill", SkillPlaceholder,
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.24f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(12f, 0f));
        }
    }
}
