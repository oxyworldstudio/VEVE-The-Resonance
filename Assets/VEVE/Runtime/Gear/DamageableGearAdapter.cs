using System;
using UnityEngine;

namespace VEVE.Gear
{
    /// <summary>
    /// Bridge between the gear model and <see cref="VEVE.Damageable"/>. The static surface is pure and
    /// unit-testable; the behaviour deliberately performs zero automatic wiring so that adding the
    /// component never mutates simulation state — the orchestrator (or a future Damageable patch)
    /// decides when mitigation applies via <see cref="TryMitigate(GearLoadout,float,float,HitZone,float,ref GearMitigationResult)"/>.
    /// </summary>
    public sealed class DamageableGearAdapter : MonoBehaviour
    {
        [SerializeField] private GearLoadout loadout;

        /// <summary>Loadout exposed by this bridge instance (serialized or set by the spawner).</summary>
        public GearLoadout Loadout
        {
            get => loadout;
            set => loadout = value;
        }

        /// <summary>
        /// Pure entry point for hit processing: mitigates an incoming strike through the loadout and
        /// fills <paramref name="result"/>. Returns false (caller should feed raw damage to Damageable.ApplyDamage)
        /// when no loadout is supplied; returns true with <see cref="GearMitigationResult.damageScale"/>
        /// when a loadout exists — multiply the intended damage by that scale before the call.
        /// </summary>
        /// <param name="loadout">Assembled gear set, may be null.</param>
        /// <param name="incomingEnergyJoules">Round energy on arrival, J.</param>
        /// <param name="velocityMps">Round velocity at impact, m/s (0 when unknown).</param>
        /// <param name="zone">Struck body zone.</param>
        /// <param name="angleDeg">Angle from armor surface normal, degrees.</param>
        /// <param name="result">Mitigation payload.</param>
        /// <returns>False when there is no gear to consult.</returns>
        /// <summary>
        /// Populate a starter kit if this rig is bare (W10). Idempotent: an equipped
        /// loadout is left untouched. Returns false only for a genuine cap/validation failure.
        /// </summary>
        public bool EnsureStarterGear()
        {
            if (loadout != null) return true;
            loadout = new GearLoadout();
            return StarterLoadoutRules.TryBuild(loadout, out _);
        }

        public static bool TryMitigate(GearLoadout loadout, float incomingEnergyJoules, float velocityMps, HitZone zone, float angleDeg, ref GearMitigationResult result)
        {
            if (loadout == null || incomingEnergyJoules <= 0f) return false;
            result = loadout.ApplyHitMitigation(incomingEnergyJoules, velocityMps, zone, angleDeg);
            return true;
        }

        /// <summary>
        /// Instance convenience over <see cref="TryMitigate(GearLoadout,float,float,HitZone,float,ref GearMitigationResult)"/>
        /// using this bridge's serialized loadout.
        /// </summary>
        /// <param name="incomingEnergyJoules">Round energy, J.</param>
        /// <param name="velocityMps">Round velocity, m/s.</param>
        /// <param name="zone">Struck zone.</param>
        /// <param name="angleDeg">Angle from normal, degrees.</param>
        /// <param name="result">Mitigation payload.</param>
        /// <returns>False when no loadout is wired on this bridge.</returns>
        public bool MitigateHit(float incomingEnergyJoules, float velocityMps, HitZone zone, float angleDeg, ref GearMitigationResult result)
        {
            return TryMitigate(loadout, incomingEnergyJoules, velocityMps, zone, angleDeg, ref result);
        }
    }
}
