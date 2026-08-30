using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class CampaignLegacyHookTests
{
    [Test]
    public void SuccessorCommissionedReplacesActiveOperatorAndKeepsFamilyId()
    {
        var go = new GameObject("campaign");
        try
        {
            CampaignState state = go.AddComponent<CampaignState>();
            string original = state.ActiveOperator.callsign;

            bool result = state.TryCommissionSuccessor("contact - hostile fire");

            Assert.That(result, Is.True);
            Assert.That(state.ActiveOperator.alive, Is.True);
            Assert.That(state.ActiveOperator.callsign, Does.StartWith(original));
            Assert.That(state.Legacy.LossCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LegacyPersistsAcrossSuccessions()
    {
        var go = new GameObject("campaign");
        try
        {
            CampaignState state = go.AddComponent<CampaignState>();
            Assert.That(state.TryCommissionSuccessor("first loss"), Is.True);
            string first = state.ActiveOperator.callsign;
            Assert.That(state.TryCommissionSuccessor("second loss"), Is.True);
            Assert.That(state.ActiveOperator.callsign, Is.Not.EqualTo(first));
            Assert.That(state.Legacy.LossCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
