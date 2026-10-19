using System.Collections.Generic;
using UnityEngine;
using YourMovieSucks.Input;
using YourMovieSucks.Typing;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 词条标签管理器：为每个词条创建/销毁头顶 World Space 标签。
    /// </summary>
    public class WordLabelManager : MonoBehaviour
    {
        [SerializeField] private TypingController typing;

        [Tooltip("用于按轨道显隐词条：特工轨只显示战斗词、导演轨只显示弱点词")]
        [SerializeField] private TrackInput trackInput;

        [Tooltip("词条材质（Assets/Art/Shaders/M_WordLabelOnTop.mat）：ZTest Always + Overlay，保证词条永远可见")]
        [SerializeField] private Material alwaysOnTopMaterial;

        private readonly Dictionary<TypingWord, WordLabel> _labels = new();

        private void OnEnable()
        {
            if (typing == null) typing = GetComponent<TypingController>();
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (typing == null) return;

            typing.OnWordSpawned += HandleSpawned;
            typing.OnWordCompleted += HandleEnded;
            typing.OnWordRemoved += HandleEnded;
            typing.OnWordFailed += HandleFailed;
        }

        private void OnDisable()
        {
            if (typing != null)
            {
            typing.OnWordSpawned -= HandleSpawned;
            typing.OnWordCompleted -= HandleEnded;
            typing.OnWordRemoved -= HandleEnded;
            typing.OnWordFailed -= HandleFailed;
            }
        }

        private void HandleSpawned(TypingWord word)
        {
            // 输入门/无标签词条（如 TYPE: ZOOM）由中央提示渲染，不生成世界标签。
            if (!word.ShowLabel) return;

            var go = new GameObject("WordLabel_" + word.MatchText);
            var label = go.AddComponent<WordLabel>();
            label.Init(word, alwaysOnTopMaterial);
            _labels[word] = label;
        }

        private void HandleEnded(TypingWord word)
        {
            if (_labels.TryGetValue(word, out var label))
            {
                _labels.Remove(word);
                if (label != null) Destroy(label.gameObject);
            }
        }

        private void HandleFailed(TypingWord word, WordFailReason reason)
        {
            HandleEnded(word);
        }

        /// <summary>
        /// 两条轨道显示不同信息：特工轨只看得见战斗词，导演轨只看得见弱点词
        /// （不允许战斗词和弱点词同时出现在场上）。
        /// </summary>
        private void LateUpdate()
        {
            if (_labels.Count == 0) return;

            var track = trackInput != null ? trackInput.CurrentTrack : CharacterTrack.Agent;

            foreach (var pair in _labels)
            {
                var label = pair.Value;
                if (label == null || pair.Key == null) continue;

                bool visible = pair.Key.Track == track;
                if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            }
        }
    }
}
