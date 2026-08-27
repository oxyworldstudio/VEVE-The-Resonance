using UnityEngine;

namespace VEVE
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float gravity = -20f;
        private CharacterController controller;
        private Vector3 velocity;
        private float currentSpeed;
        private Physiology physiology;
        private PhysicalInventory inventory;
        private MovementSimulation movement;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            physiology = GetComponent<Physiology>();
            inventory = GetComponent<PhysicalInventory>();
            movement = GetComponent<MovementSimulation>();
        }

        private void Update()
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputZ = Input.GetAxisRaw("Vertical");
            Vector3 input = Vector3.ClampMagnitude(transform.right * inputX + transform.forward * inputZ, 1f);
            float target = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
            if (physiology != null) target *= physiology.MovementFactor;
            if (inventory != null) target *= Mathf.Lerp(1f, 0.72f, inventory.LoadRatio);
            if (movement != null) target *= movement.SpeedFactor * movement.TerrainSpeedFactor;
            if (movement != null && controller.height > 0f)
            {
                float targetHeight = movement.Posture == OperatorPosture.Standing ? 1.8f :
                    movement.Posture == OperatorPosture.Crouched ? 1.25f : 0.75f;
                controller.height = Mathf.MoveTowards(controller.height, targetHeight, 4f * Time.deltaTime);
            }
            currentSpeed = Mathf.MoveTowards(currentSpeed, input.magnitude * target, acceleration * Time.deltaTime);
            controller.Move(input * currentSpeed * Time.deltaTime);
            if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
