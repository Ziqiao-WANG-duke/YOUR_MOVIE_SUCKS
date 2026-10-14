using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using YourMovieSucks.Input;
using YourMovieSucks.LabB;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 打字音效：
    ///  · 玩家按 a–z（不论当前轨道、不论对错）→ 从候选池里随机取一条播放；
    ///  · 玩家按切轨键（CapsLock）→ 播放专用音效。
    ///
    /// 候选池与切轨音效都用 VoLibrary 的 id（= 音频文件名），
    /// 由 `YourMovieSucks/Voice/0` 自动扫描 Assets/Audio/SFX/Typing 填入。
    /// </summary>
    public class TypingSfxPlayer : MonoBehaviour
    {
        [Header("Refs（留空则自动查找）")]
        [SerializeField] private DemoAudioDirector audio;
        [SerializeField] private TrackInput trackInput;

        [Header("音效 id（对应 Assets/Audio/SFX 下的文件名）")]
        [Tooltip("切轨键（CapsLock）音效")]
        [SerializeField] private string trackSwitchSfxId = "typewriter8";

        [Tooltip("打字音效候选池（不含切轨音效）；由 Voice/0 工具自动扫描填入")]
        [SerializeField] private string[] typingSfxIds = System.Array.Empty<string>();

        [Header("枪声层（与打字音效同时播）")]
        [Tooltip("枪声候选池（Assets/Audio/SFX/Gunshot）；由 Voice/0 工具自动扫描填入")]
        [SerializeField] private string[] gunshotSfxIds = System.Array.Empty<string>();

        [Tooltip("枪声本层倍数（再乘全局 sfxVolume 与 LabBTuning.gunshotVolume）")]
        [Range(0f, 2f)] [SerializeField] private float gunshotVolumeScale = 1f;

        [Header("手感")]
        [Tooltip("两次打字音之间的最小间隔（秒），防止一帧多键时叠得太乱")]
        [SerializeField] private float minIntervalSeconds = 0.03f;

        [Tooltip("最近这么多条不重复（随机时避开）")]
        [SerializeField] private int avoidRepeatWindow = 2;

        [Tooltip("打字音相对音量（再乘全局 sfxVolume）")]
        [Range(0f, 2f)] [SerializeField] private float typingVolumeScale = 1f;

        public int TypingPlayCount { get; private set; }
        public int SwitchPlayCount { get; private set; }
        public int GunshotPlayCount { get; private set; }
        public string LastTypingSfxId { get; private set; } = string.Empty;
        public string LastGunshotId { get; private set; } = string.Empty;

        private readonly List<string> _recent = new();
        private readonly List<string> _recentGunshots = new();
        private float _lastTypingTime = -99f;
        private readonly List<string> _missing = new();

        public bool HasTypingPool => typingSfxIds != null && typingSfxIds.Length > 0;
        public int TypingPoolSize => typingSfxIds != null ? typingSfxIds.Length : 0;

        public bool HasGunshotPool => gunshotSfxIds != null && gunshotSfxIds.Length > 0;
        public int GunshotPoolSize => gunshotSfxIds != null ? gunshotSfxIds.Length : 0;

        /// <summary>切轨键音效 id（自检用）。</summary>
        public string SwitchSfxId => trackSwitchSfxId;

        private void OnEnable()
        {
            if (audio == null) audio = Object.FindFirstObjectByType<DemoAudioDirector>();
            if (trackInput == null) trackInput = Object.FindFirstObjectByType<TrackInput>();
            if (trackInput == null) return;

            trackInput.OnLetterTyped += HandleLetterTyped;
            trackInput.OnTrackChanged += HandleTrackChanged;
        }

        private void OnDisable()
        {
            if (trackInput == null) return;

            trackInput.OnLetterTyped -= HandleLetterTyped;
            trackInput.OnTrackChanged -= HandleTrackChanged;
        }

        private void HandleLetterTyped(CharacterTrack track, Key key)
        {
            if (audio == null || !HasTypingPool) return;
            if (Time.unscaledTime - _lastTypingTime < minIntervalSeconds) return;

            string id = PickRandom(typingSfxIds, _recent);
            if (string.IsNullOrEmpty(id)) return;

            if (audio.PlaySfx(id, typingVolumeScale) <= 0f)
            {
                if (!_missing.Contains(id))
                {
                    _missing.Add(id);
                    Debug.LogWarning($"[TypingSfxPlayer] 音效不在 VoLibrary 里：{id}（跑一次 YourMovieSucks/Voice/0 重建库）");
                }

                return;
            }

            _lastTypingTime = Time.unscaledTime;
            TypingPlayCount++;
            LastTypingSfxId = id;

            // 同一下击键：再叠一声随机枪声（“打字即开枪”）
            PlayGunshot();
        }

        private void PlayGunshot()
        {
            if (audio == null || !HasGunshotPool) return;

            string id = PickRandom(gunshotSfxIds, _recentGunshots);
            if (string.IsNullOrEmpty(id)) return;

            float scale = gunshotVolumeScale * (LabBTuning.I != null ? LabBTuning.I.gunshotVolume : 1f);
            if (audio.PlaySfx(id, scale) <= 0f) return;

            GunshotPlayCount++;
            LastGunshotId = id;
        }

        private void HandleTrackChanged(CharacterTrack track)
        {
            if (audio == null || string.IsNullOrEmpty(trackSwitchSfxId)) return;

            // 演出层强制设定轨道（开场默认落在导演轨）不算玩家按键，不出声
            if (trackInput != null && trackInput.LastChangeWasForced) return;

            if (audio.PlaySfx(trackSwitchSfxId) > 0f) SwitchPlayCount++;
        }

        /// <summary>从指定池随机取一条，避开最近用过的几条（打起来不会连续同一音）。</summary>
        private string PickRandom(string[] pool, List<string> recent)
        {
            if (pool == null || pool.Length == 0) return null;

            int window = Mathf.Clamp(avoidRepeatWindow, 0, Mathf.Max(pool.Length - 1, 0));
            string candidate = null;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                candidate = pool[Random.Range(0, pool.Length)];
                if (string.IsNullOrEmpty(candidate)) continue;
                if (window == 0 || !recent.Contains(candidate)) break;
            }

            if (string.IsNullOrEmpty(candidate)) return null;

            recent.Add(candidate);
            while (recent.Count > window) recent.RemoveAt(0);
            return candidate;
        }
    }
}
