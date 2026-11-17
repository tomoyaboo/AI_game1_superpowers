using NUnit.Framework;

public class ObservedValuesHudTests
{
    [Test]
    public void FormatObservedValues_NormalValues_ReturnsExpectedFormat()
    {
        string result = ObservedValuesHud.FormatObservedValues(32, 47.3f, 11.2f);
        Assert.AreEqual("RAD 32 / MAG 47 / ΔT 11", result);
    }

    [Test]
    public void FormatObservedValues_NaNMag_ShowsDashInsteadOfCrashing()
    {
        string result = ObservedValuesHud.FormatObservedValues(32, float.NaN, 11.2f);
        Assert.AreEqual("RAD 32 / MAG -- / ΔT 11", result);
    }

    [Test]
    public void FormatEta_TenDaysZeroHours_ReturnsExpectedFormat()
    {
        string result = ObservedValuesHud.FormatEta(10, 0);
        Assert.AreEqual("REENTRY ETA .... 10D 00H", result);
    }

    [Test]
    public void FormatEta_SingleDigitHours_PadsWithZero()
    {
        string result = ObservedValuesHud.FormatEta(9, 5);
        Assert.AreEqual("REENTRY ETA .... 9D 05H", result);
    }
}
