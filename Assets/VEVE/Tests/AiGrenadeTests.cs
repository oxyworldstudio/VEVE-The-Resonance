using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VEVE.AI;
using VEVE.Combat;

public sealed class AiGrenadeTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void CleanupSceneObjects()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
        }
        spawned.Clear();

        // sweep any AI grenades the throw path created this domain (created outside `spawned`)
        var leftovers = Object.FindObjectsByType<GrenadeProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < leftovers.Length; i++)
        {
            if (leftovers[i] != null && leftovers[i].gameObject.name == "AI_Grenade")
                Object.DestroyImmediate(leftovers[i].gameObject);
        }
    }

    private GameObject NewObject(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        spawned.Add(go);
        return go;
    }

    private GrenadeThrowerAI NewThrower(Vector3 position)
    {
        return NewObject("thrower", position).AddComponent<GrenadeThrowerAI>();
    }

    private Transform NewTarget(Vector3 position)
    {
        return NewObject("target", position).transform;
    }

    private static int CountAiGrenades()
    {
        var all = Object.FindObjectsByType<GrenadeProjectile>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.name == "AI_Grenade") count++;
        }
        return count;
    }

    [Test]
    public void ThrowerWithTargetInBandThrows()
    {
        var thrower = NewThrower(Vector3.zero);
        var target = NewTarget(new Vector3(0f, 0f, 10f)); // inside [MinThrowRangeM=4, EngageRangeM=14]
        thrower.SetTarget(target);

        int before = CountAiGrenades();
        bool threw = thrower.TryThrowAt(target, 100f);

        Assert.IsTrue(threw, "in-band throw must succeed");
        Assert.AreEqual(before + 1, CountAiGrenades(), "exactly one grenade spawned");
        Assert.AreEqual(100f, thrower.LastThrowTime, 1e-4f, "cooldown anchor set to explicit clock");
    }

    [Test]
    public void ThrowerOutOfRangeDoesNotThrow()
    {
        var thrower = NewThrower(Vector3.zero);

        var tooClose = NewTarget(new Vector3(2f, 0f, 0f)); // below MinThrowRangeM
        var tooFar = NewTarget(new Vector3(20f, 0f, 0f));  // above default EngageRangeM (14)

        int before = CountAiGrenades();
        Assert.IsFalse(thrower.TryThrowAt(tooClose, 100f), "under min band refuses");
        Assert.IsFalse(thrower.TryThrowAt(tooFar, 100f), "over EngageRangeM refuses");
        Assert.AreEqual(before, CountAiGrenades(), "no grenade spawned out of band");
        Assert.AreEqual(float.NegativeInfinity, thrower.LastThrowTime, "cooldown untouched on refusal");

        // boundary sanity through the pure rules the overload validates with
        Assert.IsTrue(AiThrowRules.ShouldThrow(true, AiThrowRules.MinThrowRangeM, 14f, true), "min boundary inclusive");
        Assert.IsFalse(AiThrowRules.ShouldThrow(true, 14.001f, 14f, true), "over engage range refuses");
    }

    [Test]
    public void CooldownRespectedWithExplicitClock()
    {
        var thrower = NewThrower(Vector3.zero);
        var target = NewTarget(new Vector3(0f, 0f, 10f));
        thrower.SetTarget(target);

        Assert.IsTrue(thrower.TryThrowAt(target, 100f), "first throw succeeds");
        int afterFirst = CountAiGrenades();

        float cooldown = thrower.CooldownSeconds;
        Assert.IsFalse(thrower.TryThrowAt(target, 100f + cooldown - 0.5f), "inside cooldown window refuses");
        Assert.AreEqual(afterFirst, CountAiGrenades(), "no grenade while cooling down");

        Assert.IsTrue(thrower.TryThrowAt(target, 100f + cooldown), "cooldown elapsed throws again");
        Assert.AreEqual(afterFirst + 1, CountAiGrenades(), "second grenade spawned");
        Assert.AreEqual(100f + cooldown, thrower.LastThrowTime, 1e-4f);
    }

    [Test]
    public void ThrowerWithNullTargetRefuses()
    {
        var thrower = NewThrower(Vector3.zero);
        int before = CountAiGrenades();
        Assert.IsFalse(thrower.TryThrowAt(null, 100f), "null target refuses");
        Assert.AreEqual(before, CountAiGrenades());
    }

    [Test]
    public void DirectorRegistersThrowersAndThrowsInBand()
    {
        var directorGo = NewObject("director", Vector3.zero);
        var director = directorGo.AddComponent<AiGrenadeDirector>();

        var t1 = NewThrower(new Vector3(0f, 0f, 0f));
        t1.SetTarget(NewTarget(new Vector3(0f, 0f, 9f)));
        var t2 = NewThrower(new Vector3(50f, 0f, 0f));
        t2.SetTarget(NewTarget(new Vector3(50f, 0f, 12f)));
        var idle = NewThrower(new Vector3(200f, 0f, 0f)); // no target: registered but never throws

        Assert.AreEqual(0, director.RegisteredCount, "nothing registered before first scan");

        int before = CountAiGrenades();
        int thrown = director.ScanOnce(50f);

        Assert.AreEqual(2, thrown, "both in-band throwers fire");
        Assert.AreEqual(3, director.RegisteredCount, "scan cache counts every thrower in scene (targetless one included)");

        Assert.AreEqual(before + 2, CountAiGrenades(), "two grenades spawned");
        Assert.AreEqual(50f, director.LastThrowTime, 1e-4f, "LastThrowTime stamped");

        // cooldown elapsed again -> next scan (interval 2s passed) throws both again
        int thrownAgain = director.ScanOnce(70f);
        Assert.AreEqual(2, thrownAgain, "cooldown elapsed between scans fires again");
        Assert.AreEqual(70f, director.LastThrowTime, 1e-4f);
    }

    [Test]
    public void DirectorDisabledDoesNotScanOrThrow()
    {
        var directorGo = NewObject("director", Vector3.zero);
        var director = directorGo.AddComponent<AiGrenadeDirector>();
        director.Enabled = false;

        var t1 = NewThrower(new Vector3(0f, 0f, 0f));
        t1.SetTarget(NewTarget(new Vector3(0f, 0f, 9f)));

        int before = CountAiGrenades();
        Assert.AreEqual(0, director.ScanOnce(50f), "disabled scan is a no-op");
        Assert.AreEqual(0, director.RegisteredCount, "disabled director performs no scan");
        Assert.AreEqual(before, CountAiGrenades(), "disabled director never throws");

        director.Enabled = true;
        int thrown = director.ScanOnce(50f);
        Assert.AreEqual(1, thrown, "re-enabled director scans and throws");
        Assert.AreEqual(before + 1, CountAiGrenades());
        Assert.AreEqual(50f, director.LastThrowTime, 1e-4f);
    }
}
