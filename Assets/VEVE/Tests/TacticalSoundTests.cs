using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class TacticalSoundTests
{
    [Test]
    public void NoiseEventCarriesSourcePositionAndLoudness()
    {
        Vector3 receivedPosition = Vector3.zero;
        float receivedLoudness = 0f;
        System.Action<Vector3, float> handler = (position, loudness) =>
        {
            receivedPosition = position;
            receivedLoudness = loudness;
        };
        TacticalSound.NoiseProduced += handler;
        TacticalSound.Emit(new Vector3(2f, 0f, 3f), 35f);
        TacticalSound.NoiseProduced -= handler;
        Assert.AreEqual(new Vector3(2f, 0f, 3f), receivedPosition);
        Assert.AreEqual(35f, receivedLoudness);
    }
}
