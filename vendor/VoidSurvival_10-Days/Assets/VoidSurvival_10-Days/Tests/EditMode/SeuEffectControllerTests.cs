using NUnit.Framework;

public class SeuEffectControllerTests
{
    [Test]
    public void IsSaaCondition_MagWeakAndRadiationHigh_ReturnsTrue()
    {
        Assert.IsTrue(SeuEffectController.IsSaaCondition("LOW", "NORMAL", "NORMAL", "HIGH"));
    }

    [Test]
    public void IsSaaCondition_AnyMagWeak_ReturnsTrue()
    {
        Assert.IsTrue(SeuEffectController.IsSaaCondition("NORMAL", "NORMAL", "LOW", "HIGH"));
    }

    [Test]
    public void IsSaaCondition_NoMagWeak_ReturnsFalse()
    {
        Assert.IsFalse(SeuEffectController.IsSaaCondition("NORMAL", "NORMAL", "NORMAL", "HIGH"));
    }

    [Test]
    public void IsSaaCondition_RadiationNotHigh_ReturnsFalse()
    {
        Assert.IsFalse(SeuEffectController.IsSaaCondition("LOW", "NORMAL", "NORMAL", "NORMAL"));
    }

    [Test]
    public void CalculateTriggerProbability_NoShield_ReturnsBaseProbability()
    {
        Assert.AreEqual(0.04f,
            SeuEffectController.CalculateTriggerProbability(0.04f, shieldEquipped: false, shieldMultiplier: 0.25f),
            0.0001f);
    }

    [Test]
    public void CalculateTriggerProbability_WithShield_ReturnsReducedProbability()
    {
        Assert.AreEqual(0.01f,
            SeuEffectController.CalculateTriggerProbability(0.04f, shieldEquipped: true, shieldMultiplier: 0.25f),
            0.0001f);
    }
}
