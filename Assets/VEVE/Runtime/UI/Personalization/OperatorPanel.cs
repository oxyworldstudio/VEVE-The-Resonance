using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VEVE.UI.Personalization
{
    /// <summary>
    /// Operator tab: 12-card roster grid served by the <see cref="IOperatorRosterSource"/> seam
    /// (vacant slots render as "TBD" placeholders until the VEVE.Operator bridge lands). Selecting
    /// a card shows trait labels + specialty badge; APPLY publishes the card through the
    /// <see cref="OnOperatorApplied"/> Action for the workspace / persistence layer.
    /// </summary>
    public sealed class OperatorPanel : MonoBehaviour
    {
        public const int RosterSlots = 12;

        /// <summary>Apply callback seam: workspace (or SaveLoadout bridge) assigns here.</summary>
        public Action<OperatorCardData> OnOperatorApplied;

        private IOperatorRosterSource _roster = new DefaultOperatorRosterSource();
        private OperatorCardData? _selected;
        private bool _built;

        private RectTransform _gridRoot;
        private RectTransform _detailRoot;
        private Text _detailName;
        private Text _detailSpecialty;
        private Text _detailTraits;
        private Image _avatarStrip;
        private readonly Dictionary<string, Image> _cardHighlights = new Dictionary<string, Image>();

        public bool HasDetailOpen => _selected.HasValue;

        public void Bind(IOperatorRosterSource roster)
        {
            _roster = roster ?? new DefaultOperatorRosterSource();
            Refresh();
        }

        public void ClearDetail()
        {
            _selected = null;
            Refresh();
        }

        public void Build(RectTransform host)
        {
            if (_built || host == null)
                return;
            _built = true;

            Image bg = UiFactory.CreatePanel(host, "OperatorPanel", HudThemeLibrary.PanelInset);
            UiFactory.StretchFull(bg.rectTransform);

            UiFactory.CreateText(bg, "Title", "OPERATOR ROSTER", HudThemeLibrary.FontSubhead,
                HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-14f, 30f), new Vector2(10f, -8f));

            _gridRoot = new GameObject("Grid", typeof(RectTransform)).transform as RectTransform;
            _gridRoot.SetParent(bg.transform, false);
            _gridRoot.anchorMin = new Vector2(0f, 0f);
            _gridRoot.anchorMax = new Vector2(0.62f, 1f);
            _gridRoot.pivot = new Vector2(0.5f, 1f);
            _gridRoot.offsetMin = new Vector2(10f, 10f);
            _gridRoot.offsetMax = new Vector2(-8f, -44f);
            UiFactory.CreateGrid(_gridRoot, new Vector2(150f, 108f),
                new Vector2(HudThemeLibrary.SlotSpacing, HudThemeLibrary.SlotSpacing), 4);

            _detailRoot = new GameObject("Detail", typeof(RectTransform)).transform as RectTransform;
            _detailRoot.SetParent(bg.transform, false);
            _detailRoot.anchorMin = new Vector2(0.63f, 0f);
            _detailRoot.anchorMax = new Vector2(1f, 1f);
            _detailRoot.offsetMin = new Vector2(4f, 10f);
            _detailRoot.offsetMax = new Vector2(-12f, -10f);
            Image detailBG = _detailRoot.gameObject.AddComponent<Image>();
            detailBG.sprite = UiFactory.GetSolidSprite();
            detailBG.color = HudThemeLibrary.WithAlpha(HudThemeLibrary.PanelBackground, 0.9f);
            UiFactory.CreateVLayout(_detailRoot, 4f,
                new RectOffset((int)HudThemeLibrary.PaddingMd, (int)HudThemeLibrary.PaddingMd,
                    (int)HudThemeLibrary.PaddingMd, (int)HudThemeLibrary.PaddingMd), false);

            _avatarStrip = UiFactory.CreateImage(_detailRoot, "Avatar", HudThemeLibrary.OliveDim,
                Image.Type.Simple, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 70f), Vector2.zero);
            _detailName = UiFactory.CreateText(_avatarStrip, "Callsign", "NO OPERATOR SELECTED",
                HudThemeLibrary.FontSubhead, HudThemeLibrary.TextOnDark, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.6f), new Vector2(0.5f, 0.5f),
                new Vector2(-20f, 0f), Vector2.zero);
            _detailSpecialty = UiFactory.CreateText(_detailRoot, "Specialty", string.Empty,
                HudThemeLibrary.FontBody, HudThemeLibrary.Amber, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 28f), Vector2.zero);
            _detailTraits = UiFactory.CreateText(_detailRoot, "Traits", string.Empty,
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 170f), Vector2.zero);
            _detailTraits.verticalOverflow = VerticalWrapMode.Truncate;

            Button apply = UiFactory.CreateTableButton(_detailRoot, "Apply", "APPLY OPERATOR",
                HudThemeLibrary.SlotSelected, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontBody, new Vector2(0f, 40f));
            apply.onClick.AddListener(() =>
            {
                if (!_selected.HasValue)
                    return;
                OnOperatorApplied?.Invoke(_selected.Value);
            });

            Refresh();
        }

        public void Refresh()
        {
            if (!_built || _gridRoot == null)
                return;
            foreach (Transform child in _gridRoot)
            {
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            _cardHighlights.Clear();

            OperatorCardData[] operators = _roster != null ? _roster.GetOperators() : null;
            int count = operators != null ? operators.Length : 0;
            int cells = Math.Max(RosterSlots, count);

            for (int i = 0; i < cells; i++)
            {
                bool hasData = i < count;
                OperatorCardData captured = hasData ? operators[i] : default;

                RectTransform cell = new GameObject("Card_" + i, typeof(RectTransform)).transform as RectTransform;
                cell.SetParent(_gridRoot, false);
                Image cellBG = cell.gameObject.AddComponent<Image>();
                cellBG.sprite = UiFactory.GetSolidSprite();
                bool isSelected = hasData && _selected.HasValue
                    && string.Equals(_selected.Value.Id, captured.Id, StringComparison.OrdinalIgnoreCase);
                cellBG.color = isSelected
                    ? HudThemeLibrary.WithAlpha(HudThemeLibrary.SlotSelected, 0.92f)
                    : HudThemeLibrary.WithAlpha(HudThemeLibrary.PanelSurface, 1f);
                Button button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cellBG;
                if (hasData)
                    button.onClick.AddListener(() => Select(captured));
                else
                    button.interactable = false;

                if (hasData)
                {
                    Image avatar = UiFactory.CreateImage(cell, "AvatarBand",
                        ParseHexSafe(captured.AvatarColorHex, HudThemeLibrary.Olive),
                        Image.Type.Simple, new Vector2(0f, 0.55f), new Vector2(1f, 1f),
                        new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), Vector2.zero);
                    _ = avatar;
                    UiFactory.CreateText(cell, "Callsign",
                        (captured.Callsign ?? captured.Id ?? "UNKNOWN").ToUpperInvariant(),
                        HudThemeLibrary.FontBody,
                        isSelected ? HudThemeLibrary.TextOnDark : HudThemeLibrary.TextPrimary,
                        TextAnchor.MiddleCenter,
                        new Vector2(0f, 0.24f), new Vector2(1f, 0.55f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, Vector2.zero);
                    UiFactory.CreateText(cell, "Spec",
                        (captured.Specialty ?? string.Empty).ToUpperInvariant()
                        + "  ·  T" + Mathf.Max(0, captured.TraitCount),
                        HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted,
                        TextAnchor.MiddleCenter,
                        new Vector2(0f, 0f), new Vector2(1f, 0.24f), new Vector2(0.5f, 0.5f),
                        new Vector2(-6f, 0f), Vector2.zero);
                }
                else
                {
                    UiFactory.CreateText(cell, "Empty", "TBD", HudThemeLibrary.FontSubhead,
                        HudThemeLibrary.TextMuted);
                }
            }

            _ = _cardHighlights;
            UpdateDetail(operators, count);
        }

        private void Select(OperatorCardData op)
        {
            _selected = op;
            Refresh();
        }

        private void UpdateDetail(OperatorCardData[] operators, int count)
        {
            if (_detailName == null)
                return;
            if (!_selected.HasValue)
            {
                _detailName.text = count > 0 ? "NO OPERATOR SELECTED" : "ROSTER OFFLINE — SEAMS RETURN PLACEHOLDERS";
                _detailSpecialty.text = string.Empty;
                _detailTraits.text = count > 0
                    ? "PICK AN OPERATOR CARD TO REVIEW SPECIALTY AND TRAITS."
                    : "AWAITING VEVE.OPERATOR BIND (BindOperators).";
                return;
            }
            OperatorCardData op = _selected.Value;
            _detailName.text = (op.Callsign ?? op.Id ?? "UNKNOWN").ToUpperInvariant();
            if (_avatarStrip != null)
                _avatarStrip.color = ParseHexSafe(op.AvatarColorHex, HudThemeLibrary.Olive);
            _detailSpecialty.text = "◆ " + (op.Specialty ?? "UNCLASSIFIED").ToUpperInvariant()
                + "  ·  " + Mathf.Max(0, op.TraitCount) + " TRAITS";

            List<string> labels = new List<string>();
            string[] traits = _roster != null ? _roster.GetTraits(op) : null;
            if (traits != null && traits.Length > 0)
            {
                foreach (string t in traits)
                    labels.Add("· " + t);
            }
            else
            {
                for (int i = 1; i <= Mathf.Max(0, op.TraitCount); i++)
                    labels.Add("· TRAIT " + i.ToString("D2") + " — DESCRIPTION PENDING OPERATOR BIND");
            }
            if (labels.Count == 0)
                labels.Add("· NO ENTRIES ON RECORD");
            _detailTraits.text = string.Join("\n", labels);
        }

        /// <summary>#RRGGBB / RRGGBB parse with theme fallback (pure ColorUtility, no IO).</summary>
        public static Color ParseHexSafe(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex))
                return fallback;
            string normalized = hex.Trim();
            if (!normalized.StartsWith("#", StringComparison.Ordinal))
                normalized = "#" + normalized;
            return ColorUtility.TryParseHtmlString(normalized, out Color parsed) ? parsed : fallback;
        }
    }
}
