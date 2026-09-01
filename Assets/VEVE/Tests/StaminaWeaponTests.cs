using NUnit.Framework;
using UnityEngine;
using VEVE;
using VEVE.Combat;

public sealed class StaminaWeaponTests
{
    [Test]
    public void ShotCostDrainsSprintEquivalentStamina()
    {
        GameObject owner = new GameObject("StaminaShotCostTest");
        try
        {
            Assert.AreEqual(0.35f, StaminaWeaponBridge.ShotCostSeconds, 1e-6f, "default per-shot cost");
            StaminaSystem stamina = owner.AddComponent<StaminaSystem>();
            stamina.DrainSprint(StaminaWeaponBridge.ShotCostSeconds);
            float drained01 = 1f - stamina.StaminaPercentage;
            Assert.AreEqual(0.35f * 25f / 100f, drained01, 1e-4f,
                "one shot costs ShotCostSeconds of sprint drain (25 pts/s of a 100 pt tank)");
            // a second identical shot costs the same: deterministic, stateless pricing
            stamina.DrainSprint(StaminaWeaponBridge.ShotCostSeconds);
            Assert.AreEqual(0.35f * 2f * 25f / 100f, 1f - stamina.StaminaPercentage, 1e-4f);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void FireFloorRuleBlocksExhaustedRigs()
    {
        GameObject owner = new GameObject("StaminaFloorBridgeTest");
        try
        {
            StaminaSystem stamina = owner.AddComponent<StaminaSystem>();
            StaminaWeaponBridge bridge = owner.AddComponent<StaminaWeaponBridge>();

            Assert.AreEqual(1f, stamina.StaminaPercentage, 1e-5f, "fresh rig starts full");
            Assert.IsFalse(bridge.FireBlockedByExhaustion, "full tank fires");

            // 0.10 normalized: just above the 0.08 floor -> fires
            stamina.DrainSprint((1f - 0.10f) * 100f / 25f);
            Assert.AreEqual(0.10f, stamina.StaminaPercentage, 1e-4f);
            Assert.IsFalse(bridge.FireBlockedByExhaustion);

            // 0.06 normalized: below the 0.08 floor -> blocked
            stamina.DrainSprint(0.04f * 100f / 25f);
            Assert.AreEqual(0.06f, stamina.StaminaPercentage, 1e-4f);
            Assert.IsTrue(bridge.FireBlockedByExhaustion);

            // empty tank stays blocked and drain saturates at zero
            stamina.DrainSprint(10f);
            Assert.AreEqual(0f, stamina.StaminaPercentage, 1e-6f);
            Assert.IsTrue(bridge.FireBlockedByExhaustion);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void SprintSpeedMultiplierIsMonotonicWithFloor()
    {
        GameObject owner = new GameObject("StaminaSprintMultiplierTest");
        try
        {
            StaminaSystem stamina = owner.AddComponent<StaminaSystem>();
            Assert.AreEqual(1f, stamina.SprintSpeedMultiplier, 1e-5f, "full tank: full sprint speed");

            float last = stamina.SprintSpeedMultiplier;
            for (int i = 0; i < 25; i++)
            {
                stamina.DrainSprint(0.2f); // 5 pts per step; 125 pts total saturates the 100 pt tank
                float now = stamina.SprintSpeedMultiplier;
                Assert.LessOrEqual(now, last + 1e-6f, "multiplier never rises as stamina drains");
                Assert.GreaterOrEqual(now, StaminaSystem.SprintSpeedFloor - 1e-5f, "floor holds");
                last = now;
            }
            Assert.AreEqual(0f, stamina.StaminaPercentage, 1e-5f, "tank empty");
            Assert.AreEqual(StaminaSystem.SprintSpeedFloor, last, 1e-5f, "empty tank: 0.4 floor");
            Assert.AreEqual(0.4f, StaminaSystem.SprintSpeedFloor);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
