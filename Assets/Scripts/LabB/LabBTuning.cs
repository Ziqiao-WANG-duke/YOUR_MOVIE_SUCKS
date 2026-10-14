using UnityEngine;
using UnityEngine.InputSystem;

namespace YourMovieSucks.LabB
{
    /// <summary>
    /// Lab B 调参面板（F1 显示/隐藏）。
    /// 作业要求：至少 8 个玩法数值可在运行时调整；Lab B 期间调过的每个数字都要在这里。
    /// 玩法代码通过 LabBTuning.I 读取数值（Enabled=false 时回退到各自的序列化默认值）。
    /// </summary>
    public class LabBTuning : MonoBehaviour
    {
        public static LabBTuning I { get; private set; }

        [Header("总开关")]
        public bool Enabled = true;
        public bool PanelVisible = true;

        [Header("移动与威胁")]
        public float approachSpeed = 0.5f;
        public float rushSpeed = 1.6f;
        [Tooltip("怪物开始攻击玩家的距离（米，相对相机）。越小越有压迫感：1.7 ≈ 怪走到脸上、只看得见上半身")]
        public float attackRange = 1.7f;
        public float attackCountdown = 1.5f;
        public float attackDamage = 35f;

        [Header("玩家")]
        public float playerMaxHp = 100f;

        [Header("刷怪")]
        public int enemiesPerWave = 2;
        public float waveAdvanceDelay = 2f;
        // 主战斗（MAIN COMBAT）怪物数：脚本波次 4 只 + 主战斗 8 只 = 12，与脚本结算屏示例值一致。
        public int mainCombatTotalEnemies = 8;

        [Header("词条")]
        public int wordMinLength = 4;
        public int wordMaxLength = 8;

        [Header("导演轨")]
        public float zoomFov = 38f;
        public float zoomDuration = 2.5f;
        public float slowFactor = 0.3f;

        [Header("Juice（各带开关，作业要求 ≤1–2 个）")]
        public bool screenShakeOn = true;
        public float shakeStrength = 1f;
        public bool screenBloodOn = true;
        public float splatterAlpha = 0.32f;

        [Header("怪物外观 / 近身站位")]
        [Tooltip("击杀后尸体停留（秒）")]
        public float enemyDeathHold = 1.2f;
        [Tooltip("被打错字母时的整体放大倍数（骴骼模型设 1：靠奔跑动画 + 染色表达）")]
        public float rushScaleMultiplier = 1f;
        [Tooltip("每次攻击结算的前扑放大倍数（骴骼模型设 1：靠攻击动画表达）")]
        public float attackLungeScaleMultiplier = 1f;
        [Tooltip("怪进入这个「离相机前向距离」后开始把车道收窄（远处保持宽车道，词条不叠）")]
        public float laneConvergeStart = 6f;
        [Tooltip("收到攻击距离时的车道横向偏移（绝对值）")]
        public float closeLaneOffset = 0.85f;

        [Header("词条（世界空间标签）")]
        [Tooltip("近身限尺：词条到这个三维距离时表观大小压到 closeScaleFactor（更远不受影响）")]
        public float labelCloseReferenceDistance = 2f;
        [Tooltip("最近处的词条表观缩放（0.5 = 当前尺寸的一半）")]
        public float labelCloseScaleFactor = 0.5f;
        [Tooltip("词条屏幕安全区上边（留出左上血条块：IMGUI 固定像素，视口占位随窗口变）")]
        public float labelSafeTop = 0.82f;

        [Header("音频")]
        public float voVolume = 1f;
        // 注：BGM 母带很热（实测 RMS −8.3dB，比配音 −20dBFS 响 12dB），所以基础音量压得很低；
        // 对话期间还会再乘 DemoAudioDirector.dialogueDuckFactor。
        public float bgmVolume = 0.22f;
        // 打字音效换成 typewriter1–8（峰值 −3…−8dBFS，比旧素材轻）后，sfxVolume 提到 0.8
        public float sfxVolume = 0.8f;
        // 枪声层（与打字音同时播）：枪声已峰值归一到 −6dBFS，这里做整体平衡
        public float gunshotVolume = 1f;

        [Header("Variant")]
        public int variantMode;                  // 0 = 导演取景框反馈 开；1 = 关
        public string[] variantNames = { "viewfinder_on", "viewfinder_off" };

        public event System.Action<int, string> OnVariantChanged;

        public string VariantName =>
            variantNames != null && variantMode >= 0 && variantMode < variantNames.Length
                ? variantNames[variantMode]
                : variantMode.ToString();

        private void Awake()
        {
            I = this;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) PanelVisible = !PanelVisible;
            if (kb.digit1Key.wasPressedThisFrame) SetVariant(0);
            if (kb.digit2Key.wasPressedThisFrame) SetVariant(1);
        }

        public void SetVariant(int mode)
        {
            if (mode == variantMode) return;
            variantMode = mode;
            OnVariantChanged?.Invoke(variantMode, VariantName);
        }

        private void OnGUI()
        {
            if (!PanelVisible) return;

            const float width = 360f;
            GUILayout.BeginArea(new Rect(8f, 8f, width, Screen.height - 16f), GUI.skin.box);

            GUILayout.Label("== LAB B TUNING (F1) ==");
            Enabled = GUILayout.Toggle(Enabled, "  tuning enabled (off = use scene defaults)");

            GUILayout.Space(4f);
            GUILayout.Label("-- 移动与威胁 --");
            Slider("approachSpeed", ref approachSpeed, 0.05f, 5f);
            Slider("rushSpeed", ref rushSpeed, 0.05f, 8f);
            Slider("attackRange", ref attackRange, 1f, 12f);
            Slider("attackCountdown", ref attackCountdown, 0.2f, 8f);
            Slider("attackDamage", ref attackDamage, 1f, 100f);

            GUILayout.Space(4f);
            GUILayout.Label("-- 玩家 --");
            Slider("playerMaxHp", ref playerMaxHp, 10f, 300f);

            GUILayout.Space(4f);
            GUILayout.Label("-- 刷怪 --");
            IntSlider("enemiesPerWave", ref enemiesPerWave, 1, 6);
            Slider("waveAdvanceDelay", ref waveAdvanceDelay, 0.2f, 10f);
            IntSlider("mainCombatEnemies", ref mainCombatTotalEnemies, 0, 40);

            GUILayout.Space(4f);
            GUILayout.Label("-- 词条 --");
            IntSlider("wordMinLength", ref wordMinLength, 2, 10);
            IntSlider("wordMaxLength", ref wordMaxLength, 2, 16);

            GUILayout.Space(4f);
            GUILayout.Label("-- 导演轨 --");
            Slider("zoomFov", ref zoomFov, 20f, 90f);
            Slider("zoomDuration", ref zoomDuration, 0.5f, 8f);
            Slider("slowFactor", ref slowFactor, 0.05f, 1f);

            GUILayout.Space(4f);
            GUILayout.Label("-- Juice --");
            screenShakeOn = GUILayout.Toggle(screenShakeOn, "  screen shake");
            if (screenShakeOn) Slider("shakeStrength", ref shakeStrength, 0f, 3f);
            screenBloodOn = GUILayout.Toggle(screenBloodOn, "  screen blood");
            if (screenBloodOn) Slider("splatterAlpha", ref splatterAlpha, 0f, 1f);

            GUILayout.Space(4f);
            GUILayout.Label("-- 怪与站位 --");
            Slider("enemyDeathHold", ref enemyDeathHold, 0f, 5f);
            Slider("rushScaleMultiplier", ref rushScaleMultiplier, 0.5f, 2f);
            Slider("attackLungeScaleMultiplier", ref attackLungeScaleMultiplier, 0.5f, 2f);
            Slider("laneConvergeStart", ref laneConvergeStart, 0f, 15f);
            Slider("closeLaneOffset", ref closeLaneOffset, 0f, 3f);

            GUILayout.Space(4f);
            GUILayout.Label("-- 词条 --");
            Slider("labelCloseReferenceDistance", ref labelCloseReferenceDistance, 0.5f, 8f);
            Slider("labelCloseScaleFactor", ref labelCloseScaleFactor, 0.1f, 1.5f);
            Slider("labelSafeTop", ref labelSafeTop, 0.5f, 1f);

            GUILayout.Space(4f);
            GUILayout.Label("-- 音频 --");
            Slider("voVolume", ref voVolume, 0f, 1.5f);
            Slider("bgmVolume", ref bgmVolume, 0f, 1.5f);
            Slider("sfxVolume", ref sfxVolume, 0f, 1.5f);
            Slider("gunshotVolume", ref gunshotVolume, 0f, 1.5f);

            GUILayout.Space(4f);
            GUILayout.Label($"-- Variant (1/2): {VariantName} --");
            GUILayout.Label("1 = viewfinder_on   2 = viewfinder_off");

            GUILayout.EndArea();
        }

        private static void Slider(string label, ref float value, float min, float max)
        {
            GUILayout.Label($"{label}: {value:0.###}");
            value = GUILayout.HorizontalSlider(value, min, max);
        }

        private static void IntSlider(string label, ref int value, int min, int max)
        {
            GUILayout.Label($"{label}: {value}");
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
        }
    }
}
