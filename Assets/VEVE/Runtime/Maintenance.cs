using UnityEngine;

namespace VEVE
{
    public sealed class Maintenance : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float condition = 1f;
        [SerializeField, Range(0f, 1f)] private float battery = 1f;

        public float Condition => condition;
        public float Battery => battery;

        public void UseShot() => condition = Mathf.Clamp01(condition - 0.002f);
        public bool ConsumeBattery(float amount)
        {
            if (amount < 0f || battery < amount) return false;
            battery -= amount;
            return true;
        }

        public void Clean(float amount) => condition = Mathf.Clamp01(condition + Mathf.Max(0f, amount));
        public void Recharge(float amount) => battery = Mathf.Clamp01(battery + Mathf.Max(0f, amount));
    }
}
