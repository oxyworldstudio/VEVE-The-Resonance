using System;
using System.Globalization;
using UnityEngine;

namespace VEVE.Tactics
{
    /// <summary>
    /// Discrete morale-affecting events observed at squad level. Grounded in the B4 encounter
    /// director doctrine (human factors first): each event has exactly one documented delta.
    /// Continuous effects (suppression drag, fatigue recovery, rally toward the calm baseline)
    /// are time-based and applied by <see cref="SquadMorale.Tick"/>.
    /// </summary>
    public enum MoraleEvent
    {
        /// <summary>A comrade was killed in immediate sight: −12 baseline, +4 extra per further kill inside the 60 s burst window.</summary>
        ComradeKia = 0,

        /// <summary>The squad (or an adjacent element) spotted the squad being flanked: −15 once per engagement phase.</summary>
        FlankSpotted = 1,

        /// <summary>Reinforcements joined the element: +8.</summary>
        Reinforced = 2,

        /// <summary>Good initiative: a suppressive base-of-fire phase succeeded (see <c>EngagementReporter.ComputeStressDelta</c> routing): +6.</summary>
        GoodInitiative = 3,

        /// <summary>A downed comrade was dragged back alive by a medic: +5.</summary>
        MedicRevive = 4,

        /// <summary>Off-contact consolidation/regroup: clears the Routed latch and adds +10. The only way back above Shaken after a rout.</summary>
        Regroup = 5
    }

    /// <summary>
    /// Five-band morale state, ordered best to worst. Transitions are monotonic: the state moves
    /// at most one band per <see cref="SquadMorale.Tick"/> toward the band implied by the numeric
    /// morale value, except for two documented exceptions: a KIA burst (two or more kills inside
    /// the 60 s window) and an instantaneous rout trigger.
    /// </summary>
    public enum MoraleState
    {
        /// <summary>morale ≥ 85 — willing to maneuver and assault.</summary>
        Confident = 0,

        /// <summary>65 ≤ morale &lt; 85 — holds, advances cautiously.</summary>
        Steady = 1,

        /// <summary>45 ≤ morale &lt; 65 — reactive, short bounds only.</summary>
        Shaken = 2,

        /// <summary>18 ≤ morale &lt; 45 — pinned: returns fire from cover, will not move.</summary>
        Pinning = 3,

        /// <summary>morale &lt; 18 (or rout trigger) — breaking; flees, may leave wounded behind.</summary>
        Routed = 4
    }

    /// <summary>
    /// Movement doctrine derived from a raw morale value. Produced by
    /// <see cref="SquadMorale.ComputeMovement"/>; consumed by the squad mover / cognition layer.
    /// </summary>
    public enum MovementOrder
    {
        /// <summary>morale &lt; 25 — immobile, dug in, shock-absorbing posture.</summary>
        PinnedImmobile = 0,

        /// <summary>25 ≤ morale &lt; 70 — hold and fire; no deliberate advance.</summary>
        HoldAndFire = 1,

        /// <summary>morale ≥ 70 — advances / maneuvers aggressively.</summary>
        Advance = 2
    }

    /// <summary>
    /// Result of <see cref="SquadMorale.ComputeMovement"/>. <see cref="fireWhileMoving"/> gates
    /// moving-fire animation/accuracy penalties independently of the order per doctrine (≥ 55).
    /// </summary>
    [Serializable]
    public struct MovementDirective
    {
        /// <summary>Halt/hold/advance order for the squad mover.</summary>
        public MovementOrder order;

        /// <summary>True when doctrine allows firing during movement (morale ≥ 55).</summary>
        public bool fireWhileMoving;

        /// <summary>Crey-up / immediate-react fire even while pinned (morale is never below fire-from-cover).</summary>
        public bool canReturnFire;

        /// <summary>
        /// Creates a directive.
        /// </summary>
        /// <param name="order">Movement order band.</param>
        /// <param name="fireWhileMoving">Whether fire during movement is authorized.</param>
        /// <param name="canReturnFire">Whether the element returns fire at all.</param>
        public MovementDirective(MovementOrder order, bool fireWhileMoving, bool canReturnFire)
        {
            this.order = order;
            this.fireWhileMoving = fireWhileMoving;
            this.canReturnFire = canReturnFire;
        }

        /// <summary>Human-readable form for logs and debug HUDs.</summary>
        /// <returns>Order + flags summary.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} (fireWhileMoving={1}, returnFire={2})", order, fireWhileMoving, canReturnFire);
        }
    }

    /// <summary>
    /// Per-squad morale state machine for the B4 encounter director. Pure logic: no Unity
    /// components, no static mutable state; instances are fully determined by serialized fields
    /// plus <see cref="Tick"/> and <see cref="ProcessEvent"/> calls driven with an unscaled
    /// double game clock. Integration seams (who calls what) are documented on
    /// <c>TacticalEventHub</c>; nothing here auto-wires.
    ///
    /// Numeric model (all deltas clamp morale to 0..100):
    /// <list type="bullet">
    /// <item><description>Comrade KIA: −12 immediate; each additional KIA inside a rolling 60 s
    /// burst window adds a further −4 escalation (2nd kill = −16, 3rd = −20, …). Two or more
    /// kills inside the window form a burst and may skip bands downward in the same tick.</description></item>
    /// <item><description>Heavy incoming suppression: −0.8/s while the squad is pinned under it
    /// (applied by <see cref="Tick"/> with <c>underHeavySuppression = true</c>).</description></item>
    /// <item><description>Flanked detection: −15 one-shot per engagement phase
    /// (<see cref="ResetEngagementPhase"/> re-arms it, called by the integrator on a new contact).</description></item>
    /// <item><description>Reinforced: +8. Good initiative (successful suppressive base of fire): +6.
    /// Medic revived a comrade: +5. Regroup: +10 and clears the Routed latch.</description></item>
    /// <item><description>Rally: steady pull toward <see cref="calmBaseline"/> at 2.0/s × leader
    /// authority factor, but ONLY outside effective fire (being pinned stops consolidation).
    /// Authority factor = 0.55 + 0.45 × leaderRating with the raw 0..1 <see cref="leaderRating"/>
    /// clamped — i.e. authority ∈ [0.55, 1.00]. A leaderless squad (<see cref="leaderPresent"/> =
    /// false) rallies at 55 % speed, so sustained suppression out-paces recovery and decays the
    /// squad into Pinning.</description></item>
    /// <item><description>Suppression fatigue: after 5 s of no incoming fire, an additional
    /// +3/s recovery toward the baseline applies (nervous-system settling, not exfiltration).</description></item>
    /// </list>
    /// </summary>
    [Serializable]
    public sealed class SquadMorale
    {
        /// <summary>Rolling window (seconds) for KIA escalation bursts.</summary>
        public const double KiaBurstWindowSeconds = 60.0;

        /// <summary>Morale cost of the first comrade KIA in a burst window.</summary>
        public const float KiaImmediateDelta = -12f;

        /// <summary>Extra morale cost stacked per additional KIA inside the burst window.</summary>
        public const float KiaEscalationDelta = -4f;

        /// <summary>Continuous morale drain per second while under heavy incoming suppression.</summary>
        public const float HeavySuppressionPerSecond = -0.8f;

        /// <summary>One-shot −15 for flank detection.</summary>
        public const float FlankDelta = -15f;

        /// <summary>Reinforcement bonus.</summary>
        public const float ReinforcementDelta = 8f;

        /// <summary>Successful suppressive base-of-fire bonus.</summary>
        public const float GoodInitiativeDelta = 6f;

        /// <summary>Medic revive bonus.</summary>
        public const float MedicReviveDelta = 5f;

        /// <summary>Regroup bonus toward consolidation.</summary>
        public const float RegroupDelta = 10f;

        /// <summary>Base rally rate toward <see cref="calmBaseline"/>, scaled by leader authority.</summary>
        public const float BaseRallyPerSecond = 2.0f;

        /// <summary>Seconds of no incoming contact before fatigue recovery applies.</summary>
        public const float ContactBreakDelaySeconds = 5f;

        /// <summary>Extra recovery per second once contact has been broken long enough.</summary>
        public const float FatigueRecoveryPerSecond = 3.0f;

        /// <summary>Authority multiplier floor with no leader present (55 % rally speed).</summary>
        public const float LeaderAuthorityFloor = 0.55f;

        /// <summary>Authority multiplier ceiling for a perfect leader.</summary>
        public const float LeaderAuthorityCeiling = 1.0f;

        /// <summary>Morale value below which the squad routs (exclusive) when the kill threshold is met.</summary>
        public const float RoutMoraleThreshold = 18f;

        /// <summary>Casualty percentage (of original squad size) required to rout (inclusive).</summary>
        public const float RoutCasualtiesPctThreshold = 40f;

        /// <summary>Current morale, 0..100 after clamping.</summary>
        [SerializeField] private float morale = 75f;

        /// <summary>Steady-state rally target for this crew/leader combination, 0..100.</summary>
        [SerializeField] private float calmBaseline = 70f;

        /// <summary>Raw perceived leadership of the present leader, 0..1. See authority math in the class doc.</summary>
        [SerializeField] private float leaderRating = 0.5f;

        /// <summary>Whether any leader figure (squad lead or acting NCO) is present and coherent.</summary>
        [SerializeField] private bool leaderPresent = true;

        /// <summary>Original squad size used for casualty percentage (set by the integrator).</summary>
        [SerializeField] private int squadSize = 8;

        /// <summary>Killed or otherwise permanently lost members of this squad.</summary>
        [SerializeField] private int killedCount;

        /// <summary>Wounded members currently in the ranks (need care, still counted present).</summary>
        [SerializeField] private int woundedPresent;

        [NonSerialized] private MoraleState _state = MoraleState.Steady;
        [NonSerialized] private bool _stateInitialized;
        [NonSerialized] private double _lastKiaTime = double.NaN;
        [NonSerialized] private int _kiaChain;
        [NonSerialized] private bool _kiaBurstPending;
        [NonSerialized] private bool _flankPenaltyApplied;
        [NonSerialized] private bool _routedLatch;
        [NonSerialized] private float _contactTimer;

        /// <summary>
        /// Creates a squad morale with doctrine defaults.
        /// </summary>
        public SquadMorale()
        {
            EnsureStateInitialized();
        }

        /// <summary>
        /// Creates a squad morale with an explicit starting value and baseline.
        /// </summary>
        /// <param name="startingMorale">Initial morale (clamped 0..100).</param>
        /// <param name="calmBaseline">Rally target once contact breaks (clamped 0..100).</param>
        /// <exception cref="ArgumentOutOfRangeException">Never thrown; inputs clamp instead, per realism doctrine on human values.</exception>
        public SquadMorale(float startingMorale, float calmBaseline)
        {
            morale = Clamp01To100(startingMorale);
            this.calmBaseline = Clamp01To100(calmBaseline);
            EnsureStateInitialized();
        }

        /// <summary>Snapshot for tests and debug UIs: label, morale, state.</summary>
        /// <returns>Single-line snapshot string.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Morale {0:0.##} [{1}] K:{2} W:{3} (squad {4})", Morale, State, killedCount, woundedPresent, squadSize);
        }

        /// <summary>Current morale clamped to 0..100.</summary>
        public float Morale => morale;

        /// <summary>Latest committed state (see the monotonic-transition rules in the class doc).</summary>
        public MoraleState State
        {
            get
            {
                EnsureStateInitialized();
                return _state;
            }
        }

        /// <summary>Effective leader authority multiplier in [0.55, 1.0]; 0.55 when leaderless.</summary>
        public float LeaderAuthorityFactor
        {
            get
            {
                if (!leaderPresent) return LeaderAuthorityFloor;
                return LeaderAuthorityFloor + (LeaderAuthorityCeiling - LeaderAuthorityFloor) * Clamp01(leaderRating);
            }
        }

        /// <summary>True while the squad refuses to move (Pinning band or morale below the immobile line).</summary>
        public bool IsPinned => State == MoraleState.Pinning || morale < 25f;

        /// <summary>Seconds elapsed since incoming fire last touched this squad (for fatigue recovery visibility).</summary>
        public float SecondsSinceContact => _contactTimer;

        /// <summary>How many KIA are chained in the current burst window (0 when none).</summary>
        public int RecentKiaChain => _kiaChain;

        /// <summary>Rally target; settable for crew experience / mission type tuning, clamped 0..100.</summary>
        public float CalmBaseline
        {
            get { return calmBaseline; }
            set { calmBaseline = Clamp01To100(value); }
        }

        /// <summary>Perceived leadership of the present leader, clamped 0..1 (raw input into the authority factor).</summary>
        public float LeaderRating
        {
            get { return leaderRating; }
            set { leaderRating = Clamp01(value); }
        }

        /// <summary>Whether a coherent leader is with the squad; false forces the 55 % authority floor (leaderless decay).</summary>
        public bool LeaderPresent
        {
            get { return leaderPresent; }
            set { leaderPresent = value; }
        }

        /// <summary>Squad losses as a percentage of <see cref="SquadSize"/>, 0..100.</summary>
        public float CasualtiesPct
        {
            get
            {
                if (squadSize <= 0) return 0f;
                float pct = (killedCount + woundedPresent) * 100f / squadSize;
                return pct > 100f ? 100f : (pct < 0f ? 0f : pct);
            }
        }

        /// <summary>
        /// Wounded personnel left behind when the squad broke and ran (the report-mandated
        /// 'flees leaving wounded' hook): every rout adds the wounded that were present at the
        /// moment of collapse. Persistent across a mission; the integrator reports them at debrief.
        /// </summary>
        public int WoundedAbandonedCount { get; private set; }

        /// <summary>Wounded still embedded with the squad (read-only view for debrief and tests).</summary>
        public int WoundedPresent => woundedPresent;

        /// <summary>Killed members of this squad (read-only view).</summary>
        public int KilledCount => killedCount;

        /// <summary>
        /// Configures squad size and initial casualty bookkeeping. Negative values clamp to 0.
        /// </summary>
        /// <param name="size">Squad size; 0 keeps the previous value.</param>
        public void ConfigureSquad(int size)
        {
            if (size > 0) squadSize = size;
        }

        /// <summary>
        /// Sets the wounded-in-ranks count used by the rout abandonment hook and casualty math.
        /// Clamped to [0, squadSize − killedCount].
        /// </summary>
        /// <param name="woundedCount">Current wounded still embedded with the squad.</param>
        public void SetWoundedPresent(int woundedCount)
        {
            int max = squadSize - killedCount;
            if (max < 0) max = 0;
            woundedPresent = woundedCount < 0 ? 0 : (woundedCount > max ? max : woundedCount);
        }

        /// <summary>
        /// Sets the killed tally directly (save-restore / pre-brief staging). Clamped to [0, squadSize].
        /// </summary>
        /// <param name="count">Killed members.</param>
        public void SetKilledCount(int count)
        {
            if (count < 0) count = 0;
            if (count > squadSize) count = squadSize;
            killedCount = count;
        }

        /// <summary>
        /// Authoritative morale write for briefing staging or save restore: sets the value (clamped
        /// 0..100) and jumps the state band directly (no monotonic stepping applies on restore).
        /// </summary>
        /// <param name="value">Morale value to restore.</param>
        public void RestoreMorale(float value)
        {
            morale = Clamp01To100(value);
            _state = BandFor(morale);
            _stateInitialized = true;
        }

        /// <summary>
        /// Advances the continuous morale model by one simulation tick.
        /// </summary>
        /// <param name="deltaTime">Frame time in seconds; ≤ 0 or NaN is treated as 0 (no reverse time).</param>
        /// <param name="underHeavySuppression">True while effective incoming fire pins the squad (-0.8/s).</param>
        /// <param name="now">Unscaled game clock in seconds driving the KIA burst window.</param>
        /// <returns>The state after this tick's monotonic step (one band max unless burst/rout).</returns>
        public MoraleState Tick(float deltaTime, bool underHeavySuppression, double now)
        {
            float dt = deltaTime;
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;

            if (underHeavySuppression)
            {
                _contactTimer = 0f;
                ApplyDelta(HeavySuppressionPerSecond * dt);
            }
            else
            {
                _contactTimer += dt;
            }

            float gap = calmBaseline - morale;
            if (gap != 0f && !underHeavySuppression)
            {
                // Standard pinned doctrine: rally toward the baseline only outside effective fire.
                float rate = BaseRallyPerSecond * LeaderAuthorityFactor;
                if (_contactTimer >= ContactBreakDelaySeconds) rate += FatigueRecoveryPerSecond;
                float step = rate * dt;
                if (Math.Abs(gap) <= step) morale = calmBaseline;
                else morale += gap > 0f ? step : -step;
            }

            if (IsRoutTrigger(morale, CasualtiesPct) && State != MoraleState.Routed)
            {
                TriggerRout();
            }

            return StepState();
        }

        /// <summary>
        /// Applies one discrete morale event and re-evaluates the state band.
        /// </summary>
        /// <param name="type">Event kind; see <see cref="MoraleEvent"/> for exact deltas.</param>
        /// <param name="now">Unscaled game clock in seconds (KIA burst window bookkeeping).</param>
        /// <returns>The morale delta defined for the event (the stored value clamps to 0..100 afterwards; 0 for the suppressed duplicate flank / unknown ops). NaN/negative <paramref name="now"/> is clamped to clock 0.</returns>
        public float ProcessEvent(MoraleEvent type, double now)
        {
            double clock = now;
            if (double.IsNaN(clock) || clock < 0.0) clock = 0.0;
            float delta;
            switch (type)
            {
                case MoraleEvent.ComradeKia:
                    bool inWindow = HasValidKiaHistory(clock) && _lastKiaTime >= clock - KiaBurstWindowSeconds;
                    _kiaChain = inWindow ? _kiaChain + 1 : 1;
                    _lastKiaTime = clock;
                    delta = KiaImmediateDelta + KiaEscalationDelta * (_kiaChain - 1);
                    killedCount++;
                    if (_kiaChain >= 2) _kiaBurstPending = true;
                    break;
                case MoraleEvent.FlankSpotted:
                    if (_flankPenaltyApplied) return 0f;
                    _flankPenaltyApplied = true;
                    delta = FlankDelta;
                    break;
                case MoraleEvent.Reinforced:
                    delta = ReinforcementDelta;
                    break;
                case MoraleEvent.GoodInitiative:
                    delta = GoodInitiativeDelta;
                    break;
                case MoraleEvent.MedicRevive:
                    delta = MedicReviveDelta;
                    if (woundedPresent > 0) woundedPresent--;
                    break;
                case MoraleEvent.Regroup:
                    delta = RegroupDelta;
                    _routedLatch = false;
                    _flankPenaltyApplied = false;
                    break;
                default:
                    return 0f;
            }
            ApplyDelta(delta);
            if (IsRoutTrigger(morale, CasualtiesPct) && State != MoraleState.Routed)
            {
                TriggerRout();
            }
            StepState();
            return delta;
        }

        /// <summary>
        /// Clears the once-per-engagement flank penalty; the integrator calls this when a brand-new
        /// contact begins after a lull.
        /// </summary>
        public void ResetEngagementPhase()
        {
            _flankPenaltyApplied = false;
            _kiaChain = 0;
            _lastKiaTime = double.NaN;
        }

        /// <summary>
        /// Adds reinforcements to the squad roster accounting (does not apply morale; fire
        /// <see cref="MoraleEvent.Reinforced"/> separately). Clamped below <see cref="squadSize"/> ceiling of 32.
        /// </summary>
        /// <param name="count">Number of operators joining; ≤ 0 ignored.</param>
        public void AddReinforcements(int count)
        {
            if (count <= 0) return;
            squadSize += count;
            if (squadSize > 32) squadSize = 32;
        }

        /// <summary>
        /// Pure movement doctrine: &lt; 25 pinned/immobile, ≥ 70 advances, fire-while-moving authorized ≥ 55.
        /// Out-of-range morale inputs clamp to 0..100 first; NaN behaves as 0.
        /// </summary>
        /// <param name="morale">Raw morale value.</param>
        /// <returns>Directive for the squad mover and weapons gate.</returns>
        public static MovementDirective ComputeMovement(float morale)
        {
            float m = ClampMorale(morale);
            MovementOrder order;
            if (m < 25f) order = MovementOrder.PinnedImmobile;
            else if (m >= 70f) order = MovementOrder.Advance;
            else order = MovementOrder.HoldAndFire;
            return new MovementDirective(order, m >= 55f, true);
        }

        /// <summary>
        /// Rout condition (encounter director doc): flees when morale &lt; 18 AND casualties ≥ 40 %.
        /// Strictly exclusive on morale, inclusive on the percentage, per tuning sheet.
        /// </summary>
        /// <param name="morale">Raw morale (clamped 0..100 internally).</param>
        /// <param name="casualtiesPct">Percent of original squad lost (killed + wounded); values below 0 behave as 0.</param>
        /// <returns>True when the squad breaks.</returns>
        public static bool IsRoutTrigger(float morale, float casualtiesPct)
        {
            float m = ClampMorale(morale);
            float pct = casualtiesPct < 0f || float.IsNaN(casualtiesPct) ? 0f : casualtiesPct;
            return m < RoutMoraleThreshold && pct >= RoutCasualtiesPctThreshold;
        }

        private static float ClampMorale(float m)
        {
            if (float.IsNaN(m)) return 0f;
            return Clamp01To100(m);
        }

        private static float Clamp01To100(float v)
        {
            if (float.IsNaN(v)) return 0f;
            if (v > 100f) return 100f;
            if (v < 0f) return 0f;
            return v;
        }

        private static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0f;
            if (v > 1f) return 1f;
            if (v < 0f) return 0f;
            return v;
        }

        private void ApplyDelta(float delta)
        {
            if (float.IsNaN(delta)) return;
            morale = Clamp01To100(morale + delta);
        }

        private bool HasValidKiaHistory(double clock)
        {
            // true when a prior chain entry exists and the clock has not gone backwards.
            return !double.IsNaN(_lastKiaTime) && _kiaChain > 0 && clock >= _lastKiaTime;
        }

        private void TriggerRout()
        {
            _routedLatch = true;
            if (woundedPresent > 0)
            {
                // flees leaving wounded: the broken squad cannot carry everyone.
                WoundedAbandonedCount += woundedPresent;
            }
            _state = MoraleState.Routed;
            _stateInitialized = true;
            _kiaBurstPending = false;
        }

        private void EnsureStateInitialized()
        {
            if (_stateInitialized) return;
            _state = BandFor(morale);
            _stateInitialized = true;
        }

        private MoraleState StepState()
        {
            EnsureStateInitialized();
            MoraleState target = BandFor(morale);

            if (_routedLatch && target < MoraleState.Shaken)
            {
                // Recovery ladder: Routed never self-heals above Shaken without Regroup.
                target = MoraleState.Shaken;
            }

            if (_kiaBurstPending)
            {
                _kiaBurstPending = false;
                if ((int)target > (int)_state) _state = target;
                return _state;
            }

            // Monotonic: one band per tick in either direction.
            int current = (int)_state;
            int goal = (int)target;
            if (goal > current) current++;
            else if (goal < current) current--;
            _state = (MoraleState)current;
            if (_state == MoraleState.Routed) _routedLatch = true;
            return _state;
        }

        private static MoraleState BandFor(float m)
        {
            if (m < RoutMoraleThreshold) return MoraleState.Routed;
            if (m < 45f) return MoraleState.Pinning;
            if (m < 65f) return MoraleState.Shaken;
            if (m < 85f) return MoraleState.Steady;
            return MoraleState.Confident;
        }
    }
}
