using System;
using UnityEngine;
using VEVE.Comms;
using VEVE.Content;
using VEVE.Operators;

namespace VEVE.Net
{
    /// <summary>
    /// Pure (NGO-free) translations between gameplay facts and wire commands,
    /// plus journal append + radio presentation mapping. Lives apart from the
    /// NetworkBehaviour so it stays unit-testable without a live NetworkManager.
    /// </summary>
    public static class MissionNetMap
    {
        public const ushort HostSender = 1;

        public static int IndexOfTemplate(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            MissionTemplate[] all = MissionContentCatalog.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].id, id, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>A seq-zero command; authoritative sequence is assigned by the journal.</summary>
        public static NetCommand Command(NetCommandType type, int i0 = 0, int i1 = 0,
            float f0 = 0f, float f1 = 0f, Vector3 world = default, ushort sender = HostSender)
        {
            return new NetCommand
            {
                senderId = sender,
                frame = 0,
                seq = 0,
                type = type,
                i0 = i0,
                i1 = i1,
                f0 = f0,
                f1 = f1,
                world = Pack(world)
            };
        }

        public static NetCommand.VectorPack Pack(Vector3 v)
        {
            NetCommand.VectorPack p;
            p.x = v.x; p.y = v.y; p.z = v.z;
            return p;
        }

        public static Vector3 Unpack(NetCommand.VectorPack p) => new Vector3(p.x, p.y, p.z);

        public static bool IsRelayOnly(NetCommand c) => c.type == NetCommandType.RadioBark;

        /// <summary>Host bookkeeping: assigns the authoritative sequence from the journal.</summary>
        public static uint AppendToJournal(MissionCommandJournal journal, NetCommand c)
        {
            if (journal == null) return c.seq;
            return journal.Append(c.senderId, c.frame, c.type, c.i0, c.i1, c.f0, c.f1, c.world);
        }

        /// <summary>Radio chatter crosses the wire as small ints; text is rebuilt client-side from the kit.</summary>
        public static NetCommand BarkCommand(OperatorSpecialty specialty, VoiceEvent voiceEvent,
            VoiceStressTier tier, int reporterId, Vector3 where, double gameClock)
        {
            NetCommand c = Command(NetCommandType.RadioBark, (int)specialty, (int)voiceEvent,
                (float)tier, (float)reporterId, where);
            c.frame = SafeClockToFrame(gameClock);
            return c;
        }

        public static int SafeClockToFrame(double clock)
        {
            if (double.IsNaN(clock) || clock < 0d) return 0;
            return clock > int.MaxValue ? int.MaxValue : (int)clock;
        }

        public static RadioBarkEvent ToBark(NetCommand c)
        {
            OperatorSpecialty spec = EnumClamp((int)c.i0, OperatorSpecialty.Pointman);
            VoiceEvent voice = EnumClampVoice((int)c.i1, VoiceEvent.ContactFront);
            VoiceStressTier tier = (c.f0 >= 2f) ? VoiceStressTier.Panic
                : ((c.f0 >= 1f) ? VoiceStressTier.Urgency : VoiceStressTier.Calm);

            VoiceDelivery delivery = VoiceKitLibrary.GetDelivery(tier) ?? new VoiceDelivery();
            return new RadioBarkEvent
            {
                speakerId = (int)c.f1 + "@" + c.frame,
                specialty = spec,
                voiceEvent = voice,
                tier = tier,
                text = VoiceKitLibrary.GetBark(spec, voice, tier) ?? string.Empty,
                pitchMultiplier = delivery.pitchMultiplier,
                speechRateMultiplier = delivery.speechRateMultiplier,
                worldPosition = Unpack(c.world),
                gameClock = c.frame
            };
        }

        private static OperatorSpecialty EnumClamp(int value, OperatorSpecialty fallback)
        {
            Array values = Enum.GetValues(typeof(OperatorSpecialty));
            foreach (object v in values)
            {
                if ((int)v == value) return (OperatorSpecialty)v;
            }
            return fallback;
        }

        private static VoiceEvent EnumClampVoice(int value, VoiceEvent fallback)
        {
            Array values = Enum.GetValues(typeof(VoiceEvent));
            foreach (object v in values)
            {
                if ((int)v == value) return (VoiceEvent)v;
            }
            return fallback;
        }
    }
}
