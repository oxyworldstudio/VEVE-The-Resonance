using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VEVE.AI;
using VEVE.Net;

public sealed class MultiTargetRulesTests
{
    [Test]
    public void NullAndEmptySafelyYieldNull()
    {
        Assert.IsNull(MultiTargetRules.ChooseNearest(null, Vector3.zero));
        Assert.IsNull(MultiTargetRules.ChooseNearest(new List<Transform>(), Vector3.zero));
        Assert.IsFalse(MultiTargetRules.ShouldPreferPawns(0));
        Assert.IsTrue(MultiTargetRules.ShouldPreferPawns(1));
    }

    [Test]
    public void NearestWinsAndDestroyedIgnored()
    {
        var near = new GameObject("near"); near.transform.position = new Vector3(2f, 0f, 0f);
        var far = new GameObject("far"); far.transform.position = new Vector3(30f, 0f, 0f);
        var list = new List<Transform> { far.transform, null, near.transform };
        try
        {
            Assert.AreSame(near.transform, MultiTargetRules.ChooseNearest(list, Vector3.zero));
            Assert.IsNull(MultiTargetRules.ChooseNearest(list, Vector3.zero, 1f), "max distance respected");
        }
        finally
        {
            Object.DestroyImmediate(near);
            Object.DestroyImmediate(far);
        }
    }

    [Test]
    public void PawnCollectorIsDefensive()
    {
        Assert.DoesNotThrow(() => NetworkedPlayerPawn.CollectCombatTargets(null));
        var t = new List<Transform>();
        NetworkedPlayerPawn.CollectCombatTargets(t);
        Assert.AreEqual(0, t.Count, "no spawned pawns offline");
    }
}
