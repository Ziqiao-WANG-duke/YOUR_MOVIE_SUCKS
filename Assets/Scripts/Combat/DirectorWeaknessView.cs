using System.Collections.Generic;
using UnityEngine;
using YourMovieSucks.Input;
using YourMovieSucks.Level;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 导演轨「弱点识别」编排（第一版：单只怪的完整闭环，多只怪各自独立、同时最多 5 只）：
    ///
    ///   切到导演轨 → 场上未识别的怪亮红光 + 挂上弱点词（大写、走现有打字判定，**不需要额外的 ZOOM 指令**）
    ///   输入弱点词 → 标记这只怪已识别 → 镜头快速推进到弱点播短暂特写
    ///                → 把这只怪的战斗词换成更简单的词（首字母冲突时排队等空位，绝不产生吸附歧义）
    ///   切回特工轨 → 只看得见（新的）战斗词
    ///
    /// 不做的事：识别不击杀怪物、不改攻击/伤害/结算、不加新的导演技能。
    /// 两轨显示不同信息：词条标签按轨道显隐（WordLabelManager），这里只负责弱点词的生成/回收。
    /// </summary>
    public class DirectorWeaknessView : MonoBehaviour
    {
        [SerializeField] private TypingController typing;
        [SerializeField] private TrackInput trackInput;
        [SerializeField] private WeakPointCloseUpCamera closeUp;

        [Tooltip("弱点词条相对弱点锚点的固定高度偏移（米）")]
        [SerializeField] private float weaknessLabelOffsetY = 0.3f;

        private readonly Dictionary<TypingWord, MonsterWeakness> _wordToWeakness = new();
        private readonly List<MonsterWeakness> _pendingSwaps = new();
        private bool _directorView;

        private void OnEnable()
        {
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (trackInput == null) trackInput = Object.FindFirstObjectByType<TrackInput>();
            if (closeUp == null) closeUp = Object.FindFirstObjectByType<WeakPointCloseUpCamera>();

            if (typing == null) return;
            typing.OnWordCompleted += HandleWordCompleted;
            typing.OnWordRemoved += HandleWordRemoved;
        }

        private void OnDisable()
        {
            if (typing == null) return;
            typing.OnWordCompleted -= HandleWordCompleted;
            typing.OnWordRemoved -= HandleWordRemoved;
        }

        private void Update()
        {
            if (typing == null) return;

            bool director = trackInput == null || trackInput.CurrentTrack == CharacterTrack.Director;
            if (director != _directorView)
            {
                _directorView = director;
                if (director) ShowWeaknesses();
                else HideWeaknesses();
            }

            if (!_directorView) return;

            ShowWeaknesses();       // 导演轨期间新刷出来的怪也补上弱点词
            RetryPendingSwaps();
        }

        // ---------------------------------------------------------------- 显示 / 回收

        private void ShowWeaknesses()
        {
            foreach (var weakness in MonsterWeakness.All)
            {
                if (weakness == null || weakness.Recognized) continue;

                var target = weakness.GetComponent<TypingTarget>();
                if (target != null && target.IsDead) continue;

                weakness.SetVisible(true);
                SpawnWeaknessWord(weakness);
            }
        }

        private void HideWeaknesses()
        {
            foreach (var word in new List<TypingWord>(_wordToWeakness.Keys))
                typing.RemoveWord(word);   // → OnWordRemoved 里清字典与标签

            foreach (var weakness in MonsterWeakness.All)
            {
                if (weakness != null) weakness.SetVisible(false);
            }
        }

        private void SpawnWeaknessWord(MonsterWeakness weakness)
        {
            if (weakness == null || string.IsNullOrEmpty(weakness.WeakWord)) return;
            if (_wordToWeakness.ContainsValue(weakness)) return;

            var word = typing.SpawnWord(
                weakness.WeakWord,
                CharacterTrack.Director,
                0f,
                weakness.LabelAnchor,
                isWeakness: true,
                labelOffsetY: weaknessLabelOffsetY);

            _wordToWeakness[word] = weakness;
        }

        // ---------------------------------------------------------------- 识别

        private void HandleWordCompleted(TypingWord word)
        {
            if (word == null || !_wordToWeakness.TryGetValue(word, out var weakness)) return;

            _wordToWeakness.Remove(word);

            if (weakness == null || weakness.Recognized) return;

            weakness.MarkRecognized();

            // 特写期间把怪物定格（SpotlightHold）：镜头会被挪到怪物脸前，
            // 而“到站开打”是拿怪物到相机的距离判断的 —— 不定格就会在特写里误判已到达，
            // 停在原地开打（离玩家还很远），且 _arrived 会 latch 住，镜头回位后也不再走过去。
            // 镜头回位回调里再解除，所以无论特写是否真的播（轨道镜头在推就跳过）一定会恢复。
            var target = weakness.GetComponent<TypingTarget>();
            if (closeUp != null && target != null)
            {
                target.SpotlightHold = true;
                closeUp.Play(weakness.WeakPoint, () =>
                {
                    if (target != null) target.SpotlightHold = false;
                });
            }

            TrySwapCombatWord(weakness);
        }

        private void HandleWordRemoved(TypingWord word)
        {
            if (word != null) _wordToWeakness.Remove(word);
        }

        // ---------------------------------------------------------------- 换词

        private void TrySwapCombatWord(MonsterWeakness weakness)
        {
            var target = weakness.GetComponent<TypingTarget>();
            if (target == null || target.IsDead) return;

            var current = FindCombatWord(target.gameObject);
            if (current == null) return;   // 已经不在场上了

            string candidate = weakness.SimplifiedWord;
            if (!IsInitialFree(candidate, current))
                candidate = weakness.FirstTokenOfOriginalWord;   // 同首字母兜底：永远是原词的第一个词

            if (!IsInitialFree(candidate, current))
            {
                // 两个候选都被占：先保留原词（怪仍然可击杀），等场上空出首字母再换
                if (!_pendingSwaps.Contains(weakness)) _pendingSwaps.Add(weakness);
                return;
            }

            typing.RemoveWord(current);
            typing.SpawnWord(candidate, CharacterTrack.Agent, 0f, target.gameObject);
        }

        private void RetryPendingSwaps()
        {
            if (_pendingSwaps.Count == 0) return;

            for (int i = _pendingSwaps.Count - 1; i >= 0; i--)
            {
                var weakness = _pendingSwaps[i];
                if (weakness == null || weakness.Recognized == false)
                {
                    _pendingSwaps.RemoveAt(i);
                    continue;
                }

                var target = weakness.GetComponent<TypingTarget>();
                if (target == null || target.IsDead)
                {
                    _pendingSwaps.RemoveAt(i);
                    continue;
                }

                var current = FindCombatWord(target.gameObject);
                if (current == null)
                {
                    _pendingSwaps.RemoveAt(i);
                    continue;
                }

                string candidate = weakness.SimplifiedWord;
                if (!IsInitialFree(candidate, current)) candidate = weakness.FirstTokenOfOriginalWord;
                if (!IsInitialFree(candidate, current)) continue;

                typing.RemoveWord(current);
                typing.SpawnWord(candidate, CharacterTrack.Agent, 0f, target.gameObject);
                _pendingSwaps.RemoveAt(i);
            }
        }

        private TypingWord FindCombatWord(GameObject monster)
        {
            foreach (var word in typing.Words)
            {
                if (word == null || word.IsWeakness || word.IsGate || word.Target == null) continue;
                if (word.Target == monster) return word;
            }

            return null;
        }

        /// <summary>该首字母是否已被场上其它（非弱点、非门）战斗词占用；ignore 是被替换掉的那个词。</summary>
        private bool IsInitialFree(string candidate, TypingWord ignore)
        {
            string match = TypingWord.Normalize(candidate);
            if (match.Length == 0) return false;

            char initial = match[0];
            foreach (var word in typing.Words)
            {
                if (word == null || word == ignore || word.IsGate || word.IsWeakness) continue;
                if (word.MatchText.Length == 0) continue;
                if (char.ToUpperInvariant(word.MatchText[0]) == initial) return false;
            }

            return true;
        }
    }
}
