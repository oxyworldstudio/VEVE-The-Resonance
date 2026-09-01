using System.Globalization;
using System.Text;
using UnityEngine;

namespace VEVE.UI
{
    /// <summary>
    /// W-H8 combat HUD state: everything the combat readout shows, shaped as data so the
    /// panel stays presentation-only. Labels (mode, rank) are passed in as strings - this
    /// module never reaches into flow rules or scoring by itself.
    /// </summary>
    public struct CombatHudState
    {
        /// <summary>Sentinel for "no vitals source bound"; the presenter prints HEALTH --.</summary>
        public const float UnknownHealth01 = -1f;

        public string modeLabel;
        public int grenadeCount;
        public int squadAlive;
        public int squadTotal;
        public float posture01;
        public float health01;
        public string missionRankLabel;
    }

    /// <summary>
    /// Pure presenter: deterministic multi-line plain text (the view applies colors from
    /// HudThemeLibrary). Empty state (no squad) collapses the whole readout to NO SQUAD.
    /// </summary>
    public static class CombatHudPresenter
    {
        public const string EmptyStateLabel = "NO SQUAD";
        public const string UnknownLabel = "--";

        public static string Format(CombatHudState s)
        {
            if (s.squadTotal <= 0) return EmptyStateLabel;

            var sb = new StringBuilder();
            sb.Append("MODE ").Append(LabelOrUnknown(s.modeLabel)).Append('\n');
            if (CombatHudRules.ShouldShowGrenade(s.grenadeCount))
                sb.Append(GrenadeHudPresenter.Format(s.grenadeCount)).Append('\n');
            sb.Append("SQUAD ").Append(Mathf.Clamp(s.squadAlive, 0, s.squadTotal))
                .Append('/').Append(s.squadTotal).Append('\n');
            sb.Append("POSTURE ").Append(CombatHudRules.PostureLabel(s.posture01)).Append('\n');
            sb.Append("HEALTH ").Append(HealthText(s.health01)).Append('\n');
            sb.Append("RANK ").Append(LabelOrUnknown(s.missionRankLabel));
            return sb.ToString();
        }

        private static string LabelOrUnknown(string label)
        {
            return string.IsNullOrEmpty(label) ? UnknownLabel : label;
        }

        private static string HealthText(float health01)
        {
            if (float.IsNaN(health01) || health01 < 0f) return UnknownLabel;
            int pct = Mathf.RoundToInt(Mathf.Clamp01(health01) * 100f);
            return pct.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }

    /// <summary>Deterministic readout rules shared by the presenter and the panel.</summary>
    public static class CombatHudRules
    {
        /// <summary>Posture band edges on the 0-1 posture scale.</summary>
        public const float PostureMediumBand = 1f / 3f;
        public const float PostureHighBand = 2f / 3f;

        /// <summary>
        /// Monotonic squad readiness 0-1: for a fixed total, more alive members never
        /// read lower (view maps it to the squad color ramp). Degenerate totals floor at 0.
        /// </summary>
        public static float SquadColor01(int alive, int total)
        {
            if (total <= 0) return 0f;
            return Mathf.Clamp(alive, 0, total) / (float)total;
        }

        /// <summary>Three posture bands LOW &lt; MEDIUM &lt; HIGH by ascending posture01.</summary>
        public static string PostureLabel(float posture01)
        {
            float p = Mathf.Clamp01(posture01);
            if (p < PostureMediumBand) return "LOW";
            if (p < PostureHighBand) return "MEDIUM";
            return "HIGH";
        }

        /// <summary>Grenade line shows for any non-negative count (kept for future gating).</summary>
        public static bool ShouldShowGrenade(int count) => count >= 0;
    }
}
