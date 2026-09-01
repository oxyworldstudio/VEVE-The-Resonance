using NUnit.Framework;
using VEVE.World;

public sealed class BreachRulesTests
{
    private const float Epsilon = 1e-4f;

    [Test]
    public void PlanPicksChargeWhenCarriedAndViable()
    {
        // 0.5 kg at 520 J/kg = 260 damage vs 200 integrity: charge finishes the door.
        BreachPlan plan = BreachRules.Plan(DoorState.Locked, 3, 200f, 0.5f, false, true);
        Assert.AreEqual(BreachMethod.Charge, plan.method, "viable charge must win the ladder");
        Assert.AreEqual(110f, plan.noiseLoudness, "charge plan must advertise its 110 loudness");
        Assert.GreaterOrEqual(plan.estimatedSeconds, 0f);

        BreachPlan chargeOnly = BreachRules.Plan(DoorState.Locked, 3, 200f, 0.5f, false, true);
        Assert.AreEqual(BreachMethod.Charge, chargeOnly.method);
    }

    [Test]
    public void PlanPicksWhenKitAndNoCharge()
    {
        BreachPlan plan = BreachRules.Plan(DoorState.Locked, 2, 100f, 0f, true, false);
        Assert.AreEqual(BreachMethod.Pick, plan.method);
        Assert.AreEqual(DoorModel.PickSeconds(2, true), plan.estimatedSeconds);
        Assert.AreEqual(4f, plan.noiseLoudness);
    }

    [Test]
    public void PlanFallsBackToKickWithoutKitOrCharge()
    {
        BreachPlan plan = BreachRules.Plan(DoorState.Locked, 2, 100f, 0f, false, false);
        Assert.AreEqual(BreachMethod.Kick, plan.method);
        Assert.AreEqual(45f, plan.noiseLoudness);
        Assert.Greater(plan.estimatedSeconds, 0f, "kick attrition takes bounded time");
    }

    [Test]
    public void PlanNeedsNothingForOpenOrBreachedDoors()
    {
        BreachPlan open = BreachRules.Plan(DoorState.Open, 2, 100f, 0.5f, true, true);
        Assert.AreEqual(BreachMethod.None, open.method);
        Assert.AreEqual(0f, open.estimatedSeconds);
        Assert.AreEqual(0f, open.noiseLoudness);

        BreachPlan breached = BreachRules.Plan(DoorState.Breached, 3, 0f, 0.5f, true, true);
        Assert.AreEqual(BreachMethod.None, breached.method);
    }

    [Test]
    public void PlanPickRequiresActualLock()
    {
        // lockLevel 0 is a jiggle-class latch: kit alone must not plan a pick.
        BreachPlan plan = BreachRules.Plan(DoorState.Locked, 0, 100f, 0f, true, false);
        Assert.AreEqual(BreachMethod.Kick, plan.method);
    }

    [Test]
    public void PlanChargeRejectedWhenIntegrityTooHigh()
    {
        // 260 damage vs 1000 integrity: charge cannot matter, kick attrition is the plan.
        BreachPlan plan = BreachRules.Plan(DoorState.Locked, 3, 1000f, 0.5f, false, true);
        Assert.AreEqual(BreachMethod.Kick, plan.method);
        Assert.AreEqual(45f, plan.noiseLoudness);
    }

    [Test]
    public void PlanPrefersPickOverChargeAndChargeOverKick()
    {
        // Kit + viable charge: pick still wins (stealth-first ladder).
        BreachPlan withKit = BreachRules.Plan(DoorState.Locked, 2, 50f, 0.5f, true, true);
        Assert.AreEqual(BreachMethod.Pick, withKit.method);

        // Viable charge, no kit: charge beats kick.
        BreachPlan chargeOnly = BreachRules.Plan(DoorState.Locked, 2, 50f, 0.5f, false, true);
        Assert.AreEqual(BreachMethod.Charge, chargeOnly.method);
    }

    [Test]
    public void NoiseTableExact()
    {
        Assert.AreEqual(0f, BreachRules.NoiseLoudness(BreachMethod.None));
        Assert.AreEqual(4f, BreachRules.NoiseLoudness(BreachMethod.Pick));
        Assert.AreEqual(45f, BreachRules.NoiseLoudness(BreachMethod.Kick));
        Assert.AreEqual(110f, BreachRules.NoiseLoudness(BreachMethod.Charge));
        Assert.AreEqual(45f, BreachRules.NoiseLoudness(BreachMethod.Kick), "kick mirrors DoorModel.KickNoiseLoudness");
        Assert.AreEqual(DoorModel.KickNoiseLoudness, BreachRules.NoiseKick);
    }

    [Test]
    public void PlanCarriesTableLoudness()
    {
        Assert.AreEqual(BreachRules.NoiseLoudness(BreachMethod.Pick), BreachRules.Plan(DoorState.Locked, 2, 100f, 0f, true, false).noiseLoudness);
        Assert.AreEqual(BreachRules.NoiseLoudness(BreachMethod.Kick), BreachRules.Plan(DoorState.Locked, 2, 100f, 0f, false, false).noiseLoudness);
        Assert.AreEqual(BreachRules.NoiseLoudness(BreachMethod.Charge), BreachRules.Plan(DoorState.Locked, 2, 50f, 0.5f, false, true).noiseLoudness);
    }

    [Test]
    public void SecondsToBreakMonotonicInChargeMass()
    {
        const float integrity = 200f;
        const float kickDamage = 30f;
        float previous = float.MaxValue;
        for (float kg = DoorModel.MinChargeKg; kg <= 1.0001f; kg += 0.1f)
        {
            float seconds = BreachRules.SecondsToBreak(integrity, kickDamage, kg * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg);
            Assert.LessOrEqual(seconds, previous, "more charge mass must never cost more time");
            Assert.GreaterOrEqual(seconds, 0f);
            previous = seconds;
        }

        Assert.Less(BreachRules.SecondsToBreak(integrity, kickDamage, 0.05f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg),
            BreachRules.SecondsToBreak(integrity, kickDamage, 0f, 0f) + BreachRules.ChargeSeconds,
            "carried charges stay on a monotone decreasing curve");
        Assert.Less(BreachRules.SecondsToBreak(integrity, kickDamage, 0.2f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg),
            BreachRules.SecondsToBreak(integrity, kickDamage, 0.05f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg));
        Assert.Less(BreachRules.SecondsToBreak(integrity, kickDamage, 0.4f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg),
            BreachRules.SecondsToBreak(integrity, kickDamage, 0.2f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg));
        Assert.AreEqual(BreachRules.ChargeSeconds, BreachRules.SecondsToBreak(integrity, kickDamage, 0.4f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg), Epsilon);
        Assert.Less(BreachRules.SecondsToBreak(integrity, kickDamage, 0.4f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg),
            BreachRules.SecondsToBreak(integrity, kickDamage, 0f, 0f), "a strong charge must beat pure kick attrition");
    }

    [Test]
    public void SecondsToBreakBoundsImmovableDoorAtTwentyKicks()
    {
        float seconds = BreachRules.SecondsToBreak(100000f, 30f, 0f, 0f);
        Assert.AreEqual(BreachRules.MaxKickIterations * BreachRules.KickSecondsPerHit, seconds, Epsilon);

        float beyondCap = BreachRules.SecondsToBreak(21f * 30f, 30f, 0f, 0f);
        float exactlyTwenty = BreachRules.SecondsToBreak(19f * 30f + 15f, 30f, 0f, 0f);
        float underCap = BreachRules.SecondsToBreak(19f * 30f, 30f, 0f, 0f);
        Assert.AreEqual(BreachRules.MaxKickIterations * BreachRules.KickSecondsPerHit, beyondCap, Epsilon, "need >20 kicks: capped");
        Assert.AreEqual(BreachRules.MaxKickIterations * BreachRules.KickSecondsPerHit, exactlyTwenty, Epsilon, "need exactly 20 kicks: at the cap, not over");
        Assert.AreEqual(19f * BreachRules.KickSecondsPerHit, underCap, Epsilon, "need 19 kicks: under the cap");
        Assert.AreEqual(beyondCap, exactlyTwenty, Epsilon, "cap flattens the curve beyond 20 kicks");
    }

    [Test]
    public void SecondsToBreakZeroWhenAlreadyBroken()
    {
        Assert.AreEqual(0f, BreachRules.SecondsToBreak(0f, 30f, 0f, 0f));
        Assert.AreEqual(0f, BreachRules.SecondsToBreak(-5f, 30f, 0f, 0f));
        Assert.AreEqual(BreachRules.ChargeSeconds, BreachRules.SecondsToBreak(10f, 30f, 0.5f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg), "one strong charge finishes instantly");
    }

    [Test]
    public void SecondsToBreakDeterministicChargePath()
    {
        float a = BreachRules.SecondsToBreak(400f, 30f, 0.5f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg);
        float b = BreachRules.SecondsToBreak(400f, 30f, 0.5f * DoorModel.BreachJoulesPerKg, DoorModel.BreachJoulesPerKg);
        Assert.AreEqual(a, b);
        Assert.Greater(a, BreachRules.ChargeSeconds - Epsilon, "residual integrity after the blast costs kicks on top");
    }

    [Test]
    public void PlanIsDeterministic()
    {
        var first = BreachRules.Plan(DoorState.Locked, 2, 137.5f, 0.42f, true, true);
        var second = BreachRules.Plan(DoorState.Locked, 2, 137.5f, 0.42f, true, true);
        Assert.IsTrue(first.Equals(second), "same inputs must yield exactly equal plans");
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.AreEqual(first.ToString(), second.ToString());

        var kickFirst = BreachRules.Plan(DoorState.Closed, 0, 55f, 0f, false, false);
        var kickSecond = BreachRules.Plan(DoorState.Closed, 0, 55f, 0f, false, false);
        Assert.IsTrue(kickFirst.Equals(kickSecond));
    }

    [Test]
    public void BreachPlanValueEquality()
    {
        var a = new BreachPlan(BreachMethod.Kick, 4.8f, 45f);
        var b = new BreachPlan(BreachMethod.Kick, 4.8f, 45f);
        var c = new BreachPlan(BreachMethod.Charge, 4.8f, 45f);
        Assert.IsTrue(a.Equals(b));
        Assert.AreEqual(a, b);
        Assert.IsFalse(a.Equals(c));
        Assert.IsFalse(a.Equals(new BreachPlan(BreachMethod.Kick, 5f, 45f)));
    }
}
