using System;
using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    /// <summary>
    /// Enhanced player controller with realistic movement model including momentum, inertia, and posture transitions.
    /// Vertical integration is routed through the static helpers below so gravity sign and behaviour are unit-testable
    /// without a live physics scene. Gravity is a signed downward acceleration (CODATA standard -9.80665 m/s²).
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
        [SerializeField] private float gravity = DefaultGravity;
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

        /// <summary>
        /// Standard gravitational acceleration magnitude (CODATA). The signed field value
        /// must always be negative (downward in Unity's left-handed Y-up space).
        /// </summary>
        public const float StandardGravityAcceleration = 9.80665f;

        /// <summary>Sentinel velocity that keeps the controller pressed against the ground.</summary>
        public const float GroundedStickVelocity = -2f;

        /// <summary>Default serialized (signed downward) gravity.</summary>
        public const float DefaultGravity = -StandardGravityAcceleration;

        private CharacterController controller;
        private Vector3 velocity;
        private Vector3 lastFrameVelocity;
        private float currentSpeed;
        private float targetSpeed;
        private Physiology physiology;
        private PhysicalInventory inventory;
        private MovementSimulation movement;
        private StaminaSystem stamina;
        private VEVE.Operators.OperatorInstance @operator;
        private float slopeAngle;
        private float postureTransitionProgress;
        private OperatorPosture lastPosture;

        public event Action<OperatorPosture> OnPostureChanged;
        public event Action<float> OnSpeedChanged;

        /// <summary>
        /// Integrates the vertical velocity component for one tick. Order of operations:
        /// ground contact clamp → jump impulse → gravity accumulation (gravity must be negative).
        /// </summary>
        public static float IntegrateVerticalVelocity(
            float velocityY, bool isGrounded, bool jumpPressed, float gravity, float deltaTime, float jumpForce)
        {
            if (isGrounded && velocityY < 0f)
                velocityY = GroundedStickVelocity;

            if (isGrounded && jumpPressed)
                velocityY = Mathf.Abs(jumpForce);

            return velocityY + gravity * Mathf.Max(0f, deltaTime);
        }

        /// <summary>
        /// Verifies the gravity value is physically consistent (downward / non-positive).
        /// A positive gravity accelerates the player off the ground ("launch to the sky").
        /// </summary>
        public static float SanitizeGravity(float configuredGravity)
        {
            return configuredGravity > 0f ? -configuredGravity : configuredGravity;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            physiology = GetComponent<Physiology>();
            inventory = GetComponent<PhysicalInventory>();
            movement = GetComponent<MovementSimulation>();
            stamina = GetComponent<StaminaSystem>();
            @operator = GetComponentInParent<VEVE.Operators.OperatorInstance>();
            lastFrameVelocity = Vector3.zero;
            lastPosture = OperatorPosture.Standing;

            gravity = SanitizeGravity(gravity);
        }

        private void Start()
        {
            if (realismConfig != null)
            {
                walkSpeed = 1.4f;
                sprintSpeed = 2.5f;
                acceleration = 3f;
                gravity = -realismConfig.StandardGravity;
            }
        }

        private void Update()
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputZ = Input.GetAxisRaw("Vertical");
            Vector3 input = Vector3.ClampMagnitude(transform.right * inputX + transform.forward * inputZ, 1f);

            slopeAngle = CalculateSlopeAngle();
            bool canMove = slopeAngle <= maxSlopeAngle;

            OperatorPosture posture = movement != null ? movement.Posture : OperatorPosture.Standing;
            float baseSpeed = GetPostureSpeed(posture);
            float staminaMultiplier = stamina != null ? stamina.GetStaminaSpeedMultiplier() : 1f;
            // Operator/gear feel: traits and carried mass scale the walk and sprint targets.
            // With no OperatorInstance in the chain the multiplier is 1 and behaviour is unchanged.
            float operatorMoveScale = 1f;
            if (@operator != null)
            {
                operatorMoveScale = posture == OperatorPosture.Sprinting
                    ? @operator.SprintSpeedMultiplier
                    : @operator.MoveSpeedMultiplier;
            }
            targetSpeed = input.magnitude * baseSpeed * staminaMultiplier * operatorMoveScale;
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
            Vector3 momentumVelocity = new Vector3(velocity.x, 0f, velocity.z) * momentumPreservation;
            Vector3 finalVelocity = Vector3.Lerp(momentumVelocity, desiredVelocity, controlFactor * 0.5f);
            finalVelocity.y = IntegrateVerticalVelocity(
                velocity.y, controller.isGrounded, Input.GetKeyDown(KeyCode.Space), gravity, Time.deltaTime, jumpForce);

            velocity = finalVelocity;
            controller.Move(velocity * Time.deltaTime);

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
            if (controller == null) return;
            float dampingFactor = 1f - linearDamping * Time.fixedDeltaTime;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            velocity = horizontal * dampingFactor + Vector3.up * velocity.y;
            if (velocity.x * velocity.x + velocity.z * velocity.z < 0.0001f && controller.isGrounded)
            {
                velocity = new Vector3(0f, velocity.y, 0f);
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

        public Vector3 Velocity => velocity;
        public float CurrentSpeed => currentSpeed;
        public float SlopeAngle => slopeAngle;
        public float Mass => mass;
        public float InertiaTensor => inertiaTensor;
        public float Gravity => gravity;
    }
}
