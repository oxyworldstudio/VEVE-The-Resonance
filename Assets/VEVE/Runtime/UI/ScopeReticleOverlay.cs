using UnityEngine;
using UnityEngine.UI;

namespace VEVE.UI
{
    /// <summary>
    /// Diegetic scope reticle: the holdover hint produced by <see cref="ScopeTelemetryBridge"/>
    /// is shown as a physical hold marker INSIDE the optic picture (a real shooter reads a
    /// ranging reticle, not a HUD readout). Numeric chips are a training aid and are hidden
    /// under Realistic diegesis. Pure math lives in ReticleMath for unit testing.
    /// </summary>
    public sealed class ScopeReticleOverlay : MonoBehaviour
    {
        /// <summary>Default assume 6° true FOV picture (compact LPVO mid zoom) at HD canvas.</summary>
        public const float DefaultFieldOfViewDegrees = 6f;
        public const float DefaultCanvasWidthPx = 1920f;
        public const float MaxMarkerOffsetPx = 320f;
        private const float KeepAliveSeconds = 1.2f;

        private Canvas canvas;
        private RectTransform root;
        private RectTransform holdMarker;
        private Text holdLabel;
        private float pixelsPerMoa = 5.33f;

        private ScopeTelemetryBridge telemetry;
        private CampaignState campaign;
        private float lastEventTime = float.NegativeInfinity;

        /// <summary>
        /// Pure pixel-per-MOA scale for an optic picture: canvasWidth / (fovDeg * 60 MOA).
        /// Non-positive/NaN fov or width return the 6°/1920px default ratio.
        /// </summary>
        public static float PixelsPerMoa(float canvasWidthPx, float fieldOfViewDegrees)
        {
            if (!(canvasWidthPx > 0f) || !(fieldOfViewDegrees > 0f) || fieldOfViewDegrees >= 180f)
                return DefaultCanvasWidthPx / (DefaultFieldOfViewDegrees * 60f);
            float ppm = canvasWidthPx / (fieldOfViewDegrees * 60f);
            return ppm > 0f ? ppm : DefaultCanvasWidthPx / (DefaultFieldOfViewDegrees * 60f);
        }

        /// <summary>
        /// Vertical offset (UI Y) of the hold marker for a positive-"hold-high" hint:
        /// the marker shows WHERE to put the target, so +MOA (aim above) places it BELOW center.
        /// Clamped to <see cref="MaxMarkerOffsetPx"/>; NaN maps to 0.
        /// </summary>
        public static float MarkerOffsetY(float holdoverMoa, float ppm)
        {
            if (float.IsNaN(holdoverMoa) || float.IsNaN(ppm) || ppm <= 0f) return 0f;
            float y = -holdoverMoa * ppm;
            if (y > MaxMarkerOffsetPx) return MaxMarkerOffsetPx;
            if (y < -MaxMarkerOffsetPx) return -MaxMarkerOffsetPx;
            return y;
        }

        /// <summary>Pure label: "+4.9 MOA @ 118 m" (invariant; never throws on NaN).</summary>
        public static string HoldLabel(ScopeTelemetryEvent e)
        {
            if (e == null) return string.Empty;
            float m = float.IsNaN(e.holdoverMoa) ? 0f : e.holdoverMoa;
            float raw = e.distanceMeters;
            float d = (float.IsNaN(raw) || raw < 0f) ? 0f : raw;
            string moa = m >= 0f ? "+" + m.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                  : m.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            return moa + " MOA @ " + d.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m";
        }

        private void Awake()
        {
            BuildReticle();
        }

        private void BuildReticle()
        {
            canvas = UiFactory.CreateCanvas("ScopeReticle", 240);
            root = canvas.transform as RectTransform;
            UiFactory.StretchFull(root);
            canvas.gameObject.SetActive(false);

            Color ink = new Color(0.85f, 0.95f, 0.88f, 0.92f);

            // cross ticks (top/bottom) + side gaps: diegetic mil scale hint
            UiFactory.CreateImage(root, "CrossVTop", ink, Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(2f, 34f), new Vector2(0f, 42f));
            UiFactory.CreateImage(root, "CrossVBottom", ink, Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(2f, 34f), new Vector2(0f, -42f));
            UiFactory.CreateImage(root, "CrossHLeft", ink, Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(34f, 2f), new Vector2(-42f, 0f));
            UiFactory.CreateImage(root, "CrossHRight", ink, Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(34f, 2f), new Vector2(42f, 0f));

            Image marker = UiFactory.CreateImage(root, "HoldMarker", new Color(0.98f, 0.62f, 0.12f, 0.95f),
                Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(26f, 6f), new Vector2(0f, 0f));
            holdMarker = marker.rectTransform;

            holdLabel = UiFactory.CreateText(root, "HoldChip", "", 14,
                new Color(0.95f, 0.95f, 0.9f, 0.9f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(160f, 22f), new Vector2(90f, -20f));
        }

        private void OnEnable()
        {
            VEVE.EventBus.SubscribeGlobal<ScopeTelemetryEvent>(OnTelemetry);
        }

        private void OnDisable()
        {
            VEVE.EventBus.UnsubscribeGlobal<ScopeTelemetryEvent>(OnTelemetry);
        }

        private void OnTelemetry(ScopeTelemetryEvent e)
        {
            if (e == null || root == null) return;
            ApplyHint(e.holdoverMoa, e.distanceMeters);
            lastEventTime = Time.unscaledTime;
        }

        /// <summary>Public entry (also for tests/debug): position marker + label for a hint.</summary>
        public void ApplyHint(float holdoverMoa, float distanceMeters)
        {
            EnsureRefs();
            if (holdMarker != null)
            {
                float y = MarkerOffsetY(holdoverMoa, pixelsPerMoa);
                holdMarker.anchoredPosition = new Vector2(0f, y);
            }
            if (holdLabel != null)
            {
                bool numbersAllowed = campaign == null || campaign.CurrentDeathMode != DeathMode.Realistic;
                holdLabel.gameObject.SetActive(numbersAllowed);
                if (numbersAllowed)
                    holdLabel.text = HoldLabel(new ScopeTelemetryEvent
                    {
                        distanceMeters = distanceMeters,
                        holdoverMoa = holdoverMoa
                    });
            }
        }

        private void EnsureRefs()
        {
            if (telemetry == null) telemetry = UnityEngine.Object.FindFirstObjectByType<ScopeTelemetryBridge>();
            if (campaign == null) campaign = UnityEngine.Object.FindFirstObjectByType<CampaignState>();
        }

        private void Update()
        {
            if (canvas == null) return;
            EnsureRefs();
            bool show = telemetry != null && telemetry.Resolved
                && Time.unscaledTime - lastEventTime < KeepAliveSeconds;
            if (canvas.gameObject.activeSelf != show) canvas.gameObject.SetActive(show);
        }

        /// <summary>Override reticle geometry (bound later from ScopeProfile per mounted optic).</summary>
        public void SetReticleGeometry(float canvasWidthPx, float fieldOfViewDegrees)
        {
            pixelsPerMoa = PixelsPerMoa(canvasWidthPx, fieldOfViewDegrees);
        }

        /// <summary>Currently applied scale (tests / debug).</summary>
        public float PixelsPerMoaCurrent => pixelsPerMoa;
    }
}
