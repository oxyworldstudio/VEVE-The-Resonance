using UnityEngine;

namespace VEVE.Graphics
{
    /// <summary>
    /// Scene-side carrier for a <c>VEVE.Content.BiomeSceneProfile</c> so the
    /// <see cref="AtmosphereTintBridge"/> can resolve the biome fog bias without touching
    /// the editor SceneBuilder. The orchestrator (or SceneBuilder wiring guidance) sets the
    /// biome key once; the profile is resolved through
    /// <c>VEVE.Content.BiomeSceneProfiles.TryGet</c> and cached. Unknown or empty keys fall
    /// back to the town baseline, mirroring catalog behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiomeProfileHolder : MonoBehaviour
    {
        [SerializeField] private string biomeKey = "MEDIUM_TOWN";

        private VEVE.Content.BiomeSceneProfile? cached;

        /// <summary>Gets or sets the biome key used to resolve the profile from the catalog.</summary>
        public string BiomeKey
        {
            get => biomeKey;
            set
            {
                biomeKey = value;
                cached = null;
            }
        }

        /// <summary>
        /// Gets the resolved profile, caching per key. Empty/unknown keys resolve to the
        /// town baseline exactly like <c>BiomeSceneProfiles.TryGet</c>.
        /// </summary>
        public VEVE.Content.BiomeSceneProfile Profile
        {
            get
            {
                if (!cached.HasValue)
                {
                    VEVE.Content.BiomeSceneProfiles.TryGet(biomeKey, out var profile);
                    cached = profile;
                }

                return cached.Value;
            }
        }
    }
}
