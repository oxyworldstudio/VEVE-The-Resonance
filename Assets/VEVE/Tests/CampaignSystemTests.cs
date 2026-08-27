using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class CampaignSystemTests
{
    [Test]
    public void TestModeDoesNotConsumeOperator()
    {
        GameObject owner = new GameObject("CampaignTest");
        try
        {
            CampaignState state = owner.AddComponent<CampaignState>();
            Assert.IsFalse(state.HandleDeath());
            Assert.IsTrue(state.ActiveOperator.alive);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
