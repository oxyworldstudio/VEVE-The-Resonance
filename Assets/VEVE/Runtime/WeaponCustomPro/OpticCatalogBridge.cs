using UnityEngine;
using VEVE.Customization;

namespace VEVE.WeaponCustomPro
{
    /// <summary>Raised when a weapon's optic mount was (re)resolved by the Weapon.</summary>
    public sealed class OpticMountedEvent : VEVE.IEvent
    {
        public string weaponId;
        public string scopeId;
        public float fovDegAtMinZoom;
        public float elevationClickMoa;
    }

    /// <summary>
    /// Bridges the real ScopeCatalog into the classic attachment system: each published
    /// optic becomes an attachable AttachmentDefinition (slot Optic, weight from spec,
    /// no fake stat inflation), and the mounted optic is resolvable back to the exact
    /// ScopeProfile whose bore height / click values drive ZeroingSystem + the C1 reticle.
    /// </summary>
    public static class OpticCatalogBridge
    {
        /// <summary>Required operator level tiering: high-magnification glass unlocks later.</summary>
        public const int LevelRequirementBase = 1;
        public const int LevelRequirementHighPower = 6;

        public static int EnsureOpticAttachments(WeaponCustomizationManager manager)
        {
            if (manager == null) return 0;
            int added = 0;
            foreach (ScopeProfile p in ScopeCatalog.All)
            {
                if (p == null || string.IsNullOrEmpty(p.id)) continue;
                var def = new AttachmentDefinition
                {
                    attachmentId = p.id,
                    displayName = p.displayName,
                    slot = AttachmentSlot.Optic,
                    accuracyModifier = 1f,
                    recoilModifier = 1f,
                    rangeModifier = 1f,
                    ergonomicsModifier = Mathf.Clamp(1f - p.weightGrams / 4000f, 0.9f, 1f),
                    weight = p.weightGrams * 0.001f,
                    requiredLevel = p.magnificationMax >= 10f ? LevelRequirementHighPower : LevelRequirementBase
                };
                if (manager.RegisterAttachment(def)) added++;
            }
            return added;
        }

        public static bool TryGetMounted(WeaponCustomizationManager manager, string weaponId, out ScopeProfile scope)
        {
            scope = null;
            if (manager == null || string.IsNullOrEmpty(weaponId)) return false;
            string optic = EquippedOpticId(manager, weaponId);
            // C7: designer scope assets override published values transparently
            return !string.IsNullOrEmpty(optic) && VEVE.Content.ScopeCatalogSource.TryGetScoped(optic, out scope);
        }

        /// <summary>Current equipped-optic id for the weapon (null when slot empty / manager missing).</summary>
        public static string MountedOpticId(WeaponCustomizationManager manager, string weaponId)
        {
            if (manager == null || string.IsNullOrEmpty(weaponId)) return null;
            return EquippedOpticId(manager, weaponId);
        }

        private static string EquippedOpticId(WeaponCustomizationManager manager, string weaponId)
        {
            WeaponCustomizationState state = manager.GetState(weaponId);
            return string.IsNullOrEmpty(state.equippedOptic) ? null : state.equippedOptic;
        }
    }

    /// <summary>
    /// Scene singleton owning the WeaponCustomizationManager instance shared by UI panels
    /// and the Weapon optic-mount resolution (C3). Attaching an optic through any path is
    /// picked up by the polling bridge inside Weapon, so panel and simulation never fork.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponCustomizationHost : MonoBehaviour
    {
        public static WeaponCustomizationHost Instance { get; private set; }

        public WeaponCustomizationManager Customization { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Customization = new WeaponCustomizationManager();
            int added = OpticCatalogBridge.EnsureOpticAttachments(Customization);
            Debug.Log($"[WeaponCustomPro] optic catalog ready ({ScopeCatalog.Count} scope profiles, {added} attachment entries registered).");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
