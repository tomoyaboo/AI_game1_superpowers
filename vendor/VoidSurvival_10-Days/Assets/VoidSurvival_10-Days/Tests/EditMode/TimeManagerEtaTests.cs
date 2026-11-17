using NUnit.Framework;

public class TimeManagerEtaTests
{
    [Test]
    public void CalculateReentryEta_AtStart_ReturnsMaxDaysZeroHours()
    {
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 0f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(10, days);
        Assert.AreEqual(0, hours);
    }

    [Test]
    public void CalculateReentryEta_HalfwayThroughDay_ReturnsPartialHours()
    {
        // 1440ゲーム内分のうち720分経過(半日) -> 残り9日12時間
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 720f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(9, days);
        Assert.AreEqual(12, hours);
    }

    [Test]
    public void CalculateReentryEta_PastMaxDays_NeverGoesNegative()
    {
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 999999f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(0, days);
        Assert.AreEqual(0, hours);
    }

    [Test]
    public void CalculateReentryEta_ExactlyOneDayElapsed_ReturnsOneDayLess()
    {
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 1440f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(9, days);
        Assert.AreEqual(0, hours);
    }
}
