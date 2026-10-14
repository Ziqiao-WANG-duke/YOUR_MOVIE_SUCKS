using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YourMovieSucks.Demo;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 批处理自检入口：打开 PolePosition 场景 → 进入 Play Mode → 由 PolePositionAutoDrive 自动打完整个关卡
    /// → 写报告并退出（退出码 0 = 全通过，1 = 有 FAIL，2 = 自检没起来）。
    ///
    /// 用法：
    ///   Unity.exe -batchmode -projectPath &lt;proj&gt; ^
    ///     -executeMethod YourMovieSucks.EditorTools.PolePositionSelfTestRunner.RunHeadless ^
    ///     -logFile Logs/poleposition-selftest.log
    ///
    /// 只改内存里的两个开关（autoStartOnPlay / exitWhenFinished），**不保存场景**，
    /// 所以场景资产里始终是「不自动跑、不自动退编辑器」。
    ///
    /// 注意：不能在 EnterPlaymode 前注册 EditorApplication.update 回调来收尾——进入 Play Mode 会触发
    /// 域重载，静态回调会被清空（这正是先前自检“跑完却挂住”的原因）。退出由驱动器自己完成。
    /// </summary>
    public static class PolePositionSelfTestRunner
    {
        private const string ScenePath = "Assets/Scenes/PolePosition.unity";
        private const string ReportPath = "Library/Locus/tmp/poleposition-selftest.txt";

        [MenuItem("YourMovieSucks/Run POLE POSITION Self-Test (Play Mode)")]
        public static void RunHeadless()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var drive = UnityEngine.Object.FindFirstObjectByType<PolePositionAutoDrive>();
            if (drive == null)
            {
                Debug.LogError("[PolePositionSelfTest] PolePositionAutoDrive not found in scene");
                EditorApplication.Exit(2);
                return;
            }

            var so = new SerializedObject(drive);
            SetBool(so, "autoStartOnPlay", true);
            SetBool(so, "exitWhenFinished", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                if (File.Exists(ReportPath)) File.Delete(ReportPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PolePositionSelfTest] could not clear previous report: " + e.Message);
            }

            Debug.Log("[PolePositionSelfTest] entering play mode");
            EditorApplication.EnterPlaymode();
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[PolePositionSelfTest] field '{field}' not found");
                return;
            }

            property.boolValue = value;
        }
    }
}
