using System;
using System.Collections.Generic;

namespace VEVE.Net
{
    /// <summary>One locally-predicted shot outcome, mirrored on publish for reconciliation.</summary>
    public struct ShotPrediction
    {
        public int tick;
        public ulong owner;
        public bool localHit;
        public float distanceM;
    }

    /// <summary>
    /// Pure lag-compensation rules over the deterministic journal: prediction is
    /// never authoritative; mismatched predictions count bounded desync telemetry
    /// (presentation), while the server journal remains the single source of truth
    /// (already enforced by C4's parity mirror).
    /// </summary>
    public static class LagCompRules
    {
        /// <summary>Owner stand-in for offline/single-player shots: never collides with real session ids.</summary>
        public const ulong OfflineOwner = ulong.MaxValue - 1;

        public const int MinWindowFrames = 4;
        public const int MaxWindowFrames = 24;
        public const int DesyncWarnThreshold = 5;

        public static int AuthorityWindowFrames(double pingMilliseconds, int simulateHz)
        {
            if (simulateHz <= 0 || double.IsNaN(pingMilliseconds) || pingMilliseconds < 0) return MinWindowFrames;
            int frames = (int)Math.Round(pingMilliseconds / 1000.0 * simulateHz);
            if (frames < MinWindowFrames) frames = MinWindowFrames;
            if (frames > MaxWindowFrames) frames = MaxWindowFrames;
            return frames;
        }

        /// <summary>Did the authoritative journal confirm the local prediction (same frame window)?</summary>
        public static bool AuthoritativeWithinWindow(int predictedTick, int authoritativeTick, int windowFrames)
        {
            if (predictedTick < 0 || authoritativeTick < 0) return false;
            int delta = Math.Abs(authoritativeTick - predictedTick);
            return delta <= (windowFrames > 0 ? windowFrames : MinWindowFrames);
        }

        public enum Outcome { Confirmed, Desynced, Dropped }

        /// <summary>Telemetry: confirmed vs mismatched predictions; authority is always the journal.</summary>
        public static int ConfirmedCount;
        public static int DesyncCount;
        public static double DefaultPingSeconds = 0.08;
        public const int DefaultTickHz = 60;

        /// <summary>Reconcile an authoritative journal ShotFired fact against the local ring:
        /// matching hit state confirms, mismatching counts one desync, stale/foreign ignored.</summary>
        public static void Reconcile(ShotReplayWindow windowRing, ulong owner, int authoritativeTick, bool serverHit)
        {
            if (owner == 0 || owner == OfflineOwner || windowRing == null) return;
            if (!windowRing.TryGetLatest(owner, authoritativeTick, out ShotPrediction pred)) return;
            int frames = AuthorityWindowFrames(DefaultPingSeconds * 1000.0, DefaultTickHz);
            if (!AuthoritativeWithinWindow(pred.tick, authoritativeTick, frames)) return;
            if (pred.localHit == serverHit) ConfirmedCount++; else DesyncCount++;
        }

        public static Outcome Judge(ShotPrediction predicted, bool serverHit, int windowFrames)
        {
            if (!AuthoritativeWithinWindow(predicted.tick, predicted.tick, windowFrames))
                return Outcome.Dropped;
            // prediction and server disagree on the very same shot = desync (telemetry, never authority)
            return predicted.localHit == serverHit ? Outcome.Confirmed : Outcome.Desynced;
        }
    }

    /// <summary>Fixed ring of recent local predictions for reconciliation.</summary>
    public sealed class ShotReplayWindow
    {
        private readonly ShotPrediction[] slots;
        private readonly bool[] occupied;
        private int cursor;

        public ShotReplayWindow(int capacity = 128)
        {
            if (capacity < 8) capacity = 8;
            slots = new ShotPrediction[capacity];
            occupied = new bool[capacity];
        }

        public int Capacity => slots.Length;

        public void Mark(ShotPrediction p)
        {
            slots[cursor] = p;
            occupied[cursor] = true;
            cursor = cursor + 1 == slots.Length ? 0 : cursor + 1;
        }

        public bool TryGetLatest(ulong owner, int sameTickAsServer, out ShotPrediction found)
        {
            found = default;
            for (int i = 0; i < slots.Length; i++)
            {
                ref ShotPrediction s = ref slots[i];
                if (!occupied[i]) continue;
                if (s.owner == owner && s.tick == sameTickAsServer) { found = s; return true; }
            }
            // fall back to newest stored prediction for that owner (server tick may lag behind)
            bool any = false;
            int best = int.MinValue;
            ShotPrediction bestSlot = default;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!occupied[i]) continue;
                if (slots[i].owner != owner) continue;
                if (!any || slots[i].tick > best) { any = true; best = slots[i].tick; bestSlot = slots[i]; }
            }
            found = bestSlot;
            return any;
        }

        public void ForgetOwner(ulong owner)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].owner == owner) occupied[i] = false;
        }
    }
}
