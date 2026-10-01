// 职责：C05菜单复验与Windows测试构建；显式选新地图，产物写忽略目录，不修改全局Build Settings。
// 维护：controller；Editor程序集依赖C05/UnityEditor。测试包不是音频同学的最终封装。
// 规范：根AGENTS.md；交接：docs/handoffs/controller.handoff。
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Regrowth.Tests.C05
{
    public static class C05EditorTools
    {
        public static bool BuildInProgress { get; private set; }
        public static string LastOutputDirectory { get; private set; }

        [MenuItem("Tools/pawgatory/C05/Run New Map Checks (Play)")]
        public static void RunChecks()
        {
            try
            {
                NewMapCheckLauncher.StartChecks();
            }
            catch (Exception error)
            {
                Debug.LogError(error.Message);
            }
        }

        [MenuItem("Tools/pawgatory/C05/Build Windows Trial")]
        private static void BuildTrialMenu()
        {
            QueueBuild(false);
        }

        [MenuItem("Tools/pawgatory/C05/Build Windows Automated Checks")]
        private static void BuildChecksMenu()
        {
            QueueBuild(true);
        }

        /// <summary>排队构建并返回唯一产物目录；只接受干净非Play单地图，失败抛异常，不覆盖旧产物或改全局设置。</summary>
        public static string QueueBuild(bool automatedChecks)
        {
            var scene = SceneManager.GetActiveScene();
            if (BuildInProgress || BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || SceneManager.sceneCount != 1 || scene.isDirty
                || scene.path != NewMapCheckLauncher.ScenePath)
            {
                throw new InvalidOperationException("先打开并保存Level_Whitebox，退出Play/其他构建，再执行C05构建。");
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64
                || !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("需要当前Windows64目标及已安装的Windows构建支持；工具不自动切换/安装。");
            }
            string suffix = automatedChecks ? "checks" : "trial";
            string root = Directory.GetParent(Application.dataPath).FullName;
            LastOutputDirectory = Path.Combine(root, "Builds", "C05",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + suffix + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(LastOutputDirectory);
            string output = LastOutputDirectory;
            BuildInProgress = true;
            EditorApplication.delayCall += () => Build(output, automatedChecks);
            return output;
        }

        private static void Build(string output, bool checks)
        {
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { NewMapCheckLauncher.ScenePath },
                    locationPathName = Path.Combine(output, "pawgatory-C05-" + (checks ? "checks" : "trial") + ".exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development | BuildOptions.DetailedBuildReport,
                    extraScriptingDefines = checks ? new[] { "C05_PLAYER_CHECKS" } : Array.Empty<string>()
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                string summary = "C05_BUILD_RESULT " + report.summary.result
                    + "\nMode=" + (checks ? "Automated checks only" : "Manual trial - not final release")
                    + "\nScene=" + NewMapCheckLauncher.ScenePath
                    + "\nUnity=" + Application.unityVersion
                    + "\nErrors=" + report.summary.totalErrors
                    + "\nWarnings=" + report.summary.totalWarnings
                    + "\nBytes=" + report.summary.totalSize
                    + "\nDuration=" + report.summary.totalTime
                    + "\nOutput=" + report.summary.outputPath;
                File.WriteAllText(Path.Combine(output, "C05-build-report.txt"), summary);
                Debug.Log(summary);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "C05-build-report.txt"), "C05_BUILD_RESULT Exception\n" + error);
                Debug.LogException(error);
            }
            finally
            {
                BuildInProgress = false;
            }
        }
    }
}
