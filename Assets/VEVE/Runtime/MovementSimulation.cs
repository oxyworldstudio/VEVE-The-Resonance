using UnityEngine;

namespace VEVE
{
    public enum OperatorPosture { Standing, Crouched, Prone }

    public sealed class MovementSimulation : MonoBehaviour
    {
        [SerializeField] private OperatorPosture posture = OperatorPosture.Standing;
        [SerializeField, Range(0.1f, 1f)] private float terrainSpeedFactor = 1f;
        [SerializeField, Range(0f, 2f)] private float terrainNoiseFactor = 1f;
        [SerializeField] private TerrainProfile terrainProfile;
        private float noiseCooldown;

        public OperatorPosture Posture => posture;
        public float SpeedFactor => posture == OperatorPosture.Crouched ? 0.65f :
            posture == OperatorPosture.Prone ? 0.25f : 1f;
        public float NoiseFactor => posture == OperatorPosture.Crouched ? 0.5f :
            posture == OperatorPosture.Prone ? 0.2f : 1f;
        public float TerrainSpeedFactor => terrainProfile == null ? terrainSpeedFactor : terrainProfile.speedFactor;
        public float TerrainNoiseFactor => terrainProfile == null ? terrainNoiseFactor : terrainProfile.noiseFactor;
        public float CurrentNoise => NoiseFactor * TerrainNoiseFactor;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) posture = posture == OperatorPosture.Standing
                ? OperatorPosture.Crouched : OperatorPosture.Standing;
            if (Input.GetKeyDown(KeyCode.Z)) posture = OperatorPosture.Prone;
            noiseCooldown -= Time.deltaTime;
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null && controller.velocity.sqrMagnitude > 0.04f && noiseCooldown <= 0f)
            {
                float loudness = Mathf.Clamp(controller.velocity.magnitude * CurrentNoise * 8f, 0.5f, 12f);
                TacticalSound.Emit(transform.position, loudness);
                noiseCooldown = 0.35f;
            }
        }
    }
}
