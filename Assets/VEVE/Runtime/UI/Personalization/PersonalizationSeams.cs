using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.UI.Personalization
{
    /// <summary>
    /// Thin presentation seams for the Personalization UI family. VEVE.Gear and VEVE.Operator
    /// data is produced by concurrent agents; NOTHING here references those namespaces.
    /// The scene orchestrator wires concrete providers later via
    /// <c>PersonalizationWorkspace.BindGear(...)</c> / <c>BindOperators(...)</c>.
    /// Every consumer must tolerate <c>null</c> sources and fall back to the Default* instances.
    /// </summary>
    public readonly struct GearSlotKey : IEquatable<GearSlotKey>
    {
        public readonly string Key;

        public GearSlotKey(string key)
        {
            Key = key ?? string.Empty;
        }

        public static implicit operator GearSlotKey(string key) => new GearSlotKey(key);

        /// <summary>Trimmed uppercase identity used for all comparisons and dictionary keys.</summary>
        public string Normalized => Key.Trim().ToUpperInvariant();

        public bool IsEmpty => string.IsNullOrEmpty(Key);
        public override string ToString() => Normalized;
        public bool Equals(GearSlotKey other) => string.Equals(Normalized, other.Normalized, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GearSlotKey k && Equals(k);
        public override int GetHashCode() => Normalized.GetHashCode();
        public static bool operator ==(GearSlotKey a, GearSlotKey b) => a.Equals(b);
        public static bool operator !=(GearSlotKey a, GearSlotKey b) => !a.Equals(b);

        // Well-known personalization slots (string keys so VEVE.Gear can map any enum later).
        public static readonly GearSlotKey Helmet = new GearSlotKey("HELMET");
        public static readonly GearSlotKey FaceShield = new GearSlotKey("FACE_SHIELD");
        public static readonly GearSlotKey EarPro = new GearSlotKey("EAR_PRO");
        public static readonly GearSlotKey PlateCarrier = new GearSlotKey("PLATE_CARRIER");
        public static readonly GearSlotKey BackPanel = new GearSlotKey("BACK_PANEL");
        public static readonly GearSlotKey ChestRig = new GearSlotKey("CHEST_RIG");
        public static readonly GearSlotKey Pouches = new GearSlotKey("POUCHES");
        public static readonly GearSlotKey Belt = new GearSlotKey("BELT");
        public static readonly GearSlotKey Gloves = new GearSlotKey("GLOVES");
        public static readonly GearSlotKey Boots = new GearSlotKey("BOOTS");

        public static readonly GearSlotKey[] DefaultSlots =
        {
            Helmet, FaceShield, EarPro, PlateCarrier, BackPanel,
            ChestRig, Pouches, Belt, Gloves, Boots
        };
    }

    /// <summary>Body zones used for the protection coverage readout (mirrors HitZone naming in VEVE damage docs).</summary>
    public enum HitZone
    {
        Head,
        Thorax,
        Abdomen,
        Pelvis,
        Arms,
        Hands,
        Legs
    }

    /// <summary>One catalogue row for a gear item shown in the Gear panel (seam payload).</summary>
    [Serializable]
    public struct GearItemCard
    {
        public GearSlotKey slot;
        public string itemId;
        public string displayName;
        public float massKg;
        public float volumeLiters;
        /// <summary>0..100 protection this item contributes to its slot (maps to GetCoveragePercent usage).</summary>
        public float coveragePercent;
    }

    /// <summary>Operator roster card. Exact shape requested by the Personalization UI spec.</summary>
    [Serializable]
    public struct OperatorCardData
    {
        public string Id;
        public string Callsign;
        public string Specialty;
        public int TraitCount;
        public string AvatarColorHex;
    }

    /// <summary>One cosmetic finish row (backed by the local <see cref="FinishesCatalog"/> table).</summary>
    [Serializable]
    public struct FinishDefinition
    {
        public string id;
        public string displayName;
        /// <summary>Six hex digits, no '#' prefix, e.g. "B88A5F" (FDE).</summary>
        public string colorHex;
        public string durabilityLabel;
        public string hardnessLabel;
        /// <summary>IR signature exposure tag: NOIR / LOW / MED / HIGH.</summary>
        public string irSignatureTag;
    }

    /// <summary>Roster provider seam. Implemented later by a VEVE.Operator bridge.</summary>
    public interface IOperatorRosterSource
    {
        OperatorCardData[] GetOperators();
        /// <summary>Trait labels for one operator; may be null/empty (panel falls back to indexed names).</summary>
        string[] GetTraits(OperatorCardData op);
    }

    /// <summary>Gear catalogue seam. Implemented later by a VEVE.Gear bridge.</summary>
    public interface IGearRosterSource
    {
        GearSlotKey[] GetSlots();
        GearItemCard[] GetItems(GearSlotKey slot);
        /// <summary>0..100 protection currently worn in this slot (0 when nothing is equipped).</summary>
        float GetCoveragePercent(GearSlotKey slot);
    }

    /// <summary>Loadout totals seam (mass / volume / thermal envelopes + optional zone aggregates).</summary>
    public interface IGearLoadoutPresenter
    {
        float TotalMassKg { get; }
        float MassCapacityKg { get; }
        float TotalVolumeLiters { get; }
        float VolumeCapacityLiters { get; }
        /// <summary>0..1 heat-stress envelope used by the thermal bar.</summary>
        float ThermalLoad01 { get; }
        /// <summary>
        /// Aggregate protection for a zone, 0..100. May return -1 to mean
        /// "not computed" so the panel falls back to its local coverage table.
        /// </summary>
        float GetCoveragePercent(HitZone zone);
    }

    /// <summary>Destination seam for finish application: the later VEVE.Gear weapon-presenter writes strings here.</summary>
    public interface IFinishApplyTarget
    {
        string CurrentFinishId { get; }
        void ApplyFinish(FinishDefinition finish);
    }

    /// <summary>Zeroing configuration seam (avoids any direct ScopeProfile reference).</summary>
    public interface IZeroingProvider
    {
        /// <summary>Current zero/indefeata distance in metres (table origin).</summary>
        float ZeroRangeMeters { get; }
        /// <summary>Elevation adjustment per click, in MRAD (e.g. 0.1 typical tactical). </summary>
        float MilPerClick { get; }
        /// <summary>Elevation adjustment per click, in MOA (e.g. 0.25 typical). </summary>
        float MoaPerClick { get; }
    }

    // ------------------------------------------------------------------ defaults

    public sealed class DefaultOperatorRosterSource : IOperatorRosterSource
    {
        public OperatorCardData[] GetOperators() => Array.Empty<OperatorCardData>();
        public string[] GetTraits(OperatorCardData op) => Array.Empty<string>();
    }

    public sealed class DefaultGearRosterSource : IGearRosterSource
    {
        public GearSlotKey[] GetSlots() => GearSlotKey.DefaultSlots;
        public GearItemCard[] GetItems(GearSlotKey slot) => Array.Empty<GearItemCard>();
        public float GetCoveragePercent(GearSlotKey slot) => 0f;
    }

    public sealed class DefaultGearLoadoutPresenter : IGearLoadoutPresenter
    {
        public const float DefaultMassCapacityKg = 20f;
        public const float DefaultVolumeCapacityLiters = 25f;

        public float TotalMassKg => 0f;
        public float MassCapacityKg => DefaultMassCapacityKg;
        public float TotalVolumeLiters => 0f;
        public float VolumeCapacityLiters => DefaultVolumeCapacityLiters;
        public float ThermalLoad01 => 0f;
        /// <summary>-1 = "no data", so GearPanel uses its local table instead of showing all-zero bars.</summary>
        public float GetCoveragePercent(HitZone zone) => -1f;
    }

    public sealed class DefaultZeroingProvider : IZeroingProvider
    {
        public const float FallbackZeroMeters = 100f;
        public const float DefaultMilPerClick = 0.1f;
        public const float DefaultMoaPerClick = 0.25f;

        private readonly float _zero;
        private readonly float _mil;
        private readonly float _moa;

        public DefaultZeroingProvider(
            float zeroRangeMeters = FallbackZeroMeters,
            float milPerClick = DefaultMilPerClick,
            float moaPerClick = DefaultMoaPerClick)
        {
            _zero = Mathf.Max(1f, zeroRangeMeters);
            _mil = Mathf.Max(0.01f, milPerClick);
            _moa = Mathf.Max(0.01f, moaPerClick);
        }

        public float ZeroRangeMeters => _zero;
        public float MilPerClick => _mil;
        public float MoaPerClick => _moa;
    }

    // ------------------------------------------------------------------ coverage math

    /// <summary>
    /// Static fraction-of-zone table: what portion of each <see cref="HitZone"/> a gear slot
    /// physically shields when its <c>coveragePercent</c> is 100. Sums across every slot entry
    /// are deliberately kept &lt;= 1 per zone (verified by PersSeamsTests).
    /// </summary>
    public static class GearCoverageTable
    {
        private static readonly Dictionary<GearSlotKey, (HitZone zone, float fraction)[]> Table =
            new Dictionary<GearSlotKey, (HitZone, float)[]>
            {
                [GearSlotKey.Helmet] = new[] { (HitZone.Head, 0.70f) },
                [GearSlotKey.FaceShield] = new[] { (HitZone.Head, 0.20f) },
                [GearSlotKey.EarPro] = new[] { (HitZone.Head, 0.10f) },
                [GearSlotKey.PlateCarrier] = new[] { (HitZone.Thorax, 0.45f), (HitZone.Abdomen, 0.15f) },
                [GearSlotKey.BackPanel] = new[] { (HitZone.Thorax, 0.20f), (HitZone.Abdomen, 0.15f) },
                [GearSlotKey.ChestRig] = new[] { (HitZone.Thorax, 0.10f), (HitZone.Abdomen, 0.20f) },
                [GearSlotKey.Pouches] = Array.Empty<(HitZone, float)>(),
                [GearSlotKey.Belt] = new[] { (HitZone.Pelvis, 0.60f) },
                [GearSlotKey.Gloves] = new[] { (HitZone.Hands, 1.00f) },
                [GearSlotKey.Boots] = new[] { (HitZone.Legs, 0.60f) },
            };

        public static readonly HitZone[] Zones =
        {
            HitZone.Head, HitZone.Thorax, HitZone.Abdomen, HitZone.Pelvis,
            HitZone.Arms, HitZone.Hands, HitZone.Legs
        };

        public static bool TryGetEntries(GearSlotKey slot,
            out (HitZone zone, float fraction)[] entries)
        {
            return Table.TryGetValue(slot, out entries);
        }

        /// <summary>Raw table fraction for one slot/zone pair (0 when the slot does not mask that zone).</summary>
        public static float BaseCoverage(GearSlotKey slot, HitZone zone)
        {
            if (!Table.TryGetValue(slot, out (HitZone, float)[] entries))
                return 0f;
            float total = 0f;
            foreach ((HitZone z, float f) in entries)
                if (z == zone) total += f;
            return total;
        }

        /// <summary>
        /// Aggregate coverage for a zone, 0..100:
        /// clamp01( SUM over equipped slots of BaseCoverage(slot, zone) * protection01(slot) ) * 100.
        /// protection01 is the roster's GetCoveragePercent(slot)/100 (worn item factor 0..1).
        /// </summary>
        public static float AggregateZoneCoveragePercent(
            HitZone zone, IEnumerable<KeyValuePair<GearSlotKey, float>> protectionBySlot01)
        {
            if (protectionBySlot01 == null)
                return 0f;
            float sum = 0f;
            foreach (KeyValuePair<GearSlotKey, float> pair in protectionBySlot01)
            {
                float protection = Mathf.Clamp01(pair.Value);
                sum += BaseCoverage(pair.Key, zone) * protection;
            }
            return Mathf.Clamp01(sum) * 100f;
        }
    }

    // ------------------------------------------------------------------ finishes table

    /// <summary>
    /// Local cosmetic finish catalogue. Colours are real VEVE weapon-finish references;
    /// the orchestrator later routes selection into VEVE.Gear via <see cref="IFinishApplyTarget"/>.
    /// </summary>
    public static class FinishesCatalog
    {
        private static readonly FinishDefinition[] Definitions =
        {
            new FinishDefinition { id = "fde", displayName = "Flat Dark Earth", colorHex = "B88A5F",
                durabilityLabel = "Reinforced", hardnessLabel = "Matte", irSignatureTag = "LOW" },
            new FinishDefinition { id = "rgr", displayName = "Ranger Green", colorHex = "4A5235",
                durabilityLabel = "Reinforced", hardnessLabel = "Matte", irSignatureTag = "LOW" },
            new FinishDefinition { id = "odb", displayName = "OD Drab 4172", colorHex = "606B3A",
                durabilityLabel = "Standard", hardnessLabel = "Satin", irSignatureTag = "MED" },
            new FinishDefinition { id = "blk", displayName = "Covert Black", colorHex = "141414",
                durabilityLabel = "Ceramic-hardened", hardnessLabel = "Hard-Coat", irSignatureTag = "NOIR" },
            new FinishDefinition { id = "wol", displayName = "Wolf Grey", colorHex = "6C6E70",
                durabilityLabel = "Standard", hardnessLabel = "Satin", irSignatureTag = "MED" },
            new FinishDefinition { id = "tun", displayName = "Tungsten Tide", colorHex = "4E5255",
                durabilityLabel = "Reinforced", hardnessLabel = "Matte", irSignatureTag = "LOW" },
            new FinishDefinition { id = "bru", displayName = "Bruin", colorHex = "5C5448",
                durabilityLabel = "Extended", hardnessLabel = "Gloss", irSignatureTag = "MED" },
            new FinishDefinition { id = "san", displayName = "Sangria Distressed", colorHex = "6E3A3A",
                durabilityLabel = "Standard", hardnessLabel = "Satin", irSignatureTag = "HIGH" },
        };

        public static FinishDefinition[] All => (FinishDefinition[])Definitions.Clone();

        public static bool TryGet(string id, out FinishDefinition def)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (FinishDefinition candidate in Definitions)
                {
                    if (string.Equals(candidate.id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        def = candidate;
                        return true;
                    }
                }
            }
            def = default;
            return false;
        }

        public static Color SwatchColor(FinishDefinition def, Color fallback)
        {
            if (string.IsNullOrEmpty(def.colorHex))
                return fallback;
            string hex = def.colorHex.StartsWith("#", StringComparison.Ordinal)
                ? def.colorHex : "#" + def.colorHex;
            return ColorUtility.TryParseHtmlString(hex, out Color parsed) ? parsed : fallback;
        }
    }
}
