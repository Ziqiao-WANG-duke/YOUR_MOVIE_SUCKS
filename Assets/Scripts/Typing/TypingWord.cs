using System.Text;
using UnityEngine;
using YourMovieSucks.Input;

namespace YourMovieSucks.Typing
{
    public enum WordFailReason
    {
        Expired,
        Cancelled
    }

    /// <summary>
    /// 一个可打字的词条。显示文本可含空格/标点，匹配文本为去空格标点后的大写字母串。
    /// </summary>
    public class TypingWord
    {
        public string DisplayText { get; }
        public string MatchText { get; }
        public CharacterTrack Track { get; }
        public int Progress { get; private set; }
        public float TimeLimit { get; }
        public float RemainingTime { get; private set; }

        public bool HasTimeLimit => TimeLimit > 0f;
        public bool IsCompleted => Progress >= MatchText.Length;
        public bool IsExpired => HasTimeLimit && RemainingTime <= 0f;
        public char CurrentChar => Progress < MatchText.Length ? MatchText[Progress] : '\0';
        public string RemainingDisplay => MatchText.Substring(Progress);

        /// <summary>词条绑定的目标对象（如敌人）。可为 null。</summary>
        public GameObject Target { get; }

        /// <summary>导演轨的「弱点识别」词条（不是怪物战斗词）。</summary>
        public bool IsWeakness { get; }

        /// <summary>标签的固定高度偏移（米）；< 0 表示按目标包围盒自动算（旧行为）。</summary>
        public float LabelOffsetY { get; }

        /// <summary>输入门词条（演出/教学用，如 TYPE: ZOOM）：无视轨道、不挂 World Space 标签。</summary>
        public bool IsGate { get; }

        /// <summary>是否生成头顶/世界标签（输入门词条由中央提示渲染，故为 false）。</summary>
        public bool ShowLabel { get; }

        /// <summary>按轨道格式化的显示文本：特工=小写，导演=大写。</summary>
        public string TrackDisplayText =>
            Track == CharacterTrack.Agent ? MatchText.ToLowerInvariant() : MatchText.ToUpperInvariant();

        /// <summary>按轨道大小写格式化的**显示串**（保留原文空格与标点）。</summary>
        public string CasedDisplayText =>
            Track == CharacterTrack.Agent
                ? DisplayText.ToLowerInvariant()
                : DisplayText.ToUpperInvariant();

        /// <summary>
        /// 当前待输入字母在 <see cref="CasedDisplayText"/> 里的下标（用来把箭头摆到那个字母上方）；
        /// 已打完返回 -1。与 <see cref="BuildDisplayRichText"/> 的计数口径一致：只数字母，空格/标点不算。
        /// </summary>
        public int CurrentDisplayIndex
        {
            get
            {
                string text = CasedDisplayText;
                int letterIndex = 0;

                for (int i = 0; i < text.Length; i++)
                {
                    if (!char.IsLetter(text[i])) continue;
                    if (letterIndex == Progress) return i;
                    letterIndex++;
                }

                return -1;
            }
        }

        public TypingWord(string displayText, CharacterTrack track, float timeLimit = 0f, GameObject target = null,
            bool isGate = false, bool showLabel = true, bool isWeakness = false, float labelOffsetY = -1f)
        {
            DisplayText = displayText;
            Track = track;
            TimeLimit = timeLimit;
            RemainingTime = timeLimit;
            MatchText = Normalize(displayText);
            Target = target;
            IsGate = isGate;
            ShowLabel = showLabel && !isGate;
            IsWeakness = isWeakness;
            LabelOffsetY = labelOffsetY;
        }

        /// <summary>
        /// 逐字母富文本（显示保留空格与标点，只有字母参与进度）：
        /// 已完成字母用 doneHex、当前待打字母用 currentHex、未打字母用 todoHex。
        /// 用于头顶词条与中央输入门提示，避免把 MatchText 直接画出来吃掉空格。
        /// </summary>
        public string BuildDisplayRichText(string doneHex, string currentHex, string todoHex)
        {
            string text = CasedDisplayText;
            var sb = new StringBuilder(text.Length * 24);
            int letterIndex = 0;

            foreach (char c in text)
            {
                if (!char.IsLetter(c))
                {
                    sb.Append(c);
                    continue;
                }

                string hex = letterIndex < Progress
                    ? doneHex
                    : (letterIndex == Progress ? currentHex : todoHex);

                sb.Append("<color=#").Append(hex).Append('>').Append(c).Append("</color>");
                letterIndex++;
            }

            return sb.ToString();
        }

        public bool Advance()
        {
            if (IsCompleted) return false;
            Progress++;
            return true;
        }

        /// <summary>进度清零并重置超时（用于跨轨放弃锁定）。</summary>
        public void Reset()
        {
            Progress = 0;
            RemainingTime = TimeLimit;
        }

        /// <summary>倒计时；返回是否在本帧超时。</summary>
        public bool Tick(float deltaTime)
        {
            if (!HasTimeLimit || IsCompleted || IsExpired) return false;
            RemainingTime -= deltaTime;
            return IsExpired;
        }

        /// <summary>无空格无标点规则：仅保留字母并转大写。TAKE THIS -> TAKETHIS</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var c in input)
            {
                if (char.IsLetter(c))
                    sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }
}
