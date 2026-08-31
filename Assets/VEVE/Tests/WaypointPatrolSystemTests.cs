using NUnit.Framework;
using UnityEngine;
using VEVE.AI;

public sealed class WaypointPatrolSystemTests
{
    private static PatrolRoute Route(PatrolMode mode, params float[] waits)
    {
        var pts = new Waypoint[waits.Length];
        for (int i = 0; i < waits.Length; i++)
            pts[i] = new Waypoint { position = new Vector3(i * 10f, 0f, i * 3f), waitSeconds = waits[i] };
        return new PatrolRoute { id = "T", points = pts, mode = mode, applyJitter = true };
    }

    [Test]
    public void LoopWrapsForever()
    {
        var r = Route(PatrolMode.Loop, 0f, 0f, 0f);
        var s = new PatrolState();
        s.Start(r);
        Assert.AreEqual(0, s.CurrentIndex);
        for (int i = 0; i < 10; i++)
        {
            s.Arrive(r);
            Assert.IsFalse(s.Done);
        }
        s.Start(r); s.Arrive(r); s.Arrive(r); s.Arrive(r);
        Assert.AreEqual(0, s.CurrentIndex, "three arrivals on 3-node loop back to start");
    }

    [Test]
    public void OnceFinishesAfterLastNode()
    {
        var r = Route(PatrolMode.Once, 0f, 0f);
        var s = new PatrolState();
        s.Start(r);
        s.Arrive(r); s.Arrive(r);
        Assert.IsTrue(s.Done);
        int before = s.CurrentIndex;
        s.Arrive(r);
        Assert.AreEqual(before, s.CurrentIndex, "done states do not advance");
    }

    [Test]
    public void PingPongBouncesBackAndForth()
    {
        var r = Route(PatrolMode.PingPong, 0f, 0f, 0f);
        var s = new PatrolState();
        s.Start(r);
        var seen = new System.Collections.Generic.List<int> { s.CurrentIndex };
        for (int i = 0; i < 5; i++) { s.Arrive(r); seen.Add(s.CurrentIndex); }
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 1, 0, 1 }, seen);
    }

    [Test]
    public void DwellGatesProgressWithTime()
    {
        var r = Route(PatrolMode.Loop, 0f, 2f, 0f);
        var s = new PatrolState();
        s.Start(r);
        s.Arrive(r); // moved to node1 which holds 2s
        Assert.IsTrue(s.IsWaiting);
        s.Tick(1f);
        Assert.IsTrue(s.IsWaiting);
        s.Tick(1.1f);
        Assert.IsFalse(s.IsWaiting, "hold expires with time");
        s.Arrive(r);
        Assert.AreEqual(2, s.CurrentIndex);
    }

    [Test]
    public void JitterIsDeterministicPerUnitAndVariesBetweenUnits()
    {
        var r = Route(PatrolMode.Loop, 0f, 0f);
        var s = new PatrolState();
        s.Start(r);
        var unit = new Vector3(100f, 0f, 5f);
        Vector3 a = s.Destination(r, unit, 7u);
        Vector3 b = s.Destination(r, unit, 7u);
        Assert.AreEqual(a.x, b.x, 1e-6f);
        Assert.AreEqual(a.z, b.z, 1e-6f);

        bool anyDiff = false;
        foreach (uint id in new uint[] { 7u, 8u, 9u, 10u })
            if (Vector3.Distance(a, s.Destination(r, unit, id)) > 0.01f) { anyDiff = true; break; }
        Assert.IsTrue(anyDiff, "squad members must not share one painted-line path");
    }

    [Test]
    public void JitterFadesNearAndRespectsFlag()
    {
        var r = Route(PatrolMode.Loop, 0f, 0f);
        var s = new PatrolState();
        s.Start(r);
        var far = new Vector3(200f, 0f, 0f);
        Vector3 target = s.Destination(r, far, 7u);
        Vector3 node = r.points[0].position;
        Assert.Greater(Mathf.Abs(Vector3.Distance(target, far) - Vector3.Distance(node, far)), 0.001f,
            "far from node the offset must exist");

        r.applyJitter = false;
        Assert.AreEqual(node, s.Destination(r, new Vector3(1f, 1f, 1f), 7u), "flag off: exact waypoint");
    }

    [Test]
    public void EmptyRouteIsDoneImmediately()
    {
        var s = new PatrolState();
        s.Start(null);
        Assert.IsTrue(s.Done);
        var empty = new PatrolRoute { points = new Waypoint[0] };
        s.Start(empty);
        Assert.IsTrue(s.Done);
    }
}
