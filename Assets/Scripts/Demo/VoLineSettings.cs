using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 逐条音频的调节参数（id → 速度/延迟/音量/起始偏移）。
    /// 与 VoLibrary 分开存放：VoLibrary 由工具自动重建，这份是**手调资产**，不会被覆盖。
    ///
    /// 注意：速度用 AudioSource.pitch 实现 —— **变快同时会变调**（磁带加速效果），
    /// 想要"变速不变调"需要离线做时间伸缩，Unity 原生做不到。
    /// </summary>
    [CreateAssetMenu(menuName = "YourMovieSucks/VO Line Settings", fileName = "VoLineSettings")]
    public class VoLineSettings : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id = string.Empty;

            [Tooltip("播放速度（同时变调）：1 = 原速，1.2 = 快 20%")]
            [Range(0.25f, 3f)] public float speed = 1f;

            [Tooltip("播放前延迟（秒）")]
            [Range(0f, 10f)] public float delaySeconds;

            [Tooltip("这一条的相对音量（再乘全局 voVolume/sfxVolume/bgmVolume）；上限 8 倍（>2 倍很可能把峰值推过 0dBFS 而削顶）")]
            [Range(0f, 8f)] public float volume = 1f;

            [Tooltip("从音频的第几秒开始播（跳过开头）")]
            [Range(0f, 10f)] public float startOffsetSeconds;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, Entry> _map;
        private static Entry _default;

        private static Entry DefaultEntry => _default ??= new Entry();

        private void OnEnable() => _map = null;

        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();

        private Dictionary<string, Entry> Map
        {
            get
            {
                if (_map != null) return _map;

                _map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                        _map[entry.id] = entry;
                    }
                }

                return _map;
            }
        }

        /// <summary>取该 id 的参数；没有单独设置时返回默认值（1 速 / 0 延迟 / 1 音量）。</summary>
        public Entry Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return DefaultEntry;
            return Map.TryGetValue(id, out var entry) ? entry : DefaultEntry;
        }

        /// <summary>窗口用：拿到可编辑的条目，没有就建一个。</summary>
        public Entry EnsureEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return DefaultEntry;

            if (Map.TryGetValue(id, out var entry)) return entry;

            entry = new Entry { id = id };
            var list = new List<Entry>(entries ?? Array.Empty<Entry>()) { entry };
            entries = list.ToArray();
            _map = null;
            return entry;
        }

        /// <summary>窗口用：把某条恢复默认。</summary>
        public void ResetEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            var list = new List<Entry>(entries ?? Array.Empty<Entry>());
            list.RemoveAll(e => e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
            entries = list.ToArray();
            _map = null;
        }

        public void ResetAll()
        {
            entries = Array.Empty<Entry>();
            _map = null;
        }
    }
}
