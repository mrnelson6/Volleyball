using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Player builds from the menu or headless (<c>-executeMethod ...BuildKit.BuildWindows</c>).
    /// Windows is the share-with-friends build (zip the folder, send it, run the exe — the
    /// same binary also runs the dedicated-server smoke via <c>-vbserver</c>). WebGL needs its
    /// build-support module installed via Unity Hub, so it checks first and says so.
    /// </summary>
    public static class BuildKit
    {
        [MenuItem("Volleyball/Build Windows Player", priority = 40)]
        public static void BuildWindows()
            => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Volleyball.exe");

        [MenuItem("Volleyball/Build WebGL Player", priority = 41)]
        public static void BuildWebGL()
            => Build(BuildTarget.WebGL, "Builds/WebGL");

        static void Build(BuildTarget target, string output)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Unknown, target))
            {
                Debug.LogError($"[Volleyball] BUILD FAIL — {target} build support is not " +
                               "installed. Add it to this editor version via Unity Hub.");
                return;
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled).Select(s => s.path).ToArray();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            });

            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[Volleyball] BUILD OK — {target} at {s.outputPath}, " +
                          $"{s.totalSize / (1024 * 1024)} MB in {s.totalTime.TotalSeconds:F0}s.");
            else
                Debug.LogError($"[Volleyball] BUILD FAIL — {target}: {s.result}, " +
                               $"{s.totalErrors} errors.");
        }
    }
}
