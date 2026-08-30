using NUnit.Framework;
using VEVE;

public sealed class EnvironmentSimulationTests
{
    [Test]
    public void WeatherStatesAreExplicitlyDefined()
    {
        Assert.AreEqual(6, System.Enum.GetValues(typeof(WeatherState)).Length);
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeatherState), WeatherState.Clear));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeatherState), WeatherState.Overcast));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeatherState), WeatherState.Rain));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeatherState), WeatherState.Fog));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeatherState), WeatherState.Snow));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeatherState), WeatherState.Thunderstorm));
    }
}
