using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.Progression
{
    public class ProgressionManager
    {
        private PlayerProfile profile;
        private List<UnlockableItem> unlockables;
        private List<string> unlockedItemIds;

        public ProgressionManager(string playerId, string callsign)
        {
            profile = new PlayerProfile
            {
                playerId = playerId,
                callsign = callsign,
                level = 1,
                experience = 0,
                experienceToNextLevel = ProgressionCalculator.CalculateXPForLevel(1),
                tier = ProgressionTier.Bronze,
                missionsCompleted = 0,
                kills = 0,
                deaths = 0,
                accuracy = 0f,
                timePlayed = 0f
            };

            unlockables = new List<UnlockableItem>();
            unlockedItemIds = new List<string>();
            InitializeUnlockables();
        }

        private void InitializeUnlockables()
        {
            unlockables.Add(new UnlockableItem
            {
                itemId = "weapon_m4a1",
                displayName = "M4A1 Carbine",
                description = "Standard issue assault rifle",
                requiredLevel = 1,
                requiredExperience = 0,
                cost = 0,
                isUnlocked = true,
                type = UnlockableType.Weapon
            });

            unlockables.Add(new UnlockableItem
            {
                itemId = "attachment_holo",
                displayName = "Holographic Sight",
                description = "Red dot sight for medium range",
                requiredLevel = 3,
                requiredExperience = 3000,
                cost = 500,
                isUnlocked = false,
                type = UnlockableType.Attachment,
                parentItemId = "weapon_m4a1"
            });

            unlockables.Add(new UnlockableItem
            {
                itemId = "weapon_m110",
                displayName = "M110 Semi-Automatic Sniper System",
                description = "Long range precision rifle",
                requiredLevel = 10,
                requiredExperience = 10000,
                cost = 2000,
                isUnlocked = false,
                type = UnlockableType.Weapon
            });

            unlockables.Add(new UnlockableItem
            {
                itemId = "gear_plate_rifle",
                displayName = "Rifle Plate Carrier",
                description = "Level IV ballistic protection",
                requiredLevel = 5,
                requiredExperience = 6000,
                cost = 1000,
                isUnlocked = false,
                type = UnlockableType.Gear
            });

            unlockables.Add(new UnlockableItem
            {
                itemId = "skill_focus",
                displayName = "Focus",
                description = "Reduced weapon sway when aiming",
                requiredLevel = 15,
                requiredExperience = 15000,
                cost = 3000,
                isUnlocked = false,
                type = UnlockableType.Skill
            });

            unlockables.Add(new UnlockableItem
            {
                itemId = "operator_recon",
                displayName = "Recon Specialist",
                description = "Unlocks recon operator class",
                requiredLevel = 8,
                requiredExperience = 8000,
                cost = 1500,
                isUnlocked = false,
                type = UnlockableType.Operator
            });
        }

        public void AddExperience(long amount)
        {
            profile.experience += amount;
            while (profile.experience >= profile.experienceToNextLevel)
            {
                profile.experience -= profile.experienceToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            profile.level++;
            profile.experienceToNextLevel = ProgressionCalculator.CalculateXPForLevel(profile.level);
            profile.tier = ProgressionCalculator.CalculateTier(profile.level, profile.accuracy, profile.missionsCompleted);
        }

        public void CompleteMission(long xpEarned, int kills, int shotsFired)
        {
            profile.missionsCompleted++;
            profile.kills += kills;
            profile.accuracy = ProgressionCalculator.GetAccuracy(profile.kills, shotsFired);
            profile.tier = ProgressionCalculator.CalculateTier(profile.level, profile.accuracy, profile.missionsCompleted);
            AddExperience(xpEarned);
        }

        public void RecordDeath()
        {
            profile.deaths++;
            profile.accuracy = ProgressionCalculator.GetAccuracy(profile.kills, profile.kills + profile.deaths * 10);
        }

        public bool CanUnlock(string itemId)
        {
            var item = unlockables.FirstOrDefault(u => u.itemId == itemId);
            return item.itemId != null && !item.isUnlocked && profile.level >= item.requiredLevel && profile.experience >= item.requiredExperience;
        }

        public bool UnlockItem(string itemId)
        {
            if (!CanUnlock(itemId)) return false;

            var item = unlockables.First(u => u.itemId == itemId);
            item.isUnlocked = true;
            int index = unlockables.FindIndex(u => u.itemId == itemId);
            unlockables[index] = item;
            unlockedItemIds.Add(itemId);
            return true;
        }

        public bool EquipItem(string itemId)
        {
            var item = unlockables.FirstOrDefault(u => u.itemId == itemId);
            if (item.itemId == null || !item.isUnlocked) return false;

            var sameType = unlockables.Where(u => u.type == item.type).ToList();
            foreach (var u in sameType)
            {
                int index = unlockables.IndexOf(u);
                var modifiable = unlockables[index];
                modifiable.isEquipped = false;
                unlockables[index] = modifiable;
            }

            item.isEquipped = true;
            int idx = unlockables.FindIndex(u => u.itemId == itemId);
            unlockables[idx] = item;
            return true;
        }

        public List<UnlockableItem> GetAvailableUnlocks()
        {
            return unlockables.Where(u => !u.isUnlocked && profile.level >= u.requiredLevel).ToList();
        }

        public List<UnlockableItem> GetUnlockedItems()
        {
            return unlockables.Where(u => u.isUnlocked).ToList();
        }

        public List<UnlockableItem> GetEquippedItems()
        {
            return unlockables.Where(u => u.isEquipped).ToList();
        }

        public PlayerProfile GetProfile()
        {
            profile.tier = ProgressionCalculator.CalculateTier(profile.level, profile.accuracy, profile.missionsCompleted);
            return profile;
        }

        public UnlockableItem GetItem(string itemId)
        {
            return unlockables.FirstOrDefault(u => u.itemId == itemId);
        }
    }
}
