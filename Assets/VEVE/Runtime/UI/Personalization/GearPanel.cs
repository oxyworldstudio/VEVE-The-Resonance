using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VEVE.UI.Personalization
{
    /// <summary>
    /// Gear tab: slot list with equip/clear buttons driven by <see cref="IGearRosterSource"/>,
    /// mass/volume/thermal meters from <see cref="IGearLoadoutPresenter"/> (0-1 normalized),
    /// and a per-HitZone protection coverage readout. Everything degrades to a "standby"
    /// presentation when no data source is bound yet.
    /// </summary>
    public sealed class GearPanel : MonoBehaviour
    {
        /// <summary>(slot key, gear id or "" when cleared) — forwarded into the persisted selection.</summary>
        public event Action<string, string> OnSlotChanged;

        private IGearRosterSource _roster = new DefaultGearRosterSource();
        private IGearLoadoutPresenter _presenter = new DefaultGearLoadoutPresenter();
        private readonly Dictionary<GearSlotKey, int> _pickedIndex = new Dictionary<GearSlotKey, int>();

        private bool _built;
        private Text _statusText;
        private Slider _massBar;
        private Slider _volumeBar;
        private Slider _thermalBar;
        private readonly Dictionary<HitZone, Slider> _zoneBars = new Dictionary<HitZone, Slider>();
        private readonly Dictionary<HitZone, Text> _zoneLabels = new Dictionary<HitZone, Text>();
        private readonly List<GameObject> _slotRows = new List<GameObject>();
        private RectTransform _slotContent;

        public void SetSources(IGearRosterSource roster, IGearLoadoutPresenter presenter)
        {
            _roster = roster ?? new DefaultGearRosterSource();
            _presenter = presenter ?? new DefaultGearLoadoutPresenter();
            Refresh();
        }

        public void Build(RectTransform host)
        {
            if (_built || host == null)
                return;
            _built = true;

            Image bg = UiFactory.CreatePanel(host, "GearPanel", HudThemeLibrary.PanelInset);
            UiFactory.StretchFull(bg.rectTransform);

            // ------------------------------------------------ left: load meters
            RectTransform meters = new GameObject("Meters", typeof(RectTransform)).transform as RectTransform;
            meters.SetParent(bg.transform, false);
            meters.anchorMin = new Vector2(0f, 0.62f);
            meters.anchorMax = new Vector2(0.34f, 1f);
            meters.sizeDelta = new Vector2(0f, 0f);
            meters.anchoredPosition = new Vector2(8f, -8f);
            UiFactory.CreateText(meters, "Title", "LOAD ENVELOPE", HudThemeLibrary.FontSubhead,
                HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 28f), Vector2.zero);
            RectTransform bars = new GameObject("Bars", typeof(RectTransform)).transform as RectTransform;
            bars.SetParent(meters, false);
            bars.anchorMin = new Vector2(0f, 0f);
            bars.anchorMax = new Vector2(1f, 1f);
            bars.offsetMin = new Vector2(6f, 32f);
            bars.offsetMax = new Vector2(-10f, -30f);
            UiFactory.CreateVLayout(bars, 4f, new RectOffset(0, 0, 0, 0), true);
            _massBar = CreateMeterRow(bars, "MASS", HudThemeLibrary.OliveBright);
            _volumeBar = CreateMeterRow(bars, "VOLUME", HudThemeLibrary.Olive);
            _thermalBar = CreateMeterRow(bars, "THERMAL", HudThemeLibrary.AlertRedDim);

            RectTransform coverage = new GameObject("Coverage", typeof(RectTransform)).transform as RectTransform;
            coverage.SetParent(bg.transform, false);
            coverage.anchorMin = new Vector2(0f, 0f);
            coverage.anchorMax = new Vector2(0.34f, 0.60f);
            coverage.offsetMin = new Vector2(8f, 10f);
            coverage.offsetMax = new Vector2(-10f, -4f);
            UiFactory.CreateText(coverage, "Title", "PROTECTION COVERAGE // HIT ZONE",
                HudThemeLibrary.FontBody, HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 24f), Vector2.zero);
            RectTransform zoneList = new GameObject("ZoneList", typeof(RectTransform)).transform as RectTransform;
            zoneList.SetParent(coverage, false);
            zoneList.anchorMin = new Vector2(0f, 0f);
            zoneList.anchorMax = new Vector2(1f, 1f);
            zoneList.offsetMin = new Vector2(4f, 2f);
            zoneList.offsetMax = new Vector2(-4f, -26f);
            UiFactory.CreateVLayout(zoneList, 1f, new RectOffset(0, 0, 0, 0), true);
            foreach (HitZone zone in GearCoverageTable.Zones)
            {
                CreateZoneRow(zoneList, zone);
            }

            // ------------------------------------------------ right: slot grid
            RectTransform slots = new GameObject("Slots", typeof(RectTransform)).transform as RectTransform;
            slots.SetParent(bg.transform, false);
            slots.anchorMin = new Vector2(0.36f, 0f);
            slots.anchorMax = new Vector2(1f, 1f);
            slots.offsetMin = new Vector2(4f, 10f);
            slots.offsetMax = new Vector2(-12f, -44f);
            UiFactory.CreateText(slots, "Title", "GEAR RACK", HudThemeLibrary.FontSubhead,
                HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 30f), Vector2.zero);
            _statusText = UiFactory.CreateText(slots, "Status", string.Empty,
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 20f), new Vector2(0f, -34f));
            Image listBG = UiFactory.CreateImage(slots, "List", HudThemeLibrary.PanelBackground,
                Image.Type.Simple, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(0f, -29f));
            ScrollRect scroll = UiFactory.CreateScrollRect(listBG, out _slotContent);
            scroll.horizontal = false;
            VerticalLayoutGroup v = UiFactory.CreateVLayout(_slotContent, 4f,
                new RectOffset(2, 8, 2, 2), false);
            v.childForceExpandWidth = true;

            Refresh();
        }

        public void Refresh()
        {
            if (!_built)
                return;
            RebuildSlots();
            UpdateMeters();
        }

        // ------------------------------------------------------------------ slots

        private void RebuildSlots()
        {
            if (_slotContent == null)
                return;
            foreach (Transform child in _slotContent)
            {
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            _slotRows.Clear();

            GearSlotKey[] slots = _roster != null ? _roster.GetSlots() : GearSlotKey.DefaultSlots;
            if (slots == null || slots.Length == 0)
                slots = GearSlotKey.DefaultSlots;

            bool anyData = false;
            float height = 0f;
            foreach (GearSlotKey slot in slots)
            {
                GearItemCard[] items = _roster != null ? _roster.GetItems(slot) : null;
                int count = items != null ? items.Length : 0;
                if (count > 0)
                    anyData = true;

                RectTransform row = new GameObject("Slot_" + slot, typeof(RectTransform)).transform as RectTransform;
                row.SetParent(_slotContent, false);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, 34f);
                UiFactory.CreateHLayout(row, 8f, new RectOffset(6, 6, 2, 2), false);

                int picked = 0;
                string label = count > 0
                    ? BuildItemLabel(slot, items, ref picked)
                    : slot.ToString().Replace('_', ' ') + "   ·   STANDBY // AWAITING GEAR SOURCE";
                float coverage = _roster != null ? _roster.GetCoveragePercent(slot) : 0f;

                Text text = UiFactory.CreateText(row, "Label",
                    label + "   CVR " + Mathf.Clamp(coverage, 0f, 100f)
                        .ToString("F0", CultureInfo.InvariantCulture) + "%",
                    HudThemeLibrary.FontCaption,
                    count > 0 ? HudThemeLibrary.TextPrimary : HudThemeLibrary.TextMuted,
                    TextAnchor.MiddleLeft,
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                    new Vector2(420f, 28f), Vector2.zero);
                _ = text;

                if (count > 0)
                {
                    int capturedCount = count;
                    GearSlotKey capturedSlot = slot;
                    GearItemCard[] capturedItems = items;
                    Button equip = UiFactory.CreateTableButton(row, "Cycle", "EQUIP ▶",
                        HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                        HudThemeLibrary.FontCaption, new Vector2(84f, 24f));
                    equip.onClick.AddListener(() =>
                    {
                        int next = (_pickedIndex.TryGetValue(capturedSlot, out int cur)
                            ? cur + 1 : 1) % capturedCount;
                        _pickedIndex[capturedSlot] = next;
                        GearItemCard item = capturedItems[next];
                        OnSlotChanged?.Invoke(capturedSlot.ToString(), item.itemId);
                        RebuildSlots();
                    });

                    Button clear = UiFactory.CreateTableButton(row, "Clear", "CLEAR",
                        HudThemeLibrary.OliveDim, HudThemeLibrary.TextOnDark,
                        HudThemeLibrary.FontCaption, new Vector2(72f, 24f));
                    clear.onClick.AddListener(() =>
                    {
                        _pickedIndex[capturedSlot] = -1;
                        OnSlotChanged?.Invoke(capturedSlot.ToString(), string.Empty);
                        RebuildSlots();
                        UpdateMeters();
                    });
                }

                _slotRows.Add(row.gameObject);
                height += 38f;
            }

            _slotContent.sizeDelta = new Vector2(0f, height);
            if (_statusText != null)
            {
                _statusText.text = anyData
                    ? "ROSTER ONLINE — CYCLE EQUIP TO FIT, CLEAR TO STRIP."
                    : "NO GEAR ROSTER BOUND — SEAMS RETURN PLACEHOLDERS.";
            }
        }

        private string BuildItemLabel(GearSlotKey slot, GearItemCard[] items, ref int picked)
        {
            int index;
            if (_pickedIndex.TryGetValue(slot, out index) && index >= 0 && index < items.Length)
            {
                GearItemCard item = items[index];
                return slot.ToString().Replace('_', ' ') + "   ·   " + item.displayName
                     + "  [" + item.massKg.ToString("0.0", CultureInfo.InvariantCulture) + " KG / "
                     + item.volumeLiters.ToString("0.0", CultureInfo.InvariantCulture) + " L]";
            }
            return slot.ToString().Replace('_', ' ') + "   ·   [EMPTY]";
        }

        // ------------------------------------------------------------------ meters

        private void UpdateMeters()
        {
            float mass = _presenter.TotalMassKg;
            float massCap = Mathf.Max(0.01f, _presenter.MassCapacityKg);
            float vol = _presenter.TotalVolumeLiters;
            float volCap = Mathf.Max(0.01f, _presenter.VolumeCapacityLiters);
            float thermal = Mathf.Clamp01(_presenter.ThermalLoad01);

            _massBar.value = Mathf.Clamp01(mass / massCap);
            _volumeBar.value = Mathf.Clamp01(vol / volCap);
            _thermalBar.value = thermal;

            // Per-zone coverage: presenter wins when it returns >= 0, otherwise aggregate
            // the local GearCoverageTable against each slot's roster coverage (0-100 -> 0-1).
            Dictionary<GearSlotKey, float> protection = null;
            foreach (KeyValuePair<HitZone, Slider> pair in _zoneBars)
            {
                float fromPresenter = _presenter.GetCoveragePercent(pair.Key);
                float percent;
                if (fromPresenter >= 0f)
                {
                    percent = Mathf.Clamp(fromPresenter, 0f, 100f);
                }
                else
                {
                    protection ??= BuildProtectionMap();
                    percent = GearCoverageTable.AggregateZoneCoveragePercent(pair.Key, protection);
                }
                pair.Value.value = percent * 0.01f;
                if (_zoneLabels.TryGetValue(pair.Key, out Text label) && label != null)
                {
                    label.text = pair.Key.ToString().ToUpperInvariant() + "  "
                        + percent.ToString("F0", CultureInfo.InvariantCulture) + "%";
                }
            }
        }

        private Dictionary<GearSlotKey, float> BuildProtectionMap()
        {
            var map = new Dictionary<GearSlotKey, float>();
            GearSlotKey[] slots = _roster != null ? _roster.GetSlots() : GearSlotKey.DefaultSlots;
            if (slots == null)
                return map;
            foreach (GearSlotKey slot in slots)
            {
                float worn = 0f;
                if (_pickedIndex.TryGetValue(slot, out int idx) && idx >= 0)
                {
                    GearItemCard[] items = _roster.GetItems(slot);
                    if (items != null && idx < items.Length)
                        worn = Mathf.Clamp01(items[idx].coveragePercent * 0.01f);
                }
                else
                {
                    worn = Mathf.Clamp01(_roster.GetCoveragePercent(slot) * 0.01f);
                }
                map[slot] = worn;
            }
            return map;
        }

        private static Slider CreateMeterRow(RectTransform parent, string label, Color fillColor)
        {
            RectTransform row = new GameObject(label, typeof(RectTransform)).transform as RectTransform;
            row.SetParent(parent, false);
            UiFactory.SetMinSize(row.gameObject, 320f, 26f);
            UiFactory.CreateHLayout(row, 6f, new RectOffset(0, 0, 0, 0), true, TextAnchor.MiddleLeft);
            Text text = UiFactory.CreateText(row, "Label", label, HudThemeLibrary.FontCaption,
                HudThemeLibrary.TextSecondary, TextAnchor.MiddleLeft);
            UiFactory.SetMinSize(text.gameObject, 80f, 20f);
            Slider slider = UiFactory.CreateSlider(row, label + "_Bar", HudThemeLibrary.SliderTrack,
                fillColor, new Vector2(180f, 10f), Vector2.zero, 0f);
            UiFactory.SetMinSize(slider.gameObject, 180f, 10f);
            return slider;
        }

        private void CreateZoneRow(RectTransform parent, HitZone zone)
        {
            RectTransform row = new GameObject("Zone_" + zone, typeof(RectTransform)).transform as RectTransform;
            row.SetParent(parent, false);
            UiFactory.SetMinSize(row.gameObject, 320f, 22f);
            UiFactory.CreateHLayout(row, 6f, new RectOffset(0, 0, 0, 0), true, TextAnchor.MiddleLeft);
            Text label = UiFactory.CreateText(row, "Label", zone.ToString().ToUpperInvariant(),
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary, TextAnchor.MiddleLeft);
            UiFactory.SetMinSize(label.gameObject, 150f, 18f);
            _zoneLabels[zone] = label;
            Slider bar = UiFactory.CreateSlider(row, "Bar", HudThemeLibrary.SliderTrack,
                HudThemeLibrary.VitalsGreen, new Vector2(120f, 8f), Vector2.zero, 0f);
            UiFactory.SetMinSize(bar.gameObject, 120f, 8f);
            _zoneBars[zone] = bar;
        }
    }
}
