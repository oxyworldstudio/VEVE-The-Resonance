using NUnit.Framework;
using VEVE;

public sealed class EnvironmentSimulationTests
{
    [Test]
    public void WeatherStatesAreExplicitlyDefined()
    {
        Assert.AreEqual(3, System.Enum.GetValues(typeof(WeatherState)).Length);
    }
}
