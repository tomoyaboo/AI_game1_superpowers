using NUnit.Framework;
using UnityEngine;

public class FlagDataTests
{
    [Test]
    public void ToSaveData_FromSaveData_RoundTrips()
    {
        var flagData = ScriptableObject.CreateInstance<FlagData>();
        flagData.destructionRock = true;
        flagData.findSomeone = false;
        flagData.craftTutorial = true;
        flagData.craftWorkbench = false;

        var saved = flagData.ToSaveData();

        var restored = ScriptableObject.CreateInstance<FlagData>();
        restored.FromSaveData(saved);

        Assert.IsTrue(restored.destructionRock);
        Assert.IsFalse(restored.findSomeone);
        Assert.IsTrue(restored.craftTutorial);
        Assert.IsFalse(restored.craftWorkbench);

        Object.DestroyImmediate(flagData);
        Object.DestroyImmediate(restored);
    }
}
