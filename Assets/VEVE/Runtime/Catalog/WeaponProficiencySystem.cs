using System;
using System.Collections.Generic;
using System.Linq;
using VEVE.Customization;

namespace VEVE.Catalog
{
    /// <summary>
    /// Combat/practice events that feed proficiency experience.
    /// </summary>
    public enum PracticeEventType
    {
        ShotFired,
        TargetHit,
        Headshot,
        Elimination,
        Reload,
        MagazineEmpty,
        ZeroConfirmed,
        LongRangeHit,
        MissionCompleted
    }

    /// <summary>Coarse proficiency band derived from skill (0-100).</summary>
    public enum ProficiencyTier { Novice, Competent, Proficient, Expert, Master }

    /// <summary>Attachment unlock tiers gated behind per-weapon proficiency.</summary>
    public enum AttachmentMasteryTier { Standard, Improved, Advanced, Elite }

    /// <summary>
    /// Persisted record for one operator's proficiency on one weapon. Plain serializable class so it
    /// round-trips through the existing <c>VEVE.Mission.SaveSystem</c> / JsonUtility pipeline.
    /// </summary>
    [Serializable]
    public sealed class ProficiencyRecord
    {
        public string operatorId;
        public string weaponId;
        public long experience;

        /// <summary>Computed skill 0-100 from stored experience.</summary>
        public int Skill => WeaponProficiencyMath.SkillFromXp(experience);

        public ProficiencyRecord() { }
        public ProficiencyRecord(string operatorId, string weaponId, long experience = 0)
        {
            this.operatorId = operatorId;
            this.weaponId = weaponId;
            this.experience = experience;
        }
    }

    /// <summary>Container serialized by the save pipeline (a plain list, JsonUtility-friendly).</summary>
    [Serializable]
    public sealed class WeaponProficiencySaveData
    {
        public List<ProficiencyRecord> records = new List<ProficiencyRecord>();
    }

    /// <summary>
    /// Pure math for the proficiency curve. Isolated so it is trivially unit testable and reusable.
    /// </summary>
    public static class WeaponProficiencyMath
    {
        public const int MaxSkill = 100;

        /// <summary>Experience required to reach skill 100 on a weapon.</summary>
        public const long XpForMaxSkill = 200000;

        /// <summary>Quadratic XP curve: reaching skill s needs XpForMaxSkill*(s/100)^2 XP.</summary>
        public static int SkillFromXp(long xp)
        {
            if (xp <= 0) return 0;
            double ratio = Math.Sqrt(Math.Min(1.0, (double)xp / XpForMaxSkill));
            return (int)Math.Round(ratio * MaxSkill, MidpointRounding.AwayFromZero);
        }

        public static long XpForSkill(int skill)
        {
            double s = MathHelper.ClampInt(skill, 0, MaxSkill);
            return (long)Math.Round(Math.Pow(s / (double)MaxSkill, 2) * XpForMaxSkill);
        }

        /// <summary>
        /// Diminishing-returns mastery factor in [0,1]: gains are large early and flatten near the
        /// top (concave power curve). This is why high-skill operators show smaller per-point wins.
        /// </summary>
        public static double Mastery(double skill)
        {
            double s = MathHelper.Clamp01(skill / (double)MaxSkill);
            return Math.Pow(s, 0.7);
        }

        /// <summary>Recoil multiplier (lower is better). Novice 1.40x -> Master 0.72x.</summary>
        public static float RecoilMultiplier(double skill)
        {
            double m = Mastery(skill);
            return (float)(MathHelper.Lerp(1.40, 0.72, m));
        }

        /// <summary>Spread multiplier (lower is better). Novice 1.60x -> Master 0.68x.</summary>
        public static float SpreadMultiplier(double skill)
        {
            double m = Mastery(skill);
            return (float)(MathHelper.Lerp(1.60, 0.68, m));
        }

        public static ProficiencyTier TierFromSkill(int skill)
        {
            if (skill >= 90) return ProficiencyTier.Master;
            if (skill >= 75) return ProficiencyTier.Expert;
            if (skill >= 50) return ProficiencyTier.Proficient;
            if (skill >= 25) return ProficiencyTier.Competent;
            return ProficiencyTier.Novice;
        }

        public static int RequiredSkillForTier(ProficiencyTier tier)
        {
            switch (tier)
            {
                case ProficiencyTier.Competent: return 25;
                case ProficiencyTier.Proficient: return 50;
                case ProficiencyTier.Expert: return 75;
                case ProficiencyTier.Master: return 90;
                default: return 0;
            }
        }

        public static int RequiredSkillForAttachmentTier(AttachmentMasteryTier tier)
        {
            switch (tier)
            {
                case AttachmentMasteryTier.Improved: return 25;
                case AttachmentMasteryTier.Advanced: return 50;
                case AttachmentMasteryTier.Elite: return 80;
                default: return 0;
            }
        }

        /// <summary>Map existing <see cref="AttachmentDefinition.requiredLevel"/> (1-6) to a mastery tier.</summary>
        public static AttachmentMasteryTier AttachmentTierFromRequiredLevel(int requiredLevel)
        {
            if (requiredLevel <= 2) return AttachmentMasteryTier.Standard;
            if (requiredLevel <= 4) return AttachmentMasteryTier.Improved;
            if (requiredLevel == 5) return AttachmentMasteryTier.Advanced;
            return AttachmentMasteryTier.Elite;
        }

        /// <summary>Base experience awarded per single occurrence of a practice event.</summary>
        public static int XpForEvent(PracticeEventType type)
        {
            switch (type)
            {
                case PracticeEventType.ShotFired: return 2;
                case PracticeEventType.TargetHit: return 8;
                case PracticeEventType.Headshot: return 14;
                case PracticeEventType.Elimination: return 25;
                case PracticeEventType.Reload: return 5;
                case PracticeEventType.MagazineEmpty: return 6;
                case PracticeEventType.ZeroConfirmed: return 20;
                case PracticeEventType.LongRangeHit: return 40;
                case PracticeEventType.MissionCompleted: return 250;
                default: return 1;
            }
        }
    }

    internal static class MathHelper
    {
        public static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static double Lerp(double a, double b, double t) => a + (b - a) * Clamp01(t);
        public static int ClampInt(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }

    /// <summary>
    /// Tracks per-operator / per-weapon proficiency (skill 0-100), converts it into recoil and spread
    /// modifiers with diminishing returns, gates attachment tiers, and accrues XP from practice events.
    /// State is held as plain serializable records for the save pipeline.
    /// </summary>
    public sealed class WeaponProficiencySystem
    {
        private readonly Dictionary<string, ProficiencyRecord> store =
            new Dictionary<string, ProficiencyRecord>(StringComparer.Ordinal);

        /// <summary>Event raised whenever a proficiency record changes (operatorId, weaponId, newSkill).</summary>
        public event Action<string, string, int> OnProficiencyChanged;

        private static string Key(string operatorId, string weaponId) => operatorId + "|" + weaponId;

        public IReadOnlyCollection<ProficiencyRecord> AllRecords => store.Values;

        /// <summary>Return the stored record (creating a zero-XP one on demand).</summary>
        public ProficiencyRecord GetRecord(string operatorId, string weaponId)
        {
            string key = Key(operatorId, weaponId);
            if (!store.TryGetValue(key, out ProficiencyRecord record))
            {
                record = new ProficiencyRecord(operatorId, weaponId, 0);
                store[key] = record;
            }
            return record;
        }

        public int GetSkill(string operatorId, string weaponId) => GetRecord(operatorId, weaponId).Skill;

        public long GetExperience(string operatorId, string weaponId) => GetRecord(operatorId, weaponId).experience;

        public ProficiencyTier GetTier(string operatorId, string weaponId) =>
            WeaponProficiencyMath.TierFromSkill(GetSkill(operatorId, weaponId));

        /// <summary>Add raw practice experience, capped so the resulting skill never exceeds 100.</summary>
        public void AddPracticeXp(string operatorId, string weaponId, long xp)
        {
            if (xp <= 0) return;
            ProficiencyRecord record = GetRecord(operatorId, weaponId);
            long cap = WeaponProficiencyMath.XpForSkill(WeaponProficiencyMath.MaxSkill);
            record.experience = Math.Min(cap, record.experience + xp);
            OnProficiencyChanged?.Invoke(operatorId, weaponId, record.Skill);
        }

        /// <summary>Accrue experience from one or more practice events.</summary>
        public void RecordEvent(string operatorId, string weaponId, PracticeEventType type, int count = 1)
        {
            if (count <= 0) return;
            long xp = (long)WeaponProficiencyMath.XpForEvent(type) * count;
            AddPracticeXp(operatorId, weaponId, xp);
        }

        /// <summary>Recoil multiplier to apply to this operator's handling of this weapon (lower = better).</summary>
        public float GetRecoilModifier(string operatorId, string weaponId) =>
            WeaponProficiencyMath.RecoilMultiplier(GetSkill(operatorId, weaponId));

        /// <summary>Spread multiplier to apply to this operator's handling of this weapon (lower = better).</summary>
        public float GetSpreadModifier(string operatorId, string weaponId) =>
            WeaponProficiencyMath.SpreadMultiplier(GetSkill(operatorId, weaponId));

        public bool IsMasteryTierUnlocked(string operatorId, string weaponId, ProficiencyTier tier) =>
            GetSkill(operatorId, weaponId) >= WeaponProficiencyMath.RequiredSkillForTier(tier);

        public bool IsAttachmentTierUnlocked(string operatorId, string weaponId, AttachmentMasteryTier tier) =>
            GetSkill(operatorId, weaponId) >= WeaponProficiencyMath.RequiredSkillForAttachmentTier(tier);

        /// <summary>Gate an existing attachment definition for this operator / weapon combination.</summary>
        public bool IsAttachmentUnlocked(string operatorId, string weaponId, AttachmentDefinition definition)
        {
            AttachmentMasteryTier tier = WeaponProficiencyMath.AttachmentTierFromRequiredLevel(definition.requiredLevel);
            return IsAttachmentTierUnlocked(operatorId, weaponId, tier);
        }

        /// <summary>Snapshot the whole store as a serializable container for the save pipeline.</summary>
        public WeaponProficiencySaveData ExportSaveData() =>
            new WeaponProficiencySaveData { records = store.Values.ToList() };

        /// <summary>Replace the current store from a saved container (used on load).</summary>
        public void ImportSaveData(WeaponProficiencySaveData data)
        {
            store.Clear();
            if (data?.records == null) return;
            foreach (ProficiencyRecord record in data.records)
            {
                if (record == null || record.operatorId == null || record.weaponId == null) continue;
                store[Key(record.operatorId, record.weaponId)] =
                    new ProficiencyRecord(record.operatorId, record.weaponId, record.experience);
            }
        }
    }
}
