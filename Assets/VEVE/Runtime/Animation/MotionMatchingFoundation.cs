using UnityEngine;
using System.Collections.Generic;

namespace VEVE.Animation
{
    public enum LocomotionState { Idle, Walking, Running, Crouching, Proning, Sprinting, Sliding, Jumping, Falling, Landing }

    public readonly struct PoseFeatures
    {
        public readonly Vector3 footPositionLeft;
        public readonly Vector3 footPositionRight;
        public readonly Vector3 footVelocityLeft;
        public readonly Vector3 footVelocityRight;
        public readonly Vector3 hipPosition;
        public readonly Vector3 hipVelocity;
        public readonly float facingDirection;
        public readonly Vector3 trajectory;

        public PoseFeatures(Vector3 footPositionLeft, Vector3 footPositionRight, Vector3 footVelocityLeft, Vector3 footVelocityRight, Vector3 hipPosition, Vector3 hipVelocity, float facingDirection, Vector3 trajectory)
        {
            this.footPositionLeft = footPositionLeft;
            this.footPositionRight = footPositionRight;
            this.footVelocityLeft = footVelocityLeft;
            this.footVelocityRight = footVelocityRight;
            this.hipPosition = hipPosition;
            this.hipVelocity = hipVelocity;
            this.facingDirection = facingDirection;
            this.trajectory = trajectory;
        }
    }

    public static class MotionMatchingFoundation
    {
        public static float CalculatePoseCost(PoseFeatures current, PoseFeatures target)
        {
            float footPosCost = Vector3.Distance(current.footPositionLeft, target.footPositionLeft) +
                                Vector3.Distance(current.footPositionRight, target.footPositionRight);
            float footVelCost = Vector3.Distance(current.footVelocityLeft, target.footVelocityLeft) +
                                Vector3.Distance(current.footVelocityRight, target.footVelocityRight);
            float hipCost = Vector3.Distance(current.hipPosition, target.hipPosition) +
                            Vector3.Distance(current.hipVelocity, target.hipVelocity);
            float facingCost = Mathf.Abs(current.facingDirection - target.facingDirection);
            float trajCost = Vector3.Distance(current.trajectory, target.trajectory);

            return footPosCost * 0.3f + footVelCost * 0.2f + hipCost * 0.2f + facingCost * 0.1f + trajCost * 0.2f;
        }
    }
}
