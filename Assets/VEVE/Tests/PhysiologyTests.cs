using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class PhysiologyTests
{
    [Test]
    public void LimbTraumaReducesMovementFactor()
    {
        GameObject owner = new GameObject("PhysiologyTest");
        try
        {
            Physiology physiology = owner.AddComponent<Physiology>();
            float stable = physiology.MovementFactor;
            physiology.ApplyFracture(50f);
            Assert.Less(physiology.MovementFactor, stable);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
