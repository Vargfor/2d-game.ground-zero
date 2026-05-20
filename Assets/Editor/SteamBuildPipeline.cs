#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GroundZero.PuzzleCore.Editor
{
    public static class SteamBuildPipeline
    {
        private const string ProductName = "Puzzle Core Ground Zero";
        private const string CompanyName = "Ground Zero";
        private const string BuildRoot = "Builds/Steam";

        [MenuItem("Build/Steam/Configure Desktop Player")]
        public static void ConfigureDesktopPlayer()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, true);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured desktop player settings for " + ProductName + ".");
        }

        [MenuItem("Build/Steam/Build All Desktop Targets")]
        public static void BuildAllDesktopTargets()
        {
            ConfigureDesktopPlayer();
            BuildWindows();
            BuildMac();
            BuildLinux();
        }

        [MenuItem("Build/Steam/Build Windows x64")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, Path.Combine(BuildRoot, "Windows", ProductName + ".exe"));
        }

        [MenuItem("Build/Steam/Build macOS")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, Path.Combine(BuildRoot, "macOS", ProductName + ".app"));
        }

        [MenuItem("Build/Steam/Build Linux x64")]
        public static void BuildLinux()
        {
            Build(BuildTarget.StandaloneLinux64, Path.Combine(BuildRoot, "Linux", "PuzzleCoreGroundZero.x86_64"));
        }

        public static void BuildAllFromCommandLine()
        {
            BuildAllDesktopTargets();
        }

        public static void BuildWindowsFromCommandLine()
        {
            ConfigureDesktopPlayer();
            BuildWindows();
        }

        public static void BuildMacFromCommandLine()
        {
            ConfigureDesktopPlayer();
            BuildMac();
        }

        public static void BuildLinuxFromCommandLine()
        {
            ConfigureDesktopPlayer();
            BuildLinux();
        }

        private static void Build(BuildTarget target, string locationPath)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are listed in EditorBuildSettings.");
            }

            var directory = Path.GetDirectoryName(locationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                locationPathName = locationPath,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("Build failed for " + target + ": " + report.summary.result);
            }

            WriteBuildReadme(target, locationPath, report);
            Debug.Log("Built " + target + " to " + locationPath + " in " + report.summary.totalTime + ".");
        }

        private static void WriteBuildReadme(BuildTarget target, string locationPath, BuildReport report)
        {
            var directory = Path.GetDirectoryName(locationPath);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            var readme = Path.Combine(directory, "README_STEAM_DEPOT.txt");
            File.WriteAllText(readme,
                ProductName + Environment.NewLine +
                "Target: " + target + Environment.NewLine +
                "Build result: " + report.summary.result + Environment.NewLine +
                "Total size: " + report.summary.totalSize + " bytes" + Environment.NewLine +
                Environment.NewLine +
                "Before uploading this folder to SteamPipe:" + Environment.NewLine +
                "1. Replace any local testing AppID with the real Steam app ID." + Environment.NewLine +
                "2. Upload this target to its matching Windows, macOS, or Linux depot." + Environment.NewLine +
                "3. Run the build from Steam on each platform and verify save data, fullscreen mode, and controller/mouse input." + Environment.NewLine);
        }
    }
}
#endif
