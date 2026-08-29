using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Customization
{
    public enum AttachmentSlot { Rail, Muzzle, Optic, Magazine, Grip, Stock, Barrel, Laser }

    [System.Serializable]
    public struct AttachmentDefinition
    {
        public string attachmentId;
        public string displayName;
        public AttachmentSlot slot;
        public float accuracyModifier;
        public float recoilModifier;
        public float rangeModifier;
        public float fireRateModifier;
        public float ergonomicsModifier;
        public float weight;
        public int requiredLevel;
    }

    [System.Serializable]
    public struct WeaponCustomizationState
    {
        public string weaponId;
        public string equippedOptic;
        public string equippedMuzzle;
        public string equippedGrip;
        public string equippedStock;
        public string equippedMagazine;
        public string equippedBarrel;
        public string equippedLaser;
    }

    public static class CustomizationPresets
    {
        private static Dictionary<string, WeaponCustomizationState> presets = new Dictionary<string, WeaponCustomizationState>();

        public static void SavePreset(string presetName, WeaponCustomizationState state)
        {
            presets[presetName] = state;
        }

        public static bool LoadPreset(string presetName, out WeaponCustomizationState state)
        {
            return presets.TryGetValue(presetName, out state);
        }

        public static void DeletePreset(string presetName)
        {
            presets.Remove(presetName);
        }

        public static List<string> GetPresetNames()
        {
            return new List<string>(presets.Keys);
        }
    }
}
