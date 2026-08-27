using NUnit.Framework;
using VEVE;

public sealed class SoundPropagationTests
{
    [Test]
    public void DistanceAndAbsorptionReduceHeardLoudness()
    {
        float near = SoundPropagation.HeardLoudness(35f, 2f, 0f);
        float far = SoundPropagation.HeardLoudness(35f, 20f, 0.5f);
        Assert.Greater(near, far);
    }
}
