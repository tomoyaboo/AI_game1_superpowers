using NUnit.Framework;

public class EsdEffectControllerTests
{
    [Test]
    public void IsEsdCondition_DeltaAboveThresholdAndMagWeak_ReturnsTrue()
    {
        Assert.IsTrue(EsdEffectController.IsEsdCondition(25f, 20f, "LOW", "NORMAL", "NORMAL"));
    }

    [Test]
    public void IsEsdCondition_DeltaBelowThreshold_ReturnsFalse()
    {
        Assert.IsFalse(EsdEffectController.IsEsdCondition(15f, 20f, "LOW", "NORMAL", "NORMAL"));
    }

    [Test]
    public void IsEsdCondition_MagNotWeak_ReturnsFalse()
    {
        Assert.IsFalse(EsdEffectController.IsEsdCondition(25f, 20f, "NORMAL", "NORMAL", "NORMAL"));
    }

    [Test]
    public void IsEsdCondition_DeltaExactlyAtThreshold_ReturnsFalse()
    {
        // 閾値そのものは「大きい」に含めない(厳密に超えた場合のみ)
        Assert.IsFalse(EsdEffectController.IsEsdCondition(20f, 20f, "LOW", "NORMAL", "NORMAL"));
    }
}
