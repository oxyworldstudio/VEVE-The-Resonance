using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VEVE;

namespace VEVE.Editor
{
    public static class SceneBuilder
    {
        [MenuItem("VEVE/Build Milestone 1 Scene")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            Material wood = MakeMaterial("Wood", new Color(0.35f, 0.16f, 0.06f), 0.4f, 0.7f);
            Material concrete = MakeMaterial("Concrete", new Color(0.45f, 0.45f, 0.45f), 0.15f, 0.9f);
            Material metal = MakeMaterial("Metal", new Color(0.6f, 0.6f, 0.65f), 0.1f, 0.95f);
            Material glass = MakeMaterial("Glass", new Color(0.9f, 0.95f, 1.0f), 0.05f, 0.1f);
            Material dirt = MakeMaterial("Dirt", new Color(0.3f, 0.2f, 0.1f), 0.5f, 0.8f);
            
            CreateCube("Ground", new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40), dirt);
            CreateCube("BuildingFloor", new Vector3(0, 0.05f, 8), new Vector3(12, 0.1f, 10), concrete);
            CreateCube("BuildingWall", new Vector3(0, 2.5f, 13), new Vector3(12, 5, 0.4f), concrete);
            CreateCube("BuildingWall2", new Vector3(-6, 2.5f, 13), new Vector3(0.4f, 5, 10), concrete);
            CreateCube("BuildingWall3", new Vector3(6, 2.5f, 13), new Vector3(0.4f, 5, 10), concrete);
            
            GameObject woodCover = CreateCube("WoodCover", new Vector3(-4, 1.2f, 7), new Vector3(4, 2.4f, 0.3f), wood);
            woodCover.AddComponent<CoverVolume>();
            
            GameObject metalBarrier = CreateCube("MetalBarrier", new Vector3(5, 0.8f, 5), new Vector3(3, 1.6f, 0.2f), metal);
            metalBarrier.AddComponent<CoverVolume>();
            
            GameObject glassWindow = CreateCube("GlassWindow", new Vector3(0, 2.5f, 12.8f), new Vector3(2, 1.5f, 0.05f), glass);
            glassWindow.AddComponent<CoverVolume>();
            
            GameObject environment = new GameObject("EnvironmentSimulation");
            EnvironmentSimulation environmentSimulation = environment.AddComponent<EnvironmentSimulation>();
            environment.AddComponent<MissionRuntime>();
            
            GameObject simulation = new GameObject("SimulationCoordinator");
            SimulationCoordinator coordinator = simulation.AddComponent<SimulationCoordinator>();
            simulation.AddComponent<SimulationDiagnostics>();
            simulation.AddComponent<VEVE.Scoring.MissionScoreBoard>();
            simulation.AddComponent<VEVE.Content.CampaignLoopController>();
            simulation.AddComponent<VEVE.WeaponCustomPro.WeaponCustomizationHost>();
            
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.95f, 0.8f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 1.0f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.15f, 0.1f);
            RenderSettings.ambientIntensity = 0.4f;
            
            GameObject player = new GameObject("Operator");
            player.transform.position = new Vector3(0, 1, 0);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = 0.35f;
            player.AddComponent<Physiology>();
            player.AddComponent<PhysicalInventory>();
            player.AddComponent<MovementSimulation>();
            player.AddComponent<CampaignState>();
            player.AddComponent<FieldMedic>();
            player.AddComponent<PlayerController>();
            player.AddComponent<VEVE.RealisticPhysics.CharacterMassModel>();
            player.AddComponent<VEVE.RealisticPhysics.GroundContactProbe>();
            player.AddComponent<VEVE.RealisticPhysics.TerminalVelocityFallingSystem>();
            player.AddComponent<VEVE.Gear.DamageableGearAdapter>();
            player.AddComponent<VEVE.Operators.OperatorInstance>();
            player.AddComponent<Unity.Netcode.NetworkObject>();
            player.AddComponent<VEVE.Net.NetworkedPlayerAvatar>();
            
            SerializedObject coordinatorData = new SerializedObject(coordinator);
            coordinatorData.FindProperty("environment").objectReferenceValue = environmentSimulation;
            coordinatorData.FindProperty("mission").objectReferenceValue = environment.GetComponent<MissionRuntime>();
            coordinatorData.FindProperty("campaign").objectReferenceValue = player.GetComponent<CampaignState>();
            coordinatorData.ApplyModifiedPropertiesWithoutUndo();
            
            GameObject cameraObject = new GameObject("Eyes");
            cameraObject.transform.SetParent(player.transform); cameraObject.transform.localPosition = new Vector3(0, 0.7f, 0);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.25f, 0.3f);
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            
            LookController look = cameraObject.AddComponent<LookController>();
            SerializedObject lookData = new SerializedObject(look); 
            lookData.FindProperty("body").objectReferenceValue = player.transform; 
            lookData.ApplyModifiedPropertiesWithoutUndo();
            
            GameObject weaponObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weaponObject.name = "ModularCarbine"; 
            weaponObject.transform.SetParent(cameraObject.transform);
            weaponObject.transform.localPosition = new Vector3(0.3f, -0.25f, 0.7f); 
            weaponObject.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
            weaponObject.GetComponent<Renderer>().sharedMaterial = metal;
            Weapon weapon = weaponObject.AddComponent<Weapon>();
            weaponObject.AddComponent<VEVE.UI.ScopeTelemetryBridge>();
            weaponObject.AddComponent<Maintenance>();
            WeaponDefinition weaponDefinition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/VEVE/CarbineDefinition.asset");
            if (weaponDefinition == null)
            {
                weaponDefinition = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weaponDefinition, "Assets/VEVE/CarbineDefinition.asset");
            }
            SerializedObject weaponData = new SerializedObject(weapon);
            weaponData.FindProperty("aimCamera").objectReferenceValue = camera;
            weaponData.FindProperty("definition").objectReferenceValue = weaponDefinition;
            weaponData.ApplyModifiedPropertiesWithoutUndo();
            
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Enemy_Torso"; 
            enemy.transform.position = new Vector3(0, 1, 15); 
            enemy.GetComponent<Renderer>().sharedMaterial = concrete;
            Damageable damageable = enemy.AddComponent<Damageable>();
            enemy.AddComponent<VEVE.Gear.DamageableGearAdapter>();

            GameObject doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorObject.name = "BarricadedDoor";
            doorObject.transform.position = new Vector3(6f, 1.5f, 8f);
            doorObject.transform.localScale = new Vector3(1.1f, 2.6f, 0.15f);
            doorObject.GetComponent<Renderer>().sharedMaterial = wood;
            doorObject.AddComponent<VEVE.World.DoorSystem>();
            EnemyAwareness awareness = enemy.AddComponent<EnemyAwareness>();
            SerializedObject aiData = new SerializedObject(awareness); 
            aiData.FindProperty("target").objectReferenceValue = player.transform; 
            aiData.ApplyModifiedPropertiesWithoutUndo();
            
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Enemy_Head";
            head.transform.SetParent(enemy.transform);
            head.transform.localPosition = new Vector3(0, 1.2f, 0);
            head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            head.GetComponent<Renderer>().sharedMaterial = concrete;
            
            GameObject wristDisplay = new GameObject("WristDisplay");
            Canvas canvas = wristDisplay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Text text = new GameObject("Readout").AddComponent<Text>();
            text.transform.SetParent(wristDisplay.transform); 
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); 
            text.fontSize = 18; 
            text.color = Color.green;
            RectTransform rect = text.rectTransform; 
            rect.anchorMin = new Vector2(0, 1); 
            rect.anchorMax = new Vector2(0, 1); 
            rect.anchoredPosition = new Vector2(150, -90); 
            rect.sizeDelta = new Vector2(400, 150);
            DiegeticReadout readout = wristDisplay.AddComponent<DiegeticReadout>();
            SerializedObject readoutData = new SerializedObject(readout); 
            readoutData.FindProperty("weapon").objectReferenceValue = weapon; 
            readoutData.FindProperty("readout").objectReferenceValue = text; 
            readoutData.FindProperty("physiology").objectReferenceValue = player.GetComponent<Physiology>(); 
            readoutData.FindProperty("inventory").objectReferenceValue = player.GetComponent<PhysicalInventory>(); 
            readoutData.FindProperty("movement").objectReferenceValue = player.GetComponent<MovementSimulation>(); 
            readoutData.ApplyModifiedPropertiesWithoutUndo();
            
            GameObject rayTracingObject = new GameObject("RayTracingManager");
            rayTracingObject.AddComponent<RayTracingManager>();
            
            GameObject audioObject = new GameObject("AdvancedAudio");
            audioObject.AddComponent<AudioSource>();

            GameObject agentSystemObject = new GameObject("MultiAgentSystem");
            agentSystemObject.AddComponent<VEVE.Agentic.MultiAgentSystemManager>();
            agentSystemObject.AddComponent<VEVE.Agentic.CoordinatorAgent>();

            GameObject fidelityObject = new GameObject("RenderFidelity");
            fidelityObject.AddComponent<VEVE.Graphics.ProceduralSurfaceTextureFactory>();
            fidelityObject.AddComponent<VEVE.Graphics.DynamicReflectionController>();

            GameObject interfaceObject = new GameObject("AAAInterface");
            interfaceObject.AddComponent<VEVE.UI.AdvancedHUDLayout>();
            interfaceObject.AddComponent<VEVE.UI.InventoryUIController>();
            interfaceObject.AddComponent<VEVE.UI.MainMenuFlowController>();
            interfaceObject.AddComponent<VEVE.UI.Personalization.PersonalizationWorkspace>();
            interfaceObject.AddComponent<VEVE.UI.Personalization.PersonalizationBinder>();
            interfaceObject.AddComponent<VEVE.UI.HudDiegesisController>();
            interfaceObject.AddComponent<VEVE.UI.ScopeReticleOverlay>();
            interfaceObject.AddComponent<VEVE.UI.MissionDebriefView>();
            interfaceObject.AddComponent<VEVE.Comms.RadioDispatcher>();
            interfaceObject.AddComponent<VEVE.Net.NetworkGameFlow>();

            GameObject dashboardObject = new GameObject("DebugDashboard");
            dashboardObject.AddComponent<VEVE.Diagnostics.DebugDashboardOverlay>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/VEVE_Milestone1.unity");
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.position = position; cube.transform.localScale = scale; cube.GetComponent<Renderer>().sharedMaterial = material; return cube;
        }

        private static Material MakeMaterial(string name, Color color, float metallic = 0.3f, float smoothness = 0.5f)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/VEVE/" + name + ".mat");
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("No compatible material shader is available.");
            Material material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(material, "Assets/VEVE/" + name + ".mat"); 
            return material;
        }
    }
}
