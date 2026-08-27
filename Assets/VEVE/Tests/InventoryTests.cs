using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class InventoryTests
{
    [Test]
    public void InventoryRejectsItemsOverVolumeCapacity()
    {
        GameObject owner = new GameObject("InventoryTest");
        try
        {
            PhysicalInventory inventory = owner.AddComponent<PhysicalInventory>();
            Assert.IsTrue(inventory.TryAdd(new InventoryItem { id = "medkit", volumeLitres = 12f, massKg = 2f, accessible = true }));
            Assert.IsFalse(inventory.TryAdd(new InventoryItem { id = "battery", volumeLitres = 20f, massKg = 1f, accessible = true }));
            Assert.AreEqual(0.5f, inventory.LoadRatio, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
