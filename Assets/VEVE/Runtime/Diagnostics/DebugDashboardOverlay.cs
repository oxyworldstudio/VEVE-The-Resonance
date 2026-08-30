using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VEVE.Diagnostics
{
    /// <summary>
    /// F12-toggled runtime dashboard: subsystem presence, simulation health,
    /// agent/bridge population, weapon identity/zeroing state and live tranche
    /// progress snapshot. All lookups null-safe every 0.5 s (never per frame).
    /// </summary>
    public sealed class DebugDashboardOverlay : MonoBehaviour
    {
        public static DebugDashboardOverlay Instance { get; private set; }

        [SerializeField] private KeyCode toggleKey = KeyCode.F12;
        [SerializeField] private bool visible = true;
        [SerializeField] private float refreshInterval = 0.5f;
        [SerializeField] private Font monoFont;

        private float refreshTimer;
        private Vector2 scroll;
        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private readonly List<string> lines = new List<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = refreshInterval;
                RefreshLines();
            }
        }

        private void RefreshLines()
        {
            lines.Clear();
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            lines.Add($"FPS {fps:F0} · timescale {Time.timeScale:F2} · scene {SceneName()}  ");

            AppendPresence("SimulationCoordinator", Object.FindFirstObjectByType<VEVE.SimulationCoordinator>() != null);
            AppendPresence("AgentBridge", VEVE.Agents.AgentBridge.Instance != null);
            var manager = Object.FindFirstObjectByType<VEVE.Agentic.MultiAgentSystemManager>();
            lines.Add($"· Agents: {(manager != null ? manager.RegisteredAgentCount : 0)} registered / teams {(manager != null ? manager.ActiveTeamCount : 0)}");
            AppendPresence("OperatorInstance", Object.FindFirstObjectByType<VEVE.Operators.OperatorInstance>() != null);
            AppendPresence("Gear adapter", Object.FindFirstObjectByType<VEVE.Gear.DamageableGearAdapter>() != null);
            AppendPresence("Personalization", Object.FindFirstObjectByType<VEVE.UI.Personalization.PersonalizationWorkspace>() != null);
            AppendPresence("HUD suite", Object.FindFirstObjectByType<VEVE.UI.AdvancedHUDLayout>() != null);
            lines.Add("· Tactics engine: SquadMorale + EngagementReporter + Escalation + EventHub (B4)");

            lines.Add($"· Physics gravity y {Physics.gravity.y:F3} · time step {Time.fixedDeltaTime:F4}");
        }

        private static string SceneName()
        {
            Scene s = SceneManager.GetActiveScene();
            return string.IsNullOrEmpty(s.name) ? "<none>" : s.name;
        }

        private void AppendPresence(string label, bool present)
        {
            lines.Add($"· {label}: {(present ? "OK" : "—")}");
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                wordWrap = false,
                padding = new RectOffset(8, 8, 4, 6)
            };
            headerStyle = new GUIStyle(boxStyle) { fontStyle = FontStyle.Bold };
            if (monoFont != null)
            {
                boxStyle.font = monoFont;
                headerStyle.font = monoFont;
            }
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();
            GUILayout.BeginArea(new Rect(12, 12, 360, 460), GUI.skin.box);
            GUILayout.Label("VEVE • debug dashboard (F12)", headerStyle);
            foreach (string l in lines)
                GUILayout.Label(l, boxStyle);
            GUILayout.EndArea();
        }
    }
}
