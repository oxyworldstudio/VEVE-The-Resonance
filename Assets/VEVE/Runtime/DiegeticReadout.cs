using UnityEngine;
using UnityEngine.UI;
using VEVE.Customization;

namespace VEVE
{
    public sealed class DiegeticReadout : MonoBehaviour
    {
        [SerializeField] private Weapon weapon;
        [SerializeField] private Text readout;
        [SerializeField] private Physiology physiology;
        [SerializeField] private PhysicalInventory inventory;
        [SerializeField] private MovementSimulation movement;
        [SerializeField] private WeaponCustomizationManager customizationManager;
        [SerializeField] private Text weaponStatsText;
        [SerializeField] private Text physiologyStatsText;

        private void Update()
        {
            if (weapon != null && readout != null)
            {
                string weaponLine = "VEVE // WEAPON READOUT\n";
                weaponLine += $"MAG {weapon.RoundsRemaining.ToString("00")}";
                weaponLine += weapon.IsMalfunctioned ? "  MALFUNCTION" : "";

                string weaponMods = weapon.IsMalfunctioned ? "" : "\n";
                string weaponId = weapon != null ? weapon.GetType().Name : "UNKNOWN";
                if (customizationManager != null && !string.IsNullOrEmpty(weaponId))
                {
                    WeaponCustomizationState state = customizationManager.GetState(weaponId);
                    if (!string.IsNullOrEmpty(state.equippedOptic)) weaponMods += $"OPT {state.equippedOptic.ToUpperInvariant()}  ";
                    if (!string.IsNullOrEmpty(state.equippedMuzzle)) weaponMods += $"MUZ {state.equippedMuzzle.ToUpperInvariant()}  ";
                    if (!string.IsNullOrEmpty(state.equippedGrip)) weaponMods += $"GRP {state.equippedGrip.ToUpperInvariant()}  ";
                    if (!string.IsNullOrEmpty(state.equippedStock)) weaponMods += $"STK {state.equippedStock.ToUpperInvariant()}  ";
                    if (!string.IsNullOrEmpty(state.equippedBarrel)) weaponMods += $"BRL {state.equippedBarrel.ToUpperInvariant()}  ";
                }
                weaponLine += weaponMods;

                string condition = physiology == null ? "" : "\n\nVEVE // PHYSIOLOGY";
                condition += physiology == null ? "" : $"\nBLEED {physiology.State.bleeding.ToString("00")}  PAIN {physiology.State.pain.ToString("00")}";
                condition += physiology == null ? "" : $"\nHR {physiology.State.heartRate.ToString("000")}  SPO2 {physiology.State.bloodOxygenSaturation.ToString("00")}";
                condition += physiology == null ? "" : $"\nBP {physiology.State.bloodPressureSystolic:F0}/{physiology.State.bloodPressureDiastolic:F0}";

                string load = inventory == null ? "" : "\n\nVEVE // LOADOUT";
                load += inventory == null ? "" : $"\nLOAD {inventory.UsedVolumeLitres.ToString("0.0")}/{inventory.CapacityLitres.ToString("0.0")} L";

                string posture = movement == null ? "" : "\n\nVEVE // POSTURE";
                posture += movement == null ? "" : $"\n{movement.Posture.ToString().ToUpperInvariant()}";

                readout.text = weaponLine + condition + load + posture +
                    "\n\nWASD  MOVE   SHIFT  SPRINT" +
                    "\nC  CROUCH  Z  PRONE  X  CLIMB" +
                    "\nLMB  FIRE    R  RELOAD  T  INSPECT" +
                    "\nF5  SAVE MISSION   F6  QUICK LOAD" +
                    "\nESC  MENU   TAB  INVENTORY   M  MAP";
            }

            if (physiology != null && physiologyStatsText != null)
            {
                physiologyStatsText.text = $"HR: {physiology.State.heartRate:D3} | PAIN: {physiology.State.pain:D2} | BLEED: {physiology.State.bleeding:D2}";
            }

            if (weapon != null && weaponStatsText != null)
            {
                string weaponId = weapon != null ? weapon.GetType().Name : "UNKNOWN";
                float accuracy = customizationManager != null && !string.IsNullOrEmpty(weaponId)
                    ? customizationManager.CalculateModifiedAccuracy(weaponId, 1.0f)
                    : 1.0f;
                weaponStatsText.text = $"MAG: {weapon.RoundsRemaining:D2} | ACC: {accuracy * 100:F0}% | MALF: {(weapon.IsMalfunctioned ? "YES" : "NO")}";
            }
        }
    }
}
