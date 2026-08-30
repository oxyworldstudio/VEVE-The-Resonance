using System;
using System.Collections.Generic;
using System.Globalization;

namespace VEVE.Tactics
{
    /// <summary>
    /// Origin channel of a queued pulse; mirrors the two documented integration seams plus the
    /// director's own campaign channel. See <see cref="TacticalEventHub"/> class docs for the
    /// exact wiring.
    /// </summary>
    public enum PulseSource
    {
        /// <summary>AgentBridge BehaviorStep dispatch (callouts, revives observed on friendly agents).</summary>
        AgentBehaviorStep = 0,

        /// <summary>TacticalSound noise bus (gunfire heard near the squad → suppression; our noise → campaign heat).</summary>
        TacticalNoise = 1,

        /// <summary>EngagementReporter contact ratings (stress deltas, base-of-fire success).</summary>
        EngagementReport = 2,

        /// <summary>Campaign director closing a mission (escalation application).</summary>
        CampaignDirector = 3
    }

    /// <summary>
    /// One queued morale input: an event tag, its magnitude (already resolved by the hub's mapping
    /// tables — pulses carry intent, not raw noise values), and the unscaled game clock. Position is
    /// kept as three floats so the tactics layer stays free of Unity component types.
    /// </summary>
    [Serializable]
    public struct MoralePulse
    {
        /// <summary>Which morale event this pulse realizes.</summary>
        public MoraleEvent moraleEvent;

        /// <summary>Producer channel for diagnostics / double-application guards.</summary>
        public PulseSource source;

        /// <summary>Optional caller payload: KIA count, reinforcement count, stress delta, etc.</summary>
        public float magnitude;

        /// <summary>Unscaled game time stamp (seconds).</summary>
        public double gameTimeSeconds;

        /// <summary>World position of the triggering observation, for range gating by the listener.</summary>
        public float x;

        /// <summary>World Y.</summary>
        public float y;

        /// <summary>World Z.</summary>
        public float z;

        /// <summary>Log rendering.</summary>
        /// <returns>Readable pulse description.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} via {1} mag {2:0.##} t {3:0.##}", moraleEvent, source, magnitude, gameTimeSeconds);
        }
    }

    /// <summary>
    /// One queued campaign escalation input: the mission outcome awaiting application to the
    /// next-mission posture, with its profile fingerprint.
    /// </summary>
    [Serializable]
    public struct EscalationPulse
    {
        /// <summary>Biome/campaign profile key for <see cref="CampaignEscalationModel.ApplyToNextMission"/>.</summary>
        public string profileKey;

        /// <summary>Campaign seed mixed into the fingerprint.</summary>
        public int seed;

        /// <summary>Closed mission outcome.</summary>
        public MissionOutcomeInput outcome;

        /// <summary>Log rendering.</summary>
        /// <returns>Readable pulse description.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "escalate[{0}/{1}] {2}", profileKey, seed, outcome);
        }
    }

    /// <summary>
    /// Instance-based (static-free) event bridge between world noise sources and the tactics
    /// consumers. Producers (AgentBridge interpreter, TacticalSound listeners, EngagementReporter
    /// owner) <em>enqueue</em> pulses; the squad/campaign owners call <see cref="Flush"/> once per
    /// frame (or per test step) and the queued pulses are dispatched in strict FIFO to plain C#
    /// events. No static mutable state exists anywhere in this class family, so two tests each use
    /// their own <see cref="TacticalEventHub"/> instance with zero bleed-over.
    ///
    /// <para><b>Documented integration seams (B4 wires NOTHING itself — the orchestrator connects
    /// exactly these two points):</b></para>
    /// <list type="number">
    /// <item><description><b>AgentBridge BehaviorOp → morale.</b> In the BehaviorStep interpreter
    /// (runtime file <c>Assets/VEVE/Runtime/Agents/AgentBridge.cs</c>, switch over <c>BehaviorOp</c>),
    /// on each executed step call <see cref="EnqueueMoraleBehavior"/> with the step's
    /// <c>BehaviorOp</c> byte value and the plan's unscaled time. Mapping table
    /// (<c>VEVE.Agents.BehaviorOp</c> values, kept as raw bytes here to avoid a compile dependency
    /// on the concurrently-edited agents assembly): <c>5 Suppress</c> → suppression channel, see
    /// seam 2 (no discrete event); <c>6 Flank</c> executed against our element →
    /// <see cref="MoraleEvent.FlankSpotted"/>; <c>9 HealAlly</c> → none (no morale event);
    /// <c>10 ReviveCasualty</c> completed → <see cref="MoraleEvent.MedicRevive"/>;
    /// <c>11 Callout</c> when the called target is our own surviving element arriving →
    /// <see cref="MoraleEvent.Reinforced"/>; all other ops → no morale effect. Use
    /// <see cref="TryMapBehaviorOpToMoraleEvent"/> for the authoritative branch.</description></item>
    /// <item><description><b>TacticalSound.NoiseProduced → suppression / escalation heat.</b>
    /// <c>VEVE.TacticalSound</c> (declared in <c>Assets/VEVE/Runtime/Weapon.cs</c>) exposes
    /// <c>static event Action&lt;Vector3, float&gt; NoiseProduced</c> raised with (worldPosition,
    /// loudnessDbScale — muzzle report is 35). Handler: convert listener-relative intensity via
    /// <see cref="HeavySuppressionFromLoudness"/> (≥ 30 at the squad = incoming heavy fire → run
    /// <see cref="SquadMorale.Tick"/> with underHeavySuppression=true; our own shots near the squad
    /// do NOT suppress us — gate on teamId before wiring), and feed own-noise loudness into the
    /// campaign heat accumulator feeding <see cref="MissionOutcomeInput.alertLevelDuringInsert"/>
    /// at exfil.</description></item>
    /// </list>
    /// <para>The stress/intel channel (EngagementReporter records → <see cref="SquadMorale"/>
    /// events and campaign pulses) is internal to this assembly: close a contact, then enqueue
    /// <see cref="MoralePulse"/> with the computed stress as magnitude.</para>
    /// </summary>
    public sealed class TacticalEventHub
    {
        /// <summary>Loudness at or above which an incoming noise is treated as heavy suppression fire.</summary>
        public const float HeavySuppressionLoudness = 30f;

        /// <summary>Loudness of a standard unsuppressed rifle muzzle report, per Weapon.cs (documents the seam constant).</summary>
        public const float MuzzleReportLoudness = 35f;

        /// <summary>Hard cap on queued pulses per channel; overflow drops oldest with a counted discard.</summary>
        public const int MaxQueueLength = 256;

        [NonSerialized] private readonly Queue<MoralePulse> _moraleQueue = new Queue<MoralePulse>();
        [NonSerialized] private readonly Queue<EscalationPulse> _escalationQueue = new Queue<EscalationPulse>();

        /// <summary>Raised once per queued morale pulse during <see cref="Flush"/>, FIFO. Squad owners map this onto <see cref="SquadMorale.ProcessEvent"/>.</summary>
        public event Action<MoralePulse> MoralePulseProduced;

        /// <summary>Raised once per queued escalation pulse during <see cref="Flush"/>, FIFO. Campaign owners map this onto <see cref="CampaignEscalationModel.ApplyToNextMission"/>.</summary>
        public event Action<EscalationPulse> EscalationPulseProduced;

        /// <summary>Morale pulses waiting for the next flush.</summary>
        public int PendingMoraleCount => _moraleQueue.Count;

        /// <summary>Escalation pulses waiting for the next flush.</summary>
        public int PendingEscalationCount => _escalationQueue.Count;

        /// <summary>Total pulses delivered since construction (or last <see cref="Clear"/>), for determinism assertions.</summary>
        public int FlushedCount { get; private set; }

        /// <summary>Morale pulses dropped due to the queue cap; integrators may warn on non-zero at debrief.</summary>
        public int DroppedPulseCount { get; private set; }

        /// <summary>
        /// Queue a discrete morale event pulse. Bounded FIFO.
        /// </summary>
        /// <param name="pulse">Pulse to enqueue.</param>
        public void EnqueueMorale(MoralePulse pulse)
        {
            if (_moraleQueue.Count >= MaxQueueLength)
            {
                _moraleQueue.Dequeue();
                DroppedPulseCount++;
            }
            _moraleQueue.Enqueue(pulse);
        }

        /// <summary>
        /// Convenience: queue a morale pulse for a mapped behavior step (seam 1). No-op when the op
        /// maps to no morale event.
        /// </summary>
        /// <param name="behaviorOpValue">Numeric <c>VEVE.Agents.BehaviorOp</c> value of the executed step.</param>
        /// <param name="gameTimeSeconds">Unscaled game clock.</param>
        /// <param name="x">Triggering position X (optional).</param>
        /// <param name="y">Triggering position Y (optional).</param>
        /// <param name="z">Triggering position Z (optional).</param>
        /// <returns>True when a pulse was queued.</returns>
        public bool EnqueueMoraleBehavior(int behaviorOpValue, double gameTimeSeconds, float x = 0f, float y = 0f, float z = 0f)
        {
            MoraleEvent mapped;
            if (!TryMapBehaviorOpToMoraleEvent(behaviorOpValue, out mapped)) return false;
            EnqueueMorale(new MoralePulse
            {
                moraleEvent = mapped,
                source = PulseSource.AgentBehaviorStep,
                magnitude = 0f,
                gameTimeSeconds = gameTimeSeconds,
                x = x,
                y = y,
                z = z
            });
            return true;
        }

        /// <summary>
        /// Queue a campaign escalation pulse (mission closed, outcome recorded).
        /// </summary>
        /// <param name="pulse">Pulse to enqueue.</param>
        public void EnqueueEscalation(EscalationPulse pulse)
        {
            if (_escalationQueue.Count >= MaxQueueLength)
            {
                _escalationQueue.Dequeue();
                DroppedPulseCount++;
            }
            _escalationQueue.Enqueue(pulse);
        }

        /// <summary>
        /// Dispatches every queued pulse in FIFO order to the corresponding C# event, then leaves
        /// both queues empty. Calling Flush with no subscribers is still safe (pulses are consumed;
        /// call from the owner side to avoid silent loss). Re-entrant Enqueue during a handler is
        /// deferred to the next flush.
        /// </summary>
        /// <returns>Number of pulses delivered by this call.</returns>
        public int Flush()
        {
            int delivered = 0;
            var moraleSnapshot = _moraleQueue.ToArray();
            var escalationSnapshot = _escalationQueue.ToArray();
            _moraleQueue.Clear();
            _escalationQueue.Clear();
            for (int i = 0; i < moraleSnapshot.Length; i++)
            {
                if (MoralePulseProduced != null) MoralePulseProduced(moraleSnapshot[i]);
                delivered++;
            }
            for (int i = 0; i < escalationSnapshot.Length; i++)
            {
                if (EscalationPulseProduced != null) EscalationPulseProduced(escalationSnapshot[i]);
                delivered++;
            }
            FlushedCount += delivered;
            return delivered;
        }

        /// <summary>Drops all queued pulses without dispatching and resets counters (test isolation / mission reload).</summary>
        public void Clear()
        {
            _moraleQueue.Clear();
            _escalationQueue.Clear();
            FlushedCount = 0;
            DroppedPulseCount = 0;
        }

        /// <summary>
        /// Seam-1 mapping: numeric <c>VEVE.Agents.BehaviorOp</c> → morale event (authoritative
        /// table duplicated in the class docs). Kept byte-based on purpose so this assembly does
        /// not hard-reference the concurrently-edited agents code.
        /// </summary>
        /// <param name="behaviorOpValue">0..11 per BehaviorOp enum; anything else → no event.</param>
        /// <param name="moraleEvent">Mapped event when returning true.</param>
        /// <returns>True when the op carries a morale consequence.</returns>
        public static bool TryMapBehaviorOpToMoraleEvent(int behaviorOpValue, out MoraleEvent moraleEvent)
        {
            switch (behaviorOpValue)
            {
                case 6: // Flank — executed against us → flank spotted
                    moraleEvent = MoraleEvent.FlankSpotted;
                    return true;
                case 10: // ReviveCasualty completed
                    moraleEvent = MoraleEvent.MedicRevive;
                    return true;
                case 11: // Callout — arriving friendly element → reinforced (integrator must gate on teamId)
                    moraleEvent = MoraleEvent.Reinforced;
                    return true;
                case 5: // Suppress — continuous channel via noise seam, not a discrete event
                case 9: // HealAlly — no squad-level morale tag in v1
                default:
                    moraleEvent = default(MoraleEvent);
                    return false;
            }
        }

        /// <summary>
        /// Seam-2 mapping: listener-side loudness → heavy incoming suppression flag used for
        /// <see cref="SquadMorale.Tick"/>'s continuous −0.8/s channel. Values ≥
        /// <see cref="HeavySuppressionLoudness"/> pin-suppress; NaN/negative never suppress.
        /// The integrator is responsible for excluding the squad's <em>own</em> fire by team id
        /// before calling.
        /// </summary>
        /// <param name="loudnessAtListener">Perceived loudness from TacticalSound.NoiseProduced.</param>
        /// <returns>True while the squad takes effective incoming fire.</returns>
        public static bool HeavySuppressionFromLoudness(float loudnessAtListener)
        {
            if (float.IsNaN(loudnessAtListener) || loudnessAtListener < 0f) return false;
            return loudnessAtListener >= HeavySuppressionLoudness;
        }
    }
}
