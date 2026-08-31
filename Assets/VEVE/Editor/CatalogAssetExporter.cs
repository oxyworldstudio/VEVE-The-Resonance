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

        [MenuItem("VEVE/Content/Export Scope + Weapon + Gear Assets")]
        public static void ExportHardware()
        {
            EnsureFolder();
            int w = 0, s = 0, g = 0;
            foreach (VEVE.Catalog.WeaponSpec spec in VEVE.Catalog.IconicWeaponCatalog.All)
            {
                string path = Folder + "/Weapon_" + spec.id + ".asset";
                var ex = AssetDatabase.LoadAssetAtPath<CatalogItemAsset>(path);
                if (ex == null) { var a = ScriptableObject.CreateInstance<CatalogItemAsset>(); a.Configure(CatalogItemKind.Weapon, spec.id, WeaponPayloadCodec.Encode(spec)); AssetDatabase.CreateAsset(a, path); w++; }
                else { if (ex.Payload != WeaponPayloadCodec.Encode(spec)) { ex.Configure(CatalogItemKind.Weapon, spec.id, WeaponPayloadCodec.Encode(spec)); EditorUtility.SetDirty(ex); } }
            }
            foreach (VEVE.WeaponCustomPro.ScopeProfile p in VEVE.WeaponCustomPro.ScopeCatalog.All)
            {
                string path = Folder + "/Scope_" + p.id + ".asset";
                var ex = AssetDatabase.LoadAssetAtPath<CatalogItemAsset>(path);
                if (ex == null) { var a = ScriptableObject.CreateInstance<CatalogItemAsset>(); a.Configure(CatalogItemKind.Scope, p.id, ScopePayloadCodec.Encode(p)); AssetDatabase.CreateAsset(a, path); s++; }
                else { if (ex.Payload != ScopePayloadCodec.Encode(p)) { ex.Configure(CatalogItemKind.Scope, p.id, ScopePayloadCodec.Encode(p)); EditorUtility.SetDirty(ex); } }
            }
            foreach (VEVE.Gear.GearItem item in VEVE.Gear.GearCatalog.All())
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                string path = Folder + "/Gear_" + item.id + ".asset";
                var ex = AssetDatabase.LoadAssetAtPath<CatalogItemAsset>(path);
                if (ex == null) { var a = ScriptableObject.CreateInstance<CatalogItemAsset>(); a.Configure(CatalogItemKind.Gear, item.id, GearPayloadCodec.Encode(item)); AssetDatabase.CreateAsset(a, path); g++; }
                else { if (ex.Payload != GearPayloadCodec.Encode(item)) { ex.Configure(CatalogItemKind.Gear, item.id, GearPayloadCodec.Encode(item)); EditorUtility.SetDirty(ex); } }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CatalogAssetExporter] hardware export: weapons {w} new, scope {s} new, gear {g} new (+refresh).");
        }

        [MenuItem("VEVE/Content/Export Full Catalog (missions + hardware)")]
        public static void ExportEverything()
        {
            ExportMissionCatalog();
            ExportHardware();
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
