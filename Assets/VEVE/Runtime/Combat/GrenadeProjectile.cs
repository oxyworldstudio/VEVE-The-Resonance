using System.Collections.Generic;
using UnityEngine;
using VEVE.Gear;

namespace VEVE.Combat
{
    /// <summary>
    /// Thrown explosive: kinematic manual integration (deterministic, matches the
    /// character-math doctrine). Blast resolution is a single pass through the armor
    /// chain - a stopped frag still delivers trauma; thrower's own pawn is immune.
    /// </summary>
    public sealed class GrenadeProjectile : MonoBehaviour
    {
        private Vector3 velocity;
        private float fuse;
        private float blastRadius = GrenadeRules.DefaultRadiusM;
        private float blastEnergy = GrenadeRules.DefaultBlastEnergyJ;
        private ulong thrower;
        private bool done;

        public void Configure(Vector3 initialVelocity, float radius, float totalEnergy, ulong throwerOwnerId, float fuseSeconds)
        {
            velocity = initialVelocity;
            blastRadius = Mathf.Max(1f, radius);
            blastEnergy = Mathf.Max(1f, totalEnergy);
            thrower = throwerOwnerId;
            fuse = GrenadeRules.FuseClamp(fuseSeconds);
            done = false;
        }

        public bool Live => !done;
        /// <summary>Session owner that threw it (telemetry/attribution).</summary>
        public ulong ThrowerOwner => thrower;

        private void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            fuse -= dt;
            Vector3 step = velocity * dt;
            step.y += 0.5f * Physics.gravity.y * dt * dt;
            velocity += Physics.gravity * dt;

            float dist = step.magnitude;
            if (dist > 0.0001f && Physics.SphereCast(transform.position, 0.12f, step.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                Explode();
                return;
            }
            transform.position += step;
            if (fuse <= 0f) Explode();
        }

        /// <summary>Applies blast to every Damageable in radius, respecting cover gear + owner immunity. Returns hit count.</summary>
        public int Explode()
        {
            if (done) return 0;
            done = true;
            int struck = 0;
            Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var dmg = hits[i].GetComponentInParent<VEVE.Damageable>();
                if (dmg == null) continue;
                if (OwnedByThrower(dmg.transform)) continue;
                float d = Vector3.Distance(transform.position, dmg.transform.position);
                float angle = Vector3.Angle(-transform.up, (transform.position - dmg.transform.position));
                var gear = dmg.GetComponentInChildren<DamageableGearAdapter>();
                var mitigation = default(GearMitigationResult);
                bool consulted = GrenadeRules.ApplyBlastMitigation(
                    gear != null ? gear.Loadout : null, d, blastRadius, blastEnergy,
                    VeveTorsoZoneFor(dmg), angle, ref mitigation);
                float damage;
                float energy = GrenadeRules.BlastEnergyAtDistance(d, blastRadius, blastEnergy);
                if (consulted)
                {
                    damage = Mathf.Max(0f, mitigation.damageScale * energy * 0.12f);
                    if (mitigation.stopped)
                    {
                        var physiology = dmg.GetComponent<VEVE.Physiology>();
                        if (physiology != null)
                            physiology.ApplyWound(mitigation.traumaEnergyJoules * 0.01f, mitigation.traumaEnergyJoules * 0.02f);
                    }
                }
                else
                {
                    damage = energy * 0.12f;
                }
                if (damage > 0f)
                {
                    dmg.ApplyDamage(damage, VeveTorsoZoneFor(dmg));
                    struck++;
                }
            }
            VEVE.TacticalSound.Emit(transform.position, 110f);
            Destroy(gameObject);
            return struck;
        }

        /// <summary>Blast is friendly-fire-free in PvE: every player/pawn object immune.</summary>
        private bool OwnedByThrower(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                if (cur.GetComponent<VEVE.Net.NetworkedPlayerPawn>() != null) return true;
                if (cur.GetComponent<VEVE.PlayerController>() != null) return true;
                cur = cur.parent;
            }
            return false;
        }

        private static VEVE.HitZone VeveTorsoZoneFor(VEVE.Damageable d)
        {
            // whole-body proxy: blast distributes over the torso (armor coverage applies there)
            return VEVE.HitZone.UpperTorso;
        }
    }
}
