using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VEVE.Editor
{
    public static class WebGLBuilder
    {
        public static void Build()
        {
            const string outputPath = "Builds/WebGL";
            Directory.CreateDirectory(outputPath);

            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/VEVE_Milestone1.unity" },
                outputPath,
                BuildTarget.WebGL,
                BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"WebGL build failed with result {report.summary.result}.");
            }

            Debug.Log($"WebGL build completed: {report.summary.totalSize} bytes.");
        }
    }
}
