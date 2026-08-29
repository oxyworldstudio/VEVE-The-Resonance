using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Progression
{
    public enum ProgressionTier { Bronze, Silver, Gold, Platinum, Diamond }

    [System.Serializable]
    public struct PlayerProfile
    {
        public string playerId;
        public string callsign;
        public int level;
        public long experience;
        public long experienceToNextLevel;
        public ProgressionTier tier;
        public int missionsCompleted;
        public int kills;
        public int deaths;
        public float accuracy;
        public float timePlayed;
    }

    [System.Serializable]
    public struct UnlockableItem
    {
        public string itemId;
        public string displayName;
        public string description;
        public int requiredLevel;
        public int requiredExperience;
        public int cost;
        public bool isUnlocked;
        public bool isEquipped;
        public UnlockableType type;
        public string parentItemId;
    }

    public enum UnlockableType { Weapon, Attachment, Gear, Skill, Cosmetic, Operator }

    public static class ProgressionCalculator
    {
        public static long CalculateXPForLevel(int level)
        {
            return 1000 + (long)(level * level * 100);
        }

        public static int CalculateLevelFromXP(long totalXP)
        {
            int level = 1;
            long xpRequired = 0;
            while (xpRequired <= totalXP)
            {
                level++;
                xpRequired += CalculateXPForLevel(level);
            }
            return level;
        }

        public static ProgressionTier CalculateTier(int level, float accuracy, int missionsCompleted)
        {
            if (level >= 50 && accuracy > 0.8f && missionsCompleted >= 100)
                return ProgressionTier.Diamond;
            if (level >= 40 && accuracy > 0.7f && missionsCompleted >= 75)
                return ProgressionTier.Platinum;
            if (level >= 30 && accuracy > 0.6f && missionsCompleted >= 50)
                return ProgressionTier.Gold;
            if (level >= 20 && accuracy > 0.5f && missionsCompleted >= 25)
                return ProgressionTier.Silver;
            return ProgressionTier.Bronze;
        }

        public static float GetAccuracy(int kills, int shotsFired)
        {
            return shotsFired > 0 ? (float)kills / shotsFired : 0f;
        }
    }
}
