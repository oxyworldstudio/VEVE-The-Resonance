using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using VEVE.Net;

namespace VEVE.Editor
{
    /// <summary>
    /// C4f: materializes the networked player pawn prefab asset (NGO 2.x
    /// NetworkConfig.PlayerPrefab source of record). Idempotent; committed as an
    /// asset so hosts and clients load identically from Resources.
    /// </summary>
    public static class NetworkedPlayerPrefabBuilder
    {
        public const string PrefabPath = CatalogAssetExporter.Folder + "/RemotePlayer.prefab";

        [MenuItem("VEVE/Content/Build Remote Player Prefab")]
        public static void Build()
        {
            CatalogAssetExporter_SharedFolder.Ensure();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                Debug.Log("[PlayerPrefabBuilder] RemotePlayer.prefab already present.");
                return;
            }

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "RemotePlayer";

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0f, 0f);

            var controller = root.AddComponent<PlayerController>();
            var netObj = root.AddComponent<NetworkObject>(); // NGO promotes IsPlayerObject automatically (read-only, runtime-assigned) from PlayerPrefab spawns
            var avatar = root.AddComponent<NetworkedPlayerAvatar>();
            root.AddComponent<NetworkedPlayerPawn>();
            root.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            // local view hand-off (default off; the avatar enables for the owner only)
            GameObject camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();

            // A (C4f-v2): the pawn's own weapon lives on the owner camera, same
            // wiring as the scene rig - disabled alongside it until local control.
            GameObject pawnGun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pawnGun.name = "PawnCarbine";
            pawnGun.transform.SetParent(camGo.transform, false);
            pawnGun.transform.localPosition = new Vector3(0.3f, -0.25f, 0.7f);
            pawnGun.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
            pawnGun.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.1f, 0.11f, 0.12f) };
            var gunWeapon = pawnGun.AddComponent<Weapon>();
            pawnGun.AddComponent<VEVE.UI.ScopeTelemetryBridge>();
            pawnGun.AddComponent<Maintenance>();
            var pawnGear = root.AddComponent<VEVE.Gear.DamageableGearAdapter>();
            pawnGear.EnsureStarterGear();
            var gunData = new SerializedObject(gunWeapon);
            gunData.FindProperty("aimCamera").objectReferenceValue = cam;
            gunData.FindProperty("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/VEVE/CarbineDefinition.asset");
            gunData.ApplyModifiedPropertiesWithoutUndo();

            camGo.SetActive(false);

            GameObject go = root;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlayerPrefabBuilder] built {(saved != null ? "RemotePlayer" : "FAILED")} at {PrefabPath}");
        }

        internal static class CatalogAssetExporter_SharedFolder
        {
            public static void Ensure()
            {
                if (!AssetDatabase.IsValidFolder(CatalogAssetExporter.Folder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/VEVE/Resources"))
                        AssetDatabase.CreateFolder("Assets/VEVE", "Resources");
                    AssetDatabase.CreateFolder("Assets/VEVE/Resources", "Generated");
                }
            }
        }
    }
}
