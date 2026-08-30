using System;
using System.Collections.Generic;
using System.Linq;
using VEVE.Customization;

namespace VEVE.Catalog
{
    /// <summary>
    /// Accessory mounting interface found on a weapon's receiver or handguard.
    /// </summary>
    public enum RailInterface
    {
        None = 0,
        Picatinny,   // MIL-STD-1913 / STANAG 4694
        MLOCK,       // M-LOK slot system
        KeyMod,      // KeyMod slot system
        Proprietary  // manufacturer dovetail / tri-rail / receiver-specific
    }

    /// <summary>
    /// Coarse classification of how long an accessory takes to fit/remove.
    /// </summary>
    public enum AttachmentSwapCategory
    {
        NotSupported,
        QuickDetach,   // <= ~2 s, tool-less throw lever / push button
        HandTight,     // tool-less but requires spinning / seating
        SimpleTool,    // single coin / screwdriver class
        Armorer        // barrel systems, pin/torque work
    }

    /// <summary>
    /// Immutable per-weapon mounting profile: which <see cref="AttachmentSlot"/>s are open,
    /// which <see cref="RailInterface"/>s are present, the muzzle thread specification and the
    /// quick-detach swap budget for each fitted slot.
    /// </summary>
    public sealed class WeaponMountProfile
    {
        public string weaponId;
        public RailInterface upperRail;       // receiver / optic rail
        public RailInterface handguardRail;   // foreground / underbarrel accessory rail
        public RailInterface[] rails;         // every interface physically present
        public string[] muzzleThreads;        // nominal muzzle device thread specs
        public AttachmentSlot[] openSlots;
        public bool quickDetachOptic;
        public bool quickDetachMuzzle;
        public bool quickChangeBarrel;

        internal Dictionary<AttachmentSlot, float> swapOverrides;

        public WeaponMountProfile()
        {
        }

        public bool IsSlotOpen(AttachmentSlot slot) =>
            openSlots != null && Array.IndexOf(openSlots, slot) >= 0;

        public bool HasRail(RailInterface rail) =>
            rails != null && Array.IndexOf(rails, rail) >= 0;
    }

    /// <summary>
    /// Static registry mapping every catalog weapon to its mounting/attachment compatibility.
    /// Rail types are mapped onto the existing <see cref="AttachmentSlot"/> enum and expose
    /// quick-detach swap times so the loadout / customization UI can gate and price modifications.
    /// </summary>
    public static class AttachmentCompatibilityMatrix
    {
        private static readonly Dictionary<string, WeaponMountProfile> profiles = BuildProfiles();

        /// <summary>Weapon ids covered by the matrix (subset/superset of the ballistics catalog).</summary>
        public static IReadOnlyCollection<string> WeaponIds => profiles.Keys;

        public static bool HasProfile(string weaponId) =>
            weaponId != null && profiles.ContainsKey(weaponId);

        /// <summary>Get the mounting profile, or null when the weapon is not modelled.</summary>
        public static WeaponMountProfile GetProfile(string weaponId) =>
            profiles.TryGetValue(weaponId, out WeaponMountProfile p) ? p : null;

        /// <summary>Every open accessory slot for a weapon (empty if unknown).</summary>
        public static IEnumerable<AttachmentSlot> GetCompatibleSlots(string weaponId) =>
            GetProfile(weaponId)?.openSlots ?? Array.Empty<AttachmentSlot>();

        public static bool IsSlotCompatible(string weaponId, AttachmentSlot slot)
        {
            WeaponMountProfile p = GetProfile(weaponId);
            return p != null && p.IsSlotOpen(slot);
        }

        /// <summary>Rail interfaces physically present on this weapon.</summary>
        public static RailInterface[] GetRailInterfaces(string weaponId) =>
            GetProfile(weaponId)?.rails ?? new[] { RailInterface.None };

        public static bool SupportsRail(string weaponId, RailInterface rail) =>
            GetProfile(weaponId)?.HasRail(rail) ?? false;

        /// <summary>Nominal muzzle device thread specifications (thread sizes / proprietary names).</summary>
        public static string[] GetMuzzleThreads(string weaponId) =>
            GetProfile(weaponId)?.muzzleThreads ?? Array.Empty<string>();

        /// <summary>
        /// High level compatibility rule: an accessory must fill an open slot AND its mounting
        /// interface must actually exist on the weapon. Optics ride the receiver rail, underbarrel
        /// accessories (grip / laser / bipod / rail) ride the handguard rail.
        /// </summary>
        public static bool IsAttachmentCompatible(string weaponId, AttachmentSlot slot, RailInterface mount)
        {
            WeaponMountProfile p = GetProfile(weaponId);
            if (p == null || !p.IsSlotOpen(slot)) return false;

            switch (slot)
            {
                case AttachmentSlot.Optic:
                    return mount == RailInterface.None || p.HasRail(mount);
                case AttachmentSlot.Grip:
                case AttachmentSlot.Laser:
                case AttachmentSlot.Rail:
                    return mount == RailInterface.None || p.HasRail(mount);
                case AttachmentSlot.Muzzle:
                    return p.muzzleThreads != null && p.muzzleThreads.Any(t => !t.StartsWith("None", StringComparison.OrdinalIgnoreCase));
                default:
                    return true;
            }
        }

        /// <summary>
        /// Convenience overload for the existing <see cref="AttachmentDefinition"/> type: checks the
        /// definition's slot and maps slide-mounted / rail-mounted devices through the weapon profile.
        /// </summary>
        public static bool IsDefinitionCompatible(string weaponId, AttachmentDefinition definition)
        {
            WeaponMountProfile p = GetProfile(weaponId);
            if (p == null || definition.attachmentId == null) return false;

            if (!p.IsSlotOpen(definition.slot)) return false;

            // Muzzle devices must have a thread/quick-detach interface present.
            if (definition.slot == AttachmentSlot.Muzzle)
                return IsAttachmentCompatible(weaponId, AttachmentSlot.Muzzle, RailInterface.None);

            // Optic / underbarrel devices that require a Picatinny interface must find one.
            bool wantsPicatinny = definition.attachmentId.IndexOf("pic", StringComparison.OrdinalIgnoreCase) >= 0
                                  || definition.attachmentId.IndexOf("1913", StringComparison.OrdinalIgnoreCase) >= 0;
            if (wantsPicatinny) return p.HasRail(RailInterface.Picatinny);

            return true;
        }

        /// <summary>Estimated quick-detach swap time in seconds. Negative when the slot is unsupported.</summary>
        public static float GetQuickDetachSwapTime(string weaponId, AttachmentSlot slot)
        {
            WeaponMountProfile p = GetProfile(weaponId);
            if (p == null || !p.IsSlotOpen(slot)) return -1f;

            if (p.swapOverrides != null && p.swapOverrides.TryGetValue(slot, out float custom))
                return custom;

            return DefaultSwap(p, slot);
        }

        public static AttachmentSwapCategory GetSwapCategory(string weaponId, AttachmentSlot slot)
        {
            float t = GetQuickDetachSwapTime(weaponId, slot);
            if (t < 0f) return AttachmentSwapCategory.NotSupported;
            if (t <= 2.0f) return AttachmentSwapCategory.QuickDetach;
            if (t <= 4.0f) return AttachmentSwapCategory.HandTight;
            if (t <= 10.0f) return AttachmentSwapCategory.SimpleTool;
            return AttachmentSwapCategory.Armorer;
        }

        private static float DefaultSwap(WeaponMountProfile p, AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Optic:
                    if (p.quickDetachOptic) return 1.5f;
                    return p.upperRail == RailInterface.Picatinny ? 3.0f : 6.0f;
                case AttachmentSlot.Muzzle:
                    return p.quickDetachMuzzle ? 1.2f : 6.0f;
                case AttachmentSlot.Grip:
                    return p.handguardRail == RailInterface.Picatinny ? 3.0f : 6.0f;
                case AttachmentSlot.Barrel:
                    return p.quickChangeBarrel ? 20.0f : 90.0f;
                case AttachmentSlot.Stock:
                    return 5.0f;
                case AttachmentSlot.Magazine:
                    return 2.0f;
                case AttachmentSlot.Laser:
                    return 4.0f;
                case AttachmentSlot.Rail:
                    return 6.0f;
                default:
                    return 5.0f;
            }
        }

        private static WeaponMountProfile Make(
            string id, RailInterface upper, RailInterface handguard, RailInterface[] rails,
            string[] threads, AttachmentSlot[] slots,
            bool qdOptic = false, bool qdMuzzle = false, bool qcb = false,
            Dictionary<AttachmentSlot, float> overrides = null)
        {
            return new WeaponMountProfile
            {
                weaponId = id,
                upperRail = upper,
                handguardRail = handguard,
                rails = rails,
                muzzleThreads = threads,
                openSlots = slots,
                quickDetachOptic = qdOptic,
                quickDetachMuzzle = qdMuzzle,
                quickChangeBarrel = qcb,
                swapOverrides = overrides,
            };
        }

        private static Dictionary<AttachmentSlot, float> Swaps(AttachmentSlot s1, float t1,
            AttachmentSlot? s2 = null, float? t2 = null)
        {
            var d = new Dictionary<AttachmentSlot, float> { { s1, t1 } };
            if (s2.HasValue && t2.HasValue) d[s2.Value] = t2.Value;
            return d;
        }

        private static Dictionary<string, WeaponMountProfile> BuildProfiles()
        {
            const AttachmentSlot Muzzle = AttachmentSlot.Muzzle;
            const AttachmentSlot Optic = AttachmentSlot.Optic;
            const AttachmentSlot Grip = AttachmentSlot.Grip;
            const AttachmentSlot Stock = AttachmentSlot.Stock;
            const AttachmentSlot Magazine = AttachmentSlot.Magazine;
            const AttachmentSlot Barrel = AttachmentSlot.Barrel;
            const AttachmentSlot Rail = AttachmentSlot.Rail;
            const AttachmentSlot Laser = AttachmentSlot.Laser;

            var rifleSlots = new[] { Rail, Muzzle, Optic, Magazine, Grip, Stock, Barrel, Laser };

            var table = new Dictionary<string, WeaponMountProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["ak74m"] = Make("ak74m", RailInterface.Proprietary, RailInterface.Proprietary,
                    new[] { RailInterface.Proprietary }, new[] { "M24x1.5-LH" },
                    new[] { Muzzle, Optic, Magazine, Grip, Stock, Rail }),

                ["ak103"] = Make("ak103", RailInterface.Proprietary, RailInterface.Proprietary,
                    new[] { RailInterface.Proprietary }, new[] { "M14x1-LH" },
                    new[] { Muzzle, Optic, Magazine, Grip, Stock, Rail }),

                ["m4a1"] = Make("m4a1", RailInterface.Picatinny, RailInterface.Picatinny,
                    new[] { RailInterface.Picatinny }, new[] { "1/2x28-UNEF" }, rifleSlots,
                    qdOptic: true, qcb: true),

                ["hk416"] = Make("hk416", RailInterface.Picatinny, RailInterface.MLOCK,
                    new[] { RailInterface.Picatinny, RailInterface.MLOCK }, new[] { "1/2x28-UNEF" }, rifleSlots,
                    qdOptic: true, qcb: true),

                ["scar-l"] = Make("scar-l", RailInterface.Picatinny, RailInterface.Picatinny,
                    new[] { RailInterface.Picatinny }, new[] { "M18x1 (FN, proprietary)" }, rifleSlots,
                    qdOptic: true, qdMuzzle: true, qcb: true),

                ["scar-h"] = Make("scar-h", RailInterface.Picatinny, RailInterface.Picatinny,
                    new[] { RailInterface.Picatinny }, new[] { "M18x1 (FN, proprietary)" }, rifleSlots,
                    qdOptic: true, qdMuzzle: true, qcb: true),

                ["mp5a5"] = Make("mp5a5", RailInterface.Proprietary, RailInterface.Proprietary,
                    new[] { RailInterface.Proprietary }, new[] { "M15x1 (HK, proprietary)" },
                    new[] { Muzzle, Optic, Magazine, Grip, Stock, Rail }),

                ["mp7a1"] = Make("mp7a1", RailInterface.Picatinny, RailInterface.Proprietary,
                    new[] { RailInterface.Picatinny, RailInterface.Proprietary }, new[] { "None" },
                    new[] { Rail, Optic, Magazine, Stock, Grip }, qdOptic: true),

                ["p90"] = Make("p90", RailInterface.Proprietary, RailInterface.Proprietary,
                    new[] { RailInterface.Proprietary, RailInterface.Picatinny }, new[] { "None" },
                    new[] { Rail, Optic, Magazine }),

                ["m249"] = Make("m249", RailInterface.Picatinny, RailInterface.Picatinny,
                    new[] { RailInterface.Picatinny }, new[] { "1/2x28-UNEF" }, rifleSlots,
                    qdOptic: true, qcb: true,
                    overrides: Swaps(Barrel, 15f)),

                ["m240b"] = Make("m240b", RailInterface.Picatinny, RailInterface.Proprietary,
                    new[] { RailInterface.Picatinny, RailInterface.Proprietary }, new[] { "Quick-detach (no thread)" },
                    new[] { Rail, Muzzle, Optic, Magazine, Grip, Stock, Barrel },
                    qdOptic: true, qdMuzzle: true,
                    overrides: Swaps(Barrel, 45f)),

                ["m82a1"] = Make("m82a1", RailInterface.Picatinny, RailInterface.Picatinny,
                    new[] { RailInterface.Picatinny }, new[] { "1-1/8-24 TPI (proprietary)" },
                    new[] { Rail, Muzzle, Optic, Magazine, Grip, Stock, Barrel },
                    qdOptic: true,
                    overrides: Swaps(Barrel, 30f)),

                ["m110-sass"] = Make("m110-sass", RailInterface.Picatinny, RailInterface.KeyMod,
                    new[] { RailInterface.Picatinny, RailInterface.KeyMod }, new[] { "5/8x24-UNEF" }, rifleSlots,
                    qdOptic: true, qcb: true),

                ["glock-17"] = Make("glock-17", RailInterface.Proprietary, RailInterface.Picatinny,
                    new[] { RailInterface.Proprietary, RailInterface.Picatinny }, new[] { "Optional (threaded barrel)" },
                    new[] { Muzzle, Optic, Magazine, Grip, Rail, Laser }),

                ["m1911a1"] = Make("m1911a1", RailInterface.Proprietary, RailInterface.None,
                    new[] { RailInterface.Proprietary }, new[] { "Proprietary barrel bushing" },
                    new[] { Muzzle, Optic, Magazine, Grip }),

                ["remington-870"] = Make("remington-870", RailInterface.Proprietary, RailInterface.Proprietary,
                    new[] { RailInterface.Proprietary, RailInterface.Picatinny }, new[] { "Rem-Choke (proprietary)" },
                    new[] { Muzzle, Optic, Magazine, Grip, Stock, Barrel, Rail, Laser },
                    overrides: Swaps(Barrel, 8f, Stock, 10f)),

                ["mk14-ebr"] = Make("mk14-ebr", RailInterface.Picatinny, RailInterface.Picatinny,
                    new[] { RailInterface.Picatinny }, new[] { "5/8x24-UNEF" }, rifleSlots,
                    qdOptic: true, qcb: true),

                ["svd-dragunov"] = Make("svd-dragunov", RailInterface.Proprietary, RailInterface.None,
                    new[] { RailInterface.Proprietary }, new[] { "M14x1-LH" },
                    new[] { Muzzle, Optic, Magazine, Stock, Rail }),
            };

            return table;
        }
    }
}
