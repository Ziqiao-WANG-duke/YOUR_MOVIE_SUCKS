using System.Collections;
using UnityEngine;
using YourMovieSucks.LabB;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 演出音频总控：VO（配音）/ SFX / BGM 三条音源 + 逐条可调参数（<see cref="VoLineSettings"/>）。
    ///
    /// 三条关键约定：
    /// 1. **对白推进以音频长度为准**（<see cref="PlayVo"/> 返回"延迟 + 音频长度/速度 + 尾巴"），
    ///    不依赖 AudioSource.isPlaying —— 批处理/无音频设备时 isPlaying 不可靠，而 clip.length 始终可用。
    /// 2. 速度用 `AudioSource.pitch` 实现：**变快会同时变调**（磁带效果）。
    /// 3. <see cref="VoLineSettings"/> 是手调资产，Play 模式下改参数会**即时生效**：
    ///    下一条台词用新参数，正在播的那条会立刻改速度/音量。
    /// </summary>
    public class DemoAudioDirector : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private VoLibrary voLibrary;

        [Tooltip("逐条调节参数（编辑器窗口 YourMovieSucks/Voice Tuner 直接改这个资产）")]
        [SerializeField] private VoLineSettings lineSettings;

        [Header("音源（留空则运行时自建）")]
        [SerializeField] private AudioSource voSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource bgmSource;

        [Tooltip("一条台词播完后再等这么久才推进下一句")]
        [SerializeField] private float voTailSeconds = 0.15f;

        [Header("BGM 闪避（对话时压低）")]
        [Tooltip("对话期间 BGM 音量乘这个系数")]
        [Range(0f, 1f)] [SerializeField] private float dialogueDuckFactor = 0.35f;

        [Tooltip("压低/恢复的过渡速度（每秒变化多少系数）")]
        [SerializeField] private float duckRampSpeed = 2.5f;

        private string _currentVoId;
        private Coroutine _pendingVo;
        private bool _duckRequested;
        private float _duckFactor = 1f;

        public VoLibrary Library => voLibrary;
        public VoLineSettings LineSettings => lineSettings;

        /// <summary>BGM 是否在播（自检用）。</summary>
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;

        /// <summary>BGM 当前实际音量（含闪避，自检用）。</summary>
        public float BgmVolume => bgmSource != null ? bgmSource.volume : 0f;

        /// <summary>当前闪避系数（1 = 未压低）。</summary>
        public float DuckFactor => _duckFactor;

        public bool DialogueDucking => _duckRequested;

        /// <summary>本次运行真正播放过的台词条数（自检用）。</summary>
        public int VoPlayCount { get; private set; }

        /// <summary>最近一次 PlayVo 的延迟时长（字幕要等这段时间之后再开始逐字显示）。</summary>
        public float LastVoDelay { get; private set; }

        public bool IsVoPlaying => voSource != null && voSource.isPlaying;

        public string CurrentVoId => _currentVoId;

        private void Awake() => EnsureSources();

        private void Update()
        {
            var tuning = LabBTuning.I;
            float globalVo = tuning != null ? tuning.voVolume : 1f;
            float globalSfx = tuning != null ? tuning.sfxVolume : 1f;
            float globalBgm = tuning != null ? tuning.bgmVolume : 1f;

            if (sfxSource != null) sfxSource.volume = globalSfx;

            // BGM 闪避：目标系数 → 平滑过渡（突变会听出来）
            float targetDuck = _duckRequested ? dialogueDuckFactor : 1f;
            _duckFactor = Mathf.MoveTowards(_duckFactor, targetDuck, duckRampSpeed * Time.unscaledDeltaTime);
            if (bgmSource != null) bgmSource.volume = globalBgm * _duckFactor;

            if (voSource == null) return;

            // 正在播的台词：把参数改动即时应用上去（Play 模式下边听边调）
            if (voSource.isPlaying && !string.IsNullOrEmpty(_currentVoId))
            {
                var entry = SettingsFor(_currentVoId);
                voSource.pitch = Mathf.Max(entry.speed, 0.01f);
                voSource.volume = globalVo * entry.volume;
                return;
            }

            voSource.volume = globalVo;
        }

        private static VoLineSettings.Entry _defaultEntry;

        /// <summary>取该 id 的逐条参数；没有设置资产时用默认值（1 速 / 0 延迟 / 1 音量）。</summary>
        private VoLineSettings.Entry SettingsFor(string id)
        {
            if (lineSettings != null) return lineSettings.Get(id);
            return _defaultEntry ??= new VoLineSettings.Entry();
        }

        private void EnsureSources()
        {
            var parent = transform;

            if (voSource == null) voSource = CreateSource("VoSource", parent, false);
            if (sfxSource == null) sfxSource = CreateSource("SfxSource", parent, false);
            if (bgmSource == null) bgmSource = CreateSource("BgmSource", parent, true);
        }

        private static AudioSource CreateSource(string name, Transform parent, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;   // 2D：VO/BGM 不做空间衰减
            source.volume = 1f;
            return source;
        }

        /// <summary>这条音频的生效时长（延迟 + 音频长度/速度 + 尾巴）；没有音频返回 0。</summary>
        public float GetEffectiveDuration(string id, bool includeTail = true)
        {
            if (voLibrary == null || string.IsNullOrEmpty(id)) return 0f;

            var clip = voLibrary.Get(id);
            if (clip == null) return 0f;

            var entry = SettingsFor(id);
            float speed = Mathf.Max(entry.speed, 0.01f);
            float delay = Mathf.Max(entry.delaySeconds, 0f);
            float tail = includeTail ? Mathf.Max(voTailSeconds, 0f) : 0f;
            float playable = Mathf.Max(clip.length - Mathf.Clamp(entry.startOffsetSeconds, 0f, clip.length), 0f);

            return delay + playable / speed + tail;
        }

        /// <summary>
        /// 播放一条音频（VO/SFX/BGM 通用），按逐条参数应用延迟/速度/音量/起始偏移。
        /// 返回生效时长（秒）；返回 0 表示没有这条音频，调用方应退回字幕计时。
        /// </summary>
        public float PlayVo(string id)
        {
            LastVoDelay = 0f;

            if (voLibrary == null || string.IsNullOrEmpty(id)) return 0f;

            var clip = voLibrary.Get(id);
            if (clip == null) return 0f;

            var entry = SettingsFor(id);
            float speed = Mathf.Max(entry.speed, 0.01f);
            float delay = Mathf.Max(entry.delaySeconds, 0f);
            float volume = entry.volume;
            float offset = Mathf.Clamp(entry.startOffsetSeconds, 0f, Mathf.Max(clip.length - 0.05f, 0f));

            EnsureSources();
            StopVo();

            _currentVoId = id;
            LastVoDelay = delay;

            if (delay > 0f)
                _pendingVo = StartCoroutine(PlayAfterDelay(id, clip, speed, volume, offset, delay));
            else
            {
                PlayClipNow(clip, speed, volume, offset);
                VoPlayCount++;
            }

            return GetEffectiveDuration(id);
        }

        private IEnumerator PlayAfterDelay(string id, AudioClip clip, float speed, float volume, float offset, float delay)
        {
            yield return new WaitForSeconds(delay);

            _pendingVo = null;
            if (_currentVoId != id) yield break;   // 期间被别的台词打断

            PlayClipNow(clip, speed, volume, offset);
            VoPlayCount++;
        }

        private void PlayClipNow(AudioClip clip, float speed, float volume, float offset)
        {
            var tuning = LabBTuning.I;
            float globalVo = tuning != null ? tuning.voVolume : 1f;

            voSource.pitch = speed;
            voSource.volume = globalVo * volume;
            voSource.clip = clip;
            voSource.Play();

            if (offset > 0f) voSource.time = offset;
        }

        public void StopVo()
        {
            if (_pendingVo != null)
            {
                StopCoroutine(_pendingVo);
                _pendingVo = null;
            }

            _currentVoId = null;
            if (voSource != null) voSource.Stop();
        }

        /// <summary>演出层：对白开始/结束 → 压低/恢复 BGM。</summary>
        public void SetDialogueDucking(bool duck)
        {
            if (_duckRequested == duck) return;

            _duckRequested = duck;
            Debug.Log($"[DemoAudioDirector] dialogue ducking {(duck ? "ON" : "OFF")} @ {Time.unscaledTime:0.00}s");
        }

        /// <summary>音效（素材到位后使用）。按逐条参数应用延迟/速度/音量；volumeScale 为本调用额外倍数。</summary>
        public float PlaySfx(string id, float volumeScale = 1f)
        {
            if (voLibrary == null || string.IsNullOrEmpty(id)) return 0f;

            var clip = voLibrary.Get(id);
            if (clip == null) return 0f;

            var entry = SettingsFor(id);
            EnsureSources();

            sfxSource.pitch = Mathf.Max(entry.speed, 0.01f);
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(entry.volume * Mathf.Max(volumeScale, 0f)));

            return Mathf.Max(entry.delaySeconds, 0f) + clip.length / Mathf.Max(entry.speed, 0.01f);
        }

        /// <summary>循环音乐（素材到位后使用）。</summary>
        public bool PlayBgm(string id)
        {
            if (voLibrary == null || string.IsNullOrEmpty(id)) return false;

            var clip = voLibrary.Get(id);
            if (clip == null) return false;

            var entry = SettingsFor(id);
            EnsureSources();

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.pitch = Mathf.Max(entry.speed, 0.01f);
            bgmSource.Play();
            return true;
        }

        public void StopBgm()
        {
            if (bgmSource != null) bgmSource.Stop();
        }
    }
}
