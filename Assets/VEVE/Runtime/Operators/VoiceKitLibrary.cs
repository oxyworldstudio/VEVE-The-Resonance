using System;
using System.Collections.Generic;

namespace VEVE.Operators
{
    /// <summary>
    /// Emotional load the radio voice is modeling, driven by the operator's stress channel at
    /// bark time. Radio discipline doctrine assumes calm transmission is trained behavior that
    /// degrades under pressure; the tiers exist so audio implementation can blend pitch/rate
    /// and pick harder-edged line selections instead of authoring duplicates.
    /// </summary>
    public enum VoiceStressTier
    {
        /// <summary>Resting or patrol state; protocol-perfect transmissions.</summary>
        Calm = 0,

        /// <summary>Contact made or imminent; clipped, loud, still on protocol.</summary>
        Urgency = 1,

        /// <summary>Massive casualties or overrun; protocol frays, pitch and tempo jump.</summary>
        Panic = 2
    }

    /// <summary>
    /// Radio-relevant events an operator may need to get out. Keys for bark lookup are formed
    /// from these in <see cref="VoiceKitLibrary"/>; barks exist per-specialty with fallbacks.
    /// </summary>
    public enum VoiceEvent
    {
        /// <summary>Enemy confirmed in front of the element.</summary>
        ContactFront = 0,

        /// <summary>Enemy confirmed on high ground or an elevated firing position.</summary>
        ContactElevated = 1,

        /// <summary>Friendly down in the open; calls for immediate lifesaving.</summary>
        ManDown = 2,

        /// <summary>Ready-to-execution call for a dynamic entry.</summary>
        Breach = 3,

        /// <summary>Bound-forward movement order relayed to the element.</summary>
        MoveUp = 4,

        /// <summary>Volume of fire put on a target to enable maneuver.</summary>
        Suppressing = 5,

        /// <summary>Element reassembling on the leader after a break.</summary>
        Regroup = 6,

        /// <summary>Area secure; all-clear report.</summary>
        AreaSecure = 7
    }

    /// <summary>
    /// Voice rendering parameters for one stress tier. Both multipliers are strictly monotonic
    /// non-decreasing across tiers, clamped to physically plausible radio values so a panicking
    /// operator cannot sound demonic: under stress humans tense laryngeal muscles (pitch up)
    /// and race the clock (rate up), typically within +/-20%.
    /// </summary>
    [Serializable]
    public sealed class VoiceDelivery
    {
        /// <summary>Pitch multiplier applied to the voice kit base pitch (1 = base).</summary>
        public float pitchMultiplier = 1f;

        /// <summary>Speech rate multiplier applied to the voice kit base cadence (1 = base).</summary>
        public float speechRateMultiplier = 1f;
    }

    /// <summary>
    /// Static library of radio voice line KEYS and short bark texts per specialty, plus stress
    /// tier delivery math and formal radio procedure templates. No audio assets, clips, or
    /// AudioSource references here - the audio layer binds real clips by these keys and falls
    /// back through the same chain this class exposes via <see cref="GetBarkKeyChain"/>.
    /// </summary>
    public static class VoiceKitLibrary
    {
        /// <summary>Namespace prefix for every bark key produced by this library.</summary>
        public const string BarkKeyPrefix = "bark.";

        /// <summary>Fallback token used for generic, specialty-neutral lines.</summary>
        public const string GenericToken = "generic";

        /// <summary>Fallback token for events without a tier variant.</summary>
        public const string CalmToken = "calm";

        private static readonly Dictionary<string, string> Barks = BuildBarks();

        /// <summary>
        /// Radio bark line for a specialty/event/tier triple. Resolution chain: exact
        /// specialty+event+tier &gt; specialty+event+calm &gt; generic+event+tier &gt;
        /// generic+event+calm &gt; generic silent-hold. Never null, never empty.
        /// </summary>
        /// <param name="specialty">Speaker's specialty (fallbacks still return valid text).</param>
        /// <param name="vocalEvent">Event being transmitted.</param>
        /// <param name="tier">Current stress tier of the speaker.</param>
        /// <returns>Non-empty bark text.</returns>
        public static string GetBark(OperatorSpecialty specialty, VoiceEvent vocalEvent, VoiceStressTier tier)
        {
            foreach (string key in GetBarkKeyChain(specialty, vocalEvent, tier))
            {
                if (Barks.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
            return "Hold transmission.";
        }

        /// <summary>
        /// The exact ordered fallback-chain of keys <see cref="GetBark"/> walks; exposed so the
        /// audio implementation can map each key to an asset bundle lookup and log which key
        /// actually resolved (bark provenance in analytics).
        /// </summary>
        /// <param name="specialty">Speaker's specialty.</param>
        /// <param name="vocalEvent">Event being transmitted.</param>
        /// <param name="tier">Current stress tier.</param>
        /// <returns>Enumeration of dotted bark keys, most specific first.</returns>
        public static List<string> GetBarkKeyChain(OperatorSpecialty specialty, VoiceEvent vocalEvent, VoiceStressTier tier)
        {
            string spec = specialty.ToString().ToLowerInvariant();
            string evt = EventToken(vocalEvent);
            string str = tier.ToString().ToLowerInvariant();
            return new List<string>
            {
                BarkKeyPrefix + spec + "." + evt + "." + str,
                BarkKeyPrefix + spec + "." + evt + "." + CalmToken,
                BarkKeyPrefix + GenericToken + "." + evt + "." + str,
                BarkKeyPrefix + GenericToken + "." + evt + "." + CalmToken
            };
        }

        /// <summary>
        /// True when the given specialty/event pair has at least one authored bark.
        /// </summary>
        /// <param name="specialty">Specialty to test.</param>
        /// <param name="vocalEvent">Event to test.</param>
        /// <returns>Whether a specialty-specific line exists (any tier).</returns>
        public static bool HasSpecialtyBark(OperatorSpecialty specialty, VoiceEvent vocalEvent)
        {
            string prefix = BarkKeyPrefix + specialty.ToString().ToLowerInvariant() + "." + EventToken(vocalEvent) + ".";
            foreach (KeyValuePair<string, string> entry in Barks)
            {
                if (entry.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Delivery parameters (pitch and rate) for a stress tier. Strictly monotonic:
        /// higher urgency never lowers pitch or speech rate.
        /// </summary>
        /// <param name="tier">Stress tier to fetch.</param>
        /// <returns>Newly built VoiceDelivery; unknown tiers clamp to Calm.</returns>
        public static VoiceDelivery GetDelivery(VoiceStressTier tier)
        {
            switch (tier)
            {
                case VoiceStressTier.Urgency:
                    return new VoiceDelivery { pitchMultiplier = 1.06f, speechRateMultiplier = 1.14f };
                case VoiceStressTier.Panic:
                    return new VoiceDelivery { pitchMultiplier = 1.16f, speechRateMultiplier = 1.3f };
                default:
                    return new VoiceDelivery { pitchMultiplier = 1f, speechRateMultiplier = 1f };
            }
        }

        /// <summary>
        /// Formal radio-procedure template with {placeholder} tokens, keyed after the
        /// "radio.&lt;procedure&gt;" convention. Nine-line MEDEVAC requested, 5-paragraph
        /// spot report, SITREP, and precedence marks. The dialog/campaign layer formats these
        /// against live state; unknown keys return the check-in template rather than null.
        /// </summary>
        /// <param name="procedureKey">Key without prefix ("nine_line", "spot_report", "sitrep", "check_in", "precedence".</param>
        /// <returns>Non-null templated text.</returns>
        public static string GetRadioTemplate(string procedureKey)
        {
            switch (procedureKey)
            {
                case "nine_line":
                    return "Line 1 {location_grid} // Line 2 {callsign_frequency} // Line 3 {precedence} {special_cargo} // "
                           + "Line 4 {patients_military} {patients_civilian} // Line 5 {security} // Line 6 {method_marking} // "
                           + "Line 7 {nationality} // Line 8 {cbrn} // Line 9 {vehicle_type}";
                case "spot_report":
                    return "SPOTREP: 1 Size {size}; 2 Activity {activity}; 3 Location {grid}; 4 Unit {unit}; 5 Time {time}; "
                           + "6 Civilian {civilians}; 7 Terrain {terrain}; 8 Enemy {enemy}; 9 Duration {duration}; 10 Actions {actions}";
                case "sitrep":
                    return "SITREP {callsign} {hour_group}: pos {grid}, eff {effective_strength}, su {suspected_up}, "
                           + "res {resupply_state}, intent {intent}";
                case "check_in":
                    return "{callsign}, this is {net_control}, contact on arrival, over.";
                case "precedence":
                    return "Precedence flash/urgent/routine: FLASH priority traffic breaks all nets; URGENT breaks routine.";
                default:
                    return GetRadioTemplate("check_in");
            }
        }

        /// <summary>
        /// Placeholder tokens a caller must fill for a template. Kept for formatter validation
        /// so a missing field fails loudly in the dialog layer instead of sending "{grid}" on air.
        /// </summary>
        /// <param name="template">A template string from <see cref="GetRadioTemplate"/>.</param>
        /// <returns>Distinct placeholder names, braces stripped; empty on null input.</returns>
        public static List<string> ExtractPlaceholders(string template)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(template))
            {
                return result;
            }
            int cursor = 0;
            while (cursor < template.Length)
            {
                int open = template.IndexOf('{', cursor);
                if (open < 0)
                {
                    break;
                }
                int close = template.IndexOf('}', open + 1);
                if (close < 0)
                {
                    break;
                }
                string token = template.Substring(open + 1, close - open - 1).Trim();
                if (token.Length > 0 && !result.Contains(token))
                {
                    result.Add(token);
                }
                cursor = close + 1;
            }
            return result;
        }

        /// <summary>
        /// Dotted lowercase token for an event, shared by key builders and the audio binder.
        /// </summary>
        /// <param name="vocalEvent">Event to tokenize.</param>
        /// <returns>snake_case event token.</returns>
        public static string EventToken(VoiceEvent vocalEvent)
        {
            switch (vocalEvent)
            {
                case VoiceEvent.ContactFront: return "contact_front";
                case VoiceEvent.ContactElevated: return "contact_elevated";
                case VoiceEvent.ManDown: return "man_down";
                case VoiceEvent.Breach: return "breach";
                case VoiceEvent.MoveUp: return "move_up";
                case VoiceEvent.Suppressing: return "suppressing";
                case VoiceEvent.Regroup: return "regroup";
                case VoiceEvent.AreaSecure: return "area_secure";
                default: return "generic";
            }
        }

        /// <summary>
        /// Audits every specialty/event/tier triple against the fallback chain and returns
        /// problems (should always be empty for authored data).
        /// </summary>
        /// <returns>Human-readable problems; empty when every combination resolves to text.</returns>
        public static List<string> ValidateKit()
        {
            var problems = new List<string>();
            for (int s = 0; s <= (int)OperatorSpecialty.Pointman; s++)
            {
                for (int e = 0; e <= (int)VoiceEvent.AreaSecure; e++)
                {
                    for (int t = 0; t <= (int)VoiceStressTier.Panic; t++)
                    {
                        string bark = GetBark((OperatorSpecialty)s, (VoiceEvent)e, (VoiceStressTier)t);
                        if (string.IsNullOrEmpty(bark))
                        {
                            problems.Add("Empty bark for " + (OperatorSpecialty)s + "/" + (VoiceEvent)e + "/" + (VoiceStressTier)t + ".");
                        }
                    }
                }
            }
            VoiceDelivery calm = GetDelivery(VoiceStressTier.Calm);
            VoiceDelivery urgency = GetDelivery(VoiceStressTier.Urgency);
            VoiceDelivery panic = GetDelivery(VoiceStressTier.Panic);
            if (!(panic.pitchMultiplier > urgency.pitchMultiplier && urgency.pitchMultiplier > calm.pitchMultiplier))
            {
                problems.Add("Pitch multipliers are not strictly monotonic across stress tiers.");
            }
            if (!(panic.speechRateMultiplier > urgency.speechRateMultiplier && urgency.speechRateMultiplier > calm.speechRateMultiplier))
            {
                problems.Add("Speech rate multipliers are not strictly monotonic across stress tiers.");
            }
            return problems;
        }

        private static string G(VoiceEvent vocalEvent, VoiceStressTier tier)
        {
            return BarkKeyPrefix + GenericToken + "." + EventToken(vocalEvent) + "." + tier.ToString().ToLowerInvariant();
        }

        private static Dictionary<string, string> BuildBarks()
        {
            var barks = new Dictionary<string, string>(StringComparer.Ordinal);

            barks[G(VoiceEvent.ContactFront, VoiceStressTier.Calm)] = "Contact front.";
            barks[G(VoiceEvent.ContactFront, VoiceStressTier.Urgency)] = "Contact front, effective fire!";
            barks[G(VoiceEvent.ContactFront, VoiceStressTier.Panic)] = "Contact front front front, we are base!";
            barks[G(VoiceEvent.ContactElevated, VoiceStressTier.Calm)] = "Contact elevated.";
            barks[G(VoiceEvent.ContactElevated, VoiceStressTier.Urgency)] = "Sniper high, find cover!";
            barks[G(VoiceEvent.ContactElevated, VoiceStressTier.Panic)] = "Shots from above, can't see him!";
            barks[G(VoiceEvent.ManDown, VoiceStressTier.Calm)] = "Man down.";
            barks[G(VoiceEvent.ManDown, VoiceStressTier.Urgency)] = "Man down, get him behind cover!";
            barks[G(VoiceEvent.ManDown, VoiceStressTier.Panic)] = "Somebody's hit, somebody's hit, get a medic!";
            barks[G(VoiceEvent.Breach, VoiceStressTier.Calm)] = "Breach, breach, breach.";
            barks[G(VoiceEvent.Breach, VoiceStressTier.Urgency)] = "Breaching, smoke out!";
            barks[G(VoiceEvent.Breach, VoiceStressTier.Panic)] = "Going in early, going in!";
            barks[G(VoiceEvent.MoveUp, VoiceStressTier.Calm)] = "Moving.";
            barks[G(VoiceEvent.MoveUp, VoiceStressTier.Urgency)] = "Bound up, cover me!";
            barks[G(VoiceEvent.MoveUp, VoiceStressTier.Panic)] = "Fall back forward, fall back forward!";
            barks[G(VoiceEvent.Suppressing, VoiceStressTier.Calm)] = "Suppressing.";
            barks[G(VoiceEvent.Suppressing, VoiceStressTier.Urgency)] = "On him, traversing!";
            barks[G(VoiceEvent.Suppressing, VoiceStressTier.Panic)] = "Dropping rounds, hold them back!";
            barks[G(VoiceEvent.Regroup, VoiceStressTier.Calm)] = "Regroup on me.";
            barks[G(VoiceEvent.Regroup, VoiceStressTier.Urgency)] = "Rally up, count off!";
            barks[G(VoiceEvent.Regroup, VoiceStressTier.Panic)] = "Where is everybody, rally rally rally!";
            barks[G(VoiceEvent.AreaSecure, VoiceStressTier.Calm)] = "Area secure.";
            barks[G(VoiceEvent.AreaSecure, VoiceStressTier.Urgency)] = "Building clear, mostly.";
            barks[G(VoiceEvent.AreaSecure, VoiceStressTier.Panic)] = "Clear! I think it's clear!";

            barks[BarkKeyPrefix + "breacher.breach.calm"] = "On the hinge, stand back, BREACH.";
            barks[BarkKeyPrefix + "breacher.breach.urgency"] = "Charge up, down on the door!";
            barks[BarkKeyPrefix + "breacher.contact_front.urgency"] = "They're stacked in the doorway, walking him back!";
            barks[BarkKeyPrefix + "marksman.contact_elevated.calm"] = "He's high, I've got the watchtower.";
            barks[BarkKeyPrefix + "marksman.contact_elevated.urgency"] = "Rifle firing from the roofline, I have firing solutions.";
            barks[BarkKeyPrefix + "marksman.area_secure.calm"] = "Sector reads clean from my side.";
            barks[BarkKeyPrefix + "demolitions.breach.calm"] = "Charges set, all out, three two one.";
            barks[BarkKeyPrefix + "demolitions.breach.urgency"] = "Firing the charge, cover your ears!";
            barks[BarkKeyPrefix + "demolitions.contact_elevated.urgency"] = "Wire and claymores on that ridge, watch your step.";
            barks[BarkKeyPrefix + "comms.man_down.calm"] = "Casualty to the net, sending TOC.";
            barks[BarkKeyPrefix + "comms.area_secure.calm"] = "Station to station, we are solid, out.";
            barks[BarkKeyPrefix + "comms.contact_front.urgency"] = "Breaking contact and reporting, hold traffic!";
            barks[BarkKeyPrefix + "recon.contact_elevated.calm"] = "Movement observed, elevated structure, over.";
            barks[BarkKeyPrefix + "recon.contact_front.urgency"] = "Spotted us! Breaking contact to the rally point!";
            barks[BarkKeyPrefix + "recon.area_secure.calm"] = "Site cold, no sign, continuing watch.";
            barks[BarkKeyPrefix + "supportgunner.suppressing.calm"] = "Feeding him a belt, move when it shifts.";
            barks[BarkKeyPrefix + "supportgunner.suppressing.urgency"] = "Keeping his head down, GO GO!";
            barks[BarkKeyPrefix + "supportgunner.man_down.urgency"] = "I can't leave him, walking him back firing!";
            barks[BarkKeyPrefix + "medic.man_down.calm"] = "Triage, bring him to me.";
            barks[BarkKeyPrefix + "medic.man_down.urgency"] = "Pressure on the wound! Tourniquet's next!";
            barks[BarkKeyPrefix + "medic.area_secure.calm"] = "Casualty collection point set.";
            barks[BarkKeyPrefix + "pointman.breach.calm"] = "Stack's up, first man through.";
            barks[BarkKeyPrefix + "pointman.contact_front.urgency"] = "Coming through the door, I'm coming through!";
            barks[BarkKeyPrefix + "pointman.move_up.calm"] = "Up on you, up on you.";

            return barks;
        }
    }
}
