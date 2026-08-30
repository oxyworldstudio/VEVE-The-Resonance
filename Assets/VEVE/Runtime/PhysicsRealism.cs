using UnityEngine;
using VEVE.Realism;

namespace VEVE
{
    public sealed class PhysicsRealism : MonoBehaviour
    {
        [SerializeField] private VEVE.Realism.RealismConfig realismConfig;

        private void Start()
        {
            if (realismConfig == null) return;

            Physics.gravity = new Vector3(0f, -realismConfig.StandardGravity, 0f);
            Physics.defaultSolverIterations = realismConfig.PhysicsSolverIterations;
            Physics.defaultSolverVelocityIterations = realismConfig.PhysicsSolverVelocityIterations;
            Physics.bounceThreshold = 0.5f;
            Physics.sleepThreshold = 0.005f;
            Physics.defaultMaxAngularSpeed = 50f;
        }

        public float CalculateDragForce(float velocity, float dragCoefficient, float crossSectionalArea, float airDensity)
        {
            return 0.5f * airDensity * velocity * velocity * dragCoefficient * crossSectionalArea;
        }

        public float CalculateLiftForce(float velocity, float liftCoefficient, float wingArea, float airDensity)
        {
            return 0.5f * airDensity * velocity * velocity * liftCoefficient * wingArea;
        }

        public float CalculateFriction(float normalForce, float frictionCoefficient)
        {
            return normalForce * frictionCoefficient;
        }

        public Vector3 CalculateCollisionResponse(Vector3 velocity, float restitution, float mass, Vector3 normal)
        {
            float velocityAlongNormal = Vector3.Dot(velocity, normal);
            if (velocityAlongNormal > 0f) return velocity;
            float j = -(1f + restitution) * velocityAlongNormal;
            j /= 1f / mass;
            return velocity + j * normal / mass;
        }
    }
}
