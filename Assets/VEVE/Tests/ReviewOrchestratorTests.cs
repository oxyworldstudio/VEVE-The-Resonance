using System.Collections.Generic;
using NUnit.Framework;
using VEVE.CodeReview;

public sealed class ReviewOrchestratorTests
{
    static (string[] lines, List<ReviewIssue> issues) run(IReviewAgent agent, string file, string[] lines)
    {
        var found = new List<ReviewIssue>();
        foreach (var i in agent.Scan(file, lines)) found.Add(i);
        return (lines, found);
    }

    [Test]
    public void SelfCancellationAgentCatchesRoughCancel()
    {
        var (_, issues) = run(new SelfCancellationRule(), "X.cs", new[]
        {
            "float x;",
            "int a = Fold(q, S) * rough / rough;",   // the W14 incident
            "int b = a + c;",
        });
        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual("CR-SELF-01", issues[0].ruleId);
        Assert.AreEqual(ReviewSeverity.Error, issues[0].severity);
    }

    [Test]
    public void SelfCancellationDoesNotFireOnCommentsOrLegitMath()
    {
        var (_, issues) = run(new SelfCancellationRule(), "X.cs", new[]
        {
            "// amplitude = wave * rough / rough (old experiment, removed)", // comment line: must be skipped
            "float scale = range * 2f; // half then doubled by hand",      // normal code has no same-token division",",",
            "int m = mass * grams / 1000;",                                 // different symbol denominator: legit
        });
        Assert.AreEqual(0, issues.Count);
    }

    [Test]
    public void LifecycleAgentFindsAsymmetricSubscribe()
    {
        var (_, issues) = run(new SubscribePairRule(), "L.cs", new[]
        {
            "void OnEnable() { VEVE.EventBus.SubscribeGlobal<Foo>(OnFoo); }",
            "void OnDisable() { VEVE.EventBus.SubscribeGlobal<Foo>(OnFoo); }", // wrong on purpose
        });
        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual("CR-EVT-01", issues[0].ruleId);
    }

    [Test]
    public void LifecycleCleanAndNoEventIgnored()
    {
        var (_, issues) = run(new SubscribePairRule(), "OK.cs", new[]
        {
            "void OnEnable() { VEVE.EventBus.SubscribeGlobal<Foo>(OnFoo); }",
            "void OnDisable() { VEVE.EventBus.UnsubscribeGlobal<Foo>(OnFoo); }",
        });
        Assert.AreEqual(0, issues.Count);
        (_, issues) = run(new SubscribePairRule(), "NoEvents.cs", new[] { "class A {}", "int x=0;" });
        Assert.AreEqual(0, issues.Count);
    }

    [Test]
    public void FloatAssertionAgentOnlyForTestFiles()
    {
        var (_, issues) = run(new FloatAssertionRule(), "SomeTests.cs", new[]
        {
            "Assert.AreEqual(0.5f, Computed(x), \"why it must be half\");", // missing delta
            "Assert.AreEqual(2f, f, 1e-4f);",                                // ok
            "Assert.AreEqual(\"a\", b, \"c\");",                             // non-numeric first arg
        });
        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(ReviewSeverity.Warning, issues[0].severity);
        (_, issues) = run(new FloatAssertionRule(), "RuntimeStuff.cs", new[] { "Assert.AreEqual(0.5f, x);" });
        Assert.AreEqual(0, issues.Count, "only test files get checked");
    }

    [Test]
    public void OrchestratorAggregatesDeterministicallyAndBlocksOnError()
    {
        var o = ReviewOrchestrator.CreateDefault();
        Assert.GreaterOrEqual(o.AgentCount, 3);

        var fileA = new[] { "int a = h * rough / rough;", "void OnEnable(){ Event.SubscribeGlobal<A>(x);}", "void OnDisable(){}" };
        var inputs = new List<KeyValuePair<string, string[]>>
        {
            new KeyValuePair<string, string[]>("src/fileA", fileA),
        };
        var issues = o.RunAll(inputs);
        Assert.GreaterOrEqual(issues.Count, 2, "two different rule families fired together");
        Assert.IsTrue(ReviewOrchestrator.ShouldBlockGate(issues), "Error severity must block the gate");

        var (_, single) = (System.Array.Empty<string>(), new List<ReviewIssue>());
        var sorted = o.Run("x", new[] { "int a1 = p + q;  int b1 = a1;", "int a2 = p * r / r;" });
        int prevLine = 0;
        foreach (var i in sorted) { Assert.GreaterOrEqual(i.line, prevLine); prevLine = i.line; }

        // warnings-only never blocks
        var warn = new List<ReviewIssue> { new ReviewIssue { severity = ReviewSeverity.Warning } };
        Assert.IsFalse(ReviewOrchestrator.ShouldBlockGate(warn));
        Assert.IsFalse(ReviewOrchestrator.ShouldBlockGate(null));
    }

    [Test]
    public void AgentsRunOnEmptyAndNullSafety()
    {
        var o = ReviewOrchestrator.CreateDefault().Add(new SelfCancellationRule()); // idempotent: same rule ignored
        Assert.AreEqual(3, o.AgentCount);
        Assert.AreEqual(0, o.Run("empty", System.Array.Empty<string>()).Count);
    }
}
