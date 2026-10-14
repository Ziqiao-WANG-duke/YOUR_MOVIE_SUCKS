using UnityEngine;
using UnityEngine.InputSystem;
using YourMovieSucks.Input;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Demo
{
    public enum RunEndCause
    {
        Completed,   // 打完（PRINT IT!）
        Died,        // 被怪物打死（BAD TAKE）
        ManualRetry  // 玩家主动重开
    }

    /// <summary>
    /// 单局统计：结算屏（ACCURACY / WORDS TYPED / TYPING SPEED / ENEMIES KILLED / MISSTYPES）
    /// 与 Lab B 遥测共用同一套埋点，避免两套口径算不出数字。
    /// </summary>
    public class RunStats : MonoBehaviour
    {
        public static RunStats I { get; private set; }

        [SerializeField] private TypingController typing;

        public int RunIndex { get; private set; }
        public bool IsRunning { get; private set; }

        public int WordsTyped { get; private set; }
        public int LettersTyped { get; private set; }
        public int Mistypes { get; private set; }
        public int AgentWords { get; private set; }
        public int DirectorWords { get; private set; }
        public int Kills { get; private set; }

        private float _runStartUnscaled;
        private float _lastDuration;

        public float LastDuration => _lastDuration;
        public float RunDuration => IsRunning ? Time.unscaledTime - _runStartUnscaled : _lastDuration;

        /// <summary>正确字母 ÷ 全部字母（含打错的）。</summary>
        public int AccuracyPercent
        {
            get
            {
                int total = LettersTyped + Mistypes;
                return total <= 0 ? 0 : Mathf.RoundToInt(100f * LettersTyped / total);
            }
        }

        /// <summary>标准 WPM：正确字符数 ÷ 5 ÷ 分钟。</summary>
        public int Wpm
        {
            get
            {
                float minutes = RunDuration / 60f;
                if (minutes <= 0.001f) return 0;
                return Mathf.RoundToInt(LettersTyped / 5f / minutes);
            }
        }

        public event System.Action OnRunStarted;
        public event System.Action<RunEndCause> OnRunEnded;

        private void Awake()
        {
            I = this;
            Resolve();
        }

        private void OnEnable()
        {
            Resolve();
            if (typing == null) return;
            typing.OnLetterMatched += HandleMatched;
            typing.OnLetterMismatch += HandleMismatch;
            typing.OnWordCompleted += HandleCompleted;
        }

        private void OnDisable()
        {
            if (typing == null) return;
            typing.OnLetterMatched -= HandleMatched;
            typing.OnLetterMismatch -= HandleMismatch;
            typing.OnWordCompleted -= HandleCompleted;
        }

        private void Resolve()
        {
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
        }

        public void BeginRun()
        {
            RunIndex++;
            WordsTyped = 0;
            LettersTyped = 0;
            Mistypes = 0;
            AgentWords = 0;
            DirectorWords = 0;
            Kills = 0;
            _runStartUnscaled = Time.unscaledTime;
            IsRunning = true;
            OnRunStarted?.Invoke();
        }

        public void EndRun(RunEndCause cause)
        {
            if (!IsRunning) return;
            _lastDuration = Time.unscaledTime - _runStartUnscaled;
            IsRunning = false;
            OnRunEnded?.Invoke(cause);
        }

        private void HandleMatched(TypingWord word, int progress) => LettersTyped++;

        private void HandleMismatch(TypingWord word, Key key) => Mistypes++;

        private void HandleCompleted(TypingWord word)
        {
            // 输入门（演出/教学用，如 TYPE: ZOOM）只计字母，不计入词数与击杀，
            // 保证结算屏的 WORDS TYPED / ENEMIES KILLED 只反映战斗内容。
            if (word.IsGate) return;

            WordsTyped++;
            if (word.Track == CharacterTrack.Agent)
            {
                AgentWords++;
                Kills++; // 当前设计：打完特工轨词条 = 击杀
            }
            else
            {
                DirectorWords++;
            }
        }
    }
}
