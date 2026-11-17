using NUnit.Framework;
using UnityEngine;

public class HKDataDisplayFallbackTests
{
    [Test]
    public void ParseHkRows_LocalFallbackJson_ParsesSuccessfully()
    {
        var textAsset = Resources.Load<TextAsset>("LocalFallbackData/HKData");
        Assert.IsNotNull(textAsset, "LocalFallbackData/HKData.json が Resources に見つかりません");

        var rows = HKDataDisplay.ParseHkRows(textAsset.text);

        Assert.IsNotNull(rows);
        Assert.Greater(rows.Count, 0);
    }
}
