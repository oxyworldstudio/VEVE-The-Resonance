using System;
using System.Collections.Generic;
using UnityEngine;
using VEVE.Operators;
using VEVE.Tactics;

namespace VEVE.Comms
{
    /// <summary>
    /// One resolved radio line: bark text is already fallback-resolved, delivery is the
    /// stress-tier voice render, and the timestamp lets downstream audio spatialize it.
    /// </summary>
    public sealed class RadioBarkEvent : VEVE.IEvent
    {
        public string speakerId;
        public OperatorSpecialty specialty;
        public VoiceEvent voiceEvent;
        public VoiceStressTier tier;
        public string text;
        public float pitchMultiplier = 1f;
        public float speechRateMultiplier = 1f;
        public Vector3 worldPosition;
        public double gameClock;
    }

    /// <summary>
    /// Pure comms doctrine — exactly WHAT is said, in WHAT tone, and WHEN the net stays
    /// quiet (rate limiting mirrors real radio discipline: contacts get priority, one net
    /// speaks at a time, the same shooter does not key twice in two seconds).
    /// </summary>
    public static class RadioNet
    {
        public const double DuplicateSpeakerGapSeconds = 6.0;
        public const double ContactSpeakerGapSeconds = 2.5;
        public const double GlobalContactGapSeconds = 1.5;

        /// <summary>Stress tier is a deterministic projection of morale state.</summary>
        public static VoiceStressTier TierFor(MoraleState morale)
        {
            switch (morale)
            {
                case MoraleState.Confident:
                case MoraleState.Steady: return VoiceStressTier.Calm;
                case MoraleState.Shaken: return VoiceStressTier.Urgency;
                default: return VoiceStressTier.Panic;
            }
        }

        /// <summary>Morale event to radio event: doctrine mapping, every event speaks.</summary>
        public static VoiceEvent MapMoraleEvent(MoraleEvent morale)
        {
            switch (morale)
            {
                case MoraleEvent.ComradeKia: return VoiceEvent.ManDown;
                case MoraleEvent.FlankSpotted: return VoiceEvent.ContactElevated;
                case MoraleEvent.Reinforced: return VoiceEvent.MoveUp;
                case MoraleEvent.GoodInitiative: return VoiceEvent.Suppressing;
                case MoraleEvent.MedicRevive: return VoiceEvent.Regroup;
                case MoraleEvent.Regroup: return VoiceEvent.Regroup;
                default: return VoiceEvent.ContactFront;
            }
        }

        public static bool IsContact(VoiceEvent e)
        {
            return e == VoiceEvent.ContactFront || e == VoiceEvent.ContactElevated;
        }

        public static double SpeakerGapSeconds(VoiceEvent e)
        {
            return IsContact(e) ? ContactSpeakerGapSeconds : DuplicateSpeakerGapSeconds;
        }

        /// <summary>
        /// Radio discipline gate: per-speaker repeat suppression plus the one-contact-
        /// per-net rule (the first shooter to make contact owns the frequency).
        /// Unset timestamps as double.NegativeInfinity.
        /// </summary>
        public static bool Allow(double nowSeconds, double lastForSpeaker, double lastGlobalContact, VoiceEvent e)
        {
            if (double.IsNaN(nowSeconds)) return false;
            if (nowSeconds - lastForSpeaker < SpeakerGapSeconds(e)) return false;
            if (IsContact(e) && !double.IsNegativeInfinity(lastGlobalContact)
                && nowSeconds - lastGlobalContact < GlobalContactGapSeconds) return false;
            return true;
        }

        /// <summary>Compose a bark with kit text + delivery; null-safe, never empty text.</summary>
        public static RadioBarkEvent Compose(string speakerId, OperatorSpecialty specialty,
            VoiceEvent e, VoiceStressTier tier, Vector3 worldPosition, double nowSeconds)
        {
            VoiceDelivery delivery = VoiceKitLibrary.GetDelivery(tier) ?? new VoiceDelivery();
            return new RadioBarkEvent
            {
                speakerId = string.IsNullOrEmpty(speakerId) ? "NET" : speakerId,
                specialty = specialty,
                voiceEvent = e,
                tier = tier,
                text = VoiceKitLibrary.GetBark(specialty, e, tier) ?? string.Empty,
                pitchMultiplier = delivery.pitchMultiplier,
                speechRateMultiplier = delivery.speechRateMultiplier,
                worldPosition = worldPosition,
                gameClock = nowSeconds
            };
        }
    }
}
