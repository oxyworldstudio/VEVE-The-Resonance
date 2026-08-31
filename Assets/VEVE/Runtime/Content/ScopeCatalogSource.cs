using System;
using System.Collections.Generic;
using UnityEngine;
using VEVE.WeaponCustomPro;

namespace VEVE.Content
{
    /// <summary>
    /// Optic catalog resolver: published ScopeCatalog entries stay the base;
    /// designer scope assets override-by-id or append. Offline behavior is
    /// unchanged (empty Resources = built-ins, no allocations beyond a clone).
    /// </summary>
    public static class ScopeCatalogSource
    {
        public static IReadOnlyList<ScopeProfile> Resolve()
        {
            try
            {
                CatalogItemAsset[] assets = Resources.LoadAll<CatalogItemAsset>(CatalogFolder);
                return Select(assets);
            }
            catch (Exception)
            {
                return ScopeCatalog.All;
            }
        }

        public static bool TryGetScoped(string id, out ScopeProfile profile)
        {
            profile = null;
            if (string.IsNullOrEmpty(id)) return false;
            IReadOnlyList<ScopeProfile> merged = Resolve();
            for (int i = 0; i < merged.Count; i++)
            {
                if (string.Equals(merged[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    profile = merged[i];
                    return true;
                }
            }
            return false;
        }

        public const string CatalogFolder = MissionCatalogSource.ResourcesFolder;

        /// <summary>Pure merge: builtin order preserved, same-id assets override, unknown assets append; bad payloads skipped, nothing throws.</summary>
        public static IReadOnlyList<ScopeProfile> Select(CatalogItemAsset[] assets)
        {
            IReadOnlyList<ScopeProfile> builtins = ScopeCatalog.All;
            if (assets == null || assets.Length == 0) return builtins;

            var result = new List<ScopeProfile>(builtins.Count + assets.Length);
            var overridden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ScopeProfile b in builtins) result.Add(b);

            foreach (CatalogItemAsset a in assets)
            {
                if (a == null || a.Kind != CatalogItemKind.Scope) continue;
                ScopeProfile decoded;
                try { decoded = ScopePayloadCodec.Decode(a.Payload); }
                catch (Exception) { continue; }
                if (decoded == null || string.IsNullOrEmpty(decoded.id)) continue;
                overridden.Add(decoded.id);
                for (int i = 0; i < result.Count; i++)
                {
                    if (string.Equals(result[i].id, decoded.id, StringComparison.OrdinalIgnoreCase))
                    {
                        result[i] = decoded;
                        goto nextAsset;
                    }
                }
                result.Add(decoded);
                nextAsset:;
            }
            return result;
        }
    }
}
