using System;
using System.Collections.Generic;

namespace VEVE.Gear
{
    /// <summary>
    /// Serialized catalog entry for a piece of wearable gear: ballistics, ergonomics and logistics
    /// figures grounded in manufacturer datasheet values. Coverage is expressed per <see cref="HitZone"/>
    /// (0..1) and is always bounded by the anatomical mask of the item's <see cref="GearSlots"/> slot.
    /// </summary>
    [Serializable]
    public sealed class GearItem
    {
        /// <summary>Number of hit zones tracked for coverage.</summary>
        public const int ZoneCount = 16;

        /// <summary>Stable machine identifier.</summary>
        public string id;
        /// <summary>Display name (datasheet naming).</summary>
        public string displayName;
        /// <summary>Wearable slot.</summary>
        public GearSlotType slot;
        /// <summary>Functional category group.</summary>
        public GearCategory category;
        /// <summary>Ballistic rating tier (Unrated for pure load carriage / comfort gear).</summary>
        public ProtectionLevel protectionLevel = ProtectionLevel.Unrated;
        /// <summary>Per-hit stopping ceiling override; 0 defers to <see cref="GearProtectionStandard"/>.</summary>
        public float customStopEnergyJoules;
        /// <summary>Coverage per HitZone, index-aligned with the enum; clamped 0..1 on assignment via <see cref="CoverageFor"/>.</summary>
        public float[] coveragePerZone = new float[ZoneCount];
        /// <summary>Mass in kilograms as worn (real datasheet figure).</summary>
        public float massKg;
        /// <summary>Stowed volume in litres, feeding PhysicalInventory-style budgets.</summary>
        public float volumeLitres;
        /// <summary>Locomotion speed multiplier contributed by wearing this item (1 = no cost).</summary>
        public float mobilityMultiplier = 1f;
        /// <summary>Aim stability multiplier contributed by wearing this item (1 = no cost).</summary>
        public float aimMultiplier = 1f;
        /// <summary>Thermal burden index (W-equivalent insulation load) for future thermoregulation coupling.</summary>
        public float heatLoad;
        /// <summary>NIR/IR signature multiplier after IR-repellent treatment (1 = untreated fabric).</summary>
        public float irSignatureMultiplier = 1f;
        /// <summary>True when the item integrates with the comms network (PTT headsets, Intra-Com).</summary>
        public bool commsIntegration;
        /// <summary>Short flavor/spec note from the source datasheet.</summary>
        public string notes;

        /// <summary>
        /// Coverage of this item over one hit zone, clamped to [0,1] and forced to 0 outside the
        /// item's slot mask; per-zone 0..1 fraction of the zone's surface that the gear shields.
        /// </summary>
        /// <param name="zone">Zone to query.</param>
        /// <returns>Coverage fraction 0..1.</returns>
        public float CoverageFor(HitZone zone)
        {
            int index = (int)zone;
            if (coveragePerZone == null || index < 0 || index >= coveragePerZone.Length) return 0f;
            if (!GearSlots.Covers(slot, zone)) return 0f;
            return Math.Clamp(coveragePerZone[index], 0f, 1f);
        }

        /// <summary>Sets coverage for one zone (clamped 0..1 at read time).</summary>
        /// <param name="zone">Zone to set.</param>
        /// <param name="value">Coverage fraction.</param>
        public void SetCoverage(HitZone zone, float value)
        {
            if (coveragePerZone == null) coveragePerZone = new float[ZoneCount];
            int index = (int)zone;
            if (index >= 0 && index < coveragePerZone.Length) coveragePerZone[index] = Math.Clamp(value, 0f, 1f);
        }
    }

    /// <summary>
    /// Built-in reference catalog of real-world tactical gear with datasheet-mass figures.
    /// Entries are constructed once and cached; treat them as read-only prototypes.
    /// </summary>
    public static class GearCatalog
    {
        private static List<GearItem> cached;

        /// <summary>
        /// All built-in gear items (≥18 real-world entries: helmets, plates, carriers, hearing,
        /// eye protection, limb armor, shields, load carriage).
        /// </summary>
        /// <returns>Read-only list of catalog prototypes.</returns>
        public static IReadOnlyList<GearItem> All()
        {
            if (cached == null) cached = Build();
            return cached;
        }

        /// <summary>Finds a catalog entry by id.</summary>
        /// <param name="id">Item id.</param>
        /// <param name="item">Matched item when found.</param>
        /// <returns>True when the id exists.</returns>
        public static bool TryFind(string id, out GearItem item)
        {
            IReadOnlyList<GearItem> all = All();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].id == id)
                {
                    item = all[i];
                    return true;
                }
            }
            item = null;
            return false;
        }

        private static List<GearItem> Build()
        {
            var list = new List<GearItem>
            {
                Helmet("fast_mt", "Ops-Core FAST MT (Bump)", ProtectionLevel.V50_FRAG, 1.45f, 12f, 0.98f, 0.99f, 9f,
                    "DP800 aramid, bump mount; ~1.45 kg w/ shroud & NVG mount."),
                Helmet("altyn", "Altyn 3R (6B47-series)", ProtectionLevel.NIJ_IIIA, 3.6f, 14f, 0.93f, 0.95f, 16f,
                    "Titanium dome Kevlar combo, Russian Spetsnaz issue; integrated NVG mast."),
                Helmet("6b47", "6B47 (C7-class) Helmet", ProtectionLevel.NIJ_IIIA, 3.5f, 14f, 0.93f, 0.96f, 15f,
                    "Alloy-steel + Kevlar dome, NIJ-equivalent IIIA over crown/temple arcs."),

                Add("goggle_mfc", "OAKLEY MFrame w/ clear", GearSlotType.EyeProtection, GearCategory.EyeProtection,
                    ProtectionLevel.Unrated, 0.03f, 0.3f, 1f, 1.01f, 1f, 0.9f, false,
                    "ANSI Z87.1 impact lens, ballistic polycarbonate; fog-vented."),
                Add("goggle_ess", "ESS ICE Crossguard", GearSlotType.EyeProtection, GearCategory.EyeProtection,
                    ProtectionLevel.Unrated, 0.04f, 0.4f, 1f, 1.01f, 1f, 0.92f, false,
                    " MIL-PRF-31013 ballistics-rated lens, wide peripheral coverage."),

                Hearing("comtac_vi", "3M Peltor ComTac VI HID", 0.39f, 4f, 8f,
                    "Level-dependent active hearing protection, MT15H7 headband, Bluetooth In/Out."),
                Hearing("sptcomm", "SORD Sordin SPACKT", 0.28f, 3f, 4f,
                    "Swedish electronic muzzle-guard headset, NATO PTT capable."),

                Add("iiia_soft", "Leatherbeck ALTA IIIA Soft Armor", GearSlotType.SoftArmor, GearCategory.TorsoArmor,
                    ProtectionLevel.NIJ_IIIA, 5.3f, 14f, 0.92f, 0.94f, 11f, 0.55f, false,
                    "Zeta aramid soft armor front/back/underside, NIJ 0101.07 IIIA, 44 mm BFB.",
                    torso: 1f, torsoLow: 0.85f, neck: 0.15f, arms: 0.35f),
                Add("ibav", "IMTV IBAV Soft Armor Insert", GearSlotType.SoftArmor, GearCategory.TorsoArmor,
                    ProtectionLevel.NIJ_IIIA, 6.8f, 16f, 0.9f, 0.93f, 13f, 0.6f, false,
                    "Improved Ballistic Armor Vest soft insert; scales with carrier.",
                    torso: 1f, torsoLow: 0.9f, neck: 0.2f),

                Add("esapi", "ESAPI Level III+/.4 M16 Plate Set", GearSlotType.PlateCarrier, GearCategory.TorsoArmor,
                    ProtectionLevel.NIJ_IV, 11.2f, 6f, 0.84f, 0.88f, 18f, 0.7f, false,
                    "Ceramic/SLEP front+back, ~5.6 kg/plate w/ strike face; NIJ IV rated.",
                    torso: 0.62f, torsoLow: 0.55f, neck: 0f),
                Add("rf2", "AR500 RF2 Level III Plate Set", GearSlotType.PlateCarrier, GearCategory.TorsoArmor,
                    ProtectionLevel.NIJ_III, 6.6f, 5f, 0.88f, 0.9f, 14f, 0.65f, false,
                    "Hi-Capacity multi-hit AR500 steel SAPI cut, stops 60x 7.62 NATO.",
                    torso: 0.55f, torsoLow: 0.5f),
                Add("avst", "AVST FAST Rotax Plate Carrier", GearSlotType.PlateCarrier, GearCategory.LoadCarriage,
                    ProtectionLevel.Unrated, 2.3f, 10f, 0.93f, 0.95f, 10f, 0.8f, false,
                    "Rothco AVST carrier w/ cummerbund soft panel; MOLLE grid front/side.",
                    torso: 0.12f, torsoLow: 0.1f, neck: 0.05f),
                Add("cf_plate", "Crye CF Plate Carrier (G4 shroud)", GearSlotType.PlateCarrier, GearCategory.LoadCarriage,
                    ProtectionLevel.Unrated, 1.9f, 8f, 0.96f, 0.98f, 8f, 0.75f, false,
                    "Compression-fit laser-cut carrier; Abrams-lite, low profile cummerbund.",
                    torso: 0.08f, torsoLow: 0.06f),
                Add("jpc20", "Crye JPC 2.0 (EMR)", GearSlotType.LoadBearingRig, GearCategory.LoadCarriage,
                    ProtectionLevel.Unrated, 0.9f, 5f, 0.99f, 1f, 6f, 0.8f, false,
                    "Ultra-light jump panel carrier; holds plates as insert, rig mode unrated.",
                    torso: 0.06f),
                Add("lbv_1", "FILBCTEK LBV-1 Rig", GearSlotType.LoadBearingRig, GearCategory.LoadCarriage,
                    ProtectionLevel.Unrated, 0.8f, 4f, 0.99f, 0.99f, 5f, 0.85f, false,
                    "Light weight load bearing vest, H-style harness.",
                    torso: 0.05f),

                Add("sleeves", "ARMORPRO Tactical Arm Shield set", GearSlotType.ArmsSleeves, GearCategory.LimbArmor,
                    ProtectionLevel.NIJ_IIIA, 0.9f, 2f, 0.95f, 0.93f, 7f, 0.9f, false,
                    "NIJ IIIA soft armored sleeves (biceps+forearm) w/ removable pad.",
                    arms: 0.6f),
                Add("gloves_mech", "Mechanix Original", GearSlotType.Gloves, GearCategory.HandProtection,
                    ProtectionLevel.Unrated, 0.36f, 0.8f, 1f, 0.99f, 3f, 1f, false,
                    "Drosera palm; no ballistic rating.",
                    hands: 0.1f),
                Add("gloves_armorx", "ArmorX F7 Impact Glove", GearSlotType.Gloves, GearCategory.HandProtection,
                    ProtectionLevel.NIJ_I, 0.65f, 1.2f, 0.97f, 0.95f, 6f, 1f, false,
                    "TPR knuckle armor + ballistic shell; marginal light-threat rating.",
                    hands: 0.55f),
                Add("knee_elbow", "D3O Barefoot Knee/Elbow set", GearSlotType.KneeElbow, GearCategory.JointProtection,
                    ProtectionLevel.Unrated, 0.6f, 1.5f, 0.96f, 0.98f, 5f, 1f, false,
                    "Non-Newtonian impact pads; blunt only, no NIJ rating.",
                    legs: 0.5f),

                Add("assault_pack", "ALICE ASSAULT III Pack 45L", GearSlotType.Backpack, GearCategory.LoadCarriage,
                    ProtectionLevel.Unrated, 2.4f, 45f, 0.92f, 0.96f, 7f, 0.9f, false,
                    "Frameless 45 L assault pack; mass scales with cargo in inventory model."),
                Add("shield_avadeck", "3M Avadeck BA Shield", GearSlotType.BallisticShield, GearCategory.Shields,
                    ProtectionLevel.NIJ_IIIA, 5.4f, 60f, 0.55f, 0.4f, 12f, 1f, false,
                    "NIJ IIIA curved hand-carried shield, viewport; mobility handled in rigging."),
                Add("shield_h3b", "HighCom H3B Level III+", GearSlotType.BallisticShield, GearCategory.Shields,
                    ProtectionLevel.NIJ_III, 9.5f, 80f, 0.45f, 0.3f, 15f, 1f, false,
                    "Level III+ rifle-rated w/ NIJ 0108 spike rating (adjudicated as III here).")
            };
            list.Add(PeltorSportTacFallback());
            return list;
        }

        private static GearItem PeltorSportTacFallback()
        {
            return Add("comtac_sport", "Peltor SportTac", GearSlotType.EarProtection, GearCategory.HearingProtection,
                ProtectionLevel.Unrated, 0.3f, 3f, 0.98f, 0.99f, 5f, 0.5f, false,
                "Hunting-grade electronic earmuff; NRR 20, no tactical PTT.",
                head: 0.03f);
        }

        private static GearItem Helmet(string id, string name, ProtectionLevel level, float mass, float volume,
            float mobility, float aim, float heat, string notes)
        {
            return Add(id, name, GearSlotType.BallisticHelmet, GearCategory.HeadProtection, level, mass, volume,
                mobility, aim, heat, 0.7f, false, notes, head: 0.95f, neck: 0.3f);
        }

        private static GearItem Hearing(string id, string name, float mass, float volume, float heat, string notes)
        {
            return Add(id, name, GearSlotType.EarProtection, GearCategory.HearingProtection,
                ProtectionLevel.Unrated, mass, volume, 0.99f, 1f, heat, 1f, true, notes, head: 0.05f);
        }

        private static GearItem Add(
            string id, string name, GearSlotType slot, GearCategory category, ProtectionLevel level,
            float mass, float volume, float mobility, float aim, float heat, float ir, bool comms, string notes,
            float head = 0f, float torso = 0f, float torsoLow = 0f, float neck = 0f,
            float arms = 0f, float hands = 0f, float legs = 0f)
        {
            var item = new GearItem
            {
                id = id,
                displayName = name,
                slot = slot,
                category = category,
                protectionLevel = level,
                massKg = mass,
                volumeLitres = volume,
                mobilityMultiplier = mobility,
                aimMultiplier = aim,
                heatLoad = heat,
                irSignatureMultiplier = ir,
                commsIntegration = comms,
                notes = notes
            };
            Set(item, HitZone.Head, head);
            Set(item, HitZone.Neck, neck);
            Set(item, HitZone.UpperTorso, torso);
            Set(item, HitZone.LowerTorso, torsoLow);
            Set(item, HitZone.UpperArmLeft, arms);
            Set(item, HitZone.UpperArmRight, arms);
            Set(item, HitZone.ForearmLeft, arms);
            Set(item, HitZone.ForearmRight, arms);
            Set(item, HitZone.HandLeft, hands);
            Set(item, HitZone.HandRight, hands);
            Set(item, HitZone.ThighLeft, legs);
            Set(item, HitZone.ThighRight, legs);
            Set(item, HitZone.CalfLeft, legs);
            Set(item, HitZone.CalfRight, legs);
            return item;
        }

        private static void Set(GearItem item, HitZone zone, float value)
        {
            if (value > 0f) item.SetCoverage(zone, value);
        }
    }
}
