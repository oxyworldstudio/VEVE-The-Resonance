using System;
using System.Collections.Generic;
using System.Text;
using VEVE.Content;
using VEVE.Scoring;

namespace VEVE.Net
{
    /// <summary>All state-changing mission traffic. RadioBark is presentation-only (relay).</summary>
    public enum NetCommandType : byte
    {
        MissionStart = 0,
        ShotFired = 1,
        SquadTotalSet = 2,
        SquadMemberKia = 3,
        IntelObject = 4,
        ContactHeld = 5,
        Malfunction = 6,
        AlertSet = 7,
        MissionEnd = 8,
        RadioBark = 9
    }

    /// <summary>
    /// Fixed-layout command envelope - blittable by design so the real transport
    /// (Netcode for GameObjects / ENet / Steam) writes it straight into a FastBufferWriter
    /// without another serialization layer. seq is the single global ordering authority:
    /// the journal assigns it, clients acknowledge it, late-join replays by it.
    /// </summary>
    public partial struct NetCommand
    {
        public ushort senderId;
        public int frame;
        public uint seq;
        public NetCommandType type;
        public int i0;
        public int i1;
        public float f0;
        public float f1;
        public VectorPack world;

        /// <summary>Position triple kept as a plain struct: no UnityEngine types in the protocol.</summary>
        public struct VectorPack
        {
            public float x;
            public float y;
            public float z;
        }
    }

    /// <summary>Host-owned ordered history of every command (the one book of record).</summary>
    public sealed class MissionCommandJournal
    {
        private readonly List<NetCommand> _entries = new List<NetCommand>(512);

        public uint LastSequence { get; private set; }
        public IReadOnlyList<NetCommand> Entries => _entries;

        public uint Append(ushort senderId, int frame, NetCommandType type, int i0 = 0, int i1 = 0,
            float f0 = 0f, float f1 = 0f, NetCommand.VectorPack world = default)
        {
            uint seq = LastSequence + 1u;
            LastSequence = seq;
            _entries.Add(new NetCommand
            {
                senderId = senderId,
                frame = frame,
                seq = seq,
                type = type,
                i0 = i0,
                i1 = i1,
                f0 = f0,
                f1 = f1,
                world = world
            });
            return seq;
        }

        public int Count => _entries.Count;
    }

    /// <summary>
    /// Client-side deterministic reducer: feeds a journal into the SAME pure MissionSession
    /// math the host authoritatively runs - structural parity, not re-implementations -
    /// so both sides land on exactly identical scores. Incoming commands are gated through
    /// an in-order state machine with a bounded reorder buffer (jitter must never silently
    /// drop state deltas the way a naive "seq &lt;= applied skip" would).
    /// </summary>
    public sealed class NetMissionMirror
    {
        public const int ReorderBufferCapacity = 256;

        private MissionSession _session;
        private uint _appliedThrough;
        private MissionScoreBreakdown? _final;
        private readonly List<NetCommand> _outOfOrder = new List<NetCommand>(16);

        public uint AppliedThrough => _appliedThrough;
        public uint NextExpectedSequence => _appliedThrough + 1u;
        public bool Finished => _final.HasValue;
        public MissionScoreBreakdown? FinalBreakdown => _final;

        /// <summary>Stable fingerprint of the tally state for parity assertions.</summary>
        public string TallySignature
        {
            get
            {
                if (_session == null) return "none";
                var b = _session.LastBreakdown;
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0}|{1}|{2:F3}|{3}",
                    _session.Phase, _session.ShotsFired, _session.ParSeconds,
                    _final.HasValue ? b.total + "|" + b.experienceReward + "|" + b.rank : "open");
            }
        }

        /// <summary>Apply one command in sequence order; out-of-order arrivals are buffered.</summary>
        public void Apply(NetCommand c)
        {
            if (c.seq <= _appliedThrough) return; // acked duplicate
            if (c.seq != NextExpectedSequence)
            {
                if (_outOfOrder.Count >= ReorderBufferCapacity) return; // window full: request resend
                InsertSorted(c);
                return;
            }

            ApplyInPlace(c);
            _appliedThrough = c.seq;

            while (_outOfOrder.Count > 0 && _outOfOrder[0].seq == NextExpectedSequence)
            {
                NetCommand next = _outOfOrder[0];
                _outOfOrder.RemoveAt(0);
                ApplyInPlace(next);
                _appliedThrough = next.seq;
            }
        }

        private void InsertSorted(NetCommand c)
        {
            for (int i = 0; i < _outOfOrder.Count; i++)
            {
                if (_outOfOrder[i].seq == c.seq) return;
                if (c.seq < _outOfOrder[i].seq) { _outOfOrder.Insert(i, c); return; }
            }
            _outOfOrder.Add(c);
        }

        /// <summary>Pending buffered (future) sequences awaiting their turn.</summary>
        public int BufferedOutOfOrder => _outOfOrder.Count;

        private void ApplyInPlace(NetCommand c)
        {
            switch (c.type)
            {
                case NetCommandType.MissionStart:
                    MissionTemplate template = ResolveTemplate(c.i0);
                    _session = new MissionSession(template, (CampaignDifficulty)Mathf_Clamp((int)c.f0, 0, 2));
                    _session.Deploy();
                    break;
                case NetCommandType.ShotFired:
                    _session?.RecordShot(c.i0 == 1, c.i1 == 1);
                    break;
                case NetCommandType.SquadTotalSet:
                    _session?.SetSquadTotal(c.i0);
                    break;
                case NetCommandType.SquadMemberKia:
                    _session?.ReportSquadLoss();
                    break;
                case NetCommandType.IntelObject:
                    _session?.ReportIntelObject();
                    break;
                case NetCommandType.ContactHeld:
                    _session?.ReportContactHeld();
                    break;
                case NetCommandType.Malfunction:
                    _session?.ReportMalfunction();
                    break;
                case NetCommandType.AlertSet:
                    _session?.SetAlertAtInsert(c.i0);
                    break;
                case NetCommandType.MissionEnd:
                    if (_session != null && _session.Phase == MissionPhase.Deployed)
                        _final = _session.Complete(Math.Max(0f, c.f0), c.i0 == 1);
                    break;
                case NetCommandType.RadioBark:
                    // presentation only: never mutates authoritative state
                    break;
            }
        }

        public void ApplyThrough(IReadOnlyList<NetCommand> ordered, uint upToAndIncludingSeq)
        {
            if (ordered == null) return;
            for (int i = 0; i < ordered.Count; i++)
            {
                NetCommand c = ordered[i];
                if (c.seq <= _appliedThrough) continue;
                if (c.seq > upToAndIncludingSeq) break;
                Apply(c);
            }
        }

        private static MissionTemplate ResolveTemplate(int index)
        {
            MissionTemplate[] all = MissionContentCatalog.All;
            if (all != null && all.Length > 0)
            {
                int i = Mathf_Clamp(index, 0, all.Length - 1);
                return all[i];
            }
            return default;
        }

        private static int Mathf_Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }

    /// <summary>
    /// Deterministic in-process link (per-direction, fixed-frame-delay, sequence-preserving).
    /// Tests run the full host/client contract over it; the production task is only to swap
    /// Send/Receive to NGO RPCs carrying the same NetCommand layout.
    /// </summary>
    public sealed class LoopbackLink
    {
        private struct Pending { public NetCommand cmd; public int deliverFrame; }
        private readonly List<Pending> _inFlight = new List<Pending>(256);
        private readonly Queue<NetCommand> _arrived = new Queue<NetCommand>();

        public int DeliveredCount { get; private set; }
        public int DroppedCount { get; private set; }

        /// <summary>Send at fixed frame, arriving exactly +deliveryFrames (0 = same-frame).</summary>
        public void Send(NetCommand cmd, int frame, int deliveryFrames = 1)
        {
            _inFlight.Add(new Pending { cmd = cmd, deliverFrame = frame + Math.Max(0, deliveryFrames) });
        }

        /// <summary>Advance transport clock; matured commands are enqueued in (frame, seq) order.</summary>
        public void Tick(int frame)
        {
            _inFlight.Sort((a, b) =>
            {
                int c = a.deliverFrame.CompareTo(b.deliverFrame);
                return c != 0 ? c : a.cmd.seq.CompareTo(b.cmd.seq);
            });
            int i = 0;
            while (i < _inFlight.Count && _inFlight[i].deliverFrame <= frame)
            {
                _arrived.Enqueue(_inFlight[i].cmd);
                _inFlight.RemoveAt(i);
            }
        }

        public int Drain(NetCommand[] into, int max)
        {
            int n = 0;
            while (_arrived.Count > 0 && n < max && into != null && n < into.Length)
            {
                into[n++] = _arrived.Dequeue();
                DeliveredCount++;
            }
            return n;
        }
    }
}
