using NUnit.Framework;
using UnityEngine;
using VEVE.Graphics;

public sealed class MaterialPhysicsTests
{
    [Test]
    public void WetFrictionDropsMonotonicallyWithFloor()
    {
        Assert.AreEqual(0.8f, MaterialPhysics.FrictionMultiplier(0.8f, 0f), 1e-5f);
        Assert.Less(MaterialPhysics.FrictionMultiplier(0.8f, 1f), MaterialPhysics.FrictionMultiplier(0.8f, 0.5f));
        Assert.GreaterOrEqual(MaterialPhysics.FrictionMultiplier(0.8f, 1f), 0.3f, "multiplicative model: 0.8*(1-0.55) = 0.36");
        // deep-wet on a slick surface reaches the absolute floor
        Assert.AreEqual(0.05f, MaterialPhysics.FrictionMultiplier(0.05f, 9f), 1e-5f, "clamped at floor");
    }

    [Test]
    public void SpecularBoostSaturatesTowardOne()
    {
        Assert.AreEqual(0.3f, MaterialPhysics.SpecularBoost(0.3f, 0f), 1e-5f);
        Assert.AreEqual(0.79f, MaterialPhysics.SpecularBoost(0.3f, 1f), 1e-5f, "0.3 + 0.7*0.7");
        Assert.AreEqual(1f, MaterialPhysics.SpecularBoost(1f, 1f), 1e-5f, "already-perfect stays perfect");
        Assert.AreEqual(0.97f, MaterialPhysics.SpecularBoost(0.9f, 1f), 1e-5f, "0.9 + 0.1*0.7 saturates toward one");
    }

    [Test]
    public void IrSignatureRespondsToHeatAndWet()
    {
        float hot = MaterialPhysics.IrSignature(0.5f, 55f, 0f);
        float cold = MaterialPhysics.IrSignature(0.5f, 15f, 0f);
        Assert.Greater(hot, cold, "hot surfaces glow");
        Assert.LessOrEqual(MaterialPhysics.IrSignature(0.9f, 60f, 1f), 1f);
        Assert.Less(MaterialPhysics.IrSignature(0.9f, 60f, 1f), MaterialPhysics.IrSignature(0.9f, 60f, 0f), "wet = dark in IR");
    }

    [Test]
    public void BlastAbsorptionPerSurfaceClass()
    {
        float sandAbs = MaterialPhysics.BlastAbsorptionFactor("Sand", 30f, 0f);
        float metalAbs = MaterialPhysics.BlastAbsorptionFactor("Metal", 30f, 0f);
        Assert.Greater(sandAbs, metalAbs, "sand eats blast energy, metal reflects");
        // frozen ground: frostBonus REDUCES absorption (more spall/reflection)
        Assert.Less(MaterialPhysics.BlastAbsorptionFactor("Sand", -12f, 0f), sandAbs, "frozen sand reflects more spall");
        Assert.GreaterOrEqual(MaterialPhysics.BlastAbsorptionFactor("Concrete", 30f, 1f), 0f);
        Assert.LessOrEqual(MaterialPhysics.BlastAbsorptionFactor("Concrete", 30f, 1f), 1f);
    }

    [Test]
    public void AcousticAbsorptionRisesWhenWet()
    {
        Assert.Greater(MaterialPhysics.AcousticAbsorptionShift(0.3f, 1f), 0.3f);
        Assert.AreEqual(0.3f, MaterialPhysics.AcousticAbsorptionShift(0.3f, 0f), 1e-5f);
        Assert.LessOrEqual(MaterialPhysics.AcousticAbsorptionShift(0.3f, 1f), 1f);
    }

    [Test]
    public void LobbyFailFeedbackIsNotSilent()
    {
        // W-BUG-001 UI regression: failed actions must surface (never static screen)
        var go = new UnityEngine.GameObject("lobby-fail");
        try
        {
            var panel = go.AddComponent<VEVE.Net.SessionLobbyPanel>();
            panel.UseTestClock(() => System.DateTime.UtcNow);
            // no backend bound -> every action fails visibly
            Assert.IsFalse(panel.TryHost());
            Assert.IsTrue(panel.LastActionFailed, "failure must be surfaced to the player");
            Assert.IsFalse(panel.TryJoin("127.0.0.1", 7777));
            Assert.IsTrue(panel.LastActionFailed);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
