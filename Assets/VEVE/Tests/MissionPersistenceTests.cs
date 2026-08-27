using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class MissionPersistenceTests
{
    [Test]
    public void MissionStateUsesExplicitVersion()
    {
        var state = new MissionState();
        Assert.AreEqual(1, state.version);
    }

    [Test]
    public void MissionStateRejectsBlankEventIds()
    {
        GameObject owner = new GameObject("MissionRuntimeTest");
        try
        {
            MissionRuntime runtime = owner.AddComponent<MissionRuntime>();
            Assert.Throws<System.ArgumentException>(() => runtime.RecordEvent(""));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
