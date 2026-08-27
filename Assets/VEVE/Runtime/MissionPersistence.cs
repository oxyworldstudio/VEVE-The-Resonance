using System;
using System.IO;
using UnityEngine;

namespace VEVE
{
    [Serializable]
    public sealed class MissionState
    {
        public int version = 1;
        public string sceneName;
        public int missionSeed;
        public string[] significantEvents = Array.Empty<string>();
    }

    public sealed class MissionRuntime : MonoBehaviour
    {
        [SerializeField] private int missionSeed = 1701;
        [SerializeField] private string[] significantEvents = Array.Empty<string>();

        public void RecordEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("Event id is required.", nameof(eventId));
            Array.Resize(ref significantEvents, significantEvents.Length + 1);
            significantEvents[significantEvents.Length - 1] = eventId;
        }

        public void Save()
        {
            MissionPersistence.Save(new MissionState
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                missionSeed = missionSeed,
                significantEvents = significantEvents
            });
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5)) Save();
        }
    }

    public static class MissionPersistence
    {
        private const string FileName = "veve-mission.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(MissionState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string temporaryPath = SavePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(state, true));
            if (File.Exists(SavePath)) File.Replace(temporaryPath, SavePath, null);
            else File.Move(temporaryPath, SavePath);
        }

        public static MissionState Load()
        {
            if (!File.Exists(SavePath)) return null;
            string json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Mission save is empty.");
            MissionState state = JsonUtility.FromJson<MissionState>(json);
            if (state == null || state.version != 1) throw new InvalidDataException("Unsupported mission save version.");
            return state;
        }
    }
}
