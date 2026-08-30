using System;
using System.Collections.Generic;

namespace VEVE.Operators
{
    /// <summary>
    /// Tactical job a human operator is trained for in the VEVE campaign. Distinct from the
    /// squad-level <c>AgentRole</c> used by the agentic simulation; operators map onto those
    /// roles through <see cref="SpecialtyRules.TacticalRoleToken(OperatorSpecialty)"/>.
    /// </summary>
    public enum OperatorSpecialty
    {
        /// <summary>Door entry, hinge work, close-quarters clearing.</summary>
        Breacher = 0,

        /// <summary>Long-range precision engagement and watch duties.</summary>
        Marksman = 1,

        /// <summary>Explosives placement, breaching charges, IED disposal.</summary>
        Demolitions = 2,

        /// <summary>Radio operator, net discipline, signal relay.</summary>
        Comms = 3,

        /// <summary>Forward observation, patrol base, early warning.</summary>
        Recon = 4,

        /// <summary>Sustained suppressive fire with a crew-served or belt-fed weapon.</summary>
        SupportGunner = 5,

        /// <summary>Combat lifesaver: bleeding control, revives, triage.</summary>
        Medic = 6,

        /// <summary>Lead walker, first through the door, near-security at the point.</summary>
        Pointman = 7
    }

    /// <summary>
    /// Pure static rule tables for <see cref="OperatorSpecialty"/>: preferred attachment families,
    /// default proficiency skill identifiers, revive speed multipliers, grenade usage bias, and
    /// spotting bonuses. No Unity scene or component state; safe for editor tooling, tests, and
    /// deterministic simulation. Multiplier semantics: values greater than 1 favor or accelerate
    /// the described behavior, values less than 1 discourage or slow it.
    /// </summary>
    public static class SpecialtyRules
    {
        /// <summary>Number of defined specialties; roster coverage checks divide by this.</summary>
        public const int SpecialtyCount = 8;

        /// <summary>Baseline multiplier representing "no specialty bias".</summary>
        public const float NeutralMultiplier = 1f;

        /// <summary>Ceiling applied to every specialty skill floor contributed by mentorship.</summary>
        public const float MaxSkillFloor = 0.55f;

        /// <summary>
        /// Returns the dotted, lowercase attachment family keys this specialty prefers when the
        /// customization bench filters its catalogue. Keys follow the "attachment.&lt;family&gt;"
        /// convention used by the Customization catalog.
        /// </summary>
        /// <param name="specialty">Specialty to look up.</param>
        /// <returns>Non-null, non-empty array of family keys; never mutated by callers (clone returned).</returns>
        public static string[] PreferredAttachmentFamilies(OperatorSpecialty specialty)
        {
            switch (specialty)
            {
                case OperatorSpecialty.Breacher:
                    return (string[])new string[] { "attachment.breaching_barrel", "attachment.cqb_sight", "attachment.flashlight_high" }.Clone();
                case OperatorSpecialty.Marksman:
                    return (string[])new string[] { "attachment.precision_scope_high", "attachment.bipod", "attachment.suppressor_medium" }.Clone();
                case OperatorSpecialty.Demolitions:
                    return (string[])new string[] { "attachment.underbarrel_launcher", "attachment.grenade_sight", "attachment.heavy_barrel" }.Clone();
                case OperatorSpecialty.Comms:
                    return (string[])new string[] { "attachment.compact_sight", "attachment.suppressor_light", "attachment.laser_lam" }.Clone();
                case OperatorSpecialty.Recon:
                    return (string[])new string[] { "attachment.long_scope_variable", "attachment.suppressor_medium", "attachment.tracer_off" }.Clone();
                case OperatorSpecialty.SupportGunner:
                    return (string[])new string[] { "attachment.belt_feed", "attachment.bipod_heavy" }.Clone();
                case OperatorSpecialty.Medic:
                    return (string[])new string[] { "attachment.red_dot_close", "attachment.compact_stock" }.Clone();
                case OperatorSpecialty.Pointman:
                    return (string[])new string[] { "attachment.shotgun_cylinder", "attachment.buckshot_choke", "attachment.cqb_sight" }.Clone();
                default:
                    return new string[] { "attachment.milspec_default" };
            }
        }

        /// <summary>
        /// Default proficiency skill identifier that gains passive XP first for this specialty.
        /// Keys match the Progression unlockable identifier convention (e.g. "skill.marksmanship").
        /// </summary>
        /// <param name="specialty">Specialty to look up.</param>
        /// <returns>Dotted lowercase skill key.</returns>
        public static string DefaultProficiencySkill(OperatorSpecialty specialty)
        {
            switch (specialty)
            {
                case OperatorSpecialty.Breacher: return "skill.breaching";
                case OperatorSpecialty.Marksman: return "skill.marksmanship";
                case OperatorSpecialty.Demolitions: return "skill.demolitions";
                case OperatorSpecialty.Comms: return "skill.signals";
                case OperatorSpecialty.Recon: return "skill.fieldcraft";
                case OperatorSpecialty.SupportGunner: return "skill.machinegun";
                case OperatorSpecialty.Medic: return "skill.medic";
                case OperatorSpecialty.Pointman: return "skill.near_security";
                default: return "skill.general";
            }
        }

        /// <summary>
        /// Multiplier applied to combat-lifesaver revive duration when this specialty performs it.
        /// Higher is faster; Medic is fastest by doctrine, SupportGunner slowest (bulky position).
        /// </summary>
        /// <param name="specialty">Specialty of the rescuing operator.</param>
        /// <returns>Revive speed multiplier in [0.85, 1.35].</returns>
        public static float ReviveSpeedMultiplier(OperatorSpecialty specialty)
        {
            switch (specialty)
            {
                case OperatorSpecialty.Medic: return 1.35f;
                case OperatorSpecialty.Pointman: return 1.15f;
                case OperatorSpecialty.Breacher: return 1.05f;
                case OperatorSpecialty.Comms: return 0.95f;
                case OperatorSpecialty.Recon: return 0.95f;
                case OperatorSpecialty.Demolitions: return 0.92f;
                case OperatorSpecialty.Marksman: return 0.88f;
                case OperatorSpecialty.SupportGunner: return 0.85f;
                default: return NeutralMultiplier;
            }
        }

        /// <summary>
        /// Relative propensity for this specialty to request or employ fragmentation and
        /// breaching grenades. 1 is doctrinal baseline; Demolitions and Breacher over-index.
        /// </summary>
        /// <param name="specialty">Specialty to look up.</param>
        /// <returns>Grenade usage bias in [0.7, 1.5].</returns>
        public static float GrenadeUsageBias(OperatorSpecialty specialty)
        {
            switch (specialty)
            {
                case OperatorSpecialty.Demolitions: return 1.5f;
                case OperatorSpecialty.Breacher: return 1.3f;
                case OperatorSpecialty.SupportGunner: return 1.15f;
                case OperatorSpecialty.Pointman: return 1.1f;
                case OperatorSpecialty.Medic: return 0.85f;
                case OperatorSpecialty.Comms: return 0.85f;
                case OperatorSpecialty.Marksman: return 0.7f;
                case OperatorSpecialty.Recon: return 0.7f;
                default: return NeutralMultiplier;
            }
        }

        /// <summary>
        /// Flat spotting-range bonus in seconds of earlier enemy detection contributed by the
        /// specialty's observation doctrine. Applied by the perception layer; Recon highest.
        /// </summary>
        /// <param name="specialty">Specialty to look up.</param>
        /// <returns>Spotting bonus in [0.0, 0.35]; 0 means no contribution.</returns>
        public static float SpottingBonus(OperatorSpecialty specialty)
        {
            switch (specialty)
            {
                case OperatorSpecialty.Recon: return 0.35f;
                case OperatorSpecialty.Marksman: return 0.25f;
                case OperatorSpecialty.Pointman: return 0.2f;
                case OperatorSpecialty.Comms: return 0.12f;
                case OperatorSpecialty.Breacher: return 0.05f;
                case OperatorSpecialty.Demolitions: return 0.05f;
                case OperatorSpecialty.Medic: return 0.06f;
                case OperatorSpecialty.SupportGunner: return 0.08f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Maps an operator specialty onto the lowercase-agentic role token consumed by
        /// RoleManager/AgentRole (Leader, Assault, Support, Marksman, Medic, Recon, Heavy).
        /// </summary>
        /// <param name="specialty">Specialty to translate.</param>
        /// <returns>AgentRole name token for squad simulation alignment.</returns>
        public static string TacticalRoleToken(OperatorSpecialty specialty)
        {
            switch (specialty)
            {
                case OperatorSpecialty.Breacher: return "Assault";
                case OperatorSpecialty.Pointman: return "Assault";
                case OperatorSpecialty.Marksman: return "Marksman";
                case OperatorSpecialty.Demolitions: return "Assault";
                case OperatorSpecialty.Comms: return "Support";
                case OperatorSpecialty.Recon: return "Recon";
                case OperatorSpecialty.SupportGunner: return "Heavy";
                case OperatorSpecialty.Medic: return "Medic";
                default: return "Assault";
            }
        }

        /// <summary>
        /// Mentorship skill floor a replacement operator inherits when they share the fallen
        /// operator's specialty. Monotonic non-decreasing in both arguments; capped so no legacy
        /// chain can skip training entirely.
        /// </summary>
        /// <param name="serviceDays">Fallen operator's days in service; negative treated as 0.</param>
        /// <param name="missionsCompleted">Fallen operator's completed missions; negative treated as 0.</param>
        /// <returns>Skill floor in [0, <see cref="MaxSkillFloor"/>].</returns>
        public static float MentorshipSkillFloor(int serviceDays, int missionsCompleted)
        {
            int days = serviceDays < 0 ? 0 : serviceDays;
            int missions = missionsCompleted < 0 ? 0 : missionsCompleted;
            float raw = 0.35f + days * 0.0004f + missions * 0.01f;
            if (raw > MaxSkillFloor) raw = MaxSkillFloor;
            return raw;
        }

        /// <summary>
        /// Validates that every specialty produces sane, bounded table values. Called by tests
        /// and by content-pipeline auditing; returns an empty list when the tables are clean.
        /// </summary>
        /// <returns>Human-readable problems; empty when all rules are within documented bounds.</returns>
        public static List<string> ValidateTables()
        {
            var problems = new List<string>();
            for (int i = 0; i < SpecialtyCount; i++)
            {
                OperatorSpecialty specialty = (OperatorSpecialty)i;

                if (PreferredAttachmentFamilies(specialty).Length == 0)
                {
                    problems.Add("Specialty " + specialty + " has no preferred attachment families.");
                }
                if (string.IsNullOrEmpty(DefaultProficiencySkill(specialty)))
                {
                    problems.Add("Specialty " + specialty + " has an empty proficiency skill key.");
                }
                if (ReviveSpeedMultiplier(specialty) < 0.5f || ReviveSpeedMultiplier(specialty) > 2f)
                {
                    problems.Add("Specialty " + specialty + " revive speed multiplier out of [0.5,2].");
                }
                if (GrenadeUsageBias(specialty) < 0.5f || GrenadeUsageBias(specialty) > 2f)
                {
                    problems.Add("Specialty " + specialty + " grenade bias out of [0.5,2].");
                }
                if (SpottingBonus(specialty) < 0f || SpottingBonus(specialty) > 0.5f)
                {
                    problems.Add("Specialty " + specialty + " spotting bonus out of [0,0.5].");
                }
            }

            return problems;
        }
    }
}
