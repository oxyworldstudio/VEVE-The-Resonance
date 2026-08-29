using UnityEngine;
using System.Collections.Generic;

namespace VEVE
{
    public enum TacticalStance { Patrol, Investigate, Engage, Suppress, Flank, Retreat, Hold, Communicate, Search, React }

    public readonly struct TacticalDecision
    {
        public readonly TacticalStance stance;
        public readonly Vector3 destination;
        public readonly float urgency;
        public readonly float confidence;

        public TacticalDecision(TacticalStance stance, Vector3 destination, float urgency, float confidence)
        {
            this.stance = stance;
            this.destination = destination;
            this.urgency = urgency;
            this.confidence = confidence;
        }
    }

    public static class TacticalAICore
    {
        public static TacticalDecision EvaluateSituation(
            float threatDistance,
            float threatVisibility,
            float coverQuality,
            float allyCount,
            float health,
            float ammo,
            float stress,
            Vector3 lastKnownThreatPosition,
            Vector3 currentPosition)
        {
            float dangerLevel = CalculateDangerLevel(threatDistance, threatVisibility, coverQuality, health, ammo);
            float confidence = CalculateConfidence(coverQuality, allyCount, health, ammo);
            float urgency = Mathf.Clamp01(dangerLevel * 0.7f + stress * 0.3f);

            TacticalStance stance = dangerLevel > 0.7f && coverQuality < 0.3f
                ? TacticalStance.Retreat
                : dangerLevel > 0.5f && ammo > 0.3f
                ? TacticalStance.Suppress
                : dangerLevel > 0.3f
                ? TacticalStance.Engage
                : threatVisibility > 0.5f
                ? TacticalStance.Investigate
                : TacticalStance.Patrol;

            if (coverQuality > 0.6f && dangerLevel > 0.4f)
                stance = TacticalStance.Hold;

            if (allyCount >= 2 && dangerLevel > 0.5f && ammo > 0.5f)
                stance = TacticalStance.Flank;

            Vector3 destination = lastKnownThreatPosition != Vector3.zero
                ? lastKnownThreatPosition
                : currentPosition;

            return new TacticalDecision(stance, destination, urgency, confidence);
        }

        private static float CalculateDangerLevel(float threatDistance, float threatVisibility, float coverQuality, float health, float ammo)
        {
            float distanceFactor = 1.0f - Mathf.Clamp01(threatDistance / 50f);
            float visibilityFactor = threatVisibility * 0.6f;
            float coverFactor = 1.0f - coverQuality;
            float healthFactor = 1.0f - Mathf.Clamp01(health / 100f);
            float ammoFactor = 1.0f - Mathf.Clamp01(ammo);
            return Mathf.Clamp01(distanceFactor * 0.25f + visibilityFactor * 0.25f + coverFactor * 0.2f + healthFactor * 0.15f + ammoFactor * 0.15f);
        }

        private static float CalculateConfidence(float coverQuality, float allyCount, float health, float ammo)
        {
            return Mathf.Clamp01(
                coverQuality * 0.35f +
                Mathf.Clamp01(allyCount / 3f) * 0.25f +
                Mathf.Clamp01(health / 100f) * 0.25f +
                Mathf.Clamp01(ammo) * 0.15f
            );
        }
    }
}
