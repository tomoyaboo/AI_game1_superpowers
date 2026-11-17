using NUnit.Framework;

public class PowerSupplyVoltageTests
{
    [Test]
    public void VoltageEfficiencyDelta_Low_ReturnsPositiveDelta()
    {
        Assert.AreEqual(0.20f, PowerSupply.VoltageEfficiencyDelta("LOW"), 0.001f);
    }

    [Test]
    public void VoltageEfficiencyDelta_High_ReturnsNegativeDelta()
    {
        Assert.AreEqual(-0.10f, PowerSupply.VoltageEfficiencyDelta("HIGH"), 0.001f);
    }

    [Test]
    public void VoltageEfficiencyDelta_Normal_ReturnsZero()
    {
        Assert.AreEqual(0f, PowerSupply.VoltageEfficiencyDelta("NORMAL"), 0.001f);
    }

    [Test]
    public void VoltageEfficiencyDelta_UnknownState_ReturnsZero()
    {
        Assert.AreEqual(0f, PowerSupply.VoltageEfficiencyDelta("UNKNOWN"), 0.001f);
    }
}
