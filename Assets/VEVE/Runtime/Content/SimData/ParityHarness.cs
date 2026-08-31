using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VEVE.CodeReview;

namespace VEVE.Content.SimData
{
    /// <summary>
    /// Parity harness used by the strangler migration (roadmap F0):
    /// old-system output must be bitwise identical to the new Sim system.
    /// Reports first mismatch position for CI diagnostics.
    /// </summary>
    public static class ParityHarness
    {
        public struct Result
        {
            public bool Match;
            public int FirstMismatchIndex;
            public string Detail;
        }

        public static Result Compare(byte[] expected, byte[] actual)
        {
            if (expected == null || actual == null)
                return new Result { Match = ReferenceEquals(expected, actual), FirstMismatchIndex = 0, Detail = "null operand" };
            int n = Math.Min(expected.Length, actual.Length);
            for (int i = 0; i < n; i++)
            {
                if (expected[i] != actual[i]) return Fail(i, "byte " + i + ": " + expected[i] + " != " + actual[i]);
            }
            if (expected.Length != actual.Length)
                return Fail(n, "length " + expected.Length + " vs " + actual.Length);
            return new Result { Match = true, FirstMismatchIndex = -1 };
        }

        public static Result CompareText(string expected, string actual)
        {
            if (expected == null || actual == null)
                return new Result { Match = expected == actual, FirstMismatchIndex = 0, Detail = "null text" };
            if (expected == actual) return new Result { Match = true, FirstMismatchIndex = -1 };
            int n = Math.Min(expected.Length, actual.Length);
            for (int i = 0; i < n; i++)
            {
                if (expected[i] != actual[i]) return Fail(i, "char " + i + " exp '" + expected[i] + "' got '" + actual[i] + "'");
            }
            return Fail(n, "prefix-equal, tail differs len " + expected.Length + " vs " + actual.Length);
        }

        public static uint Hash(string text)
        {
            unchecked
            {
                uint h = SimDataPack.FnvOffset;
                if (text != null) for (int i = 0; i < text.Length; i++) h = (h ^ text[i]) * SimDataPack.FnvPrime;
                return h;
            }
        }

        static Result Fail(int index, string detail)
        {
            return new Result { Match = false, FirstMismatchIndex = index, Detail = detail };
        }
    }
}

namespace VEVE.CodeReview.Agents
{
    /// <summary>CR-DAT-01: Sim systems must not hold magic numeric literals (F0 SimData rule).</summary>
    public sealed class SimDataRule : IReviewAgent
    {
        static readonly Regex MagicFloat = new Regex(
            @"=\s*\d+\.\d+f?\s*[;)]", RegexOptions.Compiled);

        public string RuleId => "CR-DAT-01";
        public string Description => "magic numeric literal in Sim/Systems (move to SimData)";

        public IEnumerable<ReviewIssue> Scan(string filePath, string[] lines)
        {
            if (!IsSimPath(filePath)) yield break;
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (Regex.IsMatch(l, @"^\s*(//|///)")) continue;
                if (!MagicFloat.IsMatch(l)) continue;
                if (l.Contains("SimData", StringComparison.Ordinal)) continue; // explicit data read is legit
                yield return new ReviewIssue
                {
                    ruleId = RuleId,
                    file = filePath,
                    line = i + 1,
                    severity = ReviewSeverity.Warning,
                    message = "numeric constant in sim path - bind to SimData table instead"
                };
            }
        }

        internal static bool IsSimPath(string p)
        {
            return p != null && (p.Replace('\\', '/').Contains("/VEVE/Sim") || p.Replace('\\', '/').Contains("VEVE/Runtime/Sim"));
        }
    }

    /// <summary>CR-DET-01: sim path must not use wall clock / unseeded RNG (replay determinism).</summary>
    public sealed class DeterminismRule : IReviewAgent
    {
        static readonly Regex Forbidden = new Regex(
            @"\b(DateTime|System\.Random|UnityEngine\.Random|Environment\.TickCount64|Environment\.TickCount)\b",
            RegexOptions.Compiled);

        public string RuleId => "CR-DET-01";
        public string Description => "wall-clock/RNG in Sim path breaks journal determinism";

        public IEnumerable<ReviewIssue> Scan(string filePath, string[] lines)
        {
            if (!SimDataRule.IsSimPath(filePath)) yield break;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (!Forbidden.IsMatch(l)) continue;
                if (l.Contains("[sim-allowed]", StringComparison.Ordinal)) continue;
                yield return new ReviewIssue
                {
                    ruleId = RuleId,
                    file = filePath,
                    line = i + 1,
                    severity = ReviewSeverity.Error,
                    message = "nondeterministic source in sim context: " + Forbidden.Match(l).Value
                };
            }
        }
    }
}
