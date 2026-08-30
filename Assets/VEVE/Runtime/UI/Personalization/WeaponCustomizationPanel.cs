using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VEVE.Catalog;
using VEVE.Customization;

namespace VEVE.UI.Personalization
{
    /// <summary>
    /// 0-1 normalization helpers for the weapon card stat bars. All formulas documented here.
    /// WeaponSpec has no ergonomics field, so a documented proxy is derived from the real
    /// recoilImpulse / weaponMass values normalized across the catalog.
    /// </summary>
    public static class WeaponStatMath
    {
        /// <summary>clamp01((v - min) / (max - min)); returns 0 for a degenerate (max &lt;= min) range.</summary>
        public static float Normalize(float value, float min, float max)
        {
            if (max <= min)
                return 0f;
            return Mathf.Clamp01((value - min) / (max - min));
        }

        /// <summary>Recoil-control bar: 1 - norm(recoilImpulse). Higher = softer shooter.</summary>
        public static float RecoilControl01(float recoilImpulse, float minImpulse, float maxImpulse)
        {
            return 1f - Normalize(recoilImpulse, minImpulse, maxImpulse);
        }

        /// <summary>
        /// Ergonomics proxy = clamp01(0.85 - 0.45*massNorm - 0.25*impulseNorm),
        /// massNorm/impulseNorm being catalog-normalized [0,1] values. Light, low-impulse
        /// weapons score high (pistols ~0.85, anti-materiel ~0.15). No WeaponSpec ergonomics
        /// field exists; this keeps the bar honest without fabricating data.
        /// </summary>
        public static float ErgonomicsProxy01(float massNorm, float impulseNorm)
        {
            return Mathf.Clamp01(0.85f - 0.45f * Mathf.Clamp01(massNorm)
                - 0.25f * Mathf.Clamp01(impulseNorm));
        }

        public static float Min(IEnumerable<WeaponSpec> specs, Func<WeaponSpec, float> selector)
        {
            float min = float.PositiveInfinity;
            foreach (WeaponSpec s in specs) min = Mathf.Min(min, selector(s));
            return float.IsPositiveInfinity(min) ? 0f : min;
        }

        public static float Max(IEnumerable<WeaponSpec> specs, Func<WeaponSpec, float> selector)
        {
            float max = float.NegativeInfinity;
            foreach (WeaponSpec s in specs) max = Mathf.Max(max, selector(s));
            return float.IsNegativeInfinity(max) ? 0f : max;
        }
    }

    /// <summary>
    /// Weapon tab of the personalization workspace: searchable, role/caliber-grouped arsenal
    /// from IconicWeaponCatalog, a live weapon card with normalized stat bars, and an
    /// attachment rack driven by AttachmentCompatibilityMatrix + WeaponCustomizationManager.
    /// Attach/Detach go through the manager instance (created locally when none is bound, so
    /// the tab works standalone today).
    /// </summary>
    public sealed class WeaponCustomizationPanel : MonoBehaviour
    {
        /// <summary>Slot change notification: (ATTACHMENT_SLOT key uppercased, attachmentId or "" when cleared).</summary>
        public event Action<string, string> OnAttachmentChanged;
        public event Action<string> OnWeaponSelected;

        private WeaponCustomizationManager _manager;
        private int _playerLevel = 10;
        private enum GroupMode { ByRole, ByCaliber }
        private GroupMode _groupMode = GroupMode.ByRole;
        private string _search = string.Empty;
        private string _selectedWeaponId;

        private bool _built;
        private RectTransform _listContent;
        private RectTransform _rackContent;
        private readonly List<GameObject> _listRows = new List<GameObject>();
        private readonly List<GameObject> _rackRows = new List<GameObject>();
        private Text _cardName;
        private Text _cardMeta;
        private Slider _barRecoil;
        private Slider _barErgo;
        private Slider _barRange;
        private Slider _barDamage;
        private Text _summaryText;

        /// <summary>Manager instance used for equip/unequip. Defaults to a self-contained instance.</summary>
        public WeaponCustomizationManager Manager
        {
            get => _manager ??= new WeaponCustomizationManager();
            set
            {
                _manager = value;
                if (_built) Refresh();
            }
        }

        /// <summary>Attachment unlocks are level-gated (AttachmentDefinition.requiredLevel).</summary>
        public int PlayerLevel
        {
            get => _playerLevel;
            set
            {
                _playerLevel = Mathf.Max(1, value);
                if (_built) Refresh();
            }
        }

        public bool HasDetailOpen => !string.IsNullOrEmpty(_selectedWeaponId);
        public string SelectedWeaponId => _selectedWeaponId;

        public void ClearDetail()
        {
            _selectedWeaponId = null;
            RebuildRack();
        }

        public void Build(RectTransform host)
        {
            if (_built || host == null)
                return;
            _built = true;

            Image bg = UiFactory.CreatePanel(host, "WeaponPanel", HudThemeLibrary.PanelInset);
            UiFactory.StretchFull(bg.rectTransform);

            // ------------------------------------------------ left: arsenal list
            RectTransform left = CreateAnchored("Arsenal", bg.transform as RectTransform,
                new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(14f, 14f), new Vector2(-8f, -14f));

            UiFactory.CreateText(left, "Title", "ARSENAL", HudThemeLibrary.FontSubhead,
                HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 30f), Vector2.zero);

            Image searchBG = UiFactory.CreateImage(left, "Search", HudThemeLibrary.PanelSurface,
                Image.Type.Simple, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 30f), new Vector2(0f, -36f));
            InputField search = searchBG.gameObject.AddComponent<InputField>();
            Text searchArea = UiFactory.CreateText(searchBG, "Text", string.Empty,
                HudThemeLibrary.FontBody, HudThemeLibrary.TextPrimary, TextAnchor.MiddleLeft);
            Text placeholder = UiFactory.CreateText(searchBG, "Placeholder", "SEARCH NAME / CALIBER / ROLE",
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted, TextAnchor.MiddleLeft);
            search.textComponent = searchArea;
            search.placeholder = placeholder;
            search.characterLimit = 48;
            search.onValueChanged.AddListener(v =>
            {
                _search = v ?? string.Empty;
                RebuildList();
            });

            RectTransform groupRow = CreateAnchored("GroupRow", left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 26f), new Vector2(0f, -70f));
            UiFactory.CreateHLayout(groupRow, 6f, new RectOffset(0, 0, 0, 0), false);
            Button byRole = UiFactory.CreateTableButton(groupRow, "ByRole", "BY ROLE",
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontCaption, new Vector2(110f, 24f));
            Button byCaliber = UiFactory.CreateTableButton(groupRow, "ByCaliber", "BY CALIBER",
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontCaption, new Vector2(110f, 24f));
            byRole.onClick.AddListener(() => { _groupMode = GroupMode.ByRole; RebuildList(); });
            byCaliber.onClick.AddListener(() => { _groupMode = GroupMode.ByCaliber; RebuildList(); });

            Image listArea = UiFactory.CreateImage(left, "ListArea", HudThemeLibrary.PanelBackground,
                Image.Type.Simple, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -82f), new Vector2(0f, -82f));
            ScrollRect scroll = UiFactory.CreateScrollRect(listArea, out _listContent);
            scroll.horizontal = false;
            VerticalLayoutGroup vlist = UiFactory.CreateVLayout(_listContent, 4f,
                new RectOffset(2, 6, 2, 2), false);
            vlist.childForceExpandWidth = true;

            // ------------------------------------------------ right: card + rack
            RectTransform right = CreateAnchored("Details", bg.transform as RectTransform,
                new Vector2(0.35f, 0f), new Vector2(1f, 1f), new Vector2(4f, 14f), new Vector2(-14f, -14f));

            Image card = UiFactory.CreatePanel(right, "Card", HudThemeLibrary.PanelSurface);
            RectTransform cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0f, 0.82f);
            cardRect.anchorMax = new Vector2(1f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(-14f, 0f);

            _cardName = UiFactory.CreateText(card, "Name", "NO WEAPON SELECTED",
                HudThemeLibrary.FontSubhead + 2, HudThemeLibrary.TextPrimary, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.55f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-24f, 0f), Vector2.zero);
            _cardMeta = UiFactory.CreateText(card, "Meta", "SELECT A PLATFORM FROM THE ARSENAL",
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.3f), new Vector2(1f, 0.56f), new Vector2(0.5f, 0.5f),
                new Vector2(-24f, 0f), Vector2.zero);

            RectTransform bars = CreateAnchored("Bars", card.transform as RectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(220f, 0f), Vector2.zero);
            UiFactory.CreateVLayout(bars, 2f, new RectOffset(6, 6, 2, 2), true);
            _barRecoil = CreateStatRow(bars, "RECOIL", HudThemeLibrary.Olive);
            _barErgo = CreateStatRow(bars, "ERGONOMICS", HudThemeLibrary.OliveBright);
            _barRange = CreateStatRow(bars, "RANGE", HudThemeLibrary.Amber);
            _barDamage = CreateStatRow(bars, "DAMAGE", HudThemeLibrary.AlertRed);

            Image rackArea = UiFactory.CreateImage(right, "Rack", HudThemeLibrary.PanelBackground,
                Image.Type.Simple, new Vector2(0f, 0.14f), new Vector2(1f, 0.81f),
                new Vector2(0.5f, 0.5f), new Vector2(-14f, 0f), Vector2.zero);
            UiFactory.CreateText(rackArea, "RackTitle", "ATTACHMENT RACK // COMPATIBLE SLOTS",
                HudThemeLibrary.FontBody, HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 26f), new Vector2(6f, -2f));
            ScrollRect rackScroll = UiFactory.CreateScrollRect(rackArea, out _rackContent);
            rackScroll.horizontal = false;
            VerticalLayoutGroup vrack = UiFactory.CreateVLayout(_rackContent, 3f,
                new RectOffset(2, 8, 2, 2), false);
            vrack.childForceExpandWidth = true;

            _summaryText = UiFactory.CreateText(right, "Summary", string.Empty,
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.13f), new Vector2(0f, 0f),
                new Vector2(-14f, 0f), new Vector2(6f, 4f));

            RebuildList();
            RebuildRack();
        }

        /// <summary>Rebuilds the visible content from current filter/selection state.</summary>
        public void Refresh()
        {
            if (!_built)
                return;
            RebuildList();
            RebuildRack();
        }

        // ------------------------------------------------------------------ list

        private void RebuildList()
        {
            ClearChildren(_listContent, _listRows);
            if (_listContent == null)
                return;

            List<WeaponSpec> filtered = FilterWeapons(_search);
            float height = 0f;
            string lastHeader = null;

            foreach (WeaponSpec spec in OrderedForGrouping(filtered))
            {
                string header = _groupMode == GroupMode.ByRole
                    ? spec.role.ToString()
                    : spec.caliber;
                if (!string.IsNullOrEmpty(header) && header != lastHeader)
                {
                    lastHeader = header;
                    Text section = CreateTextRow(_listContent, "H_" + header,
                        "// " + header.ToUpperInvariant(), HudThemeLibrary.FontCaption,
                        HudThemeLibrary.AmberDim, 22f);
                    _ = section;
                    height += 26f;
                }

                bool selected = string.Equals(_selectedWeaponId, spec.id, StringComparison.OrdinalIgnoreCase);
                Image row = CreateButtonRow(_listContent, spec.displayName,
                    spec.role + "  ·  " + spec.caliber, selected, 40f);
                WeaponSpec captured = spec;
                row.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _selectedWeaponId = captured.id;
                    RebuildList();
                    RebuildRack();
                    OnWeaponSelected?.Invoke(captured.id);
                });
                _listRows.Add(row.gameObject);
                height += 44f;
            }

            _listContent.sizeDelta = new Vector2(0f, Mathf.Max(height, 1f));
            UpdateCard();
        }

        private List<WeaponSpec> FilterWeapons(string search)
        {
            List<WeaponSpec> result = new List<WeaponSpec>();
            foreach (WeaponSpec spec in IconicWeaponCatalog.All)
            {
                if (MatchesSearch(spec, search))
                    result.Add(spec);
            }
            return result;
        }

        public static bool MatchesSearch(WeaponSpec spec, string search)
        {
            if (string.IsNullOrEmpty(search))
                return true;
            string q = search.Trim();
            return ContainsIgnoreCase(spec.displayName, q)
                || ContainsIgnoreCase(spec.caliber, q)
                || ContainsIgnoreCase(spec.role.ToString(), q)
                || ContainsIgnoreCase(spec.manufacturer, q);
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<WeaponSpec> OrderedForGrouping(List<WeaponSpec> filtered)
        {
            // Stable grouping: headers follow declaration order of the grouped key.
            List<WeaponSpec> copy = new List<WeaponSpec>(filtered);
            if (_groupMode == GroupMode.ByRole)
                copy.Sort((a, b) => ((int)a.role).CompareTo((int)b.role));
            else
                copy.Sort((a, b) => string.CompareOrdinal(a.caliber ?? string.Empty, b.caliber ?? string.Empty));
            return copy;
        }

        // ------------------------------------------------------------------ card

        private void UpdateCard()
        {
            if (_cardName == null)
                return;
            if (!TrySelectedSpec(out WeaponSpec spec))
            {
                _cardName.text = "NO WEAPON SELECTED";
                _cardMeta.text = "SELECT A PLATFORM FROM THE ARSENAL";
                SetBar(_barRecoil, 0f);
                SetBar(_barErgo, 0f);
                SetBar(_barRange, 0f);
                SetBar(_barDamage, 0f);
                if (_summaryText != null)
                    _summaryText.text = string.Empty;
                return;
            }

            _cardName.text = spec.displayName;
            _cardMeta.text = spec.role + "  ·  " + spec.caliber + "  ·  " + spec.manufacturer;

            float massMin = WeaponStatMath.Min(IconicWeaponCatalog.All, s => s.weaponMass);
            float massMax = WeaponStatMath.Max(IconicWeaponCatalog.All, s => s.weaponMass);
            float impMin = WeaponStatMath.Min(IconicWeaponCatalog.All, s => s.recoilImpulse);
            float impMax = WeaponStatMath.Max(IconicWeaponCatalog.All, s => s.recoilImpulse);
            float rangeMin = WeaponStatMath.Min(IconicWeaponCatalog.All, s => s.effectiveRange);
            float rangeMax = WeaponStatMath.Max(IconicWeaponCatalog.All, s => s.effectiveRange);
            float dmgMin = WeaponStatMath.Min(IconicWeaponCatalog.All, s => s.damage);
            float dmgMax = WeaponStatMath.Max(IconicWeaponCatalog.All, s => s.damage);

            float impulseNorm = WeaponStatMath.Normalize(spec.recoilImpulse, impMin, impMax);
            float massNorm = WeaponStatMath.Normalize(spec.weaponMass, massMin, massMax);

            SetBar(_barRecoil, WeaponStatMath.RecoilControl01(spec.recoilImpulse, impMin, impMax));
            SetBar(_barErgo, WeaponStatMath.ErgonomicsProxy01(massNorm, impulseNorm));
            SetBar(_barRange, WeaponStatMath.Normalize(spec.effectiveRange, rangeMin, rangeMax));
            SetBar(_barDamage, WeaponStatMath.Normalize(spec.damage, dmgMin, dmgMax));
        }

        private static void SetBar(Slider slider, float value)
        {
            if (slider != null)
                slider.value = Mathf.Clamp01(value);
        }

        private bool TrySelectedSpec(out WeaponSpec spec)
        {
            spec = default;
            return !string.IsNullOrEmpty(_selectedWeaponId)
                && IconicWeaponCatalog.TryGet(_selectedWeaponId, out spec);
        }

        // ------------------------------------------------------------------ rack

        private void RebuildRack()
        {
            ClearChildren(_rackContent, _rackRows);
            if (_rackContent == null)
                return;
            if (!TrySelectedSpec(out WeaponSpec spec))
            {
                CreateTextRow(_rackContent, "Empty", "SELECT A WEAPON TO OPEN ITS RACK",
                    HudThemeLibrary.FontBody, HudThemeLibrary.TextMuted, 28f);
                _rackContent.sizeDelta = new Vector2(0f, 32f);
                if (_summaryText != null)
                    _summaryText.text = string.Empty;
                return;
            }

            WeaponCustomizationState state = Manager.GetState(spec.id);
            float height = 0f;
            foreach (AttachmentSlot slot in AttachmentCompatibilityMatrix.GetCompatibleSlots(spec.id))
            {
                string equippedId = GetEquipped(state, slot);
                Text header = CreateTextRow(_rackContent, "Slot_" + slot,
                    slot.ToString().ToUpperInvariant() + "  ·  "
                    + (string.IsNullOrEmpty(equippedId) ? "OPEN" : LookupName(equippedId)),
                    HudThemeLibrary.FontBody,
                    string.IsNullOrEmpty(equippedId) ? HudThemeLibrary.TextSecondary : HudThemeLibrary.TextOnDark,
                    24f);
                _ = header;
                height += 28f;

                List<AttachmentDefinition> options =
                    Manager.GetAttachmentsForSlot(slot, int.MaxValue) ?? new List<AttachmentDefinition>();
                if (options.Count == 0)
                {
                    CreateTextRow(_rackContent, "None_" + slot, "  (no published options)",
                        HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted, 20f);
                    height += 22f;
                    continue;
                }

                foreach (AttachmentDefinition def in options)
                {
                    if (!AttachmentCompatibilityMatrix.IsSlotCompatible(spec.id, slot))
                        continue;
                    height += AddAttachmentRow(spec.id, def, equippedId);
                }
            }

            if (!AttachmentCompatibilityMatrix.HasProfile(spec.id))
            {
                CreateTextRow(_rackContent, "NoMatrix", "MOUNT PROFILE NOT MODELLED IN RAIL MATRIX",
                    HudThemeLibrary.FontCaption, HudThemeLibrary.AlertRedDim, 22f);
                height += 24f;
            }

            _rackContent.sizeDelta = new Vector2(0f, Mathf.Max(height, 1f));
            UpdateSummary(spec);
        }

        private float AddAttachmentRow(string weaponId, AttachmentDefinition def, string equippedId)
        {
            RectTransform row = CreateRow(_rackContent, "Att_" + def.attachmentId, 30f);
            HorizontalLayoutGroup h = UiFactory.CreateHLayout(row, 6f,
                new RectOffset(10, 6, 2, 2), true, TextAnchor.MiddleLeft);
            _ = h;

            bool isEquipped = string.Equals(equippedId, def.attachmentId, StringComparison.OrdinalIgnoreCase);
            string deltas = FormatDelta(def);

            Text label = UiFactory.CreateText(row, "Info",
                (isEquipped ? "▶ " : "• ") + def.displayName + "   " + deltas
                + "   " + def.weight.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " KG",
                HudThemeLibrary.FontCaption,
                isEquipped ? HudThemeLibrary.Amber : HudThemeLibrary.TextSecondary,
                TextAnchor.MiddleLeft);
            UiFactory.SetMinSize(label.gameObject, 420f, 26f);

            if (isEquipped)
            {
                Button unequip = UiFactory.CreateTableButton(row, "Unequip", "UNEQUIP",
                    HudThemeLibrary.SlotSelected, HudThemeLibrary.TextOnDark,
                    HudThemeLibrary.FontCaption, new Vector2(96f, 24f));
                UnequipSlot(weaponId, def.slot, unequip);
            }
            else
            {
                Button equip = UiFactory.CreateTableButton(row, "Equip", "EQUIP",
                    HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                    HudThemeLibrary.FontCaption, new Vector2(96f, 24f));
                AttachmentSlot capturedSlot = def.slot;
                string capturedId = def.attachmentId;
                bool canAttach = false;
                try { canAttach = Manager.CanAttach(weaponId, capturedId); }
                catch (Exception) { canAttach = false; }
                equip.interactable = canAttach && def.requiredLevel <= _playerLevel;
                equip.onClick.AddListener(() =>
                {
                    if (!Manager.Attach(weaponId, capturedId))
                        return;
                    OnAttachmentChanged?.Invoke(capturedSlot.ToString().ToUpperInvariant(), capturedId);
                    RebuildList();
                    RebuildRack();
                });
            }
            _rackRows.Add(row.gameObject);
            return 34f;
        }

        private void UnequipSlot(string weaponId, AttachmentSlot slot, Button button)
        {
            button.onClick.AddListener(() =>
            {
                if (!Manager.Detach(weaponId, slot))
                    return;
                OnAttachmentChanged?.Invoke(slot.ToString().ToUpperInvariant(), string.Empty);
                RebuildList();
                RebuildRack();
            });
        }

        /// <summary>
        /// Per-item stat delta line built straight from AttachmentDefinition multiplier fields:
        /// signed percent = (multiplier - 1) * 100, so 0.85 recoil reads "RCL -15%".
        /// </summary>
        public static string FormatDelta(AttachmentDefinition def)
        {
            return "ACC " + Percent(def.accuracyModifier)
                 + "  RCL " + Percent(def.recoilModifier)
                 + "  RNG " + Percent(def.rangeModifier)
                 + "  ERG " + Percent(def.ergonomicsModifier);
        }

        private static string Percent(float multiplier)
        {
            // Convention: a non-positive modifier means "unset" (definition field never
            // authored) and is treated as neutral 1.0, so it never renders as -100%.
            float effective = multiplier > 0f ? multiplier : 1f;
            float pct = (effective - 1f) * 100f;
            string sign = pct >= 0f ? "+" : "-";
            return sign + Mathf.Abs(pct).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        // ------------------------------------------------------------------ summary

        private void UpdateSummary(WeaponSpec spec)
        {
            if (_summaryText == null)
                return;
            WeaponCustomizationState state = Manager.GetState(spec.id);
            List<string> lines = new List<string>();
            foreach (AttachmentSlot slot in Enum.GetValues(typeof(AttachmentSlot)))
            {
                string id = GetEquipped(state, slot);
                if (!string.IsNullOrEmpty(id))
                    lines.Add(SlotKey(slot) + " " + LookupName(id));
            }

            float totalMass = Manager.CalculateTotalWeight(spec.id, spec.weaponMass);
            float modifiedRecoil = Manager.CalculateModifiedRecoil(spec.id, spec.recoilImpulse);
            float modifiedRange = Manager.CalculateModifiedRange(spec.id, spec.effectiveRange);

            lines.Insert(0, "CURRENT BUILD // TOTAL MASS "
                + totalMass.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " KG"
                + "  ·  RCL " + modifiedRecoil.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + " N·s  ·  EFF RNG "
                + modifiedRange.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " M");
            if (lines.Count == 1)
                lines.Add("  (clean build — no attachments fitted)");
            _summaryText.text = string.Join("\n", lines);
        }

        // ------------------------------------------------------------------ slot state bridge

        public static string SlotKey(AttachmentSlot slot) => slot.ToString().ToUpperInvariant();

        public static string GetEquipped(WeaponCustomizationState state, AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Optic: return state.equippedOptic;
                case AttachmentSlot.Muzzle: return state.equippedMuzzle;
                case AttachmentSlot.Grip: return state.equippedGrip;
                case AttachmentSlot.Stock: return state.equippedStock;
                case AttachmentSlot.Magazine: return state.equippedMagazine;
                case AttachmentSlot.Barrel: return state.equippedBarrel;
                case AttachmentSlot.Laser: return state.equippedLaser;
                default: return null; // Rail: not modelled by WeaponCustomizationManager.
            }
        }

        private string LookupName(string attachmentId)
        {
            foreach (AttachmentSlot slot in Enum.GetValues(typeof(AttachmentSlot)))
            {
                foreach (AttachmentDefinition def in Manager.GetAttachmentsForSlot(slot, int.MaxValue))
                {
                    if (string.Equals(def.attachmentId, attachmentId, StringComparison.OrdinalIgnoreCase))
                        return def.displayName;
                }
            }
            return attachmentId;
        }

        // ------------------------------------------------------------------ layout helpers (one layout per GO)

        private static RectTransform CreateAnchored(string name, RectTransform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private static RectTransform CreateRow(Transform parent, string name, float height)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.gameObject.AddComponent<CanvasRenderer>();
            return rect;
        }

        private static Slider CreateStatRow(RectTransform parent, string label, Color fillColor)
        {
            // parent already owns a VLayout (Bars) — this cell hosts a nested HLayout child GO.
            RectTransform row = new GameObject(label, typeof(RectTransform)).transform as RectTransform;
            row.SetParent(parent, false);
            UiFactory.SetMinSize(row.gameObject, 460f, 26f);
            UiFactory.CreateHLayout(row, 8f, new RectOffset(0, 0, 0, 0), true, TextAnchor.MiddleLeft);
            Text text = UiFactory.CreateText(row, "Label", label, HudThemeLibrary.FontCaption,
                HudThemeLibrary.TextSecondary, TextAnchor.MiddleLeft);
            UiFactory.SetMinSize(text.gameObject, 130f, 22f);
            Slider slider = UiFactory.CreateSlider(row, label + "_Bar", HudThemeLibrary.SliderTrack,
                fillColor, new Vector2(240f, 10f), Vector2.zero, 0f);
            UiFactory.SetMinSize(slider.gameObject, 240f, 10f);
            return slider;
        }

        private Image CreateButtonRow(RectTransform content, string title, string subtitle,
            bool selected, float height)
        {
            Image row = UiFactory.CreateImage(content, "Row_" + title,
                selected ? HudThemeLibrary.WithAlpha(HudThemeLibrary.SlotSelected, 0.85f)
                         : HudThemeLibrary.WithAlpha(HudThemeLibrary.PanelInset, 0.9f),
                Image.Type.Simple, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, height), Vector2.zero);
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row;
            UiFactory.CreateText(row, "Title", title.ToUpperInvariant(), HudThemeLibrary.FontBody,
                selected ? HudThemeLibrary.TextOnDark : HudThemeLibrary.TextPrimary,
                TextAnchor.MiddleLeft);
            UiFactory.CreateText(row, "Sub", subtitle.ToUpperInvariant(), HudThemeLibrary.FontCaption,
                selected ? HudThemeLibrary.TextOnDark : HudThemeLibrary.TextMuted, TextAnchor.MiddleRight,
                new Vector2(0.45f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-6f, 0f), Vector2.zero);
            return row;
        }

        private static Text CreateTextRow(RectTransform content, string name, string value,
            int fontSize, Color color, float height)
        {
            RectTransform row = CreateRow(content, "T_" + name, height);
            return UiFactory.CreateText(row, "Text", value, fontSize, color, TextAnchor.MiddleLeft);
        }

        private static void ClearChildren(RectTransform content, List<GameObject> tracked)
        {
            if (content == null)
                return;
            foreach (GameObject go in tracked)
            {
                if (go != null)
                    UnityEngine.Object.Destroy(go);
            }
            tracked.Clear();
            foreach (Transform child in content)
            {
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
