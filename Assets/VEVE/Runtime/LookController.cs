using UnityEngine;

namespace VEVE
{
    public sealed class LookController : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private float sensitivity = 2.2f;
        [SerializeField] private Weapon weapon;
        private float pitch;

        private void Start()
        {
            if (weapon == null) weapon = GetComponentInChildren<Weapon>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            float recoilOffset = weapon == null ? 0f : weapon.Recoil * 0.35f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity - recoilOffset * Time.deltaTime, -85f, 85f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            body.Rotate(Vector3.up * Input.GetAxis("Mouse X") * sensitivity);
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
        }
    }
}
