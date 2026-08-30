using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using VEVE.Catalog;

namespace VEVE.UI.Personalization
{
    /// <summary>
    /// Pure vacuum holdover math for the zeroing table. Every formula documented inline.
    /// Model: no drag, no wind; drop(d) = g*d^2/(2*v^2) taken from
    /// <see cref="VEVE.Ballistics.GravityDrop(float,float,float)"/> (stable public API).
    /// The table reports holdover relative to the zero plane; the point-blank crossings use
    /// the full sight-height solution in <see cref="TryZeroCrossings"/>.
    /// </summary>
    public static class ZeroingMath
    {
        /// <summary>1 MOA subtends 0.0002908882 metres per metre of range (pi/10800).</summary>
        public const float MoaMetresPerMetre = 0.0002908882f;
        /// <summary>1 MRAD subtends 0.001 metres per metre of range.</summary>
        public const float MilMetresPerMetre = 0.001f;
        /// <summary>Typical optic-to-bore centre height used for the crossing note (m).</summary>
        public const float DefaultSightHeightMeters = 0.045f;

        public static float VacuumDropMetres(float muzzleVelocity, float distance)
        {
            return VEVE.Ballistics.GravityDrop(muzzleVelocity, distance);
        }

        /// <summary>
        /// Holdover relative to the zero plane: drop(d) - drop(zero) = g*(d^2 - z^2)/(2 v^2).
        /// Positive = aim high (hold over) at that range relative to the zero.
        /// </summary>
        public static float HoldoverMetres(float muzzleVelocity, float distance, float zeroRange)
        {
            return VacuumDropMetres(muzzleVelocity, distance)
                 - VacuumDropMetres(muzzleVelocity, zeroRange);
        }

        public static float MetresToMil(float metres, float distance)
        {
            if (distance <= 0f)
                return 0f;
            return metres / (distance * MilMetresPerMetre);
        }

        public static float MetresToMoa(float metres, float distance)
        {
            if (distance <= 0f)
                return 0f;
            return metres / (distance * MoaMetresPerMetre);
        }

        /// <summary>Clicks = angle / per-click value. Returns 0 when the click value is invalid.</summary>
        public static float Clicks(float angle, float perClick)
        {
            if (perClick <= 0f)
                return 0f;
            return angle / perClick;
        }

        /// <summary>
        /// LOS crossing distances for an elevated bore with sight height o:
        /// a = g/(2 v^2); tan(theta) = (a*z^2 + o)/z; crossings solve a d^2 - tan(theta) d + o = 0.
        /// Returns false when the sight cannot be zeroed at that range (bad input / negative discriminant).
        /// </summary>
        public static bool TryZeroCrossings(float muzzleVelocity, float zeroRange,
            float sightHeight, out float nearMeters, out float farMeters)
        {
            nearMeters = 0f;
            farMeters = 0f;
            if (muzzleVelocity <= 0f || zeroRange <= 0f || sightHeight < 0f)
                return false;
            float a = 9.80665f / (2f * muzzleVelocity * muzzleVelocity);
            float tanTheta = (a * zeroRange * zeroRange + sightHeight) / zeroRange;
            float disc = tanTheta * tanTheta - 4f * a * sightHeight;
            if (disc < 0f)
                return false;
            float root = Mathf.Sqrt(disc);
            nearMeters = (tanTheta - root) / (2f * a);
            farMeters = (tanTheta + root) / (2f * a);
            return true;
        }
    }

    /// <summary>
    /// Zero tab: 100 m-stepped holdover table (mil / MOA / clicks) for the bound weapon, a
    /// zero-range selector, and a point-blank crossing note. Zero defaults come through the
    /// <see cref="IZeroingProvider"/> seam (no ScopeProfile reference); the physics itself is
    /// the documented vacuum approximation built on the public VEVE.Ballistics API.
    /// </summary>
    public sealed class ZeroingPanel : MonoBehaviour
    {
        public const float StepMeters = 100f;
        private static readonly float[] PresetZeros = { 25f, 50f, 100f, 200f, 300f, 500f };

        private IZeroingProvider _provider = new DefaultZeroingProvider();
        private float? _zeroOverrideMeters;
        private WeaponSpec _spec;
        private bool _hasSpec;
        private bool _dirty;
        private bool _built;

        private RectTransform _tableContent;
        private readonly List<GameObject> _tableRows = new List<GameObject>();
        private Text _headerText;
        private Text _noteText;
        private Text _configText;
        private readonly List<Button> _zeroButtons = new List<Button>();

        public IZeroingProvider Provider
        {
            get => _provider;
            set
            {
                _provider = value ?? new DefaultZeroingProvider();
                _dirty = true;
            }
        }

        public float CurrentZeroMeters
        {
            get
            {
                if (_zeroOverrideMeters.HasValue)
                    return _zeroOverrideMeters.Value;
                return _provider != null
                    ? _provider.ZeroRangeMeters
                    : DefaultZeroingProvider.FallbackZeroMeters;
            }
        }

        public void Build(RectTransform host)
        {
            if (_built || host == null)
                return;
            _built = true;

            Image bg = UiFactory.CreatePanel(host, "ZeroingPanel", HudThemeLibrary.PanelInset);
            UiFactory.StretchFull(bg.rectTransform);

            RectTransform left = new GameObject("Config", typeof(RectTransform)).transform as RectTransform;
            left.SetParent(bg.transform, false);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0.32f, 1f);
            left.pivot = new Vector2(0.5f, 0.5f);
            left.sizeDelta = new Vector2(0f, -14f);
            left.anchoredPosition = new Vector2(8f, 0f);

            _headerText = UiFactory.CreateText(left, "Title", "ZEROING // VACUUM TABLE",
                HudThemeLibrary.FontSubhead, HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-12f, 56f), new Vector2(6f, -4f));
            _configText = UiFactory.CreateText(left, "Config", string.Empty,
                HudThemeLibrary.FontBody, HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-12f, 44f), new Vector2(6f, -64f));

            RectTransform zeroRow = new GameObject("ZeroRow", typeof(RectTransform)).transform as RectTransform;
            zeroRow.SetParent(left, false);
            zeroRow.anchorMin = new Vector2(0f, 1f);
            zeroRow.anchorMax = new Vector2(1f, 1f);
            zeroRow.pivot = new Vector2(0f, 1f);
            zeroRow.anchoredPosition = new Vector2(6f, -190f);
            zeroRow.sizeDelta = new Vector2(0f, 30f);
            UiFactory.CreateHLayout(zeroRow, 4f, new RectOffset(0, 0, 2, 2), false);
            foreach (float preset in PresetZeros)
            {
                float captured = preset;
                Button b = UiFactory.CreateTableButton(zeroRow, "Zero" + preset.ToString("F0", CultureInfo.InvariantCulture),
                    preset.ToString("F0", CultureInfo.InvariantCulture),
                    HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                    HudThemeLibrary.FontCaption, new Vector2(56f, 24f));
                b.onClick.AddListener(() =>
                {
                    _zeroOverrideMeters = captured;
                    _dirty = true;
                });
                _zeroButtons.Add(b);
            }

            _noteText = UiFactory.CreateText(left, "Note", string.Empty,
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-12f, 0f), new Vector2(6f, -226f));
            _noteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _noteText.verticalOverflow = VerticalWrapMode.Overflow;

            Image tableArea = UiFactory.CreateImage(bg, "Table", HudThemeLibrary.PanelBackground,
                Image.Type.Simple, new Vector2(0.33f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f), new Vector2(4f, 0f));
            UiFactory.CreateText(tableArea, "TableTitle",
                "RANGE   DROP   HOLDOVER MIL/MOA   CLICKS (0..EFFECTIVE RANGE)",
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 20f), new Vector2(6f, -2f));
            ScrollRect scroll = UiFactory.CreateScrollRect(tableArea, out _tableContent);
            scroll.horizontal = false;
            VerticalLayoutGroup vlist = UiFactory.CreateVLayout(_tableContent, 2f,
                new RectOffset(2, 10, 30, 2), false);
            vlist.childForceExpandWidth = true;

            _dirty = true;
        }

        public void BindWeapon(WeaponSpec spec)
        {
            _spec = spec;
            _hasSpec = true;
            _dirty = true;
        }

        /// <summary>MonoBehaviour message: applies pending changes without rebuilding every frame.</summary>
        public void Update()
        {
            if (!_built || !_dirty)
                return;
            _dirty = false;
            RebuildTable();
        }

        private void RebuildTable()
        {
            if (_tableContent == null)
                return;
            foreach (Transform child in _tableContent)
            {
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            _tableRows.Clear();

            float zero = CurrentZeroMeters;
            float milPerClick = _provider != null ? _provider.MilPerClick : DefaultZeroingProvider.DefaultMilPerClick;
            float moaPerClick = _provider != null ? _provider.MoaPerClick : DefaultZeroingProvider.DefaultMoaPerClick;

            if (_configText != null)
            {
                _configText.text = "ZERO " + F(zero) + " M   ·   TURRET "
                    + F(milPerClick) + " MRAD/CNK   " + F(moaPerClick) + " MOA/CNK";
            }
            foreach (Button b in _zeroButtons)
            {
                if (b == null)
                    continue;
                Image img = b.GetComponent<Image>();
                if (img != null)
                {
                    bool active = Mathf.Abs(ParseButtonValue(b) - zero) < 0.5f;
                    img.color = active ? HudThemeLibrary.SlotSelected : HudThemeLibrary.ButtonNormal;
                }
            }

            if (!_hasSpec)
            {
                if (_headerText != null)
                    _headerText.text = "ZEROING // VACUUM TABLE";
                if (_noteText != null)
                    _noteText.text = "SELECT A WEAPON IN THE RACK TAB TO GENERATE A TABLE. "
                        + "HOLDOVER = G·(D²−Z²)/(2V²), DRAG-FREE.";
                AddRow("RANGE", "DROP CM", "MIL", "MOA", "CLICKS (MIL)", true);
                _tableContent.sizeDelta = new Vector2(0f, 30f);
                return;
            }

            WeaponSpec spec = _spec;
            if (_headerText != null)
            {
                _headerText.text = "ZEROING // " + spec.displayName.ToUpperInvariant()
                    + "   M/V " + F(spec.muzzleVelocity) + " M/S";
            }

            float maxRange = Mathf.Max(StepMeters,
                Mathf.FloorToInt(Mathf.Max(spec.effectiveRange, StepMeters) / StepMeters) * StepMeters);

            float height = 0f;
            AddRow("RANGE", "DROP CM", "MIL", "MOA", "CLICKS (MIL)", true);
            height += 30f;
            for (float d = StepMeters; d <= maxRange + 0.01f; d += StepMeters)
            {
                float hold = ZeroingMath.HoldoverMetres(spec.muzzleVelocity, d, zero);
                float dropCm = ZeroingMath.VacuumDropMetres(spec.muzzleVelocity, d) * 100f;
                float mil = ZeroingMath.MetresToMil(hold, d);
                float moa = ZeroingMath.MetresToMoa(hold, d);
                float clicks = ZeroingMath.Clicks(mil, milPerClick);
                AddRow(F(d) + " M", F(dropCm), F(mil), F(moa), F(clicks), false);
                height += 26f;
            }
            _tableContent.sizeDelta = new Vector2(0f, height);

            if (_noteText != null)
            {
                if (ZeroingMath.TryZeroCrossings(spec.muzzleVelocity, zero,
                        ZeroingMath.DefaultSightHeightMeters, out float near, out float far))
                {
                    _noteText.text = "POINT-BLANK NOTE (vacuum, sight height "
                        + F(ZeroingMath.DefaultSightHeightMeters * 100f) + " CM): PATH LAUNCHES BELOW THE "
                        + "LINE OF SIGHT, RISES THROUGH IT AT ~" + F(near) + " M AND MEETS THE "
                        + F(zero) + " M ZERO AT ~" + F(far) + " M — SIGHT-ON-STRING RUNS BETWEEN THE "
                        + "CROSSINGS. DRAG-FREE VALUES READ HIGH AT LONG RANGE.";
                }
                else
                {
                    _noteText.text = "POINT-BLANK NOTE: THIS CARTRIDGE CANNOT ZERO AT " + F(zero)
                        + " M IN THE VACUUM MODEL WITH THE ASSUMED SIGHT HEIGHT. CHOOSE A LONGER ZERO.";
                }
            }
        }

        private static float ParseButtonValue(Button button)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null && float.TryParse(label.text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float v))
                return v;
            return -1f;
        }

        private static string F(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void AddRow(string range, string drop, string mil, string moa, string clicks, bool header)
        {
            RectTransform row = new GameObject("Row", typeof(RectTransform)).transform as RectTransform;
            row.SetParent(_tableContent, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, header ? 28f : 24f);
            UiFactory.CreateHLayout(row, 8f, new RectOffset(6, 6, 1, 1), false);

            Color color = header ? HudThemeLibrary.AmberDim : HudThemeLibrary.TextSecondary;
            AddCell(row, "C0", range, 90f, color);
            AddCell(row, "C1", drop, 110f, header ? color : HudThemeLibrary.TextMuted);
            AddCell(row, "C2", mil, 90f, color);
            AddCell(row, "C3", moa, 90f, color);
            AddCell(row, "C4", clicks, 120f, header ? color : HudThemeLibrary.OliveBright);
            _tableRows.Add(row.gameObject);
        }

        private static void AddCell(RectTransform row, string name, string value, float width, Color color)
        {
            // Fixed-size cell: corner anchors + explicit size so the parent HLayout
            // (childControl off) lays the columns out side by side without resizing them.
            Text text = UiFactory.CreateText(row, name, value, HudThemeLibrary.FontCaption,
                color, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                new Vector2(width, 24f), Vector2.zero);
            _ = text;
        }
    }
}
