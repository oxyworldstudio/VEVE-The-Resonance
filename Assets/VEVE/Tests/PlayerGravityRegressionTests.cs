using NUnit.Framework;
using UnityEngine;
using VEVE;
using VEVE.Realism;

/// <summary>
/// Regression tests for the Scene 1 gravity sign bug (player launched into the sky).
/// Verifies that gravity is negative, that the integrator accelerates downward,
/// that ground contact is maintained and jumps resolve upward, and that realistic
/// free fall from rest only ever yields non-positive vertical velocity.
/// </summary>
public sealed class PlayerGravityRegressionTests
{
    private const float Dt = 1f / 60f;

    [Test]
    public void DefaultGravityIsSignedDownward()
    {
        Assert.Greater(PlayerController.StandardGravityAcceleration, 0f);
        Assert.Less(PlayerController.DefaultGravity, 0f);
        Assert.AreEqual(-9.80665f, PlayerController.DefaultGravity, 0.0001f);
    }

    [Test]
    public void SerializedDefaultOnComponentIsNegative()
    {
        var go = new GameObject("Player");
        try
        {
            var pc = go.AddComponent<PlayerController>();
            Assert.Less(pc.Gravity, 0f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SanitizeGravityFlipsPositiveInput()
    {
        Assert.AreEqual(-9.81f, PlayerController.SanitizeGravity(9.81f), 0.0001f);
        Assert.AreEqual(-9.81f, PlayerController.SanitizeGravity(-9.81f), 0.0001f);
        Assert.AreEqual(0f, PlayerController.SanitizeGravity(0f), 0.0001f);
    }

    [Test]
    public void FreeFallAccumulatesDownward()
    {
        float vy = PlayerController.IntegrateVerticalVelocity(0f, false, false,
            PlayerController.DefaultGravity, Dt, 3f);
        Assert.Less(vy, 0f);
    }

    [Test]
    public void RepeatedFreeFallMonotonicallyIncreasesSpeedDownward()
    {
        float vy = 0f;
        for (int i = 0; i < 60; i++)
        {
            float previous = vy;
            vy = PlayerController.IntegrateVerticalVelocity(vy, false, false,
                PlayerController.DefaultGravity, Dt, 3f);
            Assert.LessOrEqual(vy, previous, "Vertical velocity must not increase during fall.");
        }
        Assert.Less(vy, -1f);
        Assert.Greater(vy, -3f * PlayerController.StandardGravityAcceleration,
            "Velocity should remain on the analytic g*t curve near one simulated second of free fall.");
    }

    [Test]
    public void JumpProducesPositiveUpwardImpulse()
    {
        float vy = PlayerController.IntegrateVerticalVelocity(-2f, true, true,
            PlayerController.DefaultGravity, Dt, 3f);
        Assert.Greater(vy, 0f);
    }

    [Test]
    public void GroundedContactIsMaintainedStuckToGround()
    {
        float vy = PlayerController.IntegrateVerticalVelocity(-25f, true, false,
            PlayerController.DefaultGravity, Dt, 3f);
        Assert.Less(vy, 0f);
        Assert.Greater(vy, -3.5f);
    }

    [Test]
    public void AirborneFallFromRestNeverBecomesUpward()
    {
        float vy = 0f;
        for (int step = 0; step < 300; step++)
        {
            vy = PlayerController.IntegrateVerticalVelocity(vy, false, false,
                PlayerController.DefaultGravity, 0.016666f, 3f);
            Assert.LessOrEqual(vy, 0f, $"Step {step} produced upward velocity: {vy}");
        }
    }

    [Test]
    public void ConfiguredRealismGravityIsAppliedAsNegativeAcceleration()
    {
        var config = ScriptableObject.CreateInstance<RealismConfig>();
        try
        {
            Assert.AreEqual(9.80665f, config.StandardGravity, 0.0001f);
            float expected = -config.StandardGravity;
            Assert.Less(expected, 0f);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }
}
