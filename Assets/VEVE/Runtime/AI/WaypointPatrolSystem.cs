using System;
using UnityEngine;

namespace VEVE.AI
{
    public enum PatrolMode { Loop, Once, PingPong }

    /// <summary>One leg: world destination plus the hold the shooter takes on reaching it.</summary>
    [Serializable]
    public struct Waypoint
    {
        public Vector3 position;
        public float waitSeconds;
    }

    /// <summary>Authored route data; stateless, safe to share between squads.</summary>
    [Serializable]
    public sealed class PatrolRoute
    {
        public string id;
        public Waypoint[] points;
        public PatrolMode mode = PatrolMode.Loop;
        public float arrivalRadius = 1.5f;
        /// <summary>When true destinations get a per-unit, per-node perpendicular offset so squads never walk painted lines.</summary>
        public bool applyJitter = true;
        public int jitterSeed = 1337;

        public int Count => points != null && points.Length > 0 ? points.Length : 0;
    }

    /// <summary>
    /// Pure, deterministic patrol state machine (no MonoBehaviours, no Time) driven by
    /// Arrive()/Tick(dt)/Destination(unit, id) - the exact contract needed by
    /// TacticalAICore patrol decisions and by C2 extraction legs.
    /// </summary>
    public sealed class PatrolState
    {
        private int _index;
        private int _dir = 1;
        private float _dwell;

        public bool Done { get; private set; }
        public int CurrentIndex => _index;
        public bool IsWaiting => _dwell > 0f;
        public int VisitedCount { get; private set; }

        public void Start(PatrolRoute route)
        {
            _index = 0;
            _dir = 1;
            _dwell = 0f;
            Done = route == null || route.Count == 0;
            VisitedCount = 0;
        }

        /// <summary>Advance dwell clock by dt (world time delta, caller-owned).</summary>
        public void Tick(float dt)
        {
            if (Done || _dwell <= 0f) return;
            _dwell -= dt < 0f ? 0f : dt;
        }

        /// <summary>Signal that the current node was reached; queues the hold and moves on.</summary>
        public void Arrive(PatrolRoute route)
        {
            if (Done || route == null || route.Count == 0) return;
            VisitedCount++;

            switch (route.mode)
            {
                case PatrolMode.Once:
                    if (_index >= route.Count - 1) { Done = true; _dwell = 0f; return; }
                    _index++;
                    break;
                case PatrolMode.PingPong:
                    if (route.Count == 1) { Done = true; _dwell = 0f; return; }
                    int next = _index + _dir;
                    if (next >= route.Count) { _dir = -1; next = _index + _dir; }
                    else if (next < 0) { _dir = 1; next = _index + _dir; }
                    _index = next;
                    break;
                default: // Loop
                    _index = (_index + 1) % route.Count;
                    break;
            }

            Waypoint wp = route.points[_index];
            _dwell = wp.waitSeconds > 0f ? wp.waitSeconds : 0f;
        }

        /// <summary>
        /// Deterministic destination for the given unit. Identical (route, node, unit)
        /// yields the identical point every frame; different units spread naturally.
        /// </summary>
        public Vector3 Destination(PatrolRoute route, Vector3 unitPosition, uint unitId)
        {
            if (route == null || route.Count == 0) return unitPosition;
            if (_index >= route.Count) _index = route.Count - 1;
            Waypoint wp = route.points[_index];
            if (!route.applyJitter) return wp.position;
            return Jitter(wp.position, unitPosition, route, _index, unitId);
        }

        /// <summary>Perpendicular offset that fades out near the node (last-metre precision is straight).</summary>
        public static Vector3 Jitter(Vector3 node, Vector3 unitPosition, PatrolRoute route, int nodeIndex, uint unitId)
        {
            Vector3 to = node - unitPosition;
            float dist = to.magnitude;
            if (dist < 0.05f) return node;

            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)route.jitterSeed) * 16777619u;
                h = (h ^ (uint)nodeIndex) * 16777619u;
                h = (h ^ unitId) * 16777619u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                float r = ((h & 0xFFFFu) / 65535f) * 2f - 1f;

                Vector3 dir = to / dist;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
                side.Normalize();

                float amp = 2.3f * r;
                // fade natural spread within the final 6 metres so arrival stays tidy
                float fade = Mathf.InverseLerp(1.2f, 6f, dist);
                return unitPosition + (dir * dist + side * (amp * fade));
            }
        }
    }
}
