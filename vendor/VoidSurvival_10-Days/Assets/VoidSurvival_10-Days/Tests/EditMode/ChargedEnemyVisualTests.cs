using NUnit.Framework;

public class ChargedEnemyVisualTests
{
    [Test]
    public void IsChargedCondition_AnyMagLow_True()
    {
        Assert.IsTrue(ChargedEnemyVisual.IsChargedCondition("LOW", "NORMAL", "NORMAL"));
        Assert.IsTrue(ChargedEnemyVisual.IsChargedCondition("NORMAL", "LOW", "NORMAL"));
        Assert.IsTrue(ChargedEnemyVisual.IsChargedCondition("NORMAL", "NORMAL", "LOW"));
    }

    [Test]
    public void IsChargedCondition_AllNormalOrHigh_False()
    {
        Assert.IsFalse(ChargedEnemyVisual.IsChargedCondition("NORMAL", "NORMAL", "NORMAL"));
        Assert.IsFalse(ChargedEnemyVisual.IsChargedCondition("HIGH", "NORMAL", "NORMAL"));
    }
}
