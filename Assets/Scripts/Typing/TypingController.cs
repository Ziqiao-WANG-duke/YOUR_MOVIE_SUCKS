using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using YourMovieSucks.Input;

namespace YourMovieSucks.Typing
{
    /// <summary>
    /// 打字匹配核心：词条吸附、逐字母匹配、正误判定、连击、超时。
    /// </summary>
    public class TypingController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private TrackInput trackInput;

        [Header("Test")]
        [SerializeField] private bool spawnTestWordsOnStart = true;

        [Header("Debug")]
        [Tooltip("关掉后不画左上调试词表（Demo 关卡用）")]
        [SerializeField] private bool showDebugOverlay = true;

        private readonly List<TypingWord> _words = new List<TypingWord>();
        private readonly List<string> _log = new List<string>();
        private TypingWord _agentLocked;
        private TypingWord _directorLocked;
        private TypingWord _gate;
        private int _combo;

        public int Combo => _combo;
        public IReadOnlyList<TypingWord> Words => _words;

        public TypingWord GetLockedWord(CharacterTrack track)
            => track == CharacterTrack.Agent ? _agentLocked : _directorLocked;

        /// <summary>
        /// 场上是否已有吸附词条以该字母开头。
        /// 用于“同首字母词条不得同时出现”约束：首字母吸附遇上两个同首字母词会有歧义
        /// （玩家想打 my turn 却锁到 my pole）。输入门（gate）不走吸附，故不计入。
        /// </summary>
        public bool HasActiveWordStartingWith(char letter)
        {
            char upper = char.ToUpperInvariant(letter);

            foreach (var word in _words)
            {
                if (word == null || word.IsGate || word.MatchText.Length == 0) continue;
                if (char.ToUpperInvariant(word.MatchText[0]) == upper) return true;
            }

            return false;
        }

        public event System.Action<TypingWord> OnWordSpawned;
        public event System.Action<TypingWord> OnWordCompleted;
        public event System.Action<TypingWord, WordFailReason> OnWordFailed;
        public event System.Action<TypingWord, int> OnLetterMatched;
        public event System.Action<TypingWord, Key> OnLetterMismatch;
        public event System.Action<int> OnComboChanged;

        private void Start()
        {
            if (spawnTestWordsOnStart)
            {
                SpawnWord("COP", CharacterTrack.Agent);
                SpawnWord("FAT", CharacterTrack.Agent);
                SpawnWord("NEON", CharacterTrack.Director, timeLimit: 8f);
            }
        }

        private void OnEnable()
        {
            if (trackInput == null)
                trackInput = GetComponent<TrackInput>();
            if (trackInput != null)
                trackInput.OnLetterTyped += HandleLetter;
        }

        private void OnDisable()
        {
            if (trackInput != null)
                trackInput.OnLetterTyped -= HandleLetter;
        }

        public TypingWord SpawnWord(string displayText, CharacterTrack track, float timeLimit = 0f, GameObject target = null)
        {
            var word = new TypingWord(displayText, track, timeLimit, target);
            _words.Add(word);
            Log($"SPAWN [{TrackLabel(track)}] \"{word.DisplayText}\" -> {word.MatchText}");
            OnWordSpawned?.Invoke(word);
            return word;
        }

        /// <summary>
        /// 演出/教学输入门：出现期间只接受该串的字母，无视当前轨道（用于 TYPE: ZOOM 与切轨前的过场提示）。
        /// 门词条不生成世界标签，由中央提示 UI 逐字母渲染；track 只决定显示大小写（导演轨=大写）。
        /// </summary>
        public TypingWord SpawnGate(string displayText, CharacterTrack track = CharacterTrack.Director)
        {
            ClearGate();

            var word = new TypingWord(displayText, track, 0f, null, isGate: true);
            _words.Add(word);
            _gate = word;
            Log($"GATE \"{word.DisplayText}\" -> {word.MatchText}");
            OnWordSpawned?.Invoke(word);
            return word;
        }

        /// <summary>演出层放弃当前输入门（如玩家 skip 开场）。</summary>
        public void ClearGate()
        {
            if (_gate == null) return;
            _words.Remove(_gate);
            _gate = null;
        }

        public void RemoveWord(TypingWord word)
        {
            if (_agentLocked == word) _agentLocked = null;
            if (_directorLocked == word) _directorLocked = null;
            if (_gate == word) _gate = null;
            _words.Remove(word);
        }

        private void HandleLetter(CharacterTrack track, Key key)
        {
            char letter = KeyToChar(key);
            if (letter == '\0') return;

            if (_gate != null)
            {
                HandleGateLetter(letter, key);
                return;
            }

            TypingWord locked = track == CharacterTrack.Agent ? _agentLocked : _directorLocked;

            if (locked == null)
            {
                // 首字母吸附（仅当前轨）
                foreach (var w in _words)
                {
                    if (w.Track == track && w.Progress == 0 && w.CurrentChar == letter)
                    {
                        locked = w;
                        if (track == CharacterTrack.Agent) _agentLocked = w;
                        else _directorLocked = w;
                        Log($"LOCK [{TrackLabel(track)}] \"{w.DisplayText}\" @ {letter}");
                        break;
                    }
                }

                if (locked == null)
                {
                    // 纯误击：按的字母不可能是任何一个词条的首字母。
                    // 以前只重置连击、不做任何惩罚，于是“只要不碰怪的首字母就能随便乱按”——现在同样算失误。
                    Log($"MISS [{TrackLabel(track)}] {letter} (no word starts with it)");
                    OnLetterMismatch?.Invoke(null, key);
                    ResetCombo();
                    return;
                }
            }

            if (locked.CurrentChar == letter)
            {
                locked.Advance();
                _combo++;
                OnComboChanged?.Invoke(_combo);
                OnLetterMatched?.Invoke(locked, locked.Progress);
                Log($"HIT [{TrackLabel(track)}] {letter}  \"{locked.DisplayText}\" {locked.Progress}/{locked.MatchText.Length}");

                if (locked.IsCompleted)
                {
                    Log($"COMPLETE \"{locked.DisplayText}\"");
                    if (track == CharacterTrack.Agent) _agentLocked = null;
                    else _directorLocked = null;
                    _words.Remove(locked);
                    OnWordCompleted?.Invoke(locked);
                }
            }
            else
            {
                Log($"WRONG [{TrackLabel(track)}] {letter} (expect {locked.CurrentChar})");
                OnLetterMismatch?.Invoke(locked, key);
                ResetCombo();
            }
        }

        private void ResetCombo()
        {
            if (_combo == 0) return;
            _combo = 0;
            OnComboChanged?.Invoke(0);
        }

        /// <summary>输入门匹配：不看轨道、不吸附，只与门串逐字母比对。</summary>
        private void HandleGateLetter(char letter, Key key)
        {
            if (_gate.CurrentChar == letter)
            {
                _gate.Advance();
                _combo++;
                OnComboChanged?.Invoke(_combo);
                OnLetterMatched?.Invoke(_gate, _gate.Progress);
                Log($"HIT [GATE] {letter}  \"{_gate.DisplayText}\" {_gate.Progress}/{_gate.MatchText.Length}");

                if (_gate.IsCompleted)
                {
                    var done = _gate;
                    _gate = null;
                    _words.Remove(done);
                    Log($"COMPLETE [GATE] \"{done.DisplayText}\"");
                    OnWordCompleted?.Invoke(done);
                }
            }
            else
            {
                Log($"WRONG [GATE] {letter} (expect {_gate.CurrentChar})");
                OnLetterMismatch?.Invoke(_gate, key);
                ResetCombo();
            }
        }

        private void Update()
        {
            for (int i = _words.Count - 1; i >= 0; i--)
            {
                var w = _words[i];
                if (w.Tick(Time.deltaTime))
                {
                    Log($"EXPIRED [{TrackLabel(w.Track)}] \"{w.DisplayText}\"");
                    if (_agentLocked == w) _agentLocked = null;
                    if (_directorLocked == w) _directorLocked = null;
                    _words.RemoveAt(i);
                    OnWordFailed?.Invoke(w, WordFailReason.Expired);
                }
            }
        }

        private static char KeyToChar(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return (char)('A' + (key - Key.A));
            return '\0';
        }

        private static string TrackLabel(CharacterTrack track)
            => track == CharacterTrack.Agent ? "AGENT" : "DIR";

        private void Log(string msg)
        {
            _log.Add(msg);
            if (_log.Count > 20)
                _log.RemoveAt(0);
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            const int x = 740;
            const int w = 500;

            GUI.Label(new Rect(x, 20, w, 24), $"COMBO: {_combo}", style);

            GUI.Label(new Rect(x, 50, w, 24), "ACTIVE WORDS:", style);
            int y = 78;
            for (int i = 0; i < _words.Count; i++)
            {
                var word = _words[i];
                bool locked = word == (word.Track == CharacterTrack.Agent ? _agentLocked : _directorLocked);
                bool isAgent = word.Track == CharacterTrack.Agent;

                string done = word.MatchText.Substring(0, word.Progress);
                string todo = word.MatchText.Substring(word.Progress);
                done = isAgent ? done.ToLowerInvariant() : done.ToUpperInvariant();
                todo = isAgent ? todo.ToLowerInvariant() : todo.ToUpperInvariant();

                string trackHex = isAgent ? "#ff4040" : "#4080ff";
                string time = word.HasTimeLimit ? $" {word.RemainingTime:0.0}s" : "";

                GUI.Label(new Rect(x, y, w, 22),
                    $"{(locked ? ">>" : "  ")} <color=#40ff40>{done}</color><color={trackHex}>{todo}</color>{time}", style);
                y += 22;
            }

            y += 10;
            GUI.Label(new Rect(x, y, w, 24), "LOG:", style);
            y += 24;
            for (int i = _log.Count - 1; i >= 0 && i >= _log.Count - 8; i--)
            {
                GUI.Label(new Rect(x, y, w, 20), _log[i], style);
                y += 20;
            }
        }
    }
}
