using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Courtyard.Editor
{
    /// <summary>
    /// Command-line builds. In batch mode the editor exits with code 1 on failure.
    /// Usage: Unity -batchmode -quit -projectPath . -executeMethod Courtyard.Editor.BuildScript.BuildWebGL
    /// </summary>
    public static class BuildScript
    {
        public static readonly string[] Scenes =
        {
            "Assets/_Game/Scenes/Boot.unity",
            "Assets/_Game/Scenes/Kitchen.unity",
            "Assets/_Game/Scenes/Ending.unity",
        };

        private const string WebGLOutput = "Builds/WebGL";
        private const string MacOutput = "Builds/Mac/AcrossTheCourtyard.app";

        [MenuItem("Courtyard/Build/WebGL")]
        public static void BuildWebGL()
        {
            ConfigureWebGL();
            Build(BuildTarget.WebGL, WebGLOutput);
        }

        [MenuItem("Courtyard/Build/macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, MacOutput);

        /// <summary>itch.io-friendly settings: gzip with decompression fallback, minimal template.</summary>
        public static void ConfigureWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            BuildReport report;
            try
            {
                foreach (var scene in Scenes)
                {
                    if (!File.Exists(scene)) throw new FileNotFoundException($"Build scene missing: {scene}");
                }

                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = outputPath,
                    target = target,
                    options = BuildOptions.None,
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Fail();
                return;
            }

            var summary = report.summary;
            Debug.Log($"[BuildScript] {target}: {summary.result}, {summary.totalErrors} errors, " +
                      $"{summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime:g} → {outputPath}");

            if (summary.result != BuildResult.Succeeded) Fail();
        }

        private static void Fail()
        {
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
