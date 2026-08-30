using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Gear
{
    /// <summary>
    /// Outcome of applying a modeled hit against a assembled loadout.
    /// </summary>
    public struct GearMitigationResult
    {
        /// <summary>True when the strike area stopped the round within its trauma budget.</summary>
        public bool stopped;
        /// <summary>Impact energy used in the evaluation, J.</summary>
        public float incomingEnergyJoules;
        /// <summary>Blunt trauma energy delivered to the body (full energy when penetrated), J.</summary>
        public float traumaEnergyJoules;
        /// <summary>Predicted backface deformation (mm) when stopped, else 0.</summary>
        public float backfaceMm;
        /// <summary>Zone that absorbed the hit.</summary>
        public HitZone zone;
        /// <summary>Coverage fraction applied to that zone from the loadout.</summary>
        public float coverage;
        /// <summary>Recommended damage scale for <c>Damageable.ApplyDamage</c> (1 - stopped, coverage-weighted).</summary>
        public float damageScale;
    }

    /// <summary>
    /// A fully-assembled set of wearable gear. Pure managed type (safe for EditMode tests and for
    /// serializing into save data); bridged into the scene graph through <see cref="DamageableGearAdapter"/>.
    /// </summary>
    [Serializable]
    public sealed class GearLoadout
    {
        [SerializeField] private Dictionary<GearSlotType, GearItem> equipped = new Dictionary<GearSlotType, GearItem>();
        [SerializeField] private Dictionary<GearSlotType, int> panelStrikeCounts = new Dictionary<GearSlotType, int>();
        private readonly float[] coverageScratch = new float[GearItem.ZoneCount];

        /// <summary>Number of slots currently filled.</summary>
        public int EquippedCount => equipped.Count;

        /// <summary>Item in a slot, or null.</summary>
        /// <param name="slot">Slot to read.</param>
        /// <returns>Equipped item or null.</returns>
        public GearItem Get(GearSlotType slot)
        {
            equipped.TryGetValue(slot, out GearItem item);
            return item;
        }

        /// <summary>Enumerates filled slots.</summary>
        /// <returns>Read-only dictionary view (live).</returns>
        public IReadOnlyDictionary<GearSlotType, GearItem> Equipped => equipped;

        /// <summary>Total worn mass in kilograms (gear only; cargo handled by PhysicalInventory).</summary>
        public float TotalMassKg
        {
            get
            {
                float total = 0f;
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped) total += pair.Value.massKg;
                return total;
            }
        }

        /// <summary>Total stowed volume in litres, comparable to PhysicalInventory capacity.</summary>
        public float TotalVolumeLitres
        {
            get
            {
                float total = 0f;
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped) total += pair.Value.volumeLitres;
                return total;
            }
        }

        /// <summary>Sum of item heat loads feeding future thermoregulation integration.</summary>
        public float TotalHeatLoad
        {
            get
            {
                float total = 0f;
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped) total += pair.Value.heatLoad;
                return total;
            }
        }

        /// <summary>Product of per-item mobility multipliers (uncovered = 1).</summary>
        public float AggregateMobilityMultiplier
        {
            get
            {
                float value = 1f;
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped) value *= pair.Value.mobilityMultiplier;
                return value;
            }
        }

        /// <summary>Product of per-item aim multipliers (uncovered = 1).</summary>
        public float AggregateAimMultiplier
        {
            get
            {
                float value = 1f;
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped) value *= pair.Value.aimMultiplier;
                return value;
            }
        }

        /// <summary>Lowest (strongest) IR signature multiplier across worn items; 1 when bare.</summary>
        public float AggregateIrSignature
        {
            get
            {
                float value = 1f;
                bool any = false;
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped)
                {
                    if (!any || pair.Value.irSignatureMultiplier < value) value = pair.Value.irSignatureMultiplier;
                    any = true;
                }
                return value;
            }
        }

        /// <summary>True when any worn item talks on the comms network.</summary>
        public bool HasCommsIntegration
        {
            get
            {
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped)
                    if (pair.Value.commsIntegration) return true;
                return false;
            }
        }

        /// <summary>
        /// Equips an item, replacing whatever occupied the slot. Validates slot fit (item slot must equal
        /// target slot), mutual exclusion (e.g. SoftArmor vs PlateCarrier) and the optional mass/volume
        /// budget; on any failure the loadout is left untouched.
        /// </summary>
        /// <param name="slot">Target slot; must match the item's own slot.</param>
        /// <param name="item">Item to wear.</param>
        /// <param name="failure">Reason text when the call returns false.</param>
        /// <param name="maxMassKg">Worn-mass cap; &lt;=0 means unlimited.</param>
        /// <param name="maxVolumeLitres">Stowed-volume cap; &lt;=0 means unlimited.</param>
        /// <returns>True when the item is now equipped.</returns>
        public bool TryEquip(GearSlotType slot, GearItem item, out string failure, float maxMassKg = 0f, float maxVolumeLitres = 0f)
        {
            failure = null;
            if (item == null)
            {
                failure = "item is null";
                return false;
            }
            if (item.slot != slot)
            {
                failure = $"item {item.id} belongs to slot {item.slot}, not {slot}";
                return false;
            }

            foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped)
            {
                if (pair.Key == slot) continue;
                if (GearSlots.ConflictsWith(pair.Key, slot))
                {
                    failure = $"{slot} conflicts with equipped {pair.Key}";
                    return false;
                }
            }

            float massProjected = TotalMassKg - (equipped.TryGetValue(slot, out GearItem old) ? old.massKg : 0f) + item.massKg;
            float volumeProjected = TotalVolumeLitres - (old != null ? old.volumeLitres : 0f) + item.volumeLitres;
            if (maxMassKg > 0f && massProjected > maxMassKg)
            {
                failure = $"mass budget exceeded ({massProjected:F2} > {maxMassKg:F2} kg)";
                return false;
            }
            if (maxVolumeLitres > 0f && volumeProjected > maxVolumeLitres)
            {
                failure = $"volume budget exceeded ({volumeProjected:F2} > {maxVolumeLitres:F2} L)";
                return false;
            }

            equipped[slot] = item;
            return true;
        }

        /// <summary>Removes and returns the item worn in a slot.</summary>
        /// <param name="slot">Slot to clear.</param>
        /// <returns>The removed item or null when empty.</returns>
        public GearItem Unequip(GearSlotType slot)
        {
            if (!equipped.TryGetValue(slot, out GearItem item)) return null;
            equipped.Remove(slot);
            panelStrikeCounts.Remove(slot);
            return item;
        }

        /// <summary>Records a hit absorbed by a slot's protective panel, for multi-hit derating.</summary>
        /// <param name="slot">Slot struck.</param>
        public void RegisterPanelStrike(GearSlotType slot)
        {
            panelStrikeCounts.TryGetValue(slot, out int count);
            panelStrikeCounts[slot] = count + 1;
        }

        /// <summary>Hits already absorbed by one slot's panel this engagement.</summary>
        /// <param name="slot">Slot to query.</param>
        /// <returns>Strike count.</returns>
        public int PanelStrikeCount(GearSlotType slot)
        {
            panelStrikeCounts.TryGetValue(slot, out int count);
            return count;
        }

        /// <summary>
        /// Coverage aggregated across all worn items as the maximum per-zone coverage — two items never
        /// stack, but the best layer over a zone counts.
        /// </summary>
        /// <param name="zone">Zone to evaluate.</param>
        /// <returns>Combined coverage 0..1.</returns>
        public float CoverageFor(HitZone zone)
        {
            ComputeCoverage(coverageScratch);
            int index = (int)zone;
            return index >= 0 && index < coverageScratch.Length ? coverageScratch[index] : 0f;
        }

        /// <summary>
        /// Computes the per-zone coverage vector once (max over items per zone, masked by slot).
        /// </summary>
        /// <param name="target">Array of at least <see cref="GearItem.ZoneCount"/> entries.</param>
        public void ComputeCoverage(float[] target)
        {
            if (target == null || target.Length < GearItem.ZoneCount) return;
            for (int i = 0; i < target.Length; i++) target[i] = 0f;
            foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped)
            {
                GearItem item = pair.Value;
                if (item.coveragePerZone == null) continue;
                for (int z = 0; z < GearItem.ZoneCount && z < item.coveragePerZone.Length; z++)
                {
                    float value = Math.Clamp(item.coveragePerZone[z], 0f, 1f);
                    if (value > target[z]) target[z] = value;
                }
            }
        }

        /// <summary>
        /// Damage multiplier applied to incoming hit energy for one zone, given the level of the panel
        /// covering it: (1 - coverage) leaks fully, covered fraction leaks by its 1 - stop probability.
        /// Stop probability is a piecewise-linear V50-style surrogate of energy against the derated
        /// panel ceiling. Result always in [0,1].
        /// </summary>
        /// <param name="incomingEnergyJoules">Round energy on arrival.</param>
        /// <param name="zone">Struck body zone.</param>
        /// <param name="angleDeg">Angle from armor normal, degrees.</param>
        /// <returns>Damage multiplier 0 (fully absorbed) .. 1 (no gear effect).</returns>
        public float DamageMultiplierFor(float incomingEnergyJoules, HitZone zone, float angleDeg = GearProtectionStandard.ReferenceAngleDeg)
        {
            return DamageMultiplierFor(incomingEnergyJoules, zone, angleDeg, equipped, panelStrikeCounts);
        }

        /// <summary>
        /// Pure-static form of <see cref="DamageMultiplierFor(float,float,HitZone,float)"/> so it can be
        /// tested and reused without an instance.
        /// </summary>
        /// <param name="incomingEnergyJoules">Round energy on arrival.</param>
        /// <param name="zone">Struck body zone.</param>
        /// <param name="angleDeg">Angle from armor normal, degrees.</param>
        /// <param name="equippedItems">Slot → item map.</param>
        /// <param name="strikeCounts">Slot → absorbed hit count map (may be null).</param>
        /// <returns>Damage multiplier in [0,1].</returns>
        public static float DamageMultiplierFor(
            float incomingEnergyJoules,
            HitZone zone,
            float angleDeg,
            IReadOnlyDictionary<GearSlotType, GearItem> equippedItems,
            IReadOnlyDictionary<GearSlotType, int> strikeCounts)
        {
            float bestCoverage = 0f;
            float bestStopFraction = 0f;
            if (equippedItems != null)
            {
                foreach (KeyValuePair<GearSlotType, GearItem> pair in equippedItems)
                {
                    float coverage = pair.Value.CoverageFor(zone);
                    if (coverage <= 0f) continue;
                    int strikes = 0;
                    if (strikeCounts != null) strikeCounts.TryGetValue(pair.Key, out strikes);
                    float stop = StopFractionFor(pair.Value, angleDeg, incomingEnergyJoules, strikes);
                    if (coverage * stop > bestCoverage * bestStopFraction)
                    {
                        bestCoverage = coverage;
                        bestStopFraction = stop;
                    }
                }
            }
            float multiplier = 1f - bestCoverage * bestStopFraction;
            return Math.Clamp(multiplier, 0f, 1f);
        }

        private static float StopFractionFor(GearItem item, float angleDeg, float energy, int strikes)
        {
            if (energy <= 0f) return 0f;
            if (!GearProtectionStandard.TryGetLevel(item.protectionLevel, out ProtectionLevelData data)) return 0f;
            float ceiling = data.stopEnergyJoules;
            if (item.customStopEnergyJoules > 0f) ceiling = item.customStopEnergyJoules;
            float effective = ceiling * GearProtectionStandard.ObliquityDefenseFactor(angleDeg)
                * (strikes <= 0 ? 1f : Math.Clamp(MathF.Pow(data.multiHitRetention, strikes), 0.25f, 1f));
            if (effective <= 0f) return 0f;
            float stopFraction = 1f - Math.Clamp((energy - effective) / effective, 0f, 1f);
            return energy <= effective ? 1f : Math.Clamp(stopFraction, 0f, 1f);
        }

        /// <summary>
        /// Applies the loadout's mitigation to one incoming hit: picks the best panel over the zone,
        /// evaluates <see cref="GearProtectionStandard.TryStopAmmunition"/>, registers a panel strike when
        /// stopped and returns the trauma/backface/damage-scale payload for the Damageable integration.
        /// </summary>
        /// <param name="incomingEnergyJoules">Round energy on arrival.</param>
        /// <param name="velocityMps">Round velocity at impact (0 = unknown, energy-only path).</param>
        /// <param name="zone">Struck body zone.</param>
        /// <param name="angleDeg">Angle from armor normal, degrees.</param>
        /// <returns>Mitigation outcome; penetrates untouched when no armor covers the zone.</returns>
        public GearMitigationResult ApplyHitMitigation(float incomingEnergyJoules, float velocityMps, HitZone zone, float angleDeg)
        {
            var result = new GearMitigationResult
            {
                incomingEnergyJoules = incomingEnergyJoules,
                zone = zone,
                coverage = CoverageFor(zone)
            };

            GearSlotType best = (GearSlotType)(-1);
            float bestCoverage = 0f;
            foreach (KeyValuePair<GearSlotType, GearItem> pair in equipped)
            {
                float coverage = pair.Value.CoverageFor(zone);
                if (coverage > bestCoverage)
                {
                    bestCoverage = coverage;
                    best = pair.Key;
                }
            }

            if (bestCoverage <= 0f || best == (GearSlotType)(-1))
            {
                result.traumaEnergyJoules = incomingEnergyJoules;
                result.damageScale = 1f;
                return result;
            }

            GearItem panel = equipped[best];
            panelStrikeCounts.TryGetValue(best, out int strikes);
            float angle = angleDeg;
            bool stopped = GearProtectionStandard.TryStopAmmunition(
                panel.protectionLevel, velocityMps, incomingEnergyJoules, angle, strikes,
                out float trauma, out float backface);
            result.stopped = stopped;
            result.traumaEnergyJoules = stopped ? trauma : incomingEnergyJoules;
            result.backfaceMm = stopped ? backface : 0f;
            if (stopped)
            {
                RegisterPanelStrike(best);
                // Residual risk of incapacitation from blunt deformation scales with coverage and trauma vs budget.
                float residual = Math.Clamp(trauma / 150f, 0f, 1f);
                result.damageScale = Math.Clamp(1f - bestCoverage * (1f - residual), 0f, 1f);
            }
            else
            {
                result.damageScale = Math.Clamp(DamageMultiplierFor(incomingEnergyJoules, zone, angle), 0f, 1f);
            }
            return result;
        }
    }
}
