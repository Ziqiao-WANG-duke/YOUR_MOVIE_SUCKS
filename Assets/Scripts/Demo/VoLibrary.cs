using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 配音映射资产：lineId → AudioClip。
    /// id 与音频文件名一一对应（例如 `pp_open_dir_01_production_value`），
    /// 由 `YourMovieSucks/Voice/2 · 建立 VoLibrary 并接线` 自动从 Assets/Audio/VO/PolePosition 生成。
    /// 缺某条 id 时 <see cref="Get"/> 返回 null，对白层会退回字幕计时（可分批交付）。
    /// </summary>
    [CreateAssetMenu(menuName = "YourMovieSucks/VO Library", fileName = "VoLibrary")]
    public class VoLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public AudioClip clip;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, AudioClip> _map;

        public int Count => entries != null ? entries.Length : 0;

        public IEnumerable<Entry> Entries => entries ?? Array.Empty<Entry>();

        private void OnEnable() => _map = null;

        private Dictionary<string, AudioClip> Map
        {
            get
            {
                if (_map != null) return _map;

                _map = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                        _map[entry.id] = entry.clip;
                    }
                }

                return _map;
            }
        }

        public AudioClip Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Map.TryGetValue(id, out var clip) ? clip : null;
        }

        public bool Has(string id) => Get(id) != null;

        /// <summary>该条音频时长（秒）；没有音频返回 0。</summary>
        public float GetLength(string id)
        {
            var clip = Get(id);
            return clip != null ? clip.length : 0f;
        }

        /// <summary>接线用：整体替换映射表。</summary>
        public void SetEntries(IEnumerable<Entry> newEntries)
        {
            entries = new List<Entry>(newEntries).ToArray();
            _map = null;
        }
    }
}
