using System;
using UnityEngine;

namespace VEVE.Net
{
    /// <summary>Timestamped authority sample fed by the journal's ticked facts.</summary>
    public struct NetSample
    {
        public int tick;
        public Vector3 position;
        public float yawDeg;
    }

    /// <summary>
    /// Pure interpolation rules for the deferred render clock (W11 base): the buffer
    /// consumes ascending authority samples and renders the past, never the truth-ahead.
    /// </summary>
    public static class InterpolationRules
    {
        public const int HardCapLateFrames = 3;

        public static int RenderTick(int authoritativeTick, int pingFrames)
        {
            int lag = Math.Max(0, pingFrames);
            if (lag > 40) lag = 40;
            int rt = authoritativeTick - lag;
            return rt < 0 ? 0 : rt;
        }

        public static float Alpha(int fromTick, int toTick, int atTick)
        {
            int span = toTick - fromTick;
            if (span <= 0) return 1f;
            float a = (float)(atTick - fromTick) / span;
            return a < 0f ? 0f : (a > 1f ? 1f : a);
        }
    }

    /// <summary>
    /// Ascending tick ring: pushes reject out-of-order/duplicate timestamps and keeps a
    /// sliding window; <see cref="SampleAt"/> clamps to the ends (no extrapolation past
    /// newest sample) — the authority is never invented.
    /// </summary>
    public sealed class InterpolationBuffer
    {
        private readonly NetSample[] ring;
        private int head, count;

        public InterpolationBuffer(int capacity = 48)
        {
            if (capacity < 8) capacity = 8;
            ring = new NetSample[capacity];
        }

        public int Count => count;
        public bool IsEmpty => count == 0;
        public int OldestTick => count > 0 ? ring[Wrap(head - count)].tick : -1;
        public int NewestTick => count > 0 ? ring[Wrap(head - 1)].tick : -1;

        public void Push(NetSample s)
        {
            if (count > 0 && s.tick <= NewestTick)
            {
                // duplicate/out-of-order authority tick: ignore (journal owns ordering)
                return;
            }
            ring[head] = s;
            head = (head + 1) % ring.Length;
            if (count < ring.Length) count++;
        }

        /// <summary>
        /// Interpolated pose at <paramref name="renderTick"/>; before first sample returns false,
        /// after the newest holds it (returns true, clamped newest, extrapolation off).
        /// </summary>
        public bool SampleAt(int renderTick, out Vector3 position, out float yawDeg)
        {
            position = Vector3.zero;
            yawDeg = 0f;
            if (count == 0) return false;
            if (renderTick <= OldestTick)
            {
                NetSample first = ring[Wrap(head - count)];
                position = first.position;
                yawDeg = first.yawDeg;
                return renderTick >= OldestTick; // exact oldest accepted, older = stale
            }
            if (renderTick >= NewestTick)
            {
                NetSample last = ring[Wrap(head - 1)];
                position = last.position;
                yawDeg = last.yawDeg;
                return count >= 2 || NewestTick == OldestTick; // single-sample: no motion to hide
            }

            int oldestIndex = Wrap(head - count);
            for (int i = 0; i < count - 1; i++)
            {
                NetSample a = ring[Wrap(oldestIndex + i)];
                NetSample b = ring[Wrap(oldestIndex + i + 1)];
                if (renderTick >= a.tick && renderTick <= b.tick)
                {
                    float alpha = InterpolationRules.Alpha(a.tick, b.tick, renderTick);
                    // shortest-path yaw lerp
                    float dYaw = Mathf.DeltaAngle(a.yawDeg, b.yawDeg);
                    position = Vector3.Lerp(a.position, b.position, alpha);
                    yawDeg = a.yawDeg + dYaw * alpha;
                    return true;
                }
            }
            NetSample latest = ring[Wrap(head - 1)];
            position = latest.position;
            yawDeg = latest.yawDeg;
            return true;
        }

        public void Clear() { head = count = 0; }

        int Wrap(int i)
        {
            i %= ring.Length;
            return i < 0 ? i + ring.Length : i;
        }
    }
}
