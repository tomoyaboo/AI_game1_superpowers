using NUnit.Framework;
using UnityEngine;

public class DataManagerFallbackTests
{
    [Test]
    public void ParseGameMasterData_LocalFallbackJson_ParsesSuccessfully()
    {
        var textAsset = Resources.Load<TextAsset>("LocalFallbackData/GameMasterData");
        Assert.IsNotNull(textAsset, "LocalFallbackData/GameMasterData.json が Resources に見つかりません");

        var root = DataManager.ParseGameMasterData(textAsset.text);

        Assert.IsNotNull(root);
        Assert.IsNotEmpty(root.Gyro);
        Assert.IsNotEmpty(root.Mag1);
        Assert.IsNotEmpty(root.Radiation);
    }

    [Test]
    public void ParseGameMasterData_InvalidJson_ReturnsNull()
    {
        var result = DataManager.ParseGameMasterData("not valid json");
        Assert.IsNull(result);
    }
}
