using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace YourMovieSucks.LabB.EditorTools
{
    /// <summary>
    /// Lab B 可交付版本打包：Windows x64 Standalone（作业要求"能跑起来的构建包"）。
    ///
    /// 编辑器内：菜单 YourMovieSucks/Build Windows (Lab B)
    /// 批处理（推荐，需要先关掉已打开的编辑器）：
    ///   Unity.exe -batchmode -quit -projectPath &lt;项目路径&gt; ^
    ///     -executeMethod YourMovieSucks.LabB.EditorTools.LabBBuild.BuildWindows ^
    ///     -logFile Logs/labb-build.log
    ///
    /// 产物：Build/Windows/YourMovieSucks_LabB.exe + YourMovieSucks_LabB_Data/
    /// 只打 POLE POSITION 一个场景（原型只需要这一个）。
    /// </summary>
    public static class LabBBuild
    {
        private const string ScenePath = "Assets/Scenes/PolePosition.unity";
        private const string OutputDir = "Build/Windows";
        private const string ExeName = "YourMovieSucks_LabB.exe";

        private static string OutputPath => Path.Combine(OutputDir, ExeName);

        [MenuItem("YourMovieSucks/Build Windows (Lab B)")]
        public static void BuildWindowsFromMenu()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[LabBBuild] 场景不存在：{ScenePath}");
                return;
            }

            bool ok = BuildWindows();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Lab B Build",
                    ok ? $"打包完成：\n{Path.GetFullPath(OutputPath)}" : "打包失败，详见 Console（[LabBBuild]）", "OK");
            }
        }

        /// <summary>批处理入口。失败时以退出码 1 结束，让命令行能看出失败。</summary>
        public static bool BuildWindows()
        {
            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[LabBBuild] OK  {summary.totalSize / (1024f * 1024f):0.0} MB  {summary.totalTime.TotalSeconds:0.0}s  产物：{Path.GetFullPath(OutputPath)}");
                return true;
            }

            Debug.LogError($"[LabBBuild] FAILED  result={summary.result}  errors={summary.totalErrors}  warnings={summary.totalWarnings}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }
    }
}
