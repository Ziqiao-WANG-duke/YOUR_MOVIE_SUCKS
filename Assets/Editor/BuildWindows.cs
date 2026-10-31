using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 一键打 Windows x64 包（可发给别人玩）：
    ///   菜单：YourMovieSucks/Build/Windows x64（发给朋友）
    ///   命令行：Unity.exe -batchmode -projectPath &lt;proj&gt; -executeMethod YourMovieSucks.EditorTools.BuildWindows.BuildFromCommandLine -logFile Logs/build-windows.log
    /// 只打 PolePosition 场景（Build Settings 里另外那个 SampleScene 不进包）。
    /// </summary>
    public static class BuildWindows
    {
        private const string ScenePath = "Assets/Scenes/PolePosition.unity";
        private const string OutputFolder = "Builds/Windows";
        private const string MaskShaderName = "YourMovieSucks/UIHealthCellMask";
        private const string MaskMaterialPath = "Assets/UI/Health/Resources/M_HealthCellMask.mat";

        [MenuItem("YourMovieSucks/Build/Windows x64（发给朋友）")]
        public static void BuildFromMenu()
        {
            if (Build()) EditorUtility.RevealInFinder(Path.GetFullPath(OutputFolder));
        }

        /// <summary>命令行入口（批处理构建用；自己决定退出码）。</summary>
        public static void BuildFromCommandLine()
        {
            bool ok = Build();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Build()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[Build] 找不到场景：{ScenePath}");
                return false;
            }

            EnsureMaskMaterialIncluded();

            string product = PlayerSettings.productName;
            string outDir = Path.GetFullPath(OutputFolder);
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, product + ".exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log($"[Build] 开始：{exePath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] 成功：{exePath}　大小 {summary.totalSize / (1024f * 1024f):0.0} MB　用时 {summary.totalTime.TotalSeconds:0}s");
                return true;
            }

            Debug.LogError($"[Build] 失败：{summary.result}，错误 {summary.totalErrors} 条（详见构建日志）");
            return false;
        }

        /// <summary>
        /// 血条逐格遮罩着色器运行时只用 Shader.Find 取，没有任何材质引用它 → 打正式包时会被剥掉
        /// （表现为「9 格永远是满的」）。这里放一个引用它的材质到 Resources 里，保证它被包含进包。
        /// </summary>
        private static void EnsureMaskMaterialIncluded()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaskMaterialPath) != null) return;

            Shader shader = Shader.Find(MaskShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Build] 找不到着色器 {MaskShaderName}，血格遮罩将无法使用");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/UI/Health/Resources"))
                AssetDatabase.CreateFolder("Assets/UI/Health", "Resources");

            var material = new Material(shader) { name = "M_HealthCellMask" };
            AssetDatabase.CreateAsset(material, MaskMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] 已创建 {MaskMaterialPath}（保证血条遮罩着色器进包）");
        }
    }
}
