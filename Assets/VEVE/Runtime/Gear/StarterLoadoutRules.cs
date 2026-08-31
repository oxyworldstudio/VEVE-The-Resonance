using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Gear
{
    /// <summary>
    /// Authenticated-by-catalog starter kit for networked pawns: helmet + soft torso
    /// + rig, resolved by id first and by slot category fallback, then equipped
    /// through the real <see cref="GearLoadout.TryEquip"/> validation path. Calling
    /// it twice is a no-op (no double-spend on cap/mass).
    /// </summary>
    public static class StarterLoadoutRules
    {
        public const float MassCap = 40f;
        public const float VolumeCap = 55f;

        public static readonly string[] HelmetPreference = { "fast_mt", "altyn", "6b47" };
        public static readonly string[] TorsoPreference = { "iiia_soft", "ibav" };
        public static readonly string[] RigPreference = { "jpc20", "lbv_1" };
        public static readonly string[] EyePreference = { "goggle_ess", "goggle_mfc" };
        public static readonly string[] HearingPreference = { "comtac_vi", "sptcomm" };

        public static bool TryBuild(GearLoadout target, out string failure)
        {
            failure = null;
            if (target == null) { failure = "loadout missing"; return false; }

            int filled = 0;
            int attempted = 0;

            if (!TryPrefer(target, HelmetPreference, GearSlotType.BallisticHelmet, ref filled, ref attempted, out failure)) return false;
            if (!TryPrefer(target, TorsoPreference, GearSlotType.SoftArmor, ref filled, ref attempted, out failure)) return false;
            TryFill(target, RigPreference, GearSlotType.LoadBearingRig, ref filled);
            TryFill(target, EyePreference, GearSlotType.EyeProtection, ref filled);
            TryFill(target, HearingPreference, GearSlotType.EarProtection, ref filled);

            // torso or helmet is mandatory; a fully optional kit still needs *something*
            if (filled < 1)
            {
                failure = "starter loadout has nothing to wear (catalog unavailable?)";
                return false;
            }
            return true;
        }

        private static bool TryPrefer(GearLoadout target, string[] prefer, GearSlotType slot,
            ref int filled, ref int attempted, out string failure)
        {
            failure = null;
            attempted++;
            var existing = target.Get(slot);
            if (existing != null) { filled++; return true; }
            foreach (string id in prefer)
            {
                if (!GearCatalog.TryFind(id, out var item) || item == null) continue;
                if (target.TryEquip(slot, item, out failure, MassCap, VolumeCap)) { filled++; return true; }
                return false; // a genuine equip failure (weight cap) is real, not silent
            }
            failure = prefer[0] + " unavailable and slot " + slot + " has no item";
            return false;
        }

        private static void TryFill(GearLoadout target, string[] prefer, GearSlotType slot, ref int filled)
        {
            if (target.Get(slot) != null) { filled++; return; }
            foreach (string id in prefer)
            {
                if (!GearCatalog.TryFind(id, out var item) || item == null) continue;
                if (target.TryEquip(slot, item, out _, MassCap, VolumeCap)) { filled++; return; }
            }
        }
    }
}
