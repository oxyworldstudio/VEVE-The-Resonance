using UnityEditor;
using UnityEngine;
using VEVE.Content;

namespace VEVE.Editor
{
    /// <summary>
    /// C7 pipeline: materialize the code catalog as editable Resources assets so
    /// designers tune par/alert/objectives/missions in the inspector without
    /// touching code. Idempotent: existing assets keep designer edits to fields we
    /// do not re-export? No - exporter owns payload (single source of truth), the
    /// asset is a serialized view; re-run to refresh.
    /// </summary>
    public static class CatalogAssetExporter
    {
        public const string Folder = "Assets/VEVE/Resources/Generated";

        [MenuItem("VEVE/Content/Export Mission Catalog Assets")]
        public static void ExportMissionCatalog()
        {
            EnsureFolder();
            int created = 0;
            int updated = 0;
            foreach (MissionTemplate t in MissionContentCatalog.All)
            {
                string path = Folder + "/Mission_" + t.id + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<CatalogItemAsset>(path);
                if (existing == null)
                {
                    var asset = ScriptableObject.CreateInstance<CatalogItemAsset>();
                    asset.Configure(CatalogItemKind.Mission, t.id, MissionPayloadCodec.Encode(t));
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else
                {
                    if (existing.Payload != MissionPayloadCodec.Encode(t))
                    {
                        existing.Configure(CatalogItemKind.Mission, t.id, MissionPayloadCodec.Encode(t));
                        EditorUtility.SetDirty(existing);
                        updated++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CatalogAssetExporter] missions: {created} created, {updated} refreshed ({MissionContentCatalog.All.Length} total).");
        }

        [MenuItem("VEVE/Content/Count Generated Assets")]
        public static void CountGenerated()
        {
            var assets = Resources.LoadAll<CatalogItemAsset>(MissionCatalogSource.ResourcesFolder);
            Debug.Log($"[CatalogAssetExporter] loadable mission asset count: {assets?.Length ?? 0}");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/VEVE/Resources"))
                    AssetDatabase.CreateFolder("Assets/VEVE", "Resources");
                AssetDatabase.CreateFolder("Assets/VEVE/Resources", "Generated");
            }
        }
    }
}
