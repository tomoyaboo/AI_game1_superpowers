using NUnit.Framework;

public class WeatherTesterThreeAxisTests
{
    // 軸2: 荒れ具合(Weather enum再利用) — HIGHの個数だけで決まる
    [Test]
    public void EvaluateRoughWeather_NoHigh_ReturnsSunny()
    {
        Assert.AreEqual(Weather.Sunny,
            WeatherTester.EvaluateRoughWeather("LOW", "NORMAL", "LOW", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_OneHigh_ReturnsCloudy()
    {
        Assert.AreEqual(Weather.Cloudy,
            WeatherTester.EvaluateRoughWeather("HIGH", "NORMAL", "LOW", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_TwoHigh_ReturnsRainy()
    {
        Assert.AreEqual(Weather.Rainy,
            WeatherTester.EvaluateRoughWeather("HIGH", "HIGH", "NORMAL", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_ThreeHigh_ReturnsThunderstorm()
    {
        Assert.AreEqual(Weather.Thunderstorm,
            WeatherTester.EvaluateRoughWeather("HIGH", "HIGH", "HIGH", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_AllFourHigh_ReturnsThunderstorm()
    {
        // 4つ全部HIGHでも配列の範囲を超えずThunderstormに留まることを確認
        Assert.AreEqual(Weather.Thunderstorm,
            WeatherTester.EvaluateRoughWeather("HIGH", "HIGH", "HIGH", "HIGH"));
    }

    // 軸1: 明暗
    [Test]
    public void EvaluateLightPhase_CurrentLow_ReturnsEclipse()
    {
        Assert.AreEqual(LightPhase.Eclipse, WeatherTester.EvaluateLightPhase("LOW"));
    }

    [Test]
    public void EvaluateLightPhase_CurrentNormalOrHigh_ReturnsSun()
    {
        Assert.AreEqual(LightPhase.Sun, WeatherTester.EvaluateLightPhase("NORMAL"));
        Assert.AreEqual(LightPhase.Sun, WeatherTester.EvaluateLightPhase("HIGH"));
    }

    // 軸3: 計器信頼性
    [Test]
    public void EvaluateInstrumentReliability_GyroHigh_ReturnsDisturbed()
    {
        Assert.AreEqual(InstrumentReliability.Disturbed,
            WeatherTester.EvaluateInstrumentReliability("HIGH"));
    }

    [Test]
    public void EvaluateInstrumentReliability_GyroNormalOrLow_ReturnsStable()
    {
        Assert.AreEqual(InstrumentReliability.Stable,
            WeatherTester.EvaluateInstrumentReliability("NORMAL"));
        Assert.AreEqual(InstrumentReliability.Stable,
            WeatherTester.EvaluateInstrumentReliability("LOW"));
    }
}
