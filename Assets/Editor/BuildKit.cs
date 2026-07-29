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

        /// <summary>Headless Linux dedicated-server build — run with <c>-vbhost</c> on the
        /// server box to host a join-code session, or <c>-vbserver</c> for the smoke test.</summary>
        [MenuItem("Volleyball/Build Linux Server", priority = 42)]
        public static void BuildLinuxServer()
            => Build(BuildTarget.StandaloneLinux64, "Builds/LinuxServer/Volleyball.x86_64",
                     StandaloneBuildSubtarget.Server);

        static void Build(BuildTarget target, string output,
                          StandaloneBuildSubtarget subtarget = StandaloneBuildSubtarget.Player)
        {
            // The deploy script stamps every build of a release with one version string
            // (e.g. "0.1.0+a1b2c3d") via this env var; the online version handshake then
            // guarantees only same-release builds can play together.
            string stamp = System.Environment.GetEnvironmentVariable("VB_VERSION");
            if (!string.IsNullOrEmpty(stamp) && PlayerSettings.bundleVersion != stamp)
            {
                PlayerSettings.bundleVersion = stamp;
                Debug.Log($"[Volleyball] build version stamped: {stamp}");
            }

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
                subtarget = (int)subtarget,
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
