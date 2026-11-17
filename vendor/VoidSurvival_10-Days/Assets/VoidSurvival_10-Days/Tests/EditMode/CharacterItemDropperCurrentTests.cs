using NUnit.Framework;

public class CharacterItemDropperCurrentTests
{
    [Test]
    public void CurrentDropRateMultiplier_High_ReturnsBoostedMultiplier()
    {
        Assert.AreEqual(1.2f, CharacterItemDropper.CurrentDropRateMultiplier("HIGH"), 0.001f);
    }

    [Test]
    public void CurrentDropRateMultiplier_Low_ReturnsReducedMultiplier()
    {
        Assert.AreEqual(0.8f, CharacterItemDropper.CurrentDropRateMultiplier("LOW"), 0.001f);
    }

    [Test]
    public void CurrentDropRateMultiplier_Normal_ReturnsOne()
    {
        Assert.AreEqual(1.0f, CharacterItemDropper.CurrentDropRateMultiplier("NORMAL"), 0.001f);
    }

    [Test]
    public void EffectiveDropRate_NeverExceedsOne()
    {
        Assert.LessOrEqual(
            CharacterItemDropper.EffectiveDropRate(0.9f, "HIGH"), 1.0f);
    }
}
