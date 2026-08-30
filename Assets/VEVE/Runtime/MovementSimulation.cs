using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public enum OperatorPosture { Standing, Crouched, Prone, Sprinting }

    /// <summary>
    /// Enhanced movement simulation with terrain-specific movement modifiers, noise generation, and stamina system integration.
    /// </summary>
    public sealed class MovementSimulation : MonoBehaviour
    {
        [Header("Posture Configuration")]
        [SerializeField] private OperatorPosture posture = OperatorPosture.Standing;
        [SerializeField, Range(0.1f, 1f)] private float terrainSpeedFactor = 1f;
        [SerializeField, Range(0f, 2f)] private float terrainNoiseFactor = 1f;
        [SerializeField] private TerrainProfile terrainProfile;
        [SerializeField] private float postureTransitionTime = 0.3f;

        [Header("Noise Generation")]
        [SerializeField] private float baseNoiseMultiplier = 8f;
        [SerializeField] private float movementNoiseThreshold = 0.04f;
        [SerializeField] private float noiseCooldownMin = 0.2f;
        [SerializeField] private float noiseCooldownMax = 0.6f;

        [Header("Stamina Integration")]
        [SerializeField] private float sprintStaminaMultiplier = 1f;

        private float noiseCooldown;
        private float postureTransitionTimer;
        private OperatorPosture targetPosture;
        private bool isTransitioning;
        private CharacterController controller;
        private StaminaSystem stamina;
        private PlayerController playerController;

        public OperatorPosture Posture => posture;
        public bool IsTransitioning => isTransitioning;
        public float PostureTransitionProgress => postureTransitionTimer / postureTransitionTime;

        public float SpeedFactor => posture == OperatorPosture.Crouched ? 0.65f :
            posture == OperatorPosture.Prone ? 0.25f : posture == OperatorPosture.Sprinting ? 1.2f : 1f;

        public float NoiseFactor => posture == OperatorPosture.Crouched ? 0.5f :
            posture == OperatorPosture.Prone ? 0.2f : posture == OperatorPosture.Sprinting ? 1.5f : 1f;

        public float TerrainSpeedFactor => terrainProfile == null ? terrainSpeedFactor : terrainProfile.speedFactor;
        public float TerrainNoiseFactor => terrainProfile == null ? terrainNoiseFactor : terrainProfile.noiseFactor;
        public float CurrentNoise => NoiseFactor * TerrainNoiseFactor;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            HandlePostureInput();
            HandleStaminaInput();
            if (isTransitioning)
            {
                UpdatePostureTransition();
            }
            noiseCooldown -= Time.deltaTime;
            if (controller != null && controller.velocity.sqrMagnitude > movementNoiseThreshold && noiseCooldown <= 0f)
            {
                GenerateNoise();
                noiseCooldown = UnityEngine.Random.Range(noiseCooldownMin, noiseCooldownMax);
            }
        }

        public void SetPosture(OperatorPosture newPosture, bool instant = false)
        {
            if (posture == newPosture) return;
            if (instant)
            {
                posture = newPosture;
                isTransitioning = false;
                postureTransitionTimer = 0f;
                OnPostureChangedInstant();
            }
            else
            {
                targetPosture = newPosture;
                isTransitioning = true;
                postureTransitionTimer = 0f;
            }
        }

        public void CancelTransition()
        {
            isTransitioning = false;
            postureTransitionTimer = 0f;
            targetPosture = posture;
        }

        private void HandlePostureInput()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                OperatorPosture nextPosture = posture == OperatorPosture.Standing
                    ? OperatorPosture.Crouched : OperatorPosture.Standing;
                SetPosture(nextPosture);
            }
            if (Input.GetKeyDown(KeyCode.Z))
            {
                SetPosture(OperatorPosture.Prone);
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SetPosture(OperatorPosture.Standing);
            }
        }

        private void HandleStaminaInput()
        {
            if (Input.GetKey(KeyCode.LeftShift) && stamina != null && posture == OperatorPosture.Standing)
            {
                stamina.ConsumeStamina(ActivityType.Sprinting);
                if (posture != OperatorPosture.Sprinting)
                {
                    SetPosture(OperatorPosture.Sprinting);
                }
            }
            else if (posture == OperatorPosture.Sprinting && stamina != null && !stamina.CanSprint)
            {
                SetPosture(OperatorPosture.Standing);
            }
        }

        private void UpdatePostureTransition()
        {
            postureTransitionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(postureTransitionTimer / postureTransitionTime);
            if (progress >= 1f)
            {
                posture = targetPosture;
                isTransitioning = false;
                postureTransitionTimer = 0f;
                OnPostureChangedInstant();
            }
        }

        private void GenerateNoise()
        {
            if (controller == null) return;
            float loudness = Mathf.Clamp(controller.velocity.magnitude * CurrentNoise * baseNoiseMultiplier, 0.5f, 12f);
            TacticalSound.Emit(transform.position, loudness);
        }

        private void OnPostureChangedInstant()
        {
            if (controller != null && controller.height > 0f)
            {
                float targetHeight = posture == OperatorPosture.Standing || posture == OperatorPosture.Sprinting ? 1.8f :
                    posture == OperatorPosture.Crouched ? 1.25f : 0.75f;
                controller.height = Mathf.MoveTowards(controller.height, targetHeight, postureTransitionTime * Time.deltaTime);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, CurrentNoise);
        }
    }
}
