using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.Analytics
{
    public enum EventType { SessionStart, SessionEnd, MissionStart, MissionEnd, Death, Kill, WeaponFired, ItemUsed, SettingChanged, Error }

    [System.Serializable]
    public struct AnalyticsEvent
    {
        public string eventId;
        public EventType type;
        public float timestamp;
        public Dictionary<string, string> parameters;
    }

    public class AnalyticsManager
    {
        private List<AnalyticsEvent> sessionEvents;
        private float sessionStartTime;
        private bool isSessionActive;

        public AnalyticsManager()
        {
            sessionEvents = new List<AnalyticsEvent>();
            StartSession();
        }

        public void StartSession()
        {
            if (isSessionActive) return;
            isSessionActive = true;
            sessionStartTime = Time.realtimeSinceStartup;
            LogEvent(EventType.SessionStart, new Dictionary<string, string>
            {
                { "platform", Application.platform.ToString() },
                { "unity_version", Application.unityVersion }
            });
        }

        public void EndSession()
        {
            if (!isSessionActive) return;
            isSessionActive = false;
            float sessionDuration = Time.realtimeSinceStartup - sessionStartTime;
            LogEvent(EventType.SessionEnd, new Dictionary<string, string>
            {
                { "duration", sessionDuration.ToString("F2") },
                { "event_count", sessionEvents.Count.ToString() }
            });
        }

        public void LogEvent(EventType type, Dictionary<string, string> parameters = null)
        {
            var evt = new AnalyticsEvent
            {
                eventId = System.Guid.NewGuid().ToString(),
                type = type,
                timestamp = Time.realtimeSinceStartup,
                parameters = parameters ?? new Dictionary<string, string>()
            };
            sessionEvents.Add(evt);
        }

        public void LogMissionEvent(EventType type, string missionId, bool success, float duration)
        {
            var parameters = new Dictionary<string, string>
            {
                { "mission_id", missionId },
                { "success", success.ToString() },
                { "duration", duration.ToString("F2") }
            };
            LogEvent(type, parameters);
        }

        public void LogCombatEvent(EventType type, string weaponId, float distance, bool headshot)
        {
            var parameters = new Dictionary<string, string>
            {
                { "weapon_id", weaponId },
                { "distance", distance.ToString("F1") },
                { "headshot", headshot.ToString() }
            };
            LogEvent(type, parameters);
        }

        public void LogError(string errorMessage, string stackTrace)
        {
            var parameters = new Dictionary<string, string>
            {
                { "error", errorMessage },
                { "stack", stackTrace }
            };
            LogEvent(EventType.Error, parameters);
        }

        public List<AnalyticsEvent> GetSessionEvents()
        {
            return new List<AnalyticsEvent>(sessionEvents);
        }

        public int GetEventCount(EventType type)
        {
            return sessionEvents.Count(e => e.type == type);
        }

        public float GetSessionDuration()
        {
            if (!isSessionActive) return 0f;
            return Time.realtimeSinceStartup - sessionStartTime;
        }

        public void ClearEvents()
        {
            sessionEvents.Clear();
        }
    }
}
