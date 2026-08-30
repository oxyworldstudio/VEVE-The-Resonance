using System;
using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    /// <summary>
    /// Enhanced player controller with realistic movement model including momentum, inertia, and posture transitions.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement Parameters")]
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float sprintSpeed = 2.5f;
        [SerializeField] private float crouchSpeed = 0.91f;
        [SerializeField] private float proneSpeed = 0.35f;
        [SerializeField] private float acceleration = 3f;
        [SerializeField] private float deceleration = 6f;
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float jumpForce = 3f;
        [SerializeField] private float mass = 80f;
        [SerializeField] private float airControl = 0.4f;
        [SerializeField] private float groundControl = 1f;
        [SerializeField] private float momentumPreservation = 0.85f;
        [SerializeField] private float postureTransitionSpeed = 4f;
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("Inertia Parameters")]
        [SerializeField] private float linearDamping = 0.1f;
        [SerializeField] private float angularDamping = 0.2f;
        [SerializeField] private float inertiaTensor = 10f;

        [SerializeField] private RealismConfig realismConfig;

        private CharacterController controller;
        private Vector3 velocity;
        private Vector3 lastFrameVelocity;
        private float currentSpeed;
        private float targetSpeed;
        private Physiology physiology;
        private PhysicalInventory inventory;
        private MovementSimulation movement;
        private StaminaSystem stamina;
        private float slopeAngle;
        private float postureTransitionProgress;
        private OperatorPosture lastPosture;

        public event Action<OperatorPosture> OnPostureChanged;
        public event Action<float> OnSpeedChanged;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            physiology = GetComponent<Physiology>();
            inventory = GetComponent<PhysicalInventory>();
            movement = GetComponent<MovementSimulation>();
            stamina = GetComponent<StaminaSystem>();
            lastFrameVelocity = Vector3.zero;
            lastPosture = OperatorPosture.Standing;
        }

        private void Update()
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputZ = Input.GetAxisRaw("Vertical");
            Vector3 input = Vector3.ClampMagnitude(transform.right * inputX + transform.forward * inputZ, 1f);

            slopeAngle = CalculateSlopeAngle();
            bool canMove = slopeAngle <= maxSlopeAngle;

            float baseSpeed = GetPostureSpeed(movement != null ? movement.Posture : OperatorPosture.Standing);
            float staminaMultiplier = stamina != null ? stamina.GetStaminaSpeedMultiplier() : 1f;
            targetSpeed = input.magnitude * baseSpeed * staminaMultiplier;
            if (physiology != null) targetSpeed *= physiology.MovementFactor;
            if (inventory != null) targetSpeed *= Mathf.Lerp(1f, 0.72f, inventory.LoadRatio);
            if (movement != null) targetSpeed *= movement.SpeedFactor * movement.TerrainSpeedFactor;
            targetSpeed *= canMove ? 1f : 0.3f;

            if (movement != null && movement.Posture != lastPosture)
            {
                OnPostureChanged?.Invoke(movement.Posture);
                lastPosture = movement.Posture;
            }

            float controlFactor = controller.isGrounded ? groundControl : airControl;
            float accel = input.magnitude > 0.01f ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * controlFactor * Time.deltaTime);

            Vector3 desiredVelocity = input * currentSpeed;
            Vector3 momentumVelocity = velocity * momentumPreservation;
            Vector3 finalVelocity = Vector3.Lerp(momentumVelocity, desiredVelocity, controlFactor * 0.5f);
            finalVelocity.y = velocity.y;

            if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
            if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded) velocity.y = jumpForce;
            velocity.y += gravity * Time.deltaTime;

            Vector3 movementDelta = (finalVelocity * Time.deltaTime) + (velocity * Time.deltaTime);
            controller.Move(movementDelta);

            OnSpeedChanged?.Invoke(currentSpeed);
            lastFrameVelocity = velocity;
        }

        private void FixedUpdate()
        {
            ApplyInertialDamping();
        }

        private float CalculateSlopeAngle()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.5f))
            {
                return Vector3.Angle(hit.normal, Vector3.up);
            }
            return 0f;
        }

        private float GetPostureSpeed(OperatorPosture posture)
        {
            return posture switch
            {
                OperatorPosture.Crouched => crouchSpeed,
                OperatorPosture.Prone => proneSpeed,
                OperatorPosture.Sprinting => sprintSpeed,
                _ => walkSpeed
            };
        }

        private void ApplyInertialDamping()
        {
            float dampingFactor = 1f - linearDamping * Time.fixedDeltaTime;
            velocity *= dampingFactor;
            if (velocity.magnitude < 0.01f && controller.isGrounded)
            {
                velocity = Vector3.zero;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y < 0.7f && velocity.y > 0f)
            {
                velocity.y = 0f;
            }
            Vector3 pushDirection = hit.normal * 0.5f;
            velocity += pushDirection * hit.moveLength;
        }

        private void Start()
        {
            if (realismConfig != null)
            {
                walkSpeed = 1.4f;
                sprintSpeed = 2.5f;
                acceleration = 3f;
                gravity = realismConfig.StandardGravity;
            }
        }

        public Vector3 Velocity => velocity;
        public float CurrentSpeed => currentSpeed;
        public float SlopeAngle => slopeAngle;
        public float Mass => mass;
        public float InertiaTensor => inertiaTensor;
    }
}
