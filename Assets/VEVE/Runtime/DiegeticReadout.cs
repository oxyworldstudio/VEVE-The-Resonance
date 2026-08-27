using UnityEngine;
using UnityEngine.UI;

namespace VEVE
{
    public sealed class DiegeticReadout : MonoBehaviour
    {
        [SerializeField] private Weapon weapon;
        [SerializeField] private Text readout;
        [SerializeField] private Physiology physiology;
        [SerializeField] private PhysicalInventory inventory;
        [SerializeField] private MovementSimulation movement;

        private void Update()
        {
            if (weapon != null && readout != null)
            {
                string condition = physiology == null ? "" : "\nBLEED " + physiology.State.bleeding.ToString("00") +
                    "  PAIN " + physiology.State.pain.ToString("00") +
                    "  HR " + physiology.State.heartRate.ToString("000");
                string load = inventory == null ? "" : "\nLOAD " + inventory.UsedVolumeLitres.ToString("0.0") + "/" + inventory.CapacityLitres.ToString("0.0") + " L";
                string posture = movement == null ? "" : "\nPOSTURE " + movement.Posture.ToString().ToUpperInvariant();
                readout.text = "VEVE // FIELD TEST\nMAG " + weapon.RoundsRemaining.ToString("00") +
                    (weapon.IsMalfunctioned ? "  ACTION REQUIRED" : "") +
                    condition + load + posture + "\nWASD  MOVE   SHIFT  SPRINT\nC  CROUCH  Z  PRONE\nLMB  FIRE    R  RELOAD\nF5  SAVE MISSION";
            }
        }
    }
}
