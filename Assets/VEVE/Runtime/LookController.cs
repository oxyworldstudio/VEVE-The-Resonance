using UnityEngine;
using VEVE.Operators;

namespace VEVE
{
    public sealed class LookController : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private float sensitivity = 2.2f;
        [SerializeField] private Weapon weapon;
        private float pitch;
        private OperatorInstance @operator;

        private void Start()
        {
            if (weapon == null) weapon = GetComponentInChildren<Weapon>();
            @operator = GetComponentInParent<OperatorInstance>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            // Sway input from the operator feel cache: aim stability attenuates recoil sway
            // amplitude, sway recovery accelerates the pull back toward centre. No OperatorInstance
            // in the parent chain => both are 1 and behaviour is byte-identical to before.
            float aimStability = @operator != null ? @operator.AimStabilityMultiplier : 1f;
            float swayRecovery = @operator != null ? @operator.SwayRecoveryMultiplier : 1f;
            float recoilOffset = weapon == null ? 0f : weapon.Recoil * 0.35f;
            recoilOffset /= Mathf.Max(0.25f, aimStability);
            recoilOffset *= Mathf.Max(0.25f, swayRecovery);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity - recoilOffset * Time.deltaTime, -85f, 85f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            body.Rotate(Vector3.up * Input.GetAxis("Mouse X") * sensitivity);
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
        }
    }
}
