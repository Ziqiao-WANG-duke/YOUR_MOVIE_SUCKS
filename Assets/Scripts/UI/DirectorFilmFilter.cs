using UnityEngine;
using YourMovieSucks.Combat;
using YourMovieSucks.Input;
using YourMovieSucks.Retro;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 导演模式滤镜：切到导演轨时把整个世界画面变成「黑白复古胶片」，
    /// 回到特工轨立刻恢复。界面与弱点标记由原生分辨率相机绘制，不受影响。
    ///
    /// 实现方式：所有参数写进 Retro 合成材质（RetroRendering/ScreenComposite），
    /// 所以不做额外全屏 Pass、不新增渲染目标；关闭时同一个材质只是把强度归零。
    /// </summary>
    [DefaultExecutionOrder(30001)] // 在 RetroRenderingController（30000）之后跑，材质已经建好
    [DisallowMultipleComponent]
    public sealed class DirectorFilmFilter : MonoBehaviour
    {
        [Header("引用（留空自动查找）")]
        public TrackInput trackInput;
        public RetroRenderingController retro;
        [Tooltip("战斗编排：只有它进入战斗后才开启滤镜（开场对话与演出不放滤镜）")]
        public YourMovieSucks.Demo.ScriptedEncounter encounter;

        [Header("开关")]
        [Tooltip("总开关：关掉则导演模式保持原色")]
        public bool enabled_ = true;
        [Tooltip("只在战斗中生效（开场/收尾演出即使处于导演轨也不上滤镜）")]
        public bool onlyDuringCombat = true;
        [Tooltip("进入/退出导演模式的黑白过渡速度（0 = 立即切换）")]
        [Range(0f, 30f)] public float fadeSpeed = 12f;

        [Header("黑白与对比")]
        [Tooltip("对比度：1 = 原始，1.8 左右接近硬调黑白胶片")]
        [Range(0.5f, 3f)] public float contrast = 1.9f;
        [Tooltip("抬黑（正数让暗部发灰，像老胶片的蒙尘感）")]
        [Range(-0.1f, 0.3f)] public float lift = 0.03f;
        [Tooltip("亮度（黑白画面的曝光增益，>1 更亮）")]
        [Range(0.2f, 3f)] public float brightness = 1.35f;
        [Tooltip("阈值（黑位）：低于这个亮度的像素直接压黑，调高会剔除暗部噪声、只保留主体")]
        [Range(0f, 0.5f)] public float threshold = 0.10f;

        [Header("胶片质感")]
        [Tooltip("颗粒强度")]
        [Range(0f, 1f)] public float grain = 0.35f;
        [Tooltip("颗粒尺寸（越大颗粒越粗）")]
        [Range(0.5f, 8f)] public float grainScale = 1.6f;
        [Tooltip("曝光闪烁强度")]
        [Range(0f, 1f)] public float flicker = 0.25f;
        [Tooltip("闪烁频率（每秒跳变次数）")]
        [Range(1f, 60f)] public float flickerSpeed = 14f;

        [Header("镜头与损毁")]
        [Tooltip("暗角强度")]
        [Range(0f, 1f)] public float vignette = 0.55f;
        [Tooltip("竖向干扰条纹强度")]
        [Range(0f, 1f)] public float verticalStripes = 0.35f;
        [Tooltip("胶片划痕强度")]
        [Range(0f, 1f)] public float scratches = 0.45f;
        [Tooltip("划痕出现过快慢（每秒换一条）")]
        [Range(0.1f, 12f)] public float scratchSpeed = 2.2f;

        [Header("弱点高亮")]
        [Tooltip("导演模式下弱点标记放大倍数（1 = 保持不变）")]
        [Range(1f, 3f)] public float weakPointScale = 1.5f;

        private static readonly int AmountId = Shader.PropertyToID("_DirectorAmount");
        private static readonly int ContrastId = Shader.PropertyToID("_DirectorContrast");
        private static readonly int LiftId = Shader.PropertyToID("_DirectorLift");
        private static readonly int BrightnessId = Shader.PropertyToID("_DirectorBrightness");
        private static readonly int ThresholdId = Shader.PropertyToID("_DirectorThreshold");
        private static readonly int GrainId = Shader.PropertyToID("_DirectorGrain");
        private static readonly int GrainScaleId = Shader.PropertyToID("_DirectorGrainScale");
        private static readonly int FlickerId = Shader.PropertyToID("_DirectorFlicker");
        private static readonly int FlickerSpeedId = Shader.PropertyToID("_DirectorFlickerSpeed");
        private static readonly int VignetteId = Shader.PropertyToID("_DirectorVignette");
        private static readonly int ScratchId = Shader.PropertyToID("_DirectorScratch");
        private static readonly int ScratchSpeedId = Shader.PropertyToID("_DirectorScratchSpeed");
        private static readonly int StripeId = Shader.PropertyToID("_DirectorStripe");
        private static readonly int TimeId = Shader.PropertyToID("_DirectorTime");

        private float amount;
        private bool directorActive;

        private void Awake()
        {
            if (!trackInput) trackInput = FindFirstObjectByType<TrackInput>();
            if (!retro) retro = FindFirstObjectByType<RetroRenderingController>();
            if (!encounter) encounter = FindFirstObjectByType<YourMovieSucks.Demo.ScriptedEncounter>();
        }

        private void OnEnable()
        {
            if (trackInput) trackInput.OnTrackChanged += HandleTrackChanged;
            HandleTrackChanged(trackInput ? trackInput.CurrentTrack : CharacterTrack.Agent);
            Apply(1f);
        }

        private void OnDisable()
        {
            if (trackInput) trackInput.OnTrackChanged -= HandleTrackChanged;
            amount = 0f;
            directorActive = false;
            MonsterWeakness.GlobalMarkerScale = 1f;
            Apply(1f);
        }

        private void HandleTrackChanged(CharacterTrack track)
        {
            directorActive = track == CharacterTrack.Director;
        }

        private void Update()
        {
            // ForceTrack 之类的切换不一定发事件，这里再对齐一次
            if (trackInput) directorActive = trackInput.CurrentTrack == CharacterTrack.Director;
            if (!encounter) encounter = FindFirstObjectByType<YourMovieSucks.Demo.ScriptedEncounter>();

            // 开场/收尾演出即使是导演轨也不上滤镜：只认「战斗进行中」
            bool combatGate = !onlyDuringCombat || (encounter && encounter.IsRunning);

            float target = enabled_ && directorActive && combatGate ? 1f : 0f;
            amount = fadeSpeed <= 0.01f ? target : Mathf.MoveTowards(amount, target, fadeSpeed * Time.unscaledDeltaTime);
            MonsterWeakness.GlobalMarkerScale = Mathf.Lerp(1f, Mathf.Max(1f, weakPointScale), amount);
        }

        private void LateUpdate() => Apply(Time.unscaledTime);

        /// <summary>把当前参数写进合成材质；材质不存在（Retro 未启用）时什么也不做。</summary>
        private void Apply(float time)
        {
            var material = retro ? retro.CompositeMaterial : null;
            if (!material) return;

            material.SetFloat(AmountId, amount);
            material.SetFloat(ContrastId, contrast);
            material.SetFloat(LiftId, lift);
            material.SetFloat(BrightnessId, brightness);
            material.SetFloat(ThresholdId, threshold);
            material.SetFloat(GrainId, grain);
            material.SetFloat(GrainScaleId, grainScale);
            material.SetFloat(FlickerId, flicker);
            material.SetFloat(FlickerSpeedId, flickerSpeed);
            material.SetFloat(VignetteId, vignette);
            material.SetFloat(ScratchId, scratches);
            material.SetFloat(ScratchSpeedId, scratchSpeed);
            material.SetFloat(StripeId, verticalStripes);
            material.SetFloat(TimeId, time);
        }

        /// <summary>供外部（编辑器预览 / 自动化测试）直接指定是否处于导演模式。</summary>
        public void SetDirectorOverride(bool? active)
        {
            if (active.HasValue) directorActive = active.Value;
            enabled_ = true;
        }
    }
}
