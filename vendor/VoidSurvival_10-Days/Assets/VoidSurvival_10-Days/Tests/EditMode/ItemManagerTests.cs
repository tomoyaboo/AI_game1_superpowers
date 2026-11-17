using NUnit.Framework;
using UnityEngine;

public class ItemManagerTests
{
    [Test]
    public void RemoveItem_ItemNotInInventory_DoesNotThrow()
    {
        var item = ScriptableObject.CreateInstance<MaterialItem>();
        item.ItemID = int.MinValue; // 既存インベントリと衝突しないID

        Assert.DoesNotThrow(() => ItemManager.Instance.RemoveItem(item, 1));

        Object.DestroyImmediate(item);
    }
}
