using NUnit.Framework;
using UnityEngine;
using VEVE;

public sealed class DamageableTests
{
    [Test]
    public void TorsoDamageIsLocalAndDisablesAtZero()
    {
        GameObject owner = new GameObject("DamageableTest");
        try
        {
            Damageable damageable = owner.AddComponent<Damageable>();
            damageable.ApplyDamage(25f, HitZone.UpperTorso);
            Assert.AreEqual(75f, damageable.TorsoIntegrity);
            Assert.IsFalse(damageable.IsDisabled);
            damageable.ApplyDamage(75f, HitZone.UpperTorso);
            Assert.IsTrue(damageable.IsDisabled);
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }
}
