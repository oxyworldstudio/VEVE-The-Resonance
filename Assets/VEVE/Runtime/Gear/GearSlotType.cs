using System;
using System.Collections.Generic;

namespace VEVE.Gear
{
    /// <summary>
    /// Wearable gear slot taxonomy modeled on real load-bearing equipment hierarchies (helmet → shield).
    /// A loadout holds at most one item per slot; see <see cref="GearSlots"/> for anatomical coverage masks
    /// over the <see cref="HitZone"/> set defined by <c>VEVE.Damageable</c>.
    /// </summary>
    public enum GearSlotType
    {
        BallisticHelmet,
        EyeProtection,
        EarProtection,
        PlateCarrier,
        SoftArmor,
        ArmsSleeves,
        Gloves,
        KneeElbow,
        LoadBearingRig,
        Backpack,
        BallisticShield
    }

    /// <summary>
    /// Static slot metadata: anatomical coverage masks and mutual exclusion rules per <see cref="GearSlotType"/>.
    /// </summary>
    public static class GearSlots
    {
        /// <summary>Total number of wearable slots.</summary>
        public const int SlotCount = 11;

        private static readonly HashSet<HitZone>[] Masks = CreateMasks();

        private static readonly Dictionary<GearSlotType, GearSlotType[]> Conflicts = new Dictionary<GearSlotType, GearSlotType[]>
        {
            { GearSlotType.SoftArmor, new[] { GearSlotType.PlateCarrier } },
            { GearSlotType.PlateCarrier, new[] { GearSlotType.SoftArmor } }
        };

        /// <summary>
        /// Hit zones this slot can physically protect. Items within the slot may assign partial
        /// per-zone coverage; the mask only bounds what the slot is able to cover.
        /// </summary>
        /// <param name="slot">Slot to query.</param>
        /// <returns>Read-only list of coverable hit zones.</returns>
        public static IReadOnlyList<HitZone> CoverageMask(GearSlotType slot)
        {
            return new System.Collections.ObjectModel.ReadOnlyCollection<HitZone>(new List<HitZone>(Masks[(int)slot]));
        }

        /// <summary>
        /// True when the slot is physically able to shield the given zone.
        /// </summary>
        /// <param name="slot">Slot to test.</param>
        /// <param name="zone">Zone to test.</param>
        /// <returns>Whether the zone lies inside the slot's coverage mask.</returns>
        public static bool Covers(GearSlotType slot, HitZone zone)
        {
            return Masks[(int)slot].Contains(zone);
        }

        /// <summary>
        /// True when two slots compete for the same body hardware and cannot be worn together,
        /// e.g. a standalone soft-armor vest cannot be worn under a plate carrier whose cummerbund
        /// already provides the soft-armor layer.
        /// </summary>
        /// <param name="a">First slot.</param>
        /// <param name="b">Second slot.</param>
        /// <returns>Whether equipping both slots simultaneously is invalid.</returns>
        public static bool ConflictsWith(GearSlotType a, GearSlotType b)
        {
            if (a == b) return false;
            if (Conflicts.TryGetValue(a, out GearSlotType[] list))
                for (int i = 0; i < list.Length; i++)
                    if (list[i] == b) return true;
            return false;
        }

        private static HashSet<HitZone>[] CreateMasks()
        {
            var masks = new HashSet<HitZone>[SlotCount];
            masks[(int)GearSlotType.BallisticHelmet] = new HashSet<HitZone> { HitZone.Head, HitZone.Neck };
            masks[(int)GearSlotType.EyeProtection] = new HashSet<HitZone> { HitZone.Head };
            masks[(int)GearSlotType.EarProtection] = new HashSet<HitZone> { HitZone.Head };
            masks[(int)GearSlotType.PlateCarrier] = new HashSet<HitZone> { HitZone.UpperTorso, HitZone.LowerTorso, HitZone.Neck };
            masks[(int)GearSlotType.SoftArmor] = new HashSet<HitZone> { HitZone.UpperTorso, HitZone.LowerTorso };
            masks[(int)GearSlotType.ArmsSleeves] = new HashSet<HitZone> { HitZone.UpperArmLeft, HitZone.UpperArmRight, HitZone.ForearmLeft, HitZone.ForearmRight };
            masks[(int)GearSlotType.Gloves] = new HashSet<HitZone> { HitZone.HandLeft, HitZone.HandRight };
            masks[(int)GearSlotType.KneeElbow] = new HashSet<HitZone> { HitZone.ThighLeft, HitZone.ThighRight, HitZone.CalfLeft, HitZone.CalfRight };
            masks[(int)GearSlotType.LoadBearingRig] = new HashSet<HitZone> { HitZone.UpperTorso };
            masks[(int)GearSlotType.Backpack] = new HashSet<HitZone> { HitZone.UpperTorso };
            masks[(int)GearSlotType.BallisticShield] = new HashSet<HitZone> { HitZone.UpperTorso, HitZone.LowerTorso, HitZone.Head, HitZone.UpperArmLeft, HitZone.UpperArmRight, HitZone.ForearmLeft, HitZone.ForearmRight };
            return masks;
        }
    }
}
