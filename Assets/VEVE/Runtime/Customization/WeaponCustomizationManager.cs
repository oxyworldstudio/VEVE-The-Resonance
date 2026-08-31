using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VEVE.Customization
{
    public class WeaponCustomizationManager
    {
        private List<AttachmentDefinition> availableAttachments;
        private Dictionary<string, WeaponCustomizationState> weaponStates;

        public WeaponCustomizationManager()
        {
            availableAttachments = new List<AttachmentDefinition>();
            weaponStates = new Dictionary<string, WeaponCustomizationState>();
            InitializeDefaultAttachments();
        }

        private void InitializeDefaultAttachments()
        {
            availableAttachments.Add(new AttachmentDefinition
            {
                attachmentId = "optic_holo",
                displayName = "Holographic Sight",
                slot = AttachmentSlot.Optic,
                accuracyModifier = 1.05f,
                recoilModifier = 1.0f,
                rangeModifier = 1.1f,
                fireRateModifier = 1.0f,
                ergonomicsModifier = 0.95f,
                weight = 0.1f,
                requiredLevel = 3
            });

            availableAttachments.Add(new AttachmentDefinition
            {
                attachmentId = "muzzle_comp",
                displayName = "Compensator",
                slot = AttachmentSlot.Muzzle,
                accuracyModifier = 1.0f,
                recoilModifier = 0.85f,
                rangeModifier = 1.0f,
                fireRateModifier = 1.0f,
                ergonomicsModifier = 0.98f,
                weight = 0.05f,
                requiredLevel = 5
            });

            availableAttachments.Add(new AttachmentDefinition
            {
                attachmentId = "grip_ergonomic",
                displayName = "Ergonomic Grip",
                slot = AttachmentSlot.Grip,
                accuracyModifier = 1.02f,
                recoilModifier = 0.9f,
                rangeModifier = 1.0f,
                fireRateModifier = 1.0f,
                ergonomicsModifier = 1.1f,
                weight = -0.05f,
                requiredLevel = 2
            });

            availableAttachments.Add(new AttachmentDefinition
            {
                attachmentId = "stock_tactical",
                displayName = "Tactical Stock",
                slot = AttachmentSlot.Stock,
                accuracyModifier = 1.03f,
                recoilModifier = 0.88f,
                rangeModifier = 1.0f,
                fireRateModifier = 1.0f,
                ergonomicsModifier = 1.05f,
                weight = 0.1f,
                requiredLevel = 4
            });

            availableAttachments.Add(new AttachmentDefinition
            {
                attachmentId = "mag_extended",
                displayName = "Extended Magazine",
                slot = AttachmentSlot.Magazine,
                accuracyModifier = 1.0f,
                recoilModifier = 1.0f,
                rangeModifier = 1.0f,
                fireRateModifier = 1.0f,
                ergonomicsModifier = 0.95f,
                weight = 0.2f,
                requiredLevel = 1
            });

            availableAttachments.Add(new AttachmentDefinition
            {
                attachmentId = "barrel_long",
                displayName = "Long Barrel",
                slot = AttachmentSlot.Barrel,
                accuracyModifier = 1.08f,
                recoilModifier = 1.0f,
                rangeModifier = 1.2f,
                fireRateModifier = 0.95f,
                ergonomicsModifier = 0.9f,
                weight = 0.3f,
                requiredLevel = 6
            });
        }

        public bool CanAttach(string weaponId, string attachmentId)
        {
            var attachment = availableAttachments.FirstOrDefault(a => a.attachmentId == attachmentId);
            if (attachment.attachmentId == null) return false;

            if (!weaponStates.ContainsKey(weaponId))
            {
                weaponStates[weaponId] = new WeaponCustomizationState { weaponId = weaponId };
            }

            var state = weaponStates[weaponId];
            bool slotOccupied = IsSlotOccupied(state, attachment.slot);
            return !slotOccupied;
        }

        public bool Attach(string weaponId, string attachmentId)
        {
            if (!CanAttach(weaponId, attachmentId)) return false;

            var attachment = availableAttachments.First(a => a.attachmentId == attachmentId);
            var state = weaponStates[weaponId];

            switch (attachment.slot)
            {
                case AttachmentSlot.Optic: state.equippedOptic = attachmentId; break;
                case AttachmentSlot.Muzzle: state.equippedMuzzle = attachmentId; break;
                case AttachmentSlot.Grip: state.equippedGrip = attachmentId; break;
                case AttachmentSlot.Stock: state.equippedStock = attachmentId; break;
                case AttachmentSlot.Magazine: state.equippedMagazine = attachmentId; break;
                case AttachmentSlot.Barrel: state.equippedBarrel = attachmentId; break;
                case AttachmentSlot.Laser: state.equippedLaser = attachmentId; break;
            }

            weaponStates[weaponId] = state;
            return true;
        }

        public bool Detach(string weaponId, AttachmentSlot slot)
        {
            if (!weaponStates.ContainsKey(weaponId)) return false;

            var state = weaponStates[weaponId];
            switch (slot)
            {
                case AttachmentSlot.Optic: state.equippedOptic = null; break;
                case AttachmentSlot.Muzzle: state.equippedMuzzle = null; break;
                case AttachmentSlot.Grip: state.equippedGrip = null; break;
                case AttachmentSlot.Stock: state.equippedStock = null; break;
                case AttachmentSlot.Magazine: state.equippedMagazine = null; break;
                case AttachmentSlot.Barrel: state.equippedBarrel = null; break;
                case AttachmentSlot.Laser: state.equippedLaser = null; break;
                default: return false;
            }

            weaponStates[weaponId] = state;
            return true;
        }

        private static float Neutralize(float modifier)
        {
            return modifier > 0f ? modifier : 1f;
        }

        public float CalculateModifiedAccuracy(string weaponId, float baseAccuracy)
        {
            var state = GetState(weaponId);
            float modifier = Neutralize(GetAttachmentModifier(state.equippedOptic, a => a.accuracyModifier));
            modifier *= Neutralize(GetAttachmentModifier(state.equippedMuzzle, a => a.accuracyModifier));
            modifier *= Neutralize(GetAttachmentModifier(state.equippedGrip, a => a.accuracyModifier));
            modifier *= Neutralize(GetAttachmentModifier(state.equippedStock, a => a.accuracyModifier));
            return baseAccuracy * modifier;
        }

        public float CalculateModifiedRecoil(string weaponId, float baseRecoil)
        {
            var state = GetState(weaponId);
            float modifier = Neutralize(GetAttachmentModifier(state.equippedMuzzle, a => a.recoilModifier));
            modifier *= Neutralize(GetAttachmentModifier(state.equippedGrip, a => a.recoilModifier));
            modifier *= Neutralize(GetAttachmentModifier(state.equippedStock, a => a.recoilModifier));
            return baseRecoil * modifier;
        }

        public float CalculateModifiedRange(string weaponId, float baseRange)
        {
            var state = GetState(weaponId);
            float modifier = Neutralize(GetAttachmentModifier(state.equippedOptic, a => a.rangeModifier));
            modifier *= Neutralize(GetAttachmentModifier(state.equippedBarrel, a => a.rangeModifier));
            return baseRange * modifier;
        }

        public float CalculateTotalWeight(string weaponId, float baseWeight)
        {
            var state = GetState(weaponId);
            float totalWeight = baseWeight;
            totalWeight += GetAttachmentModifier(state.equippedOptic, a => a.weight);
            totalWeight += GetAttachmentModifier(state.equippedMuzzle, a => a.weight);
            totalWeight += GetAttachmentModifier(state.equippedGrip, a => a.weight);
            totalWeight += GetAttachmentModifier(state.equippedStock, a => a.weight);
            totalWeight += GetAttachmentModifier(state.equippedMagazine, a => a.weight);
            totalWeight += GetAttachmentModifier(state.equippedBarrel, a => a.weight);
            totalWeight += GetAttachmentModifier(state.equippedLaser, a => a.weight);
            return totalWeight;
        }

        /// <summary>
        /// Registers an extra attachment definition (idempotent: an existing id is kept,
        /// returns false). Used by the optic catalog bridge to expose real scopes.
        /// </summary>
        public bool RegisterAttachment(AttachmentDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.attachmentId)) return false;
            for (int i = 0; i < availableAttachments.Count; i++)
            {
                if (availableAttachments[i].attachmentId == definition.attachmentId) return false;
            }
            availableAttachments.Add(definition);
            return true;
        }

        public List<AttachmentDefinition> GetAvailableAttachments(int playerLevel)
        {
            return availableAttachments.Where(a => a.requiredLevel <= playerLevel).ToList();
        }

        public List<AttachmentDefinition> GetAttachmentsForSlot(AttachmentSlot slot, int playerLevel)
        {
            return availableAttachments.Where(a => a.slot == slot && a.requiredLevel <= playerLevel).ToList();
        }

        public WeaponCustomizationState GetState(string weaponId)
        {
            if (!weaponStates.ContainsKey(weaponId))
            {
                weaponStates[weaponId] = new WeaponCustomizationState { weaponId = weaponId };
            }
            return weaponStates[weaponId];
        }

        private bool IsSlotOccupied(WeaponCustomizationState state, AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Optic: return !string.IsNullOrEmpty(state.equippedOptic);
                case AttachmentSlot.Muzzle: return !string.IsNullOrEmpty(state.equippedMuzzle);
                case AttachmentSlot.Grip: return !string.IsNullOrEmpty(state.equippedGrip);
                case AttachmentSlot.Stock: return !string.IsNullOrEmpty(state.equippedStock);
                case AttachmentSlot.Magazine: return !string.IsNullOrEmpty(state.equippedMagazine);
                case AttachmentSlot.Barrel: return !string.IsNullOrEmpty(state.equippedBarrel);
                case AttachmentSlot.Laser: return !string.IsNullOrEmpty(state.equippedLaser);
                default: return false;
            }
        }

        private float GetAttachmentModifier(string attachmentId, System.Func<AttachmentDefinition, float> selector)
        {
            if (string.IsNullOrEmpty(attachmentId)) return 1.0f;
            var attachment = availableAttachments.FirstOrDefault(a => a.attachmentId == attachmentId);
            if (attachment.attachmentId == null) return 1.0f;
            return selector(attachment);
        }
    }
}
