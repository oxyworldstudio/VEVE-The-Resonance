using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace VEVE.Editor
{
    public enum FeatureStatus { Implemented, InIntegration, Planned }

    public sealed class FeatureEntry
    {
        public string domain;
        public string name;
        public FeatureStatus status;
        public string[] keyPaths;
        public string note;

        public bool AllFilesExist()
        {
            if (keyPaths == null || keyPaths.Length == 0) return false;
            foreach (string rel in keyPaths)
            {
                if (!File.Exists(Path.Combine(ProgressDashboard.ProjectRoot, rel))) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Editor dashboard: validates feature registry against the on-disk project,
    /// parses the latest Unity EditMode XML results, lists recent commits and
    /// per-domain code volume, with persistent done-notes per feature.
    /// </summary>
    public sealed class ProgressDashboard : EditorWindow
    {
        private const string PrefsDonePrefix = "VEVE.Dashboard.Done.";

        internal static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private List<FeatureEntry> features;
        private List<CommitRow> commits = new List<CommitRow>();
        private TestSummary tests;
        private Vector2 scroll;
        private double lastGather;

        private struct CommitRow
        {
            public string hash;
            public string subject;
            public string date;
        }

        private class TestSuiteStat
        {
            public string name;
            public int total;
            public int passed;
            public List<string> failures = new List<string>();
        }

        private class TestSummary
        {
            public string file;
            public int total;
            public int passed;
            public int failed;
            public Dictionary<string, TestSuiteStat> bySuite = new Dictionary<string, TestSuiteStat>();
        }

        [MenuItem("VEVE/Progress Dashboard")]
        public static void Open()
        {
            ProgressDashboard win = GetWindow<ProgressDashboard>("VEVE Progress");
            win.minSize = new Vector2(640, 420);
        }

        private void OnEnable() { Gather(); }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup - lastGather > 8.0)
            {
                lastGather = EditorApplication.timeSinceStartup;
                Gather();
                Repaint();
            }
        }

        private static List<FeatureEntry> BuildRegistry()
        {
            string rt = "Assets/VEVE/Runtime/";
            string ts = "Assets/VEVE/Tests/";
            return new List<FeatureEntry>
            {
                new FeatureEntry { domain = "Simulation", name = "Realism core (RealismConfig, SimulationCoordinator, PhysicsRealism, RenderingRealism)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "RealismConfig.cs", rt + "SimulationCoordinator.cs", rt + "PhysicsRealism.cs", rt + "RenderingRealism.cs" } },
                new FeatureEntry { domain = "Simulation", name = "Gravity fix + regression guard (Scene 1)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "PlayerController.cs", ts + "PlayerGravityRegressionTests.cs" } },
                new FeatureEntry { domain = "Ballistics", name = "6-DOF projectiles, terminal, ricochet, Coriolis/spin drift", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Ballistics.cs", rt + "AdvancedBallistics.cs", rt + "Physics/ProjectileBallistics.cs" } },
                new FeatureEntry { domain = "Weapons", name = "Iconic catalog (18) + attachment matrix + proficiency", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Catalog/IconicWeaponCatalog.cs", rt + "Catalog/AttachmentCompatibilityMatrix.cs", rt + "Catalog/WeaponProficiencySystem.cs", ts + "BallisticConsistencyTests.cs" } },
                new FeatureEntry { domain = "Weapons", name = "Customization pro (optics, zeroing/range card, identity, finishes)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "WeaponCustomPro/ScopeProfile.cs", rt + "WeaponCustomPro/ZeroingSystem.cs", rt + "WeaponCustomPro/WeaponInstanceIdentity.cs", rt + "WeaponCustomPro/CosmeticFinishSystem.cs" } },
                new FeatureEntry { domain = "Weapons", name = "Legacy manager + attachment slots", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Customization/WeaponCustomizationManager.cs" } },
                new FeatureEntry { domain = "Gear", name = "NIJ/VPAM protection, loadout validation, mobility penalties", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Gear/GearProtectionStandard.cs", rt + "Gear/GearLoadout.cs", rt + "Gear/MobilityPenaltyModel.cs", rt + "Gear/DamageableGearAdapter.cs", ts + "GearProtectionTests.cs" } },
                new FeatureEntry { domain = "Operators", name = "Traits/specialties/roster + permadeath legacy + voice kits", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Operators/OperatorProfile.cs", rt + "Operators/OperatorTraits.cs", rt + "Operators/OperatorLegacySystem.cs", rt + "Operators/VoiceKitLibrary.cs" } },
                new FeatureEntry { domain = "Operators", name = "Player-feel integration (B2): sway/speed/zeroing wired", status = FeatureStatus.InIntegration, note = "B2 agent verified Roslyn+31 checks; awaiting Unity gate",
                    keyPaths = new[] { rt + "Operators/OperatorInstance.cs", ts + "OperatorInstanceFeelTests.cs" } },
                new FeatureEntry { domain = "Campaign", name = "KIA hook + legacy successor + Personalization binders (B3, orchestrator takeover)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "CampaignSystem.cs", rt + "UI/Personalization/PersonalizationRuntimeBindings.cs", ts + "CampaignLegacyHookTests.cs" } },
                new FeatureEntry { domain = "AI", name = "Behavior trees + squad + formations + waypoints + communication", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "AI/BehaviorTree.cs", rt + "AI/BehaviorTreeNodes.cs", rt + "AI/SquadManager.cs", rt + "AI/FormationSystem.cs" } },
                new FeatureEntry { domain = "AI", name = "Multi-agent system (VEVE.Agentic) + LOD bridge (VEVE.Agents)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Agentic/MultiAgentSystemManager.cs", rt + "Agents/AgentBridge.cs", rt + "Agents/AgentLOD.cs", ts + "AgentLODTests.cs" } },
                new FeatureEntry { domain = "Tactics", name = "Squad morale FSM + engagement/intel + campaign escalation (B4)", status = FeatureStatus.InIntegration,
                    keyPaths = new[] { rt + "Tactics/SquadMorale.cs", rt + "Tactics/EngagementReporter.cs", rt + "Tactics/CampaignEscalationModel.cs", ts + "TacMoraleTests.cs" } },
                new FeatureEntry { domain = "UI", name = "HUD suite + inventory + menu flow + personalization workspace (5 tabs)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "UI/AdvancedHUDLayout.cs", rt + "UI/Personalization/PersonalizationWorkspace.cs", rt + "UI/Personalization/WeaponCustomizationPanel.cs", rt + "UI/Personalization/ZeroingPanel.cs" } },
                new FeatureEntry { domain = "Graphics", name = "Procedural PBR texture factory + reflection probes + PBR compliance audit", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Graphics/ProceduralSurfaceTextureFactory.cs", rt + "Graphics/DynamicReflectionController.cs", "Assets/VEVE/Editor/PBRMaterialComplianceChecker.cs" } },
                new FeatureEntry { domain = "Audio", name = "Propagation + occlusion + zones + acoustic physics + procedural audio", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Audio/AdvancedSoundPropagation.cs", rt + "Audio/AudioOcclusion.cs", rt + "Physics/AcousticPhysics.cs", rt + "Audio/ProceduralAudio.cs" } },
                new FeatureEntry { domain = "Environment", name = "Weather/climate/skybox/fog volumes + environmental physics", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "WeatherSystem.cs", rt + "ClimateSystem.cs", rt + "SkyboxController.cs", rt + "FogVolume.cs" } },
                new FeatureEntry { domain = "Procedural", name = "Map generator + biomes + props + room functions + tactical evaluator", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Procedural/ProceduralMapGenerator.cs", rt + "Procedural/BiomeProfiles.cs", rt + "Procedural/PropScatterSystem.cs", rt + "Procedural/TacticalLayoutEvaluator.cs" } },
                new FeatureEntry { domain = "Progression", name = "XP/rank/unlocks + analytics", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Progression/ProgressionManager.cs", rt + "Analytics/AnalyticsManager.cs" } },
                new FeatureEntry { domain = "Infra", name = "GameLoop + EventBus + QualityPreset + PerformanceManager + save", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "GameLoop.cs", rt + "EventBus.cs", rt + "SaveSystem.cs", "Assets/VEVE/Editor/SceneBuilder.cs" } },
                new FeatureEntry { domain = "Infra", name = "Progress dashboard + runtime debug overlay", status = FeatureStatus.Implemented,
                    keyPaths = new[] { "Assets/VEVE/Editor/ProgressDashboard.cs", rt + "Diagnostics/DebugDashboardOverlay.cs" } },
                new FeatureEntry { domain = "Tactility", name = "Doors/breach + partial reload (B5)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "World/DoorModel.cs", rt + "World/DoorSystem.cs", rt + "Combat/AmmunitionModel.cs", ts + "WorldInteractionModelsTests.cs" } },
                new FeatureEntry { domain = "UI", name = "HUD diegesis per death mode (B6)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "UI/HudDiegesisProfile.cs", ts + "HudDiegesisProfileTests.cs" } },
                new FeatureEntry { domain = "Campaign", name = "Mission scoring + rewards (B7)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Scoring/MissionScoring.cs", ts + "MissionScoringTests.cs" } },
                new FeatureEntry { domain = "Campaign", name = "Content catalog + difficulty tracks + scope telemetry (B8)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Content/MissionContent.cs", rt + "UI/ScopeTelemetryBridge.cs", ts + "MissionContentTests.cs" } },
                new FeatureEntry { domain = "UI", name = "Diegetic scope reticle (C1)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "UI/ScopeReticleOverlay.cs", ts + "ScopeReticleOverlayTests.cs" } },
                new FeatureEntry { domain = "Weapons", name = "Real optic mount C3: ScopeCatalog -> attachment bridge, live reticle scale", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "WeaponCustomPro/OpticCatalogBridge.cs", ts + "OpticCatalogBridgeTests.cs" } },
                new FeatureEntry { domain = "Netcode", name = "Host-authoritative mission protocol (C4 pure layer)", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Net/MissionNetProtocol.cs", ts + "MissionNetProtocolTests.cs" }, note = "NGO NetworkBehaviour adapter pending as next install" },
                new FeatureEntry { domain = "Campaign", name = "Campaign loop C2: draft -> tally -> scoring -> XP -> escalation", status = FeatureStatus.Implemented,
                    keyPaths = new[] { rt + "Content/MissionSession.cs", rt + "Content/CampaignLoopController.cs", ts + "MissionSessionTests.cs", ts + "MissionContentTests.cs" } },
                new FeatureEntry { domain = "Planned", name = "Multiplayer netcode / VR", status = FeatureStatus.Planned }
            };
        }

        private void Gather()
        {
            features = BuildRegistry();
            commits = GatherCommits();
            tests = GatherTests();
        }

        private static List<CommitRow> GatherCommits()
        {
            List<CommitRow> rows = new List<CommitRow>();
            string logPath = Path.Combine(ProjectRoot, ".git", "logs", "HEAD");
            if (File.Exists(logPath))
            {
                foreach (string line in File.ReadAllLines(logPath).Reverse().Take(16))
                {
                    int tab = line.IndexOf('\t');
                    if (tab < 0) continue;
                    string rest = line.Substring(tab + 1);
                    string hash = line.Substring(0, Math.Min(7, line.IndexOf(' ', StringComparison.Ordinal)));
                    string msg = rest.StartsWith("commit:") ? rest.Substring(7).Trim() : rest.Trim();
                    if (msg.Length > 96) msg = msg.Substring(0, 93) + "...";
                    rows.Add(new CommitRow { hash = hash, subject = msg, date = "" });
                }
            }
            return rows;
        }

        private TestSummary GatherTests()
        {
            TestSummary s = new TestSummary();
            try
            {
                DirectoryInfo dir = new DirectoryInfo(ProjectRoot);
                FileInfo latest = dir.GetFiles("test-results*.xml").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (latest == null) return s;
                s.file = latest.Name;
                XDocument doc = XDocument.Load(latest.FullName);
                XElement run = doc.Root;
                int.TryParse((string)run.Attribute("total"), out s.total);
                int.TryParse((string)run.Attribute("passed"), out s.passed);
                int.TryParse((string)run.Attribute("failed"), out s.failed);
                foreach (XElement suite in run.Descendants("test-suite").Where(e => (string)e.Attribute("type") == "TestFixture"))
                {
                    TestSuiteStat st = new TestSuiteStat
                    {
                        name = (string)suite.Attribute("name") ?? "?"
                    };
                    int.TryParse((string)suite.Attribute("total"), out st.total);
                    int.TryParse((string)suite.Attribute("passed"), out st.passed);
                    foreach (XElement f in suite.Descendants("test-case").Where(t => (string)t.Attribute("result") == "Failed"))
                    {
                        st.failures.Add((string)f.Attribute("name"));
                    }
                    s.bySuite[st.name] = st;
                }
            }
            catch (Exception e)
            {
                Debug.Log("[Dashboard] test parse failed: " + e.Message);
            }
            return s;
        }

        private static (int files, int lines) DomainStats(string folder)
        {
            string abs = Path.Combine(ProjectRoot, "Assets/VEVE/" + folder);
            if (!Directory.Exists(abs)) return (0, 0);
            int files = 0, lines = 0;
            foreach (string f in Directory.GetFiles(abs, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                lines += File.ReadAllLines(f).Length;
            }
            return (files, lines);
        }

        private void OnGUI()
        {
            if (features == null) Gather();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80))) Gather();
            EditorGUILayout.LabelField("Project: " + ProjectRoot, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            TestSummary head = tests ?? new TestSummary();
            Color saved = GUI.contentColor;
            GUI.contentColor = head.failed == 0 && head.total > 0 ? Color.green : Color.yellow;
            EditorGUILayout.LabelField($"Self-test: {head.passed}/{head.total} passed, {head.failed} failed   ({head.file ?? "no run yet"})");
            GUI.contentColor = saved;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Domains (Runtime)", EditorStyles.boldLabel);
            foreach (string d in new[] { "Gear", "Operators", "Tactics", "WeaponCustomPro", "Catalog", "Customization", "Agentic", "Agents", "AI", "Procedural", "Physics", "Graphics", "Audio", "UI", "Analytics", "Diagnostics", "Progression" })
            {
                var (f, l) = DomainStats(d);
                if (f > 0) EditorGUILayout.LabelField(d.PadRight(18) + " — " + f + " files, " + l.ToString("N0") + " loc");
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Feature registry", EditorStyles.boldLabel);
            DrawFeatures(head);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Recent commits (git reflog)", EditorStyles.boldLabel);
            foreach (CommitRow c in commits.Take(14))
            {
                EditorGUILayout.LabelField(c.hash, c.subject);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawFeatures(TestSummary head)
        {
            foreach (var grp in features.GroupBy(f => f.domain))
            {
                EditorGUILayout.LabelField(new GUIContent("▸ " + grp.Key), EditorStyles.foldoutHeader);
                foreach (FeatureEntry fe in grp)
                {
                    bool verified = fe.AllFilesExist() || fe.status == FeatureStatus.Planned;
                    EditorGUILayout.BeginHorizontal();
                    Color saved = GUI.backgroundColor;
                    GUI.backgroundColor = fe.status switch
                    {
                        FeatureStatus.Implemented => verified ? new Color(0.55f, 0.85f, 0.55f) : new Color(0.85f, 0.65f, 0.45f),
                        FeatureStatus.InIntegration => new Color(0.95f, 0.85f, 0.45f),
                        _ => new Color(0.6f, 0.6f, 0.62f)
                    };
                    int total = head.bySuite.TryGetValue(fe.domain + "Suite", out var ts) ? ts.total : 0;
                    string statusText = fe.status == FeatureStatus.Implemented
                        ? (verified ? "OK" : "FILES-MISSING")
                        : (fe.status == FeatureStatus.InIntegration ? "IN-INTEGRATION" : "planned");
                    GUILayout.Label($"{fe.name}   [{statusText}]  {(total > 0 ? total + " tests" : "")} ", GUILayout.MinWidth(480));
                    bool done = EditorPrefs.GetBool(PrefsDonePrefix + fe.domain + "." + fe.name, fe.status == FeatureStatus.Implemented);
                    if (GUILayout.Toggle(done, "✓", GUILayout.Width(22)) != done)
                        EditorPrefs.SetBool(PrefsDonePrefix + fe.domain + "." + fe.name, !done);
                    GUI.backgroundColor = saved;
                    EditorGUILayout.EndHorizontal();
                    if (!string.IsNullOrEmpty(fe.note))
                        EditorGUILayout.LabelField("        " + fe.note, EditorStyles.miniLabel);
                }
            }
        }
    }
}
