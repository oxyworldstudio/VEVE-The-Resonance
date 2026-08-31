using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Content
{
    /// <summary>
    /// Mission pool resolver: designer-tuned Resources assets take precedence
    /// over the built-in code catalog, and the built-in set is the permanent
    /// fallback (an empty/failed Resources load must never break the campaign).
    /// </summary>
    public static class MissionCatalogSource
    {
        public const string ResourcesFolder = "Generated";

        public static IReadOnlyList<MissionTemplate> Resolve()
        {
            try
            {
                CatalogItemAsset[] assets = Resources.LoadAll<CatalogItemAsset>(ResourcesFolder);
                return Select(assets, null);
            }
            catch (Exception)
            {
                return MissionContentCatalog.All;
            }
        }

        /// <summary>Pure selection rule (unit-testable without any Resource IO).</summary>
        public static IReadOnlyList<MissionTemplate> Select(CatalogItemAsset[] assets, IReadOnlyList<MissionTemplate> fallback)
        {
            IReadOnlyList<MissionTemplate> builtin = fallback ?? MissionContentCatalog.All;
            if (assets == null || assets.Length == 0) return builtin;

            var list = new List<MissionTemplate>(assets.Length);
            foreach (CatalogItemAsset a in assets)
            {
                if (a == null || a.Kind != CatalogItemKind.Mission) continue;
                MissionTemplate decoded = a.AsMission();
                if (string.IsNullOrEmpty(decoded.id)) continue;
                list.Add(decoded);
            }
            return list.Count > 0 ? list : builtin;
        }
    }
}
