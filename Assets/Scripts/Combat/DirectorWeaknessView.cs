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
    ///   输入弱点词 → 标记这只怪已识别 → 把这只怪的战斗词换成战斗编辑器「弱点后词库」里的新词
    ///   （词库为空则不换、保持原词；首字母冲突时排队等空位，绝不产生吸附歧义）
    ///   切回特工轨 → 只看得见（新的）战斗词
    ///
    /// **刻意不做镜头动作**：识别弱点不推近、不晃、不改变构图（作者 2026-10-20：战斗中任何镜头动作都是在干扰玩家）。
    ///
    /// 不做的事：识别不击杀怪物、不改攻击/伤害/结算、不加新的导演技能。
    /// 两轨显示不同信息：词条标签按轨道显隐（WordLabelManager），这里只负责弱点词的生成/回收。
    /// </summary>
    public class DirectorWeaknessView : MonoBehaviour
    {
        [SerializeField] private TypingController typing;
        [SerializeField] private TrackInput trackInput;

        [Tooltip("弱点词条相对弱点锚点的固定高度偏移（米）")]
        [SerializeField] private float weaknessLabelOffsetY = 0.3f;

        private readonly Dictionary<TypingWord, MonsterWeakness> _wordToWeakness = new();
        private readonly List<MonsterWeakness> _pendingSwaps = new();
        private readonly HashSet<string> _warnedNoPool = new();

        /// <summary>每个词库（同一怪物类型的同一個数组实例）一个“洗好的袋子”：抽完一轮前不重复，避免总给同一个词。</summary>
        private readonly Dictionary<string[], Queue<string>> _bags = new();
        private bool _directorView;

        private void OnEnable()
        {
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (trackInput == null) trackInput = Object.FindFirstObjectByType<TrackInput>();

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

            // 换词排队不能只看“当前在哪条轨”：否则玩家一看回特工视角，待换的词就永远不再重试
            RetryPendingSwaps();

            if (_directorView) ShowWeaknesses();       // 导演轨期间新刷出来的怪也补上弱点词
        }

        // ---------------------------------------------------------------- 显示 / 回收

        private void ShowWeaknesses()
        {
            foreach (var weakness in MonsterWeakness.All)
            {
                if (weakness == null || weakness.Recognized) continue;

                var target = weakness.GetComponent<TypingTarget>();
                if (target != null && target.IsDead) continue;

                // 这类怪没有「弱点后词库」（类型词库与默认词库都空）：不给它挂弱点词。
                // 否则玩家打完弱点却什么都看不到（词不变、弱点也消失），很像 bug。
                if (!CombatEncounterAsset.HasAnyWord(weakness.PostWeakWords))
                {
                    weakness.SetVisible(false);
                    WarnMissingPool(weakness);
                    continue;
                }

                weakness.SetVisible(true);
                SpawnWeaknessWord(weakness);
            }
        }

        /// <summary>没有弱点后词库的警告：每种怪只报一次，避免刷屏。</summary>
        private void WarnMissingPool(MonsterWeakness weakness)
        {
            string key = weakness.name.Replace("_Entrance", string.Empty);
            if (!_warnedNoPool.Add(key)) return;
            Debug.LogWarning($"[DirectorWeaknessView] {key} 没有配置「弱点后词库」（战斗编辑器 CombatEncounter：怪物配置里的词库，或总览里的默认词库），" +
                             "所以导演轨不给它挂弱点词（打了也不会换词）。");
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

            // 这里刻意不动镜头（作者 2026-10-20：战斗中的镜头动作会干扰玩家）
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

            string candidate = PickReplacement(weakness, current);
            if (candidate == null)
            {
                // 词库/简化词的首字母都被占：先保留原词（怪仍可击杀），等场上空出首字母再换
                if (!_pendingSwaps.Contains(weakness)) _pendingSwaps.Add(weakness);
                return;
            }

            if (TypingWord.Normalize(candidate) == current.MatchText) return;   // 新词与当前词一样：不用换

            typing.RemoveWord(current);
            typing.SpawnWord(candidate, CharacterTrack.Agent, 0f, target.gameObject);
        }

        /// <summary>
        /// 弱点破坏后给这只怪换上的新词：从该怪物类型的「弱点后词库」里随机抽（洗牌后按轮抽，不重复），
        /// 跳过首字母已破占用的词（避开锁定吸附歧义）。没有可用的词则返回 null（调用方排队等）。
        /// </summary>
        private string PickReplacement(MonsterWeakness weakness, TypingWord ignore)
        {
            var pool = weakness.PostWeakWords;
            if (pool == null || pool.Length == 0) return null;

            if (!_bags.TryGetValue(pool, out var bag) || bag == null)
            {
                bag = new Queue<string>();
                _bags[pool] = bag;
            }

            // 最多抽两轮：首字母被占的词丢掉，继续抽下一个
            for (int attempt = 0; attempt < pool.Length * 2; attempt++)
            {
                if (bag.Count == 0) RefillBag(bag, pool);
                if (bag.Count == 0) return null;

                string candidate = bag.Dequeue();
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                candidate = candidate.Trim();
                if (candidate.Length == 0) continue;
                if (!IsInitialFree(candidate, ignore)) continue;

                return candidate;
            }

            return null;
        }

        /// <summary>把词库洗牌后装进袋子（抽完一轮才会重复）。</summary>
        private static void RefillBag(Queue<string> bag, string[] pool)
        {
            var words = new List<string>(pool.Length);
            foreach (var raw in pool)
                if (!string.IsNullOrWhiteSpace(raw)) words.Add(raw.Trim());
            if (words.Count == 0) return;

            for (int i = words.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (words[i], words[j]) = (words[j], words[i]);
            }

            foreach (var w in words) bag.Enqueue(w);
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

                string candidate = PickReplacement(weakness, current);
                if (candidate == null) continue;
                if (TypingWord.Normalize(candidate) == current.MatchText) { _pendingSwaps.RemoveAt(i); continue; }

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
