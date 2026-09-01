using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using VEVE.Combat;

namespace VEVE.UI
{
    /// <summary>Pure presenter: grenade count label + empty-state pulse rule (W15).</summary>
    public static class GrenadeHudPresenter
    {
        public static string Format(int count)
        {
            int c = count < 0 ? 0 : count;
            return "FRAG x" + c.ToString(CultureInfo.InvariantCulture);
        }

        public static bool ShouldPulse(int count) => count <= 0;

        public static Color LabelColor(int count, Color normal, Color empty)
        {
            return ShouldPulse(count) ? empty : normal;
        }
    }

    /// <summary>
    /// W15 HUD: grenade counter bound to the local weapon via GrenadesChanged
    /// (event-driven, no polling). Built in Awake so EditMode tests see it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeHudPanel : MonoBehaviour
    {
        private Weapon weapon;
        private Text label;
        private Image pulseImage;

        public string CurrentLabel => label != null ? label.text : string.Empty;
        public bool Bound => weapon != null;

        private void Awake()
        {
            BuildHud();
        }

        private void Start()
        {
            if (weapon == null) Bind(FindFirstObjectByType<Weapon>());
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>Bind to a weapon (auto-finds one in the scene when omitted).</summary>
        public void Bind(Weapon source)
        {
            Unbind();
            weapon = source;
            if (weapon == null)
            {
                weapon = FindFirstObjectByType<Weapon>();
            }
            if (weapon != null)
            {
                weapon.GrenadesChanged += OnGrenadesChanged;
                OnGrenadesChanged(weapon.GrenadesRemaining);
            }
        }

        public void Unbind()
        {
            if (weapon != null) weapon.GrenadesChanged -= OnGrenadesChanged;
            weapon = null;
        }

        private void OnGrenadesChanged(int count)
        {
            if (label == null) return;
            label.text = GrenadeHudPresenter.Format(count);
            label.color = GrenadeHudPresenter.LabelColor(count,
                HudThemeLibrary.TextOnDark, HudThemeLibrary.AlertRed);
        }

        private void BuildHud()
        {
            canvas = UiFactory.CreateCanvas("GrenadeHud", 240);
            var root = UiFactory.CreatePanel(canvas.transform as RectTransform, "Root",
                new Color(0f, 0f, 0f, 0.35f));
            root.rectTransform.anchorMin = new Vector2(1f, 0f);
            root.rectTransform.anchorMax = new Vector2(1f, 0f);
            root.rectTransform.pivot = new Vector2(1f, 0f);
            root.rectTransform.sizeDelta = new Vector2(180f, 44f);
            root.rectTransform.anchoredPosition = new Vector2(-24f, 24f);

            label = UiFactory.CreateText(root.rectTransform, "Count", GrenadeHudPresenter.Format(3), 18,
                HudThemeLibrary.TextOnDark, TextAnchor.MiddleRight,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-16f, 0f), Vector2.zero);
        }

        private Canvas canvas;
    }
}
