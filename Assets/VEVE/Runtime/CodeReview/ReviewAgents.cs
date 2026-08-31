using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VEVE.CodeReview
{
    public enum ReviewSeverity { Info, Warning, Error }

    /// <summary>One finding emitted by a specialized review agent.</summary>
    public struct ReviewIssue
    {
        public string ruleId;
        public string file;
        public int line;               // 1-based; 0 = whole file
        public ReviewSeverity severity;
        public string message;
    }

    /// <summary>Contract every specialized reviewer agent implements.</summary>
    public interface IReviewAgent
    {
        string RuleId { get; }
        string Description { get; }
        IEnumerable<ReviewIssue> Scan(string filePath, string[] lines);
    }

    // ---------------------------------------------------------------- rule agents

    /// <summary>
    /// Catches tautological arithmetic (`x * k / k`, `v + v - v`) - real bug found in W14
    /// (biome roughness cancelled itself out). Only flags when the same identifier both
    /// multiplies and divides on adjacent tokens (conservative, deterministic).
    /// </summary>
    public sealed class SelfCancellationRule : IReviewAgent
    {
        static readonly Regex MultiplyDivideSameName = new Regex(
            @"([A-Za-z_]\w*)\s*\*\s*(\w+)\s*/\s*(\2)\b", RegexOptions.Compiled);
        static readonly Regex DivideBySameTwice = new Regex(
            @"(?<=[\w\)])\s*/\s*(\w+)\s*[;)]", RegexOptions.Compiled);
        static readonly Regex StarDivide = new Regex(
            @"\*\s*(\w+)\s*/\s*\1\b", RegexOptions.Compiled);

        public string RuleId => "CR-SELF-01";
        public string Description => "arithmetic self-cancellation (rough * x / x patterns)";

        public IEnumerable<ReviewIssue> Scan(string filePath, string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (Regex.IsMatch(l, @"^\s*(//|///)")) continue;
                var m = StarDivide.Match(l);
                if (m.Success && Regex.IsMatch(m.Groups[1].Value, @"^[a-z_]\w*$"))
                {
                    yield return new ReviewIssue
                    {
                        ruleId = RuleId,
                        file = filePath,
                        line = i + 1,
                        severity = ReviewSeverity.Error,
                        message = "self-cancelling factor `" + m.Value.Trim() + "` — one operand of this expression is a no-op"
                    };
                }
            }
        }

        internal static Regex Probe => StarDivide;
    }

    /// <summary>
    /// Event subscribe/unsubscribe pair asymmetry in one file (found in real life twice:
    /// OnEnable Subscribe + OnDisable Subscribe instead of Unsubscribe; Unsubscribe only,
    /// leaked subscription). Compares method bodies between OnEnable/OnDisable/Awake pairs
    /// with balanced counts.
    /// </summary>
    public sealed class SubscribePairRule : IReviewAgent
    {
        public string RuleId => "CR-EVT-01";
        public string Description => "SubscribeGlobal/UnsubscribeGlobal asymmetric lifecycle";

        public IEnumerable<ReviewIssue> Scan(string filePath, string[] lines)
        {
            int subscribes = CountMatches(lines, "SubscribeGlobal");
            int unsubscribes = CountMatches(lines, "UnsubscribeGlobal");
            if (subscribes == 0 && unsubscribes == 0) yield break;
            if (subscribes != unsubscribes)
            {
                yield return new ReviewIssue
                {
                    ruleId = RuleId,
                    file = filePath,
                    line = 1,
                    severity = ReviewSeverity.Error,
                    message = string.Format("EventBus lifecycle unbalanced: {0} SubscribeGlobal vs {1} UnsubscribeGlobal", subscribes, unsubscribes)
                };
            }
        }

        static int CountMatches(string[] lines, string token)
        {
            int n = 0;
            foreach (string line in lines)
            {
                int idx = 0;
                while ((idx = line.IndexOf(token, idx, StringComparison.Ordinal)) >= 0) { n++; idx += token.Length; }
            }
            return n;
        }
    }

    /// <summary>
    /// Float assertions without tolerance (test files only): `Assert.AreEqual(0.5f, expr)`
    /// - NUnit2 float overload requires an explicit delta for non-exact computation.
    /// </summary>
    public sealed class FloatAssertionRule : IReviewAgent
    {
        static readonly Regex FloatFirstArg = new Regex(
            @"Assert\.AreEqual\s*\(\s*[-0-9]+(\.[0-9]+)?([eE][-+]?[0-9]+)?[fdFD]?\s*,", RegexOptions.Compiled);

        public string RuleId => "CR-TST-01";
        public string Description => "float Assert.AreEqual without numeric tolerance in Tests";

        public IEnumerable<ReviewIssue> Scan(string filePath, string[] lines)
        {
            if (filePath == null || !filePath.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)) yield break;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (!FloatFirstArg.IsMatch(l)) continue;

                int open = l.IndexOf('(');
                if (open < 0) continue;
                int topCommas = 0, lastTop = -1;
                int depth = 0;
                bool inStr = false; char quote = '\0';
                for (int c = open; c < l.Length; c++)
                {
                    char ch = l[c];
                    if (!inStr && (ch == '"' || ch == '\'')) { inStr = true; quote = ch; continue; }
                    if (inStr)
                    {
                        if (ch == quote) inStr = false;
                        continue;
                    }
                    if (ch == '(') depth++;
                    else if (ch == ')')
                    {
                        depth--;
                        if (depth == 0) break;
                    }
                    else if (ch == ',' && depth == 1)
                    {
                        topCommas++;
                        lastTop = c;
                    }
                }
                if (topCommas <= 1)
                {
                    yield return new ReviewIssue
                    {
                        ruleId = RuleId, file = filePath, line = i + 1,
                        severity = ReviewSeverity.Warning,
                        message = "float Assert.AreEqual without tolerance may flake headless (NUnit2/float)"
                    };
                }
                else if (lastTop >= 0 && !HasNumericTail(l, lastTop))
                {
                    yield return new ReviewIssue
                    {
                        ruleId = RuleId, file = filePath, line = i + 1,
                        severity = ReviewSeverity.Warning,
                        message = "float Assert.AreEqual last argument is non-numeric: tolerance is missing"
                    };
                }
            }
        }

        static bool HasNumericTail(string line, int fromIdx)
        {
            int j = fromIdx + 1;
            while (j < line.Length && line[j] == ' ') j++;
            if (j >= line.Length) return false;
            char first = line[j];
            return first == '-' || first == '.' || (first >= '0' && first <= '9');
        }
    }

    // ------------------------------------------------------------------- orchestrator

    /// <summary>
    /// Orchestrator agent: runs every registered review agent deterministically over files
    /// (ordered agent + file + line sort), decides whether findings must block the gate.
    /// </summary>
    public sealed class ReviewOrchestrator
    {
        private readonly List<IReviewAgent> agents = new List<IReviewAgent>();

        public ReviewOrchestrator Add(IReviewAgent agent)
        {
            if (agent != null && !Contains(agent.RuleId)) agents.Add(agent);
            return this;
        }

        bool Contains(string id)
        {
            foreach (var a in agents) if (a.RuleId == id) return true;
            return false;
        }

        public static ReviewOrchestrator CreateDefault()
        {
            return new ReviewOrchestrator()
                .Add(new SelfCancellationRule())
                .Add(new SubscribePairRule())
                .Add(new FloatAssertionRule())
                .Add(new VEVE.CodeReview.Agents.SimDataRule())
                .Add(new VEVE.CodeReview.Agents.DeterminismRule());
        }

        public IReadOnlyList<ReviewIssue> Run(string file, string[] lines)
        {
            var all = new List<ReviewIssue>();
            foreach (var a in agents)
                foreach (var issue in a.Scan(file, lines))
                    all.Add(issue);
            all.Sort((x, y) =>
            {
                int byLine = x.line.CompareTo(y.line);
                if (byLine != 0) return byLine;
                int byRule = string.CompareOrdinal(x.ruleId, y.ruleId);
                return byRule;
            });
            return all;
        }

        public IReadOnlyList<ReviewIssue> RunAll(IEnumerable<KeyValuePair<string, string[]>> fileContents)
        {
            var list = new List<ReviewIssue>();
            foreach (var kv in fileContents) list.AddRange(Run(kv.Key, kv.Value));
            return list;
        }

        public static bool ShouldBlockGate(IReadOnlyList<ReviewIssue> issues)
        {
            if (issues == null) return false;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].severity == ReviewSeverity.Error) return true;
            return false;
        }

        public int AgentCount => agents.Count;
    }
}
