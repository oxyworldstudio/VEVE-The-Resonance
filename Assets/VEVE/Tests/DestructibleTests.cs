using NUnit.Framework;
using UnityEngine;

namespace VEVE.Tests
{
    public sealed class DestructibleTests
    {
        private const System.Reflection.BindingFlags PrivateFlags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        [Test]
        public void LowEnergyImpactsErodeIntegrityWithoutPenetration()
        {
            GameObject go = new GameObject("DestructibleTest");
            Destructible destructible = go.AddComponent<Destructible>();
            typeof(Destructible).GetField("integrity", PrivateFlags).SetValue(destructible, 100f);
            typeof(Destructible).GetField("maxIntegrity", PrivateFlags).SetValue(destructible, 100f);

            // 0.05 energia su legno (resistenza 0.35, spessore 0.2) -> 0.05 - 0.07 = 0 -> non penetra
            bool penetrated = destructible.AbsorbImpact(0.05f, out float remaining);

            Assert.IsFalse(penetrated, "Low energy should be stopped by intact cover.");
            Assert.AreEqual(0f, remaining, 0.001f, "Energy must be fully absorbed.");
            Assert.Less(destructible.Integrity, 100f, "Integrity must erode after impact.");
            Assert.AreEqual(DestructionState.Intact, destructible.State);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DestroyedCoverStopsBlockingAndRecordsState()
        {
            GameObject go = new GameObject("DestructibleTest2");
            Destructible destructible = go.AddComponent<Destructible>();
            typeof(Destructible).GetField("integrity", PrivateFlags).SetValue(destructible, 0.01f);
            typeof(Destructible).GetField("maxIntegrity", PrivateFlags).SetValue(destructible, 100f);

            destructible.AbsorbImpact(0.05f, out _);

            Assert.AreEqual(DestructionState.Destroyed, destructible.State);
            Assert.AreEqual(0.01f, destructible.CurrentThickness(), 0.001f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DamagedCoverHasReducedThickness()
        {
            GameObject go = new GameObject("DestructibleTest3");
            Destructible destructible = go.AddComponent<Destructible>();
            typeof(Destructible).GetField("integrity", PrivateFlags).SetValue(destructible, 50f);
            typeof(Destructible).GetField("maxIntegrity", PrivateFlags).SetValue(destructible, 100f);

            // Un impatto erode l'integrità e aggiorna lo stato a Damaged
            destructible.AbsorbImpact(0.05f, out _);

            float thickness = destructible.CurrentThickness();

            Assert.Less(thickness, 0.2f, "Damaged cover must protect less than intact cover.");
            Assert.AreEqual(DestructionState.Damaged, destructible.State);
            Object.DestroyImmediate(go);
        }
    }
}