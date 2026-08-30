using System;
using System.Collections.Generic;

namespace VEVE.Operators
{
    /// <summary>
    /// Deterministic human-factors traits an operator can possess. Traits are flavor-driven
    /// performance biases, never stat floors: every authored multiplier stays inside the
    /// 0.88-1.15 human-envelope table in <see cref="TraitCatalog"/> so that training, gear,
    /// and physiology remain the dominant terms.
    /// </summary>
    public enum OperatorTraitId
    {
        /// <summary>Reduced weapon sway and faster recovery from muzzle rise.</summary>
        SteadyHands = 0,

        /// <summary>Superior acuity and target acquisition at extended range.</summary>
        EagleEyed = 1,

        /// <summary>Fast, economical magazine changes and manipulations.</summary>
        FastDraw = 2,

        /// <summary>Aerobic base above cohort average; sustained bounding under load.</summary>
        SteelLungs = 3,

        /// <summary>Resists stress accumulation and sheds it quickly once contact breaks.</summary>
        ColdNerve = 4,

        /// <summary>Heavy-frame shooter: steady on rapid fire but slower, louder manipulations.</summary>
        HeavyHitter = 5,

        /// <summary>Treats casualties with minimal iatrogenic harm; fast, careful hands.</summary>
        GentleHands = 6,

        /// <summary>Walks point without snapping twigs; moves quietly at pace.</summary>
        Scout = 7,

        /// <summary>Methodical around live ordnance; steady pulse near fused charges.</summary>
        DemolitionSafe = 8,

        /// <summary>Brief, clean transmissions; voice control keeps the net usable under fire.</summary>
        CommsDiscipline = 9,

        /// <summary>Nocturnal adaptation: better low-light acuity and steadiness after dusk.</summary>
        NightOwl = 10,

        /// <summary>Risk/reward: moves and manipulates faster while taking fire, but aim recovers sluggishly.</summary>
        AdrenalineJunkie = 11,

        /// <summary>High pain tolerance, flat affect; hard to rattle but misses peripheral cues.</summary>
        Flatline = 12,

        /// <summary>Clumsy: snags gear, loud footfalls, fumbles reloads.</summary>
        Clumsy = 13
    }

    /// <summary>
    /// Named performance channels a trait can bias. Directional semantics are uniform: a
    /// higher aggregated value is always doctrinally "better" for the first six channels;
    /// for <see cref="TraitChannel.NoiseLoudness"/> lower is better (the channel stores an
    /// emitted-noise multiplier consumed by SoundPropagation/MovementSimulation noise gates).
    /// </summary>
    public enum TraitChannel
    {
        /// <summary>Weapon and camera steadiness while aiming (LookController sway input).</summary>
        AimStability = 0,

        /// <summary>Locomotion speed bias (MovementSimulation/StaminaSystem input).</summary>
        MoveSpeed = 1,

        /// <summary>Reload and weapon-manipulation speed bias.</summary>
        ReloadSpeed = 2,

        /// <summary>Rate at which post-shot sway settles back to zero.</summary>
        SwayRecovery = 3,

        /// <summary>Rate at which accumulated stress decays toward resting baseline.</summary>
        StressDecay = 4,

        /// <summary>Speed of medical treatment and revive procedures (MedicalTreatment input).</summary>
        MedicalSpeed = 5,

        /// <summary>Emitted noise multiplier; higher is louder and therefore worse.</summary>
        NoiseLoudness = 6,

        /// <summary>Effective visual spotting range bias.</summary>
        SightRange = 7
    }

    /// <summary>
    /// One trait's bias for exactly one channel. Kept as a serializable pair so profiles can
    /// persist a flattened view without dictionary support in JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class TraitMultiplier
    {
        /// <summary>Channel this multiplier applies to.</summary>
        public TraitChannel channel;

        /// <summary>Bias value; authored inside the 0.90-1.12 human envelope.</summary>
        public float multiplier = 1f;

        /// <summary>
        /// Optional darkness weight in [0,1]: at full darkness the effective multiplier
        /// blends toward <see cref="NightMultiplier"/>; 0 means the trait is night-neutral.
        /// Used by NightOwl to model nocturnal pupillary and circadian adaptation.
        /// </summary>
        public float nightWeight;

        /// <summary>Multiplier applied at full darkness; ignored when <see cref="nightWeight"/> is 0.</summary>
        public float nightMultiplier = 1f;
    }

    /// <summary>
    /// Static definition of a trait: identity, flavor text, unlock threshold, and channel biases.
    /// Instances are created only by <see cref="TraitCatalog"/> and treated as immutable.
    /// </summary>
    [Serializable]
    public sealed class TraitDefinition
    {
        /// <summary>Trait identity key.</summary>
        public OperatorTraitId id;

        /// <summary>Short display name for the roster UI.</summary>
        public string displayName;

        /// <summary>One-line human-factors flavor shown on operator cards.</summary>
        public string description;

        /// <summary>Progression level at which the trait becomes selectable (1 = available at enlistment).</summary>
        public int unlockLevel = 1;

        /// <summary>Channel biases; empty means a purely narrative trait.</summary>
        public TraitMultiplier[] multipliers = Array.Empty<TraitMultiplier>();
    }

    /// <summary>
    /// Aggregated, clamped outcome of combining every trait contribution on every channel.
    /// Consumed by physiology, movement, camera, medical, and audio layers (see integration seams).
    /// </summary>
    [Serializable]
    public sealed class ChannelVector
    {
        /// <summary>Smallest aggregate any channel may report; guards against degenerate trait stacks.</summary>
        public const float MinAggregate = 0.5f;

        /// <summary>Largest aggregate any channel may report.</summary>
        public const float MaxAggregate = 2f;

        /// <summary>Number of blendable channels; mirrors <see cref="TraitChannel"/> count.</summary>
        public const int ChannelCount = 8;

        /// <summary>Aim steadiness bias, 1 when neutral.</summary>
        public float aimStability = 1f;

        /// <summary>Locomotion speed bias, 1 when neutral.</summary>
        public float moveSpeed = 1f;

        /// <summary>Reload speed bias, 1 when neutral.</summary>
        public float reloadSpeed = 1f;

        /// <summary>Sway decay-rate bias, 1 when neutral.</summary>
        public float swayRecovery = 1f;

        /// <summary>Stress decay-rate bias, 1 when neutral.</summary>
        public float stressDecay = 1f;

        /// <summary>Medical procedure speed bias, 1 when neutral.</summary>
        public float medicalSpeed = 1f;

        /// <summary>Emitted noise bias, 1 when neutral; lower is quieter.</summary>
        public float noiseLoudness = 1f;

        /// <summary>Spotting range bias, 1 when neutral.</summary>
        public float sightRange = 1f;

        /// <summary>
        /// Reads one channel by enum without exposing internals.
        /// </summary>
        /// <param name="channel">Channel to read.</param>
        /// <returns>Aggregated multiplier, already clamped to [<see cref="MinAggregate"/>, <see cref="MaxAggregate"/>].</returns>
        public float Get(TraitChannel channel)
        {
            switch (channel)
            {
                case TraitChannel.AimStability: return aimStability;
                case TraitChannel.MoveSpeed: return moveSpeed;
                case TraitChannel.ReloadSpeed: return reloadSpeed;
                case TraitChannel.SwayRecovery: return swayRecovery;
                case TraitChannel.StressDecay: return stressDecay;
                case TraitChannel.MedicalSpeed: return medicalSpeed;
                case TraitChannel.NoiseLoudness: return noiseLoudness;
                case TraitChannel.SightRange: return sightRange;
                default: return 1f;
            }
        }

        /// <summary>
        /// Writes one channel, clamping to the aggregate envelope. Used by aggregation and by
        /// legacy mentorship overlays; silently normalizes NaN/Infinity back to neutral.
        /// </summary>
        /// <param name="channel">Channel to write.</param>
        /// <param name="value">Raw multiplier; &lt;0 or non-finite resets to 1.</param>
        public void Set(TraitChannel channel, float value)
        {
            if (!IsFinitePositive(value))
            {
                value = 1f;
            }
            float clamped = value < MinAggregate ? MinAggregate : (value > MaxAggregate ? MaxAggregate : value);
            switch (channel)
            {
                case TraitChannel.AimStability: aimStability = clamped; break;
                case TraitChannel.MoveSpeed: moveSpeed = clamped; break;
                case TraitChannel.ReloadSpeed: reloadSpeed = clamped; break;
                case TraitChannel.SwayRecovery: swayRecovery = clamped; break;
                case TraitChannel.StressDecay: stressDecay = clamped; break;
                case TraitChannel.MedicalSpeed: medicalSpeed = clamped; break;
                case TraitChannel.NoiseLoudness: noiseLoudness = clamped; break;
                case TraitChannel.SightRange: sightRange = clamped; break;
            }
        }

        /// <summary>
        /// Fresh all-neutral vector (every channel exactly 1).
        /// </summary>
        /// <returns>New neutral vector.</returns>
        public static ChannelVector Neutral()
        {
            return new ChannelVector();
        }

        /// <summary>
        /// True when the value is finite and greater than zero; the minimum sanity gate before
        /// a multiplier may enter an aggregate. Rejects unbounded (infinite, NaN, non-positive)
        /// inputs at the API boundary rather than clamping them into existence.
        /// </summary>
        /// <param name="value">Candidate multiplier.</param>
        /// <returns>Whether the multiplier is admissible.</returns>
        public static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }

    /// <summary>
    /// Deterministic catalog of the fourteen campaign traits with their authored human-factors
    /// values. Values were chosen at the conservative end of published marksmanship, aerobic,
    /// and tactical-medicine effect sizes; every base multiplier sits inside
    /// [<see cref="TraitCatalog.MinAuthoredBase"/>, <see cref="TraitCatalog.MaxAuthoredBase"/>]
    /// and night-conditioned overrides are additionally bounded by the catalog validator.
    /// </summary>
    public static class TraitCatalog
    {
        /// <summary>Highest authored base multiplier allowed for any trait channel.</summary>
        public const float MaxAuthoredBase = 1.15f;

        /// <summary>Lowest authored base multiplier allowed for any trait channel.</summary>
        public const float MinAuthoredBase = 0.88f;

        /// <summary>Progression level cap assumed when validating unlock thresholds.</summary>
        public const int MaxTraitUnlockLevel = 25;

        private static readonly Dictionary<OperatorTraitId, TraitDefinition> Definitions = BuildDefinitions();

        /// <summary>
        /// Looks up the immutable definition of a trait.
        /// </summary>
        /// <param name="id">Trait to look up.</param>
        /// <returns>Non-null definition; unknown ids return a neutral placeholder rather than throwing.</returns>
        public static TraitDefinition Get(OperatorTraitId id)
        {
            if (Definitions.TryGetValue(id, out TraitDefinition definition))
            {
                return definition;
            }
            return new TraitDefinition
            {
                id = id,
                displayName = id.ToString(),
                description = "Undefined trait; neutral placeholder.",
                unlockLevel = 1,
                multipliers = Array.Empty<TraitMultiplier>()
            };
        }

        /// <summary>
        /// True when the id is present in the catalog.
        /// </summary>
        /// <param name="id">Trait to probe.</param>
        /// <returns>Catalog membership.</returns>
        public static bool IsDefined(OperatorTraitId id)
        {
            return Definitions.ContainsKey(id);
        }

        /// <summary>
        /// Enumerates every cataloged trait deterministically by enum order.
        /// </summary>
        /// <returns>Array of definitions sorted by trait id.</returns>
        public static TraitDefinition[] AllDefinitions()
        {
            var keys = new List<OperatorTraitId>(Definitions.Keys);
            keys.Sort();
            var output = new TraitDefinition[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                output[i] = Definitions[keys[i]];
            }
            return output;
        }

        /// <summary>
        /// Progression level at which the trait may be equipped.
        /// </summary>
        /// <param name="id">Trait to query.</param>
        /// <returns>Unlock threshold, always at least 1.</returns>
        public static int UnlockLevel(OperatorTraitId id)
        {
            return Get(id).unlockLevel;
        }

        /// <summary>
        /// Collects authoring problems: envelopes violated, unknown channels, duplicate entries,
        /// unbounded multipliers, and unlock levels outside [1, <see cref="MaxTraitUnlockLevel"/>].
        /// </summary>
        /// <returns>Human-readable problem list; empty when the catalog is clean.</returns>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            foreach (KeyValuePair<OperatorTraitId, TraitDefinition> pair in Definitions)
            {
                TraitDefinition definition = pair.Value;
                if (definition.id != pair.Key)
                {
                    problems.Add("Catalog key/value mismatch for " + pair.Key + ".");
                }
                if (string.IsNullOrWhiteSpace(definition.displayName))
                {
                    problems.Add(definition.id + " has no display name.");
                }
                if (definition.unlockLevel < 1 || definition.unlockLevel > MaxTraitUnlockLevel)
                {
                    problems.Add(definition.id + " unlock level " + definition.unlockLevel + " outside [1," + MaxTraitUnlockLevel + "].");
                }
                if (definition.multipliers == null)
                {
                    problems.Add(definition.id + " has a null multiplier array.");
                    continue;
                }
                foreach (TraitMultiplier multiplier in definition.multipliers)
                {
                    if (!Enum.IsDefined(typeof(TraitChannel), multiplier.channel))
                    {
                        problems.Add(definition.id + " references undefined channel " + multiplier.channel + ".");
                    }
                    if (multiplier.multiplier < MinAuthoredBase || multiplier.multiplier > MaxAuthoredBase)
                    {
                        problems.Add(definition.id + "/" + multiplier.channel + " base " + multiplier.multiplier + " outside authored envelope.");
                    }
                    if (!ChannelVector.IsFinitePositive(multiplier.nightMultiplier))
                    {
                        problems.Add(definition.id + "/" + multiplier.channel + " night multiplier is unbounded or non-positive.");
                    }
                    if (multiplier.nightWeight < 0f || multiplier.nightWeight > 1f)
                    {
                        problems.Add(definition.id + "/" + multiplier.channel + " night weight outside [0,1].");
                    }
                }
            }
            return problems;
        }

        private static TraitDefinition Make(OperatorTraitId id, string displayName, string description, int unlockLevel, params TraitMultiplier[] multipliers)
        {
            return new TraitDefinition
            {
                id = id,
                displayName = displayName,
                description = description,
                unlockLevel = unlockLevel,
                multipliers = multipliers
            };
        }

        private static Dictionary<OperatorTraitId, TraitDefinition> BuildDefinitions()
        {
            var map = new Dictionary<OperatorTraitId, TraitDefinition>();

            map[OperatorTraitId.SteadyHands] = Make(
                OperatorTraitId.SteadyHands, "Steady Hands",
                "Shoots smoother than they ought to; sixty-round cold-barrel groups.", 1,
                new TraitMultiplier { channel = TraitChannel.AimStability, multiplier = 1.12f },
                new TraitMultiplier { channel = TraitChannel.SwayRecovery, multiplier = 1.08f });

            map[OperatorTraitId.EagleEyed] = Make(
                OperatorTraitId.EagleEyed, "Eagle Eyed",
                "Reads muzzle flash through haze; the spotter everyone wants.", 1,
                new TraitMultiplier { channel = TraitChannel.SightRange, multiplier = 1.10f });

            map[OperatorTraitId.FastDraw] = Make(
                OperatorTraitId.FastDraw, "Fast Draw",
                "Dwarfy on the reload carrier; three-tenths faster on the reload course.", 1,
                new TraitMultiplier { channel = TraitChannel.ReloadSpeed, multiplier = 1.10f });

            map[OperatorTraitId.SteelLungs] = Make(
                OperatorTraitId.SteelLungs, "Steel Lungs",
                "Rucks the hill and still calls for a second wind.", 1,
                new TraitMultiplier { channel = TraitChannel.MoveSpeed, multiplier = 1.04f },
                new TraitMultiplier { channel = TraitChannel.StressDecay, multiplier = 1.05f });

            map[OperatorTraitId.ColdNerve] = Make(
                OperatorTraitId.ColdNerve, "Cold Nerve",
                "Pulse never over 110; takes fire like weather.", 6,
                new TraitMultiplier { channel = TraitChannel.StressDecay, multiplier = 1.12f },
                new TraitMultiplier { channel = TraitChannel.AimStability, multiplier = 1.03f });

            map[OperatorTraitId.HeavyHitter] = Make(
                OperatorTraitId.HeavyHitter, "Heavy Hitter",
                "Carries the belt-fed like a rifle case; steadier on the long burst, louder doing it.", 3,
                new TraitMultiplier { channel = TraitChannel.SwayRecovery, multiplier = 1.05f },
                new TraitMultiplier { channel = TraitChannel.ReloadSpeed, multiplier = 0.93f },
                new TraitMultiplier { channel = TraitChannel.NoiseLoudness, multiplier = 1.06f });

            map[OperatorTraitId.GentleHands] = Make(
                OperatorTraitId.GentleHands, "Gentle Hands",
                "Seats a tourniquet without cracking ribs; fast and clean.", 5,
                new TraitMultiplier { channel = TraitChannel.MedicalSpeed, multiplier = 1.12f },
                new TraitMultiplier { channel = TraitChannel.StressDecay, multiplier = 1.03f });

            map[OperatorTraitId.Scout] = Make(
                OperatorTraitId.Scout, "Scout",
                "Walks point without the gravel knowing.", 2,
                new TraitMultiplier { channel = TraitChannel.NoiseLoudness, multiplier = 0.9f },
                new TraitMultiplier { channel = TraitChannel.MoveSpeed, multiplier = 1.05f },
                new TraitMultiplier { channel = TraitChannel.SightRange, multiplier = 1.03f });

            map[OperatorTraitId.DemolitionSafe] = Make(
                OperatorTraitId.DemolitionSafe, "Demolition Safe",
                "Calm hands on fused charges; counts the seconds like groceries.", 5,
                new TraitMultiplier { channel = TraitChannel.StressDecay, multiplier = 1.08f },
                new TraitMultiplier { channel = TraitChannel.MedicalSpeed, multiplier = 1.04f });

            map[OperatorTraitId.CommsDiscipline] = Make(
                OperatorTraitId.CommsDiscipline, "Comms Discipline",
                "Cuts the net chatter; keeps reports short and the plan audible.", 3,
                new TraitMultiplier { channel = TraitChannel.StressDecay, multiplier = 1.04f },
                new TraitMultiplier { channel = TraitChannel.NoiseLoudness, multiplier = 0.95f });

            map[OperatorTraitId.NightOwl] = Make(
                OperatorTraitId.NightOwl, "Night Owl",
                "Owns the 0200-0400 watch; everything else about them is a compromise.", 4,
                new TraitMultiplier { channel = TraitChannel.SightRange, multiplier = 0.97f, nightWeight = 1f, nightMultiplier = 1.10f },
                new TraitMultiplier { channel = TraitChannel.AimStability, multiplier = 0.96f, nightWeight = 1f, nightMultiplier = 1.06f },
                new TraitMultiplier { channel = TraitChannel.StressDecay, multiplier = 1.01f });

            map[OperatorTraitId.AdrenalineJunkie] = Make(
                OperatorTraitId.AdrenalineJunkie, "Adrenaline Junkie",
                "Fastest hands in the squad while bullets snap overhead; steady once things go quiet.", 8,
                new TraitMultiplier { channel = TraitChannel.MoveSpeed, multiplier = 1.12f },
                new TraitMultiplier { channel = TraitChannel.ReloadSpeed, multiplier = 1.08f },
                new TraitMultiplier { channel = TraitChannel.SwayRecovery, multiplier = 0.88f },
                new TraitMultiplier { channel = TraitChannel.AimStability, multiplier = 0.9f });

            map[OperatorTraitId.Flatline] = Make(
                OperatorTraitId.Flatline, "Flatline",
                "Packs a leg wound in stride and misses the movement in the treeline.", 7,
                new TraitMultiplier { channel = TraitChannel.StressDecay, multiplier = 1.12f },
                new TraitMultiplier { channel = TraitChannel.SightRange, multiplier = 0.9f },
                new TraitMultiplier { channel = TraitChannel.NoiseLoudness, multiplier = 1.05f });

            map[OperatorTraitId.Clumsy] = Make(
                OperatorTraitId.Clumsy, "Clumsy",
                "Drops mags, kicks cans, snags drag on everything.", 1,
                new TraitMultiplier { channel = TraitChannel.NoiseLoudness, multiplier = 1.12f },
                new TraitMultiplier { channel = TraitChannel.ReloadSpeed, multiplier = 0.9f },
                new TraitMultiplier { channel = TraitChannel.SwayRecovery, multiplier = 0.9f },
                new TraitMultiplier { channel = TraitChannel.MoveSpeed, multiplier = 0.97f });

            return map;
        }
    }

    /// <summary>
    /// An operator's equipped trait selection and the pure math that folds it into one
    /// <see cref="ChannelVector"/>. Multiplicative per channel, clamped to
    /// [<see cref="ChannelVector.MinAggregate"/>, <see cref="ChannelVector.MaxAggregate"/>];
    /// unbounded (non-finite or non-positive) inputs are rejected, never clamped, so bad
    /// authoring fails loudly at the boundary.
    /// </summary>
    [Serializable]
    public sealed class TraitSet
    {
        /// <summary>Equipped traits, in stable selection order.</summary>
        public List<OperatorTraitId> traitIds = new List<OperatorTraitId>();

        /// <summary>
        /// Adds a trait if cataloged and not already present.
        /// </summary>
        /// <param name="id">Trait to equip.</param>
        /// <returns>True when newly added; false on duplicate or unknown trait.</returns>
        public bool Add(OperatorTraitId id)
        {
            if (!TraitCatalog.IsDefined(id) || traitIds.Contains(id))
            {
                return false;
            }
            traitIds.Add(id);
            return true;
        }

        /// <summary>
        /// Removes a trait from the set.
        /// </summary>
        /// <param name="id">Trait to unequip.</param>
        /// <returns>True when removed.</returns>
        public bool Remove(OperatorTraitId id)
        {
            return traitIds.Remove(id);
        }

        /// <summary>
        /// Whether this exact trait is equipped.
        /// </summary>
        /// <param name="id">Trait to probe.</param>
        /// <returns>Membership.</returns>
        public bool Contains(OperatorTraitId id)
        {
            return traitIds != null && traitIds.Contains(id);
        }

        /// <summary>
        /// Folds every equipped trait's multipliers into one clamped channel vector for the
        /// supplied darkness condition.
        /// </summary>
        /// <param name="darkness">0 = full daylight, 1 = full darkness; blends night-conditioned multipliers.</param>
        /// <returns>Newly aggregated vector; neutral when the set is empty.</returns>
        public ChannelVector Aggregate(float darkness = 0f)
        {
            return Aggregate(DefinitionsFor(traitIds), darkness);
        }

        /// <summary>
        /// Pure static fold of trait definitions into one clamped vector. Per-channel result is
        ///   product(conditions applied to each contribution), then clamp to [MinAggregate, MaxAggregate].
        /// A contribution may be rejected: NaN, Infinity, or values beyond the bounded-admission
        /// window (see <see cref="AggregateChannel"/>) throw instead of silently clamping.
        /// </summary>
        /// <param name="definitions">Traits to fold; null treated as empty.</param>
        /// <param name="darkness">0 = full daylight, 1 = full darkness.</param>
        /// <returns>Aggregated vector with every channel clamped.</returns>
        public static ChannelVector Aggregate(IEnumerable<TraitDefinition> definitions, float darkness = 0f)
        {
            float light = darkness < 0f ? 0f : (darkness > 1f ? 1f : darkness);
            var products = new float[ChannelVector.ChannelCount];
            for (int i = 0; i < products.Length; i++)
            {
                products[i] = 1f;
            }
            var seen = new bool[ChannelVector.ChannelCount];

            if (definitions != null)
            {
                foreach (TraitDefinition definition in definitions)
                {
                    if (definition == null || definition.multipliers == null)
                    {
                        continue;
                    }
                    foreach (TraitMultiplier multiplier in definition.multipliers)
                    {
                        if (multiplier == null)
                        {
                            continue;
                        }
                        int index = (int)multiplier.channel;
                        if (index < 0 || index >= products.Length)
                        {
                            continue;
                        }
                        float contribution = BlendForLight(multiplier, light);
                        if (!ChannelVector.IsFinitePositive(contribution) || contribution > TraitCatalog.MaxAuthoredBase * 4f)
                        {
                            throw new ArgumentException(
                                "Unbounded trait multiplier on channel " + multiplier.channel +
                                " of trait " + definition.id + ".", nameof(definitions));
                        }
                        products[index] *= contribution;
                        seen[index] = true;
                    }
                }
            }

            var vector = new ChannelVector();
            for (int i = 0; i < products.Length; i++)
            {
                vector.Set((TraitChannel)i, AggregateChannel(products[i], seen[i]));
            }
            return vector;
        }

        /// <summary>
        /// Clamps one pre-computed channel product into the aggregate envelope. A channel with
        /// no contributions is neutral 1, not zero.
        /// </summary>
        /// <param name="product">Raw multiplicative product for one channel.</param>
        /// <param name="hadContributions">Whether any trait contributed to the channel.</param>
        /// <returns>Clamped aggregate.</returns>
        public static float AggregateChannel(float product, bool hadContributions)
        {
            if (!hadContributions || !ChannelVector.IsFinitePositive(product))
            {
                return 1f;
            }
            if (product < ChannelVector.MinAggregate) return ChannelVector.MinAggregate;
            if (product > ChannelVector.MaxAggregate) return ChannelVector.MaxAggregate;
            return product;
        }

        /// <summary>
        /// True when no trait in the sequence has a channel that would violate envelope rules.
        /// Cheap precondition used by roster validation.
        /// </summary>
        /// <param name="definitions">Traits to audit.</param>
        /// <returns>Whether all definitions are bounded and admissible.</returns>
        public static bool ValidateNoUnbounded(IEnumerable<TraitDefinition> definitions)
        {
            if (definitions == null) return true;
            foreach (TraitDefinition definition in definitions)
            {
                if (definition == null || definition.multipliers == null)
                {
                    continue;
                }
                foreach (TraitMultiplier multiplier in definition.multipliers)
                {
                    if (multiplier == null)
                    {
                        continue;
                    }
                    if (!ChannelVector.IsFinitePositive(multiplier.multiplier) || !ChannelVector.IsFinitePositive(multiplier.nightMultiplier))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Deep copy of the trait selection.
        /// </summary>
        /// <returns>Independent copy; mutating it never touches the source.</returns>
        public TraitSet Clone()
        {
            var clone = new TraitSet();
            if (traitIds != null)
            {
                clone.traitIds.AddRange(traitIds);
            }
            return clone;
        }

        private static IEnumerable<TraitDefinition> DefinitionsFor(List<OperatorTraitId> ids)
        {
            var output = new List<TraitDefinition>();
            if (ids == null)
            {
                return output;
            }
            foreach (OperatorTraitId id in ids)
            {
                if (TraitCatalog.IsDefined(id))
                {
                    output.Add(TraitCatalog.Get(id));
                }
            }
            return output;
        }

        private static float BlendForLight(TraitMultiplier multiplier, float darkness)
        {
            float weight = multiplier.nightWeight < 0f ? 0f : (multiplier.nightWeight > 1f ? 1f : multiplier.nightWeight);
            float night = multiplier.nightMultiplier <= 0f ? 1f : multiplier.nightMultiplier;
            // Lerp: base + (night - base) * (weight * darkness)
            return multiplier.multiplier + (night - multiplier.multiplier) * (weight * darkness);
        }
    }
}
