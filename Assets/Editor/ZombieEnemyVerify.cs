using System.IO;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 丧尸敌人的自动验证：跑一次关卡自检（PolePositionSelfTestRunner）的同时，逐帧记录怪物状态，
    /// 结论写进 `Library/Locus/tmp/zombie-probe.txt`。用来回答三个不能靠肉眼猜的问题：
    ///   ① 动画真的在动吗（Animator 当前是哪个状态）
    ///   ② 动画是不是「原地」的（Hips 相对怪物根节点的位移有没有一路跑远 → 会飘离自己的逻辑位置）
    ///   ③ 模型是不是站在台面上（SkinnedMeshRenderer 包围盒最低点 vs 舞台面 0.5）
    /// 截图由自检自带的 Capture（Library/Locus/tmp/selftest-shots）负责，这里不重复拍。
    ///
    /// 注意：静态回调会被「进入 Play Mode 的域重载」清空，所以探针不在调用点注册，
    /// 而是靠 SessionState 留个标记，由 [InitializeOnLoad] 在重载后自己重接 EditorApplication.update。
    ///
    /// 用法：
    ///   Unity.exe -batchmode -projectPath &lt;proj&gt;
    ///     -executeMethod YourMovieSucks.EditorTools.ZombieEnemyVerify.RunWithProbe
    ///     -logFile Logs/zombie-probe.log
    /// </summary>
    [InitializeOnLoad]
    public static class ZombieEnemyVerify
    {
        private const string ProbeFlag = "YourMovieSucks.ZombieProbe";
        private const string ProbeFile = "Library/Locus/tmp/zombie-probe.txt";

        private static float _nextSample;
        private static bool _frameCapApplied;

        static ZombieEnemyVerify()
        {
            if (SessionState.GetBool(ProbeFlag, false))
                EditorApplication.update += Tick;
        }

        public static void RunWithProbe()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProbeFile) ?? ".");
            File.WriteAllText(ProbeFile, "== zombie probe (self-test + capture) ==\n");
            SessionState.SetBool(ProbeFlag, true);
            Debug.Log("[ZombieEnemyVerify] 开始：跑关卡自检并记录怪物状态");

            PolePositionSelfTestRunner.RunHeadless();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                // 退出 Play Mode（自检结束或被中止）：收尾解绑，避免回调留到下一次
                EditorApplication.update -= Tick;
                SessionState.SetBool(ProbeFlag, false);
                return;
            }

            float now = Time.realtimeSinceStartup;

            // 批处理没有 vsync，自检会跑到 ~1100fps：而打字音效有 0.03s 的**真实时间**节流，
            // 帧率高到键间隔不足 0.03s 时，音效计数会远低于真机（上次 29 pass / 0 fail 是在编辑器内 60fps 跑的）。
            // 这里只给本次探针运行限个帧率，让无人值守跑分的行为像真机；不改任何工程设置。
            if (!_frameCapApplied)
            {
                _frameCapApplied = true;
                Application.targetFrameRate = 60;
                Log($"t={now,6:0.00} 探针：targetFrameRate=60（仅本次运行）");
            }

            if (now < _nextSample) return;
            _nextSample = now + 0.4f;

            var zombies = Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None);
            if (zombies.Length == 0)
            {
                Log($"t={now,6:0.00} 敌人=0");
                return;
            }

            var zombie = zombies[0];
            var animator = zombie.GetComponentInChildren<Animator>();
            var skinned = zombie.GetComponentInChildren<SkinnedMeshRenderer>();
            var hips = FindHips(zombie.transform);

            string animState = "<无 Animator>";
            if (animator != null)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                animState = $"{StateName(info)} t={info.normalizedTime:0.00} speed={animator.speed:0.00} enabled={animator.enabled} trans={animator.IsInTransition(0)}";
            }

            var model = animator != null ? animator.transform : null;
            string modelText = model != null
                ? $"模型本地pos=({model.localPosition.x:0.00},{model.localPosition.y:0.00},{model.localPosition.z:0.00}) 本地scale={model.localScale.x:0.00} 世界pos=({model.position.x:0.00},{model.position.y:0.00},{model.position.z:0.00})"
                : "模型=<找不到>";

            var health = Object.FindFirstObjectByType<PlayerHealth>();
            string hpText = health != null ? $"HP={health.CurrentHp:0}" : "HP=?";

            var mainCamera = Camera.main;
            string cameraText = "";
            if (mainCamera != null)
            {
                Vector3 toEnemy = zombie.transform.position - mainCamera.transform.position;
                Vector3 flat = toEnemy; flat.y = 0f;
                cameraText = $"相机z={mainCamera.transform.position.z:0.00} 离相机3D={toEnemy.magnitude:0.00} 水平={flat.magnitude:0.00}";
            }

            string hipsText = hips != null
                ? $"hips本地Z={hips.localPosition.z:0.000} hips世界=({hips.position.x:0.00},{hips.position.y:0.00},{hips.position.z:0.00})"
                : "hips=<找不到>";

            string boundsText = skinned != null
                ? $"网格底y={skinned.bounds.min.y:0.000} 顶y={skinned.bounds.max.y:0.000}"
                : "网格=<找不到>";

            Log($"t={now,6:0.00} 敌人={zombies.Length} {hpText} {zombie.name} 状态={zombie.State} Frozen={zombie.Frozen} " +
                $"位置=({zombie.transform.position.x:0.00},{zombie.transform.position.y:0.00},{zombie.transform.position.z:0.00}) " +
                $"{cameraText} 根scale={zombie.transform.localScale.x:0.00} " +
                $"动画=[{animState}] {modelText} {hipsText} {boundsText}");
        }

        private static string StateName(AnimatorStateInfo info)
        {
            string[] names = { "Idle", "Walk", "Run", "Attack", "Death" };
            foreach (string name in names)
            {
                if (info.IsName(name)) return name;
            }

            return $"#{info.shortNameHash}";
        }

        private static Transform FindHips(Transform root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name.EndsWith("Hips")) return transform;
            }

            return null;
        }

        private static void Log(string line)
        {
            try
            {
                File.AppendAllText(ProbeFile, line + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ZombieEnemyVerify] 写探针文件失败：" + e.Message);
            }
        }
    }
}
