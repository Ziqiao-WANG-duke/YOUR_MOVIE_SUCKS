using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YourMovieSucks.Combat;
using YourMovieSucks.LabB;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 玩家受伤音效：只在 PlayerHealth.OnDamaged（真正掉血）时触发，和屏幕血效同一事件源。
    /// · 优先播放 hurtSfxIds 里的素材（放 Assets/Audio/SFX/Hurt/ 下，用 YourMovieSucks/Voice/0 扫描接线）
    /// · 没有素材时自动用一个程序生成的闷响占位音，保证反馈不缺失
    /// · 带音量倍数、音高随机、最小间隔、避免连播同一条
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHurtSfx : MonoBehaviour
    {
        /// <summary>一组受伤台词：先播 firstId，放完接着播 secondId。</summary>
        [System.Serializable]
        public sealed class HurtLineGroup
        {
            public string firstId;
            public string secondId;
        }

        [Header("引用（留空自动查找）")]
        [SerializeField] private DemoAudioDirector audio;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("受伤台词组（每次受伤随机选一组，播完第一句接第二句）")]
        [SerializeField] private HurtLineGroup[] hurtGroups = System.Array.Empty<HurtLineGroup>();

        [Tooltip("脚本对白/其它 VO 正在播时不抢话（不播受伤台词）")]
        [SerializeField] private bool skipWhileDialoguePlaying = true;
        [Tooltip("上一组还没播完时忽略新的受伤，避免两组叠在一起")]
        [SerializeField] private bool waitForGroupToFinish = true;
        [Tooltip("同一组不连续出现（三组轮着来）")]
        [SerializeField] private bool avoidRepeatGroup = true;

        [Header("音效 id（= Assets/Audio/SFX/Hurt 下的文件名，不含扩展名）")]
        [SerializeField] private string[] hurtSfxIds = System.Array.Empty<string>();

        [Header("手感")]
        [Tooltip("本层音量倍数（再乘全局 sfxVolume）")]
        [Range(0f, 2f)] [SerializeField] private float volumeScale = 1f;
        [Tooltip("每次播放的音高随机幅度（0.08 = ±8%）")]
        [Range(0f, 0.5f)] [SerializeField] private float pitchJitter = 0.08f;
        [Tooltip("两次受伤音的最小间隔（秒），防止同帧多次结算叠成一坨")]
        [Min(0f)] [SerializeField] private float minIntervalSeconds = 0.06f;
        [Tooltip("随机时避开最近播放过的几条")]
        [Min(0)] [SerializeField] private int avoidRepeatWindow = 1;

        [Header("占位音 / 冲击层（没有台词素材时）")]
        [Tooltip("没有任何台词组也没有 SFX 素材时，程序生成一个闷响当作受伤音")]
        [SerializeField] private bool usePlaceholderWhenNoClips = true;
        [Range(0f, 1f)] [SerializeField] private float placeholderVolume = 0.8f;
        [Tooltip("占位音时长（秒）")]
        [Range(0.05f, 1f)] [SerializeField] private float placeholderDuration = 0.3f;
        [Tooltip("播台词时额外叠一层闷响（0 = 不叠）")]
        [Range(0f, 1f)] [SerializeField] private float impactLayerVolume = 0f;

        /// <summary>累计播放次数（自检用）。</summary>
        public int HurtPlayCount { get; private set; }
        /// <summary>最近一次播放的 id；走占位音时为空。</summary>
        public string LastHurtId { get; private set; } = string.Empty;
        /// <summary>最近一次是否播放的占位音。</summary>
        public bool LastPlayedPlaceholder { get; private set; }
        /// <summary>最近一次播放的台词组序号（-1 = 没走台词组）。</summary>
        public int LastGroupIndex { get; private set; } = -1;
        /// <summary>台词组是否正在连播中。</summary>
        public bool GroupPlaying => _groupRoutine != null;
        public int GroupCount => hurtGroups != null ? hurtGroups.Length : 0;
        public bool HasHurtPool => hurtSfxIds != null && hurtSfxIds.Length > 0;

        private readonly List<string> _recent = new List<string>();
        private AudioSource _source;
        private AudioClip _placeholder;
        private float _lastPlayTime = -99f;
        private Coroutine _groupRoutine;
        private int _lastGroupIndex = -1;

        private void Awake() => Resolve();

        private void OnEnable()
        {
            Resolve();
            if (playerHealth != null) playerHealth.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDamaged -= HandleDamaged;
            if (_groupRoutine != null) { StopCoroutine(_groupRoutine); _groupRoutine = null; }
        }

        private void Resolve()
        {
            if (audio == null) audio = Object.FindFirstObjectByType<DemoAudioDirector>();
            if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
                if (_source == null) _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;   // 纯 2D：受伤音直接贴耳
                _source.volume = 1f;
            }
        }

        private void HandleDamaged()
        {
            if (Time.unscaledTime - _lastPlayTime < minIntervalSeconds) return;

            // 1) 优先：受伤台词组（随机一组，先第一句、放完接第二句）
            if (TryPlayGroup()) return;

            _lastPlayTime = Time.unscaledTime;
            float global = LabBTuning.I != null && LabBTuning.I.Enabled ? LabBTuning.I.sfxVolume : 1f;
            float pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);

            string id = PickId(out var clip);
            if (clip != null)
            {
                _source.pitch = pitch;
                _source.PlayOneShot(clip, Mathf.Clamp01(volumeScale * global));
                HurtPlayCount++;
                LastHurtId = id;
                LastPlayedPlaceholder = false;
                return;
            }

            if (!usePlaceholderWhenNoClips) return;

            EnsurePlaceholder();
            _source.pitch = pitch;
            _source.PlayOneShot(_placeholder, Mathf.Clamp01(placeholderVolume * volumeScale * global));
            HurtPlayCount++;
            LastHurtId = string.Empty;
            LastPlayedPlaceholder = true;
        }

        /// <summary>随机一组受伤台词并开始连播；返回 false 表示这次不走台词（交给 SFX / 占位音）。</summary>
        private bool TryPlayGroup()
        {
            if (audio == null || audio.Library == null) return false;
            if (hurtGroups == null || hurtGroups.Length == 0) return false;

            // 上一组还在播：按设置选择忽略本次受伤
            if (_groupRoutine != null && waitForGroupToFinish) return true; // 当作“已处理”，不叠 SFX

            // 脚本对白正在播：不抢话
            if (skipWhileDialoguePlaying && audio.IsVoPlaying && _groupRoutine == null) return true;

            int index = Random.Range(0, hurtGroups.Length);
            if (avoidRepeatGroup && hurtGroups.Length > 1 && index == _lastGroupIndex)
                index = (index + 1) % hurtGroups.Length;

            var group = hurtGroups[index];
            if (group == null || string.IsNullOrEmpty(group.firstId)) return false;
            if (audio.Library.Get(group.firstId) == null) return false;

            _lastPlayTime = Time.unscaledTime;
            _lastGroupIndex = index;
            LastGroupIndex = index;
            LastHurtId = group.firstId;
            LastPlayedPlaceholder = false;
            HurtPlayCount++;

            if (impactLayerVolume > 0f)
            {
                EnsurePlaceholder();
                float global = LabBTuning.I != null && LabBTuning.I.Enabled ? LabBTuning.I.sfxVolume : 1f;
                _source.pitch = 1f;
                _source.PlayOneShot(_placeholder, Mathf.Clamp01(impactLayerVolume * global));
            }

            if (_groupRoutine != null) StopCoroutine(_groupRoutine);
            _groupRoutine = StartCoroutine(PlayGroup(group));
            return true;
        }

        /// <summary>先播第一句，等它放完再播第二句；两句都结束才允许下一组。</summary>
        private IEnumerator PlayGroup(HurtLineGroup group)
        {
            float firstDuration = audio.PlayVo(group.firstId);
            if (firstDuration <= 0f) firstDuration = 0.8f;   // 没有逐条设置时给一个兜底间隔
            yield return new WaitForSeconds(firstDuration);

            if (!string.IsNullOrEmpty(group.secondId) && audio.Library.Get(group.secondId) != null)
            {
                float secondDuration = audio.GetEffectiveDuration(group.secondId);
                LastHurtId = group.secondId;
                audio.PlayVo(group.secondId);
                if (secondDuration <= 0f) secondDuration = 0.8f;
                yield return new WaitForSeconds(secondDuration);
            }

            _groupRoutine = null;
        }

        /// <summary>从候选池里挑一个（避开最近播放过的），解析成 AudioClip。</summary>
        private string PickId(out AudioClip clip)
        {
            clip = null;
            if (audio == null || audio.Library == null || hurtSfxIds == null || hurtSfxIds.Length == 0) return string.Empty;

            var candidates = new List<string>(hurtSfxIds.Length);
            foreach (var id in hurtSfxIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (avoidRepeatWindow > 0 && _recent.Contains(id) && hurtSfxIds.Length > avoidRepeatWindow) continue;
                if (audio.Library.Get(id) == null) continue;
                candidates.Add(id);
            }
            if (candidates.Count == 0)
            {
                // 全被避开规则挡掉时退回整池
                foreach (var id in hurtSfxIds)
                    if (!string.IsNullOrEmpty(id) && audio.Library.Get(id) != null) candidates.Add(id);
            }
            if (candidates.Count == 0) return string.Empty;

            string pick = candidates[Random.Range(0, candidates.Count)];
            _recent.Add(pick);
            while (_recent.Count > Mathf.Max(0, avoidRepeatWindow)) _recent.RemoveAt(0);

            clip = audio.Library.Get(pick);
            return pick;
        }

        /// <summary>程序生成的受伤占位音：低频闷响 + 短噪声冲击，带快速衰减。</summary>
        private void EnsurePlaceholder()
        {
            if (_placeholder != null) return;

            const int sampleRate = 44100;
            int length = Mathf.Max(64, Mathf.RoundToInt(sampleRate * Mathf.Max(0.05f, placeholderDuration)));
            var data = new float[length];
            float phase = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                // 低频下滑：130Hz → 55Hz
                float freq = Mathf.Lerp(130f, 55f, Mathf.Pow(t, 0.6f));
                phase += 2f * Mathf.PI * freq / sampleRate;
                float body = Mathf.Sin(phase) * Mathf.Exp(-6f * t);
                // 噪声冲击（前 25% 最明显）
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-14f * t) * 0.55f;
                float sample = body * 0.9f + noise;
                // 软削波，给一点“挨打”的硬度
                data[i] = Mathf.Clamp(sample * 1.6f, -1f, 1f) * 0.9f;
            }

            // 尾部 8ms 淡出，避免爆音
            int fade = Mathf.Min(360, length / 4);
            for (int i = 0; i < fade; i++) data[length - 1 - i] *= i / (float)fade;

            _placeholder = AudioClip.Create("HurtPlaceholder", length, 1, sampleRate, false);
            _placeholder.SetData(data, 0);
        }
    }
}
