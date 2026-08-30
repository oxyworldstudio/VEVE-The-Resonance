using System.Collections.Generic;
using UnityEngine;

namespace VEVE.UI
{
    /// <summary>
    /// Tactical readability theme: muted olive / amber / red palette with spacing and
    /// typography constants. All colors are authored in the 0-1 sRGB range so they can be
    /// assigned directly to legacy Graphic components without gamma surprises.
    /// Designed to be consumed as parameters by <see cref="UiFactory"/>.
    /// </summary>
    public static class HudThemeLibrary
    {
        // ---- Canvas baselines (matches UIManager.ApplyAccessibilitySettings) ----
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float MatchWidthOrHeight = 0.5f;

        // ---- Surfaces ----
        public static readonly Color PanelBackground = new Color(0.075f, 0.082f, 0.062f, 0.92f);
        public static readonly Color PanelSurface = new Color(0.105f, 0.113f, 0.086f, 0.94f);
        public static readonly Color PanelInset = new Color(0.055f, 0.06f, 0.048f, 0.88f);
        public static readonly Color ScreenFade = new Color(0.02f, 0.025f, 0.02f, 1f);

        // ---- Core tactical hues ----
        public static readonly Color Olive = new Color(0.372f, 0.419f, 0.24f, 1f);
        public static readonly Color OliveBright = new Color(0.541f, 0.607f, 0.337f, 1f);
        public static readonly Color OliveDim = new Color(0.231f, 0.258f, 0.165f, 1f);
        public static readonly Color Amber = new Color(0.937f, 0.694f, 0.176f, 1f);
        public static readonly Color AmberDim = new Color(0.604f, 0.451f, 0.125f, 1f);
        public static readonly Color AlertRed = new Color(0.784f, 0.188f, 0.149f, 1f);
        public static readonly Color AlertRedDim = new Color(0.431f, 0.11f, 0.094f, 1f);
        public static readonly Color VitalsGreen = new Color(0.427f, 0.541f, 0.298f, 1f);
        public static readonly Color SquadBlue = new Color(0.29f, 0.44f, 0.52f, 1f);

        // ---- Text ----
        public static readonly Color TextPrimary = new Color(0.878f, 0.867f, 0.784f, 1f);
        public static readonly Color TextSecondary = new Color(0.659f, 0.647f, 0.565f, 0.878f);
        public static readonly Color TextMuted = new Color(0.494f, 0.486f, 0.427f, 0.72f);
        public static readonly Color TextOnDark = new Color(0.922f, 0.914f, 0.851f, 1f);

        // ---- Interaction ----
        public static readonly Color SlotNormal = new Color(0.137f, 0.145f, 0.11f, 0.94f);
        public static readonly Color SlotHover = new Color(0.204f, 0.22f, 0.153f, 0.96f);
        public static readonly Color SlotSelected = new Color(0.447f, 0.365f, 0.114f, 1f);
        public static readonly Color ButtonNormal = new Color(0.176f, 0.204f, 0.133f, 1f);
        public static readonly Color ButtonPressed = new Color(0.078f, 0.086f, 0.059f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.102f, 0.11f, 0.086f, 0.588f);
        public static readonly Color SliderTrack = new Color(0.047f, 0.051f, 0.043f, 0.92f);

        // ---- Spacing / sizing ----
        public const float PaddingXs = 4f;
        public const float PaddingSm = 8f;
        public const float PaddingMd = 14f;
        public const float PaddingLg = 22f;
        public const float PaddingXl = 32f;
        public const float BarThickness = 6f;
        public const float SlotCellSize = 74f;
        public const float SlotSpacing = 6f;
        public const float PipSize = 44f;

        // ---- Font sizes (pre-scaling bases; UiFactory clamps post-scale to [12, 72]) ----
        public const int FontCaption = 13;
        public const int FontBody = 16;
        public const int FontSubhead = 20;
        public const int FontReadout = 30;
        public const int FontHeading = 40;
        public const int FontCinematic = 64;
        public const int FontMinReadable = UiFactory.MinReadableFont;
        public const int FontMaxReadable = UiFactory.MaxReadableFont;

        /// <summary>
        /// Every theme-issued color; used by validation/tests and colorblind audits.
        /// Kept in declaration order so new colors must be added here.
        /// </summary>
        public static readonly IReadOnlyList<Color> AllColors = new List<Color>
        {
            PanelBackground, PanelSurface, PanelInset, ScreenFade,
            Olive, OliveBright, OliveDim, Amber, AmberDim,
            AlertRed, AlertRedDim, VitalsGreen, SquadBlue,
            TextPrimary, TextSecondary, TextMuted, TextOnDark,
            SlotNormal, SlotHover, SlotSelected,
            ButtonNormal, ButtonPressed, ButtonDisabled, SliderTrack
        }.AsReadOnly();

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(alpha));
        }

        private static HashSet<string> ownedFieldCache;
        private static UnityEngine.Object ownedFieldCacheTarget;

        /// <summary>
        /// True when an existing HUDController already declares the given serialized
        /// binding (e.g. "ammoText"), so advanced overlays should not duplicate it.
        /// </summary>
        public static bool HudControllerOwns(HUDController controller, string fieldName)
        {
            if (controller == null || string.IsNullOrEmpty(fieldName))
                return false;
            if (ownedFieldCacheTarget != controller || ownedFieldCache == null)
            {
                ownedFieldCacheTarget = controller;
                ownedFieldCache = UiFactory.GetSerializedFieldNames(controller);
            }
            return ownedFieldCache.Contains(fieldName);
        }
    }
}
