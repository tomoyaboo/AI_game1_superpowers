using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

public class HKDataDisplayThermalDeltaTests
{
    [Test]
    public void ComputeThermalDelta_MultipleColumns_ReturnsMaxMinusMin()
    {
        var row = new JObject
        {
            ["Panel1 (°C)"] = 10.0,
            ["Panel2 (°C)"] = 30.0,
            ["Panel3 (°C)"] = 15.0,
        };
        var cols = new List<string> { "Panel1 (°C)", "Panel2 (°C)", "Panel3 (°C)" };

        Assert.AreEqual(20f, HKDataDisplay.ComputeThermalDelta(row, cols), 0.001f);
    }

    [Test]
    public void ComputeThermalDelta_NoValidColumns_ReturnsZero()
    {
        var row = new JObject();
        var cols = new List<string> { "Missing (°C)" };

        Assert.AreEqual(0f, HKDataDisplay.ComputeThermalDelta(row, cols), 0.001f);
    }

    [Test]
    public void ComputeThermalDelta_EmptyColumnList_ReturnsZero()
    {
        var row = new JObject { ["Panel1 (°C)"] = 10.0 };
        Assert.AreEqual(0f, HKDataDisplay.ComputeThermalDelta(row, new List<string>()), 0.001f);
    }
}
