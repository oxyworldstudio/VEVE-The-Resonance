using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VEVE.CodeReview;

namespace VEVE.Editor
{
    /// <summary>
    /// Automation entry point: the orchestrator agent runs each registered reviewer
    /// over the runtime/editor sources, reports issues (Debug view) and can enforce.
    /// Batch entry point `RunReviewForGate` makes it callable from the native -batchmode
    /// verification pass (exit-code gating), not only from the editor.
    /// </summary>
    public static class ReviewAutomation
    {
        [MenuItem("VEVE/Code Review/Run Specialist Scan")]
        public static void RunFromMenu()
        {
            IReadOnlyList<ReviewIssue> issues = ScanProject();
            foreach (ReviewIssue issue in issues)
            {
                string tag = issue.severity == ReviewSeverity.Error ? "CR-ERROR" : (issue.severity == ReviewSeverity.Warning ? "CR-WARN" : "CR-INFO");
                Debug.Log($"[{tag}] {issue.ruleId} {issue.file}:{issue.line}: {issue.message}");
            }
            Debug.Log($"[CodeReview] complete: {issues.Count} findings across {CountFiles()}.");
        }

        /// <summary>Non-interactive: return non-zero when blocking errors. -batchmode gate hook.</summary>
        public static int RunReviewForGate()
        {
            IReadOnlyList<ReviewIssue> issues = ScanProject();
            int errors = 0;
            foreach (ReviewIssue i in issues)
            {
                if (i.severity != ReviewSeverity.Error) continue;
                errors++;
                Debug.LogError($"[CodeReview:GATE] {i.ruleId} {i.file}:{i.line}: {i.message}");
            }
            return errors == 0 ? 0 : 1;
        }

        static IReadOnlyList<ReviewIssue> ScanProject()
        {
            var orchestrator = ReviewOrchestrator.CreateDefault();
            var files = new List<KeyValuePair<string, string[]>>();
            foreach (string path in GatherSources())
            {
                try { files.Add(new KeyValuePair<string, string[]>(path, File.ReadAllLines(path))); }
                catch (System.IO.IOException) { }
                catch (System.UnauthorizedAccessException) { }
            }
            return orchestrator.RunAll(files);
        }

        static IEnumerable<string> GatherSources()
        {
            var results = new List<string>();
            string root = Directory.GetParent(Application.dataPath).FullName;
            Collect(root, "Assets/VEVE/Runtime", results);
            Collect(root, "Assets/VEVE/Editor", results);
            return results;
        }

        static void Collect(string root, string relative, List<string> output)
        {
            string path = Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(path)) return;
            foreach (string f in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                string rel = f.Substring(root.Length + 1);
                output.Add(rel);
            }
        }

        static int CountFiles()
        {
            int n = 0;
            foreach (var _ in GatherSources()) n++;
            return n;
        }
    }
}
