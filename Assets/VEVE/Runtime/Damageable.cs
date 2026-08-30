using UnityEngine;

namespace VEVE
{
    public enum HitZone { Head, Neck, UpperTorso, LowerTorso, UpperArmLeft, UpperArmRight, ForearmLeft, ForearmRight, HandLeft, HandRight, ThighLeft, ThighRight, CalfLeft, CalfRight, FootLeft, FootRight }

    public sealed class Damageable : MonoBehaviour
    {
        [SerializeField] private float torsoIntegrity = 100f;
        [SerializeField] private float headIntegrity = 50f;
        [SerializeField] private float limbIntegrity = 40f;
        [SerializeField] private Physiology physiology;
        public bool IsDisabled { get; private set; }
        public float TorsoIntegrity => torsoIntegrity;
        public float HeadIntegrity => headIntegrity;
        public float LimbIntegrity => limbIntegrity;

        public void ApplyDamage(float amount, HitZone zone)
        {
            if (amount <= 0f || IsDisabled) return;
            if (physiology == null) physiology = GetComponent<Physiology>();
            switch (zone)
            {
                case HitZone.Head: headIntegrity -= amount; physiology?.ApplyWound(amount * 0.08f, amount * 0.7f); break;
                case HitZone.UpperArmLeft:
                case HitZone.UpperArmRight:
                case HitZone.ForearmLeft:
                case HitZone.ForearmRight:
                case HitZone.HandLeft:
                case HitZone.HandRight:
                case HitZone.ThighLeft:
                case HitZone.ThighRight:
                case HitZone.CalfLeft:
                case HitZone.CalfRight:
                case HitZone.FootLeft:
                case HitZone.FootRight:
                    limbIntegrity -= amount; physiology?.ApplyWound(amount * 0.04f, amount * 0.35f); physiology?.ApplyFracture(amount * 0.3f); break;
                default: torsoIntegrity -= amount; physiology?.ApplyWound(amount * 0.06f, amount * 0.5f); break;
            }
            if (headIntegrity <= 0f || torsoIntegrity <= 0f) IsDisabled = true;
        }

        public void Treat(float bleedingReduction, float painReduction)
        {
            if (physiology == null) physiology = GetComponent<Physiology>();
            if (physiology != null) physiology.Treat(bleedingReduction, painReduction);
        }
    }
}
