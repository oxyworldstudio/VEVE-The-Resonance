using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.UI.Personalization
{
    using GameHitZone = VEVE.HitZone;
    using GearLoadoutType = VEVE.Gear.GearLoadout;
    using Operators;

    /// <summary>
    /// Bridges <see cref="VEVE.Operators"/> profiles into the Personalization roster seam.
    /// </summary>
    public sealed class RosterProvider : IOperatorRosterSource
    {
        private readonly List<VEVE.Operators.OperatorProfile> profiles = new List<VEVE.Operators.OperatorProfile>();

        public RosterProvider(IEnumerable<VEVE.Operators.OperatorProfile> source)
        {
            if (source != null)
            {
                foreach (VEVE.Operators.OperatorProfile p in source)
                {
                    if (p != null) profiles.Add(p);
                }
            }
        }

        public OperatorCardData[] GetOperators()
        {
            OperatorCardData[] cards = new OperatorCardData[profiles.Count];
            for (int i = 0; i < profiles.Count; i++)
            {
                VEVE.Operators.OperatorProfile p = profiles[i];
                cards[i] = new OperatorCardData
                {
                    Id = string.IsNullOrEmpty(p.operatorId) ? p.callsign : p.operatorId,
                    Callsign = p.callsign,
                    Specialty = p.defaultSpecialty.ToString(),
                    TraitCount = p.traits != null ? p.traits.traitIds.Count : 0,
                    AvatarColorHex = StableHex(p.operatorId ?? p.callsign)
                };
            }
            return cards;
        }

        public string[] GetTraits(OperatorCardData op)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                VEVE.Operators.OperatorProfile p = profiles[i];
                string pid = string.IsNullOrEmpty(p.operatorId) ? p.callsign : p.operatorId;
                if (!string.Equals(pid, op.Id, StringComparison.Ordinal)) continue;
                if (p.traits == null || p.traits.traitIds.Count == 0) return Array.Empty<string>();
                string[] names = new string[p.traits.traitIds.Count];
                for (int t = 0; t < names.Length; t++)
                {
                    names[t] = TraitsLabel(p.traits.traitIds[t]);
                }
                return names;
            }
            return Array.Empty<string>();
        }

        private static string TraitsLabel(OperatorTraitId id) => id.ToString();

        /// <summary>FNV-1a over the seed mapped to a 6-digit hex swatch string.</summary>
        public static string StableHex(string seed)
        {
            unchecked
            {
                uint h = 2166136261u;
                string s = seed ?? string.Empty;
                for (int i = 0; i < s.Length; i++)
                {
                    h = (h ^ s[i]) * 16777619u;
                }
                // Keep hue in the upper two bytes; force full alpha for readability.
                return ((h >> 8) & 0xFFFFFFu).ToString("X6");
            }
        }
    }

    /// <summary>
    /// Live gear provider: maps a <see cref="VEVE.Gear.GearLoadout"/> onto both the roster
    /// and presenter seams. Coverage numbers are display values: per-slot protection reads as
    /// a fixed 0..100 scale per protection level; zone aggregates come from the loadout's own
    /// per-zone coverage (0..1) converted to percent via the seam zone mapping table.
    /// </summary>
    public sealed class LiveGearProvider : IGearRosterSource, IGearLoadoutPresenter
    {
        public const float ThermalHeatCeiling = 80f;
        public const float DefaultMassCapacity = 20f;
        public const float DefaultVolumeCapacity = 25f;

        private readonly Func<GearLoadoutType> loadoutAccessor;

        public LiveGearProvider(Func<GearLoadoutType> loadoutAccessor)
        {
            this.loadoutAccessor = loadoutAccessor ?? (Func<GearLoadoutType>)(() => null);
        }

        public static float ProtectionPercentForLevel(VEVE.Gear.ProtectionLevel level)
        {
            switch (level)
            {
                case VEVE.Gear.ProtectionLevel.Unrated: return 0f;
                case VEVE.Gear.ProtectionLevel.NIJ_I: return 20f;
                case VEVE.Gear.ProtectionLevel.NIJ_II: return 32f;
                case VEVE.Gear.ProtectionLevel.NIJ_IIIA: return 48f;
                case VEVE.Gear.ProtectionLevel.VPAM_TRS: return 52f;
                case VEVE.Gear.ProtectionLevel.VPAM_BRS_S: return 58f;
                case VEVE.Gear.ProtectionLevel.VPAM_SR9: return 66f;
                case VEVE.Gear.ProtectionLevel.NIJ_III: return 70f;
                case VEVE.Gear.ProtectionLevel.VPAM_GRW1: return 72f;
                case VEVE.Gear.ProtectionLevel.VPAM_B6: return 82f;
                case VEVE.Gear.ProtectionLevel.NIJ_IV: return 88f;
                case VEVE.Gear.ProtectionLevel.VPAM_B7: return 90f;
                case VEVE.Gear.ProtectionLevel.V50_FRAG: return 60f;
                default: return 0f;
            }
        }

        public static bool SlotMatches(VEVE.Gear.GearSlotType slot, GearSlotKey key)
        {
            string g = slot.ToString().ToUpperInvariant();
            string k = key.Normalized;
            return k.Length > 0 && (g == k || g.Replace("_", "-") == k.Replace("_", "-") || g.Contains(k) || k.Contains(g.Replace(" ", "_")));
        }

        private static bool TryGetSlot(GearLoadoutType loadout, GearSlotKey slot, out VEVE.Gear.GearItem item)
        {
            item = null;
            if (loadout == null) return false;
            int slotCount = System.Enum.GetValues(typeof(VEVE.Gear.GearSlotType)).Length;
            for (int i = 0; i < slotCount; i++)
            {
                VEVE.Gear.GearSlotType s = (VEVE.Gear.GearSlotType)System.Enum.ToObject(typeof(VEVE.Gear.GearSlotType), i);
                if (!SlotMatches(s, slot)) continue;
                VEVE.Gear.GearItem found = loadout.Get(s);
                if (found != null) { item = found; return true; }
            }
            return false;
        }

        public GearSlotKey[] GetSlots() => GearSlotKey.DefaultSlots;

        public GearItemCard[] GetItems(GearSlotKey slot)
        {
            List<GearItemCard> rows = new List<GearItemCard>();
            System.Collections.Generic.IReadOnlyList<VEVE.Gear.GearItem> catalog = VEVE.Gear.GearCatalog.All();
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    VEVE.Gear.GearItem item = catalog[i];
                    if (item == null) continue;
                    if (!SlotMatches(item.slot, slot)) continue;
                    rows.Add(new GearItemCard
                    {
                        slot = slot,
                        itemId = item.id,
                        displayName = item.displayName,
                        massKg = item.massKg,
                        volumeLiters = item.volumeLitres,
                        coveragePercent = ProtectionPercentForLevel(item.protectionLevel)
                    });
                }
            }
            return rows.ToArray();
        }

        public float GetCoveragePercent(GearSlotKey slot)
        {
            if (TryGetSlot(loadoutAccessor(), slot, out VEVE.Gear.GearItem worn))
                return ProtectionPercentForLevel(worn.protectionLevel);
            return 0f;
        }

        // ------------------------------------------------------------- presenter

        private GearLoadoutType Loadout => loadoutAccessor != null ? loadoutAccessor() : null;

        public float TotalMassKg
        {
            get
            {
                GearLoadoutType l = Loadout;
                return l != null ? l.TotalMassKg : 0f;
            }
        }

        public float MassCapacityKg => DefaultMassCapacity;

        public float TotalVolumeLiters
        {
            get
            {
                GearLoadoutType l = Loadout;
                return l != null ? l.TotalVolumeLitres : 0f;
            }
        }

        public float VolumeCapacityLiters => DefaultVolumeCapacity;

        public float ThermalLoad01
        {
            get
            {
                GearLoadoutType l = Loadout;
                return l != null ? Mathf.Clamp01(l.TotalHeatLoad / ThermalHeatCeiling) : 0f;
            }
        }

        public static GameHitZone[] MapZone(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return new[] { GameHitZone.Head, GameHitZone.Neck };
                case HitZone.Thorax: return new[] { GameHitZone.UpperTorso };
                case HitZone.Abdomen: return new[] { GameHitZone.LowerTorso };
                case HitZone.Pelvis: return new[] { GameHitZone.LowerTorso };
                case HitZone.Arms: return new[] { GameHitZone.UpperArmLeft, GameHitZone.UpperArmRight, GameHitZone.ForearmLeft, GameHitZone.ForearmRight };
                case HitZone.Hands: return new[] { GameHitZone.HandLeft, GameHitZone.HandRight };
                case HitZone.Legs: return new[] { GameHitZone.ThighLeft, GameHitZone.ThighRight, GameHitZone.CalfLeft, GameHitZone.CalfRight, GameHitZone.FootLeft, GameHitZone.FootRight };
                default: return Array.Empty<GameHitZone>();
            }
        }

        public float GetCoveragePercent(HitZone zone)
        {
            GearLoadoutType l = Loadout;
            if (l == null) return -1f;
            GameHitZone[] zones = MapZone(zone);
            if (zones.Length == 0) return -1f;
            float sum = 0f;
            for (int i = 0; i < zones.Length; i++) sum += l.CoverageFor(zones[i]);
            return Mathf.Clamp01(sum / zones.Length) * 100f;
        }
    }

    /// <summary>
    /// Routes finish selection onto the per-weapon identity (when wired) or the local string.
    /// </summary>
    public sealed class FinishProvider : IFinishApplyTarget
    {
        private readonly VEVE.WeaponCustomPro.WeaponInstanceIdentity identity;
        private string fallbackId = string.Empty;

        public FinishProvider(VEVE.WeaponCustomPro.WeaponInstanceIdentity identity)
        {
            this.identity = identity;
        }

        public string CurrentFinishId => identity != null ? identity.finishId : fallbackId;

        public void ApplyFinish(FinishDefinition finish)
        {
            string id = string.IsNullOrEmpty(finish.id) ? string.Empty : finish.id;
            if (identity != null) identity.finishId = id;
            else fallbackId = id;
        }
    }

    /// <summary>
    /// Zeroing seam defaults; the orchestrator overrides click values per optic mount,
    /// which is why no direct ScopeProfile field name is assumed here.
    /// </summary>
    public sealed class ZeroingProviderStub : IZeroingProvider
    {
        public float ZeroRangeMeters { get; set; } = DefaultZeroingProvider.FallbackZeroMeters;
        public float MilPerClick { get; set; } = DefaultZeroingProvider.DefaultMilPerClick;
        public float MoaPerClick { get; set; } = DefaultZeroingProvider.DefaultMoaPerClick;
    }

    /// <summary>
    /// Drop-in scene component: finds the Personalization workspace and binds every seam
    /// to its live providers. Pure additive wiring, null-safe without a workspace present.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersonalizationBinder : MonoBehaviour
    {
        [SerializeField] private VEVE.Gear.DamageableGearAdapter gearAdapter;

        private PersonalizationWorkspace workspace;
        private float pollTimer;
        private string lastSelectionSignature;

        private void OnEnable()
        {
            workspace = UnityEngine.Object.FindFirstObjectByType<PersonalizationWorkspace>();
            if (workspace == null) return;

            RosterProvider roster = new RosterProvider(VEVE.Operators.OperatorProfile.CreateDefaultRoster());
            LiveGearProvider gear = new LiveGearProvider(GetLoadout);
            ZeroingProviderStub zeroing = new ZeroingProviderStub();
            FinishProvider finishes = new FinishProvider(gearAdapter != null ? gearAdapter.GetComponent<VEVE.WeaponCustomPro.WeaponInstanceIdentity>() : null);

            workspace.BindOperators(roster);
            workspace.BindGear(gear, gear);
            workspace.BindZeroing(zeroing);
            workspace.BindFinishes(finishes);
        }

        private VEVE.Gear.GearLoadout GetLoadout()
        {
            if (gearAdapter == null)
                gearAdapter = GetComponentInChildren<VEVE.Gear.DamageableGearAdapter>();
            return gearAdapter != null ? gearAdapter.Loadout : null;
        }

        private void LateUpdate()
        {
            if (workspace == null) return;
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer > 0f) return;
            pollTimer = 0.25f;

            UserLoadoutSelection sel = workspace.Selection;
            string sig = sel != null ? sel.ToString() : null;
            if (sig == null) return;
            if (string.Equals(sig, lastSelectionSignature, StringComparison.Ordinal)) return;
            lastSelectionSignature = sig;
        }

        public string DebugSummary
        {
            get
            {
                UserLoadoutSelection sel = workspace != null ? workspace.Selection : null;
                return sel == null ? "no-workspace" : sel.weaponId + "|" + sel.operatorId + "|" + sel.finishId;
            }
        }
    }
}
