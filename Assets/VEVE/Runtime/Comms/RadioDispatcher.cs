using System.Collections.Generic;
using UnityEngine;
using VEVE.Operators;
using VEVE.Tactics;

namespace VEVE.Comms
{
    /// <summary>
    /// Scene dispatcher holding net discipline state and emitting RadioBarkEvent for
    /// whatever audio layer binds voice clips. Public API is clock-injectable so squads
    /// (and tests) can drive deterministic chatter without Time hacks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RadioDispatcher : MonoBehaviour
    {
        public static RadioDispatcher Instance { get; private set; }

        private readonly Dictionary<string, double> lastBySpeaker = new Dictionary<string, double>();
        private double lastGlobalContact = double.NegativeInfinity;
        private bool subscribed;

        public RadioBarkEvent LastBark { get; private set; }
        public double NetClock { get; private set; } = double.NegativeInfinity;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureSubscribed(true);
        }

        private void OnEnable() { EnsureSubscribed(true); }
        private void OnDisable() { EnsureSubscribed(false); }
        private void OnDestroy() { EnsureSubscribed(false); }

        private void EnsureSubscribed(bool on)
        {
            if (on == subscribed) return;
            subscribed = on;
            if (on) VEVE.EventBus.SubscribeGlobal<VEVE.Content.MissionPhaseChangedEvent>(OnMissionPhase);
            else VEVE.EventBus.UnsubscribeGlobal<VEVE.Content.MissionPhaseChangedEvent>(OnMissionPhase);
        }

        private void OnMissionPhase(VEVE.Content.MissionPhaseChangedEvent e)
        {
            if (e == null) return;
            if (e.phase == VEVE.Content.MissionPhase.Debrief)
            {
                BroadcastMorale("NET", OperatorSpecialty.Pointman, MoraleEvent.Regroup,
                    MoraleState.Confident, Vector3.zero, Now());
            }
        }

        /// <summary>Radio contact report from a shooter who physically spotted the enemy.</summary>
        public bool BroadcastContact(string speakerId, OperatorSpecialty specialty, bool elevated,
            MoraleState morale, Vector3 worldPosition, double? clockOverride = null)
        {
            VoiceEvent e = elevated ? VoiceEvent.ContactElevated : VoiceEvent.ContactFront;
            return TryDispatch(speakerId, specialty, e, morale, worldPosition, Now(clockOverride));
        }

        /// <summary>Chatter triggered by the B4 morale machine (KIA, flank found, regroup...). </summary>
        public bool BroadcastMorale(string speakerId, OperatorSpecialty specialty, MoraleEvent moraleEvent,
            MoraleState morale, Vector3 worldPosition, double? clockOverride = null)
        {
            return TryDispatch(speakerId, specialty, RadioNet.MapMoraleEvent(moraleEvent), morale,
                worldPosition, Now(clockOverride));
        }

        private bool TryDispatch(string speakerId, OperatorSpecialty specialty, VoiceEvent e,
            MoraleState morale, Vector3 worldPosition, double now)
        {
            if (NetClock < now) NetClock = now;
            string key = (speakerId ?? "NET") + ":" + e;
            double last = lastBySpeaker.TryGetValue(key, out double t) ? t : double.NegativeInfinity;
            if (!RadioNet.Allow(now, last, lastGlobalContact, e)) return false;

            lastBySpeaker[key] = now;
            if (RadioNet.IsContact(e)) lastGlobalContact = now;

            RadioBarkEvent bark = RadioNet.Compose(speakerId, specialty, e, RadioNet.TierFor(morale), worldPosition, now);
            LastBark = bark;
            VEVE.EventBus.PublishGlobal(bark);
            Debug.Log($"[RADIO] {bark.speakerId} ({bark.tier}) {bark.voiceEvent}: {bark.text}");
            return true;
        }

        /// <summary>Wipe discipline memory (new mission / extraction handoff).</summary>
        public void ResetNet()
        {
            lastBySpeaker.Clear();
            lastGlobalContact = double.NegativeInfinity;
            NetClock = double.NegativeInfinity;
            LastBark = null;
        }

        private static double Now(double? clockOverride = null)
        {
            return clockOverride ?? UnityEngine.Time.unscaledTimeAsDouble;
        }
    }
}
