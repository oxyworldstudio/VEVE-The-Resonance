using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VEVE.UI
{
    /// <summary>
    /// Pure diegesis policy: which HUD features a human operator could physically have,
    /// per campaign death mode. Test/Assisted = full HUD (training aids); Realistic =
    /// compass + vitals only (a wounded operator still knows north and their own pulse);
    /// a future Immersive mode maps to none. Monotonic by design: Test ⊇ Assisted  Realistic.
    /// </summary>
    public static class HudDiegesisProfile
    {
        private static readonly HashSet<string> FullSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AdvancedHUDLayout.Features.Compass,
            AdvancedHUDLayout.Features.Objectives,
            AdvancedHUDLayout.Features.Squad,
            AdvancedHUDLayout.Features.Ammo,
            AdvancedHUDLayout.Features.Vitals,
            AdvancedHUDLayout.Features.Damage,
            AdvancedHUDLayout.Features.KillFeed,
            AdvancedHUDLayout.Features.Vignette,
            AdvancedHUDLayout.Features.Stamina
        };

        private static readonly HashSet<string> RealisticSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AdvancedHUDLayout.Features.Compass,
            AdvancedHUDLayout.Features.Vitals
        };

        public static IReadOnlyCollection<string> EnabledFeatures(VEVE.DeathMode mode)
        {
            switch (mode)
            {
                case VEVE.DeathMode.Realistic: return RealisticSet;
                default: return FullSet;
            }
        }

        public static bool IsFeatureEnabled(VEVE.DeathMode mode, string feature)
        {
            if (string.IsNullOrEmpty(feature)) return false;
            return EnabledFeatures(mode).Contains(feature);
        }

        /// <summary>Apply the policy to a layout instance (null-safe).</summary>
        public static void Apply(AdvancedHUDLayout layout, VEVE.DeathMode mode)
        {
            if (layout == null) return;
            foreach (string feature in FullSet)
            {
                if (IsFeatureEnabled(mode, feature))
                    layout.EnableFeature(feature);
                else
                    layout.DisableFeature(feature);
            }
        }
    }

    /// <summary>
    /// Scene component that binds the diegesis policy to the live campaign death mode,
    /// re-applying on mode changes (polled cheaply; no per-frame work).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudDiegesisController : MonoBehaviour
    {
        private AdvancedHUDLayout layout;
        private VEVE.CampaignState campaign;
        private VEVE.DeathMode applied = (VEVE.DeathMode)(-1);
        private float pollTimer;

        private void OnEnable()
        {
            layout = GetComponent<AdvancedHUDLayout>();
            if (layout == null) layout = UnityEngine.Object.FindFirstObjectByType<AdvancedHUDLayout>();
            campaign = UnityEngine.Object.FindFirstObjectByType<VEVE.CampaignState>();
            applied = (VEVE.DeathMode)(-1);
        }

        private void Update()
        {
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer > 0f) return;
            pollTimer = 0.5f;
            if (layout == null || campaign == null) return;
            VEVE.DeathMode mode = campaign.CurrentDeathMode;
            if (mode == applied) return;
            applied = mode;
            HudDiegesisProfile.Apply(layout, mode);
        }

        /// <summary>Force-apply a mode (tests / debug).</summary>
        public void ApplyMode(VEVE.DeathMode mode)
        {
            applied = mode;
            HudDiegesisProfile.Apply(layout, mode);
        }
    }
}
