using UnityEngine;

namespace VEVE.Agents
{
    /// <summary>
    /// Simulation level of detail for autonomous agents, gating how much
    /// cognition each agent performs according to distance from the player.
    /// </summary>
    public enum AgentLODTier
    {
        Full = 0,
        Standard = 1,
        Simplified = 2,
        Statistical = 3
    }

    /// <summary>
    /// Static snapshot of the player camera position used as the LOD reference
    /// point without touching Camera.main from every agent.
    /// </summary>
    public static class AgentViewContext
    {
        public static Vector3 Position;
    }

    /// <summary>
    /// Pure distance-based LOD classification and per-tier tick intervals.
    /// Burst-compatible: no allocations, no scene dependencies.
    /// </summary>
    public static class AgentLOD
    {
        public static readonly int[] TickIntervals = { 1, 2, 6, 20 };

        public static AgentLODTier ComputeLOD(Vector3 agentPosition, Vector3 cameraPosition)
        {
            float dist = Vector3.Distance(agentPosition, cameraPosition);
            if (dist < 20f) return AgentLODTier.Full;
            if (dist < 50f) return AgentLODTier.Standard;
            if (dist < 100f) return AgentLODTier.Simplified;
            return AgentLODTier.Statistical;
        }

        public static int GetTickInterval(AgentLODTier tier)
        {
            return TickIntervals[(int)tier];
        }

        /// <summary>
        /// Deterministic per-agent phase offset so time-sliced tiers do not
        /// all fire on the same frame.
        /// </summary>
        public static int GetStaggerFor(int instanceId)
        {
            uint hash = (uint)(instanceId & 0x7FFFFFFF);
            hash = (hash ^ (hash >> 16)) * 0x7FEB352Du;
            hash = (hash ^ (hash >> 15)) * 0x846CA68Bu;
            return (int)(hash ^ (hash >> 16));
        }

        public static bool ShouldTick(AgentLODTier tier, int staggerHash)
        {
            int interval = GetTickInterval(tier);
            if (interval <= 1) return true;
            return (Time.frameCount + staggerHash) % interval == 0;
        }
    }
}
