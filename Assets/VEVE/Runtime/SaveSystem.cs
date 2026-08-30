using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VEVE.Mission
{
    /// <summary>
    /// Versioned save system handling mission state, operator profiles, progression, and settings.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        /// <summary>
        /// Current save data version. Increment when the save schema changes.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Name of the save folder relative to the persistent data path.
        /// </summary>
        public const string SaveFolderName = "VEVE_Saves";

        /// <summary>
        /// File extension for save files.
        /// </summary>
        public const string SaveFileExtension = ".sav";

        /// <summary>
        /// Event raised when a save operation completes successfully.
        /// </summary>
        public event Action<string> OnSaveCompleted;

        /// <summary>
        /// Event raised when a load operation completes successfully.
        /// </summary>
        public event Action<string> OnLoadCompleted;

        /// <summary>
        /// Event raised when a save or load operation fails.
        /// </summary>
        public event Action<string> OnOperationFailed;

        private string saveDirectory;

        private void Awake()
        {
            saveDirectory = Path.Combine(Application.persistentDataPath, SaveFolderName);
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }
        }

        /// <summary>
        /// Saves the complete game state to disk.
        /// </summary>
        /// <param name="slotName">Name of the save slot.</param>
        /// <param name="missionState">Current mission state.</param>
        /// <param name="operatorProfile">Active operator profile.</param>
        /// <param name="progressionData">Progression data.</param>
        /// <param name="settings">Game settings snapshot.</param>
        public void SaveGame(string slotName, MissionState missionState, OperatorProfile operatorProfile, ProgressionData progressionData, GameSettings settings)
        {
            try
            {
                var saveData = new SaveData
                {
                    version = CurrentVersion,
                    timestamp = DateTime.UtcNow.Ticks,
                    slotName = slotName,
                    missionState = missionState,
                    operatorProfile = operatorProfile,
                    progressionData = progressionData,
                    settings = settings
                };

                string json = JsonUtility.ToJson(saveData, true);
                string path = GetSavePath(slotName);
                File.WriteAllText(path, json);

                OnSaveCompleted?.Invoke(slotName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to save game '{slotName}': {ex.Message}");
                OnOperationFailed?.Invoke($"Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a save from disk by slot name.
        /// </summary>
        /// <param name="slotName">Name of the save slot to load.</param>
        /// <returns>The loaded save data, or null if the save does not exist or is invalid.</returns>
        public SaveData LoadGame(string slotName)
        {
            try
            {
                string path = GetSavePath(slotName);
                if (!File.Exists(path))
                {
                    OnOperationFailed?.Invoke($"Save slot '{slotName}' not found.");
                    return null;
                }

                string json = File.ReadAllText(path);
                var saveData = JsonUtility.FromJson<SaveData>(json);

                if (saveData == null)
                {
                    OnOperationFailed?.Invoke($"Save file '{slotName}' is corrupt.");
                    return null;
                }

                if (saveData.version != CurrentVersion)
                {
                    Debug.LogWarning($"[SaveSystem] Save version mismatch. Expected {CurrentVersion}, found {saveData.version}. Attempting migration.");
                    saveData = MigrateSave(saveData);
                }

                OnLoadCompleted?.Invoke(slotName);
                return saveData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to load game '{slotName}': {ex.Message}");
                OnOperationFailed?.Invoke($"Load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a save file from disk.
        /// </summary>
        /// <param name="slotName">Name of the save slot to delete.</param>
        /// <returns>True if the file was deleted; otherwise false.</returns>
        public bool DeleteSave(string slotName)
        {
            try
            {
                string path = GetSavePath(slotName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to delete save '{slotName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns a list of all existing save slot names.
        /// </summary>
        /// <returns>List of save slot names.</returns>
        public List<string> GetSaveSlots()
        {
            var slots = new List<string>();
            try
            {
                if (Directory.Exists(saveDirectory))
                {
                    foreach (string file in Directory.GetFiles(saveDirectory, $"*{SaveFileExtension}"))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        slots.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to enumerate save slots: {ex.Message}");
            }
            return slots;
        }

        /// <summary>
        /// Checks whether a save slot exists.
        /// </summary>
        /// <param name="slotName">The slot name to check.</param>
        /// <returns>True if the save exists; otherwise false.</returns>
        public bool HasSave(string slotName)
        {
            return File.Exists(GetSavePath(slotName));
        }

        private string GetSavePath(string slotName)
        {
            return Path.Combine(saveDirectory, slotName + SaveFileExtension);
        }

        private SaveData MigrateSave(SaveData oldData)
        {
            while (oldData.version < CurrentVersion)
            {
                oldData = MigrateVersion(oldData, oldData.version, oldData.version + 1);
                oldData.version++;
            }
            return oldData;
        }

        private SaveData MigrateVersion(SaveData data, int fromVersion, int toVersion)
        {
            switch (toVersion)
            {
                case 1:
                    data.settings = data.settings ?? new GameSettings();
                    break;
            }
            return data;
        }
    }

    /// <summary>
    /// Complete save data container for versioned serialization.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// Save data schema version.
        /// </summary>
        public int version;

        /// <summary>
        /// UTC timestamp of when the save was created.
        /// </summary>
        public long timestamp;

        /// <summary>
        /// Name of the save slot.
        /// </summary>
        public string slotName;

        /// <summary>
        /// Mission state at the time of saving.
        /// </summary>
        public MissionState missionState;

        /// <summary>
        /// Operator profile at the time of saving.
        /// </summary>
        public OperatorProfile operatorProfile;

        /// <summary>
        /// Progression data at the time of saving.
        /// </summary>
        public ProgressionData progressionData;

        /// <summary>
        /// Game settings snapshot.
        /// </summary>
        public GameSettings settings;
    }

    /// <summary>
    /// Snapshot of mission state for saving.
    /// </summary>
    [Serializable]
    public sealed class MissionState
    {
        /// <summary>
        /// Unique identifier of the active mission.
        /// </summary>
        public string missionId;

        /// <summary>
        /// Current checkpoint or scene name.
        /// </summary>
        public string checkpoint;

        /// <summary>
        /// Elapsed play time in seconds.
        /// </summary>
        public float elapsedTime;

        /// <summary>
        /// Current player health.
        /// </summary>
        public float playerHealth;

        /// <summary>
        /// Current player position.
        /// </summary>
        public Vector3 playerPosition;

        /// <summary>
        /// Serialized state of all mission objectives.
        /// </summary>
        public List<MissionObjective> objectives;

        /// <summary>
        /// Serialized state of all mission events.
        /// </summary>
        public List<MissionEvent> events;

        /// <summary>
        /// Current inventory contents.
        /// </summary>
        public List<string> inventoryItems;

        /// <summary>
        /// Current ammunition counts per weapon.
        /// </summary>
        public Dictionary<string, int> ammoCounts;
    }

    /// <summary>
    /// Operator profile data for saving.
    /// </summary>
    [Serializable]
    public sealed class OperatorProfile
    {
        /// <summary>
        /// Unique operator identifier.
        /// </summary>
        public string operatorId;

        /// <summary>
        /// Display name of the operator.
        /// </summary>
        public string operatorName;

        /// <summary>
        /// Current level of the operator.
        /// </summary>
        public int level;

        /// <summary>
        /// Current experience points.
        /// </summary>
        public int experience;

        /// <summary>
        /// Loadout configuration identifier.
        /// </summary>
        public string loadoutId;

        /// <summary>
        /// Equipped weapon identifiers.
        /// </summary>
        public List<string> equippedWeapons;

        /// <summary>
        /// Equipped gear identifiers.
        /// </summary>
        public List<string> equippedGear;

        /// <summary>
        /// Operator customization settings.
        /// </summary>
        public string customizationData;

        /// <summary>
        /// Current health of the operator.
        /// </summary>
        public float health;

        /// <summary>
        /// Maximum health of the operator.
        /// </summary>
        public float maxHealth;
    }

    /// <summary>
    /// Progression data for saving.
    /// </summary>
    [Serializable]
    public sealed class ProgressionData
    {
        /// <summary>
        /// Total missions completed.
        /// </summary>
        public int missionsCompleted;

        /// <summary>
        /// Total play time in seconds.
        /// </summary>
        public float totalPlayTime;

        /// <summary>
        /// Total enemies eliminated.
        /// </summary>
        public int totalKills;

        /// <summary>
        /// Total accuracy percentage from 0 to 100.
        /// </summary>
        public float accuracy;

        /// <summary>
        /// Unlocked campaign nodes.
        /// </summary>
        public List<string> unlockedNodes;

        /// <summary>
        /// Completed mission identifiers.
        /// </summary>
        public List<string> completedMissions;

        /// <summary>
        /// Unlocked achievements.
        /// </summary>
        public List<string> achievements;

        /// <summary>
        /// Current reputation score.
        /// </summary>
        public int reputation;

        /// <summary>
        /// Currency balance.
        /// </summary>
        public int currency;
    }

    /// <summary>
    /// Game settings snapshot for saving.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        /// <summary>
        /// Master volume from 0 to 100.
        /// </summary>
        public float masterVolume;

        /// <summary>
        /// Music volume from 0 to 100.
        /// </summary>
        public float musicVolume;

        /// <summary>
        /// SFX volume from 0 to 100.
        /// </summary>
        public float sfxVolume;

        /// <summary>
        /// Voice volume from 0 to 100.
        /// </summary>
        public float voiceVolume;

        /// <summary>
        /// Mouse sensitivity multiplier.
        /// </summary>
        public float mouseSensitivity;

        /// <summary>
        /// Invert mouse Y axis.
        /// </summary>
        public bool invertMouseY;

        /// <summary>
        /// Controller vibration enabled.
        /// </summary>
        public bool controllerVibration;

        /// <summary>
        /// Difficulty level identifier.
        /// </summary>
        public string difficulty;

        /// <summary>
        /// Subtitles enabled.
        /// </summary>
        public bool subtitlesEnabled;

        /// <summary>
        /// Language code for localization.
        /// </summary>
        public string language;

        /// <summary>
        /// Screen resolution width.
        /// </summary>
        public int screenWidth;

        /// <summary>
        /// Screen resolution height.
        /// </summary>
        public int screenHeight;

        /// <summary>
        /// Fullscreen mode index.
        /// </summary>
        public int fullscreenMode;

        /// <summary>
        /// Vertical sync enabled.
        /// </summary>
        public bool vSyncEnabled;

        /// <summary>
        /// Frame rate cap. Zero means uncapped.
        /// </summary>
        public int frameRateCap;

        /// <summary>
        /// Graphics quality level index.
        /// </summary>
        public int graphicsQuality;
    }
}
