using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VEVE.UI.Personalization
{
    /// <summary>
    /// Finishes tab: swatch grid straight from the local <see cref="FinishesCatalog"/> table
    /// (name / durability / hardness / IR-signature labels). Selection is forwarded through the
    /// <see cref="IFinishApplyTarget"/> seam so a later VEVE.Gear weapon presenter can receive
    /// the finish string without the UI referencing that namespace.
    /// </summary>
    public sealed class FinishesPanel : MonoBehaviour
    {
        public event Action<FinishDefinition> OnFinishApplied;

        private IFinishApplyTarget _target;
        private string _selectedId;
        private bool _built;

        private RectTransform _gridRoot;
        private Text _detailName;
        private Text _detailTags;
        private Image _previewSwatch;
        private readonly Dictionary<string, Image> _swatches = new Dictionary<string, Image>();

        public void Bind(IFinishApplyTarget target)
        {
            _target = target;
            if (!_built)
                return;
            string current = _target != null ? _target.CurrentFinishId : null;
            if (!string.IsNullOrEmpty(current))
                _selectedId = current;
            Refresh();
        }

        public void Build(RectTransform host)
        {
            if (_built || host == null)
                return;
            _built = true;

            Image bg = UiFactory.CreatePanel(host, "FinishesPanel", HudThemeLibrary.PanelInset);
            UiFactory.StretchFull(bg.rectTransform);

            UiFactory.CreateText(bg, "Title", "FINISHES // WEAPON COSMETICS",
                HudThemeLibrary.FontSubhead, HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-14f, 30f), new Vector2(10f, -8f));

            _gridRoot = new GameObject("Grid", typeof(RectTransform)).transform as RectTransform;
            _gridRoot.SetParent(bg.transform, false);
            _gridRoot.anchorMin = new Vector2(0f, 0.30f);
            _gridRoot.anchorMax = new Vector2(1f, 1f);
            _gridRoot.pivot = new Vector2(0.5f, 1f);
            _gridRoot.offsetMin = new Vector2(10f, 0f);
            _gridRoot.offsetMax = new Vector2(-10f, -40f);
            UiFactory.CreateGrid(_gridRoot, new Vector2(300f, 62f),
                new Vector2(HudThemeLibrary.SlotSpacing, HudThemeLibrary.SlotSpacing), 3);

            RectTransform detail = new GameObject("Detail", typeof(RectTransform)).transform as RectTransform;
            detail.SetParent(bg.transform, false);
            detail.anchorMin = new Vector2(0f, 0f);
            detail.anchorMax = new Vector2(1f, 0.28f);
            detail.pivot = new Vector2(0.5f, 0f);
            detail.offsetMin = new Vector2(10f, 10f);
            detail.offsetMax = new Vector2(-10f, 0f);
            UiFactory.CreateHLayout(detail, 8f, new RectOffset(4, 4, 4, 4), false, TextAnchor.MiddleLeft);

            _previewSwatch = UiFactory.CreateImage(detail, "Preview", HudThemeLibrary.OliveDim,
                Image.Type.Simple, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0.5f), new Vector2(60f, 52f), Vector2.zero);
            _detailName = UiFactory.CreateText(detail, "Name", "NO FINISH SELECTED",
                HudThemeLibrary.FontBody, HudThemeLibrary.TextPrimary, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                new Vector2(360f, 52f), Vector2.zero);
            _detailTags = UiFactory.CreateText(detail, "Tags", string.Empty,
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                new Vector2(620f, 52f), Vector2.zero);

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
            _swatches.Clear();

            string currentTarget = _target != null ? _target.CurrentFinishId : null;
            if (string.IsNullOrEmpty(_selectedId) && !string.IsNullOrEmpty(currentTarget))
                _selectedId = currentTarget;

            foreach (FinishDefinition def in FinishesCatalog.All)
            {
                FinishDefinition captured = def;
                bool isSelected = string.Equals(_selectedId, captured.id, StringComparison.OrdinalIgnoreCase);

                RectTransform cell = new GameObject("Finish_" + captured.id,
                    typeof(RectTransform)).transform as RectTransform;
                cell.SetParent(_gridRoot, false);
                Image cellBG = cell.gameObject.AddComponent<Image>();
                cellBG.sprite = UiFactory.GetSolidSprite();
                cellBG.color = isSelected
                    ? HudThemeLibrary.WithAlpha(HudThemeLibrary.SlotSelected, 0.9f)
                    : HudThemeLibrary.WithAlpha(HudThemeLibrary.PanelSurface, 1f);
                Button button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cellBG;
                // One layout on the cell only (swatch + labels row inside it).
                UiFactory.CreateHLayout(cell, 6f, new RectOffset(6, 6, 6, 6), false, TextAnchor.MiddleLeft);

                Image swatch = UiFactory.CreateImage(cell, "Swatch",
                    FinishesCatalog.SwatchColor(captured, HudThemeLibrary.Olive),
                    Image.Type.Simple, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(0f, 0.5f), new Vector2(42f, 42f), Vector2.zero);
                _swatches[captured.id] = swatch;

                RectTransform texts = new GameObject("Texts", typeof(RectTransform)).transform as RectTransform;
                texts.SetParent(cell, false);
                texts.anchorMin = new Vector2(0f, 0f);
                texts.anchorMax = new Vector2(0f, 0f);
                texts.pivot = new Vector2(0f, 0.5f);
                texts.sizeDelta = new Vector2(240f, 52f);
                UiFactory.CreateVLayout(texts, 0f, new RectOffset(0, 0, 0, 0), false);
                UiFactory.CreateText(texts, "Name", captured.displayName.ToUpperInvariant(),
                    HudThemeLibrary.FontBody,
                    isSelected ? HudThemeLibrary.TextOnDark : HudThemeLibrary.TextPrimary,
                    TextAnchor.MiddleLeft,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, 24f), Vector2.zero);
                UiFactory.CreateText(texts, "Tags",
                    captured.durabilityLabel.ToUpperInvariant() + " · "
                    + captured.hardnessLabel.ToUpperInvariant() + " · IR:"
                    + captured.irSignatureTag,
                    HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted, TextAnchor.MiddleLeft,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, 20f), Vector2.zero);

                button.onClick.AddListener(() => Select(captured));
            }

            UpdateDetail();
        }

        private void Select(FinishDefinition def)
        {
            _selectedId = def.id;
            try
            {
                _target?.ApplyFinish(def);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FinishesPanel] apply target rejected finish: " + ex.Message);
            }
            UpdateDetail();
            Refresh();
            OnFinishApplied?.Invoke(def);
        }

        private void UpdateDetail()
        {
            if (_detailName == null)
                return;
            if (FinishesCatalog.TryGet(_selectedId, out FinishDefinition def))
            {
                _detailName.text = def.displayName.ToUpperInvariant() + "  ·  #" + def.colorHex.ToUpperInvariant();
                _detailTags.text = "DURABILITY " + def.durabilityLabel.ToUpperInvariant()
                    + "   ·   HARDNESS " + def.hardnessLabel.ToUpperInvariant()
                    + "   ·   IR SIGNATURE " + def.irSignatureTag
                    + (_target != null ? "   ·   APPLIED" : "   ·   LOCAL PREVIEW ONLY");
                if (_previewSwatch != null)
                    _previewSwatch.color = FinishesCatalog.SwatchColor(def, HudThemeLibrary.Olive);
            }
            else
            {
                _detailName.text = "NO FINISH SELECTED";
                _detailTags.text = string.Empty;
            }
        }
    }
}
