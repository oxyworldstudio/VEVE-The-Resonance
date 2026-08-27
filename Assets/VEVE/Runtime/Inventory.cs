using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE
{
    [Serializable]
    public sealed class InventoryItem
    {
        public string id;
        public float massKg;
        public float volumeLitres;
        public bool accessible;
        public int quantity = 1;
    }

    public sealed class PhysicalInventory : MonoBehaviour
    {
        [SerializeField] private float capacityLitres = 24f;
        [SerializeField] private List<InventoryItem> items = new List<InventoryItem>();

        public float TotalMassKg
        {
            get
            {
                float total = 0f;
                foreach (InventoryItem item in items) total += item.massKg * item.quantity;
                return total;
            }
        }

        public float UsedVolumeLitres
        {
            get
            {
                float total = 0f;
                foreach (InventoryItem item in items) total += item.volumeLitres * item.quantity;
                return total;
            }
        }

        public float CapacityLitres => capacityLitres;
        public float LoadRatio => capacityLitres <= 0f ? 1f : Mathf.Clamp01(UsedVolumeLitres / capacityLitres);

        public bool TryAdd(InventoryItem item)
        {
            if (item == null || item.quantity < 1) return false;
            if (UsedVolumeLitres + item.volumeLitres * item.quantity > capacityLitres) return false;
            items.Add(item);
            return true;
        }

        public bool HasAccessible(string id)
        {
            foreach (InventoryItem item in items)
                if (item.id == id && item.accessible && item.quantity > 0) return true;
            return false;
        }
    }
}
