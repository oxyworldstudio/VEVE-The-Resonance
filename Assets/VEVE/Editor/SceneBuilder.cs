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
            Material wood = MakeMaterial("Wood", new Color(0.35f, 0.16f, 0.06f));
            Material concrete = MakeMaterial("Concrete", new Color(0.45f, 0.45f, 0.45f));
            CreateCube("Ground", new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40), concrete);
            CreateCube("BuildingFloor", new Vector3(0, 0.05f, 8), new Vector3(12, 0.1f, 10), concrete);
            CreateCube("BuildingWall", new Vector3(0, 2.5f, 13), new Vector3(12, 5, 0.4f), concrete);
            GameObject woodCover = CreateCube("WoodCover", new Vector3(-4, 1.2f, 7), new Vector3(4, 2.4f, 0.3f), wood);
            woodCover.AddComponent<CoverVolume>();
            GameObject environment = new GameObject("EnvironmentSimulation");
            EnvironmentSimulation environmentSimulation = environment.AddComponent<EnvironmentSimulation>();
            environment.AddComponent<MissionRuntime>();
            GameObject simulation = new GameObject("SimulationCoordinator");
            SimulationCoordinator coordinator = simulation.AddComponent<SimulationCoordinator>();
            simulation.AddComponent<SimulationDiagnostics>();
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            SerializedObject environmentData = new SerializedObject(environmentSimulation);
            environmentData.FindProperty("sun").objectReferenceValue = sun;
            environmentData.ApplyModifiedPropertiesWithoutUndo();
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
            SerializedObject coordinatorData = new SerializedObject(coordinator);
            coordinatorData.FindProperty("environment").objectReferenceValue = environmentSimulation;
            coordinatorData.FindProperty("mission").objectReferenceValue = environment.GetComponent<MissionRuntime>();
            coordinatorData.FindProperty("campaign").objectReferenceValue = player.GetComponent<CampaignState>();
            coordinatorData.ApplyModifiedPropertiesWithoutUndo();
            GameObject cameraObject = new GameObject("Eyes");
            cameraObject.transform.SetParent(player.transform); cameraObject.transform.localPosition = new Vector3(0, 0.7f, 0);
            Camera camera = cameraObject.AddComponent<Camera>();
            LookController look = cameraObject.AddComponent<LookController>();
            SerializedObject lookData = new SerializedObject(look); lookData.FindProperty("body").objectReferenceValue = player.transform; lookData.ApplyModifiedPropertiesWithoutUndo();
            GameObject weaponObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weaponObject.name = "ModularCarbine"; weaponObject.transform.SetParent(cameraObject.transform);
            weaponObject.transform.localPosition = new Vector3(0.3f, -0.25f, 0.7f); weaponObject.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
            Weapon weapon = weaponObject.AddComponent<Weapon>();
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
            enemy.name = "Enemy_Torso"; enemy.transform.position = new Vector3(0, 1, 15); enemy.AddComponent<Damageable>();
            EnemyAwareness awareness = enemy.AddComponent<EnemyAwareness>();
            SerializedObject aiData = new SerializedObject(awareness); aiData.FindProperty("target").objectReferenceValue = player.transform; aiData.ApplyModifiedPropertiesWithoutUndo();
            Canvas canvas = new GameObject("WristDisplay").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Text text = new GameObject("Readout").AddComponent<Text>();
            text.transform.SetParent(canvas.transform); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 18; text.color = Color.green;
            RectTransform rect = text.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.anchoredPosition = new Vector2(150, -90); rect.sizeDelta = new Vector2(400, 150);
            DiegeticReadout readout = canvas.gameObject.AddComponent<DiegeticReadout>();
            SerializedObject readoutData = new SerializedObject(readout); readoutData.FindProperty("weapon").objectReferenceValue = weapon; readoutData.FindProperty("readout").objectReferenceValue = text; readoutData.FindProperty("physiology").objectReferenceValue = player.GetComponent<Physiology>(); readoutData.FindProperty("inventory").objectReferenceValue = player.GetComponent<PhysicalInventory>(); readoutData.FindProperty("movement").objectReferenceValue = player.GetComponent<MovementSimulation>(); readoutData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/VEVE_Milestone1.unity");
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.position = position; cube.transform.localScale = scale; cube.GetComponent<Renderer>().sharedMaterial = material; return cube;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/VEVE/" + name + ".mat");
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("No compatible material shader is available.");
            Material material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, "Assets/VEVE/" + name + ".mat"); return material;
        }
    }
}
