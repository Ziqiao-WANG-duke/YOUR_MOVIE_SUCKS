using System;
using System.Collections;
using UnityEngine;

namespace YourMovieSucks.Demo
{
    public enum DemoSpeaker
    {
        Director,
        Agent
    }

    /// <summary>
    /// 一行叙事台词（不可打字）。
    /// · id：对应配音文件名（VoLibrary 里的 key，如 `pp_open_dir_01_production_value`）；留空或没有音频时退回字幕计时。
    /// · text 为空时表示纯停顿，用 hold 表示时长。
    /// </summary>
    [Serializable]
    public class DemoLine
    {
        public string id = string.Empty;
        public DemoSpeaker speaker = DemoSpeaker.Director;
        [TextArea] public string text = "";
        [Tooltip("没有配音时的停留时长（秒）；有配音时以音频长度为准")]
        public float hold = 1.6f;
    }

    /// <summary>
    /// 对白播放层：有配音时**以音频长度为准**推进（字幕按音频长度铺开），没有配音时退回"逐字显示 + hold"。
    /// abort 回调供演出层 skip 使用（返回 true 时立刻中断整段对白，并把 WasAborted 置位）。
    /// </summary>
    public class DemoDialogue : MonoBehaviour
    {
        [SerializeField] private DemoHud hud;

        [Tooltip("配音总控；留空则全部台词走字幕计时")]
        [SerializeField] private DemoAudioDirector audio;

        [Tooltip("没有配音时，每秒显示的字符数（复古打字机感）")]
        [SerializeField] private float typeSpeed = 42f;

        [Tooltip("没有配音时，每行最短停留时长")]
        [SerializeField] private float minHold = 0.4f;

        [Tooltip("有配音时，字幕在音频前百分之多少内显示完")]
        [Range(0.2f, 1f)]
        [SerializeField] private float voicedRevealRatio = 0.55f;

        private bool _aborted;

        public bool IsPlaying { get; private set; }

        /// <summary>上一段对白是否被 skip 中断（演出层据此结束整段演出）。</summary>
        public bool WasAborted => _aborted;

        public IEnumerator Play(DemoLine[] lines, Func<bool> abort = null)
        {
            if (lines == null) yield break;

            IsPlaying = true;
            _aborted = false;

            // 对白期间压低 BGM，播完恢复（由音频总控平滑过渡）
            if (audio != null) audio.SetDialogueDucking(true);

            foreach (var line in lines)
            {
                if (line == null) continue;

                string speaker = line.speaker == DemoSpeaker.Director ? "DIRECTOR" : "AGENT";

                if (string.IsNullOrEmpty(line.text))
                {
                    if (hud != null) hud.HideSubtitle();
                    if (audio != null) audio.StopVo();

                    yield return Wait(Mathf.Max(line.hold, 0f), abort);
                    if (_aborted) break;
                    continue;
                }

                float voLength = audio != null ? audio.PlayVo(line.id) : 0f;

                if (voLength > 0f)
                    yield return PlayVoicedLine(speaker, line.text, voLength, audio.LastVoDelay, abort);
                else
                    yield return PlayTypewriterLine(speaker, line.text, abort);

                if (_aborted) break;
            }

            Stop();
        }

        /// <summary>有配音：等完逐条设置的延迟后字幕才开始铺开，音频播完（含尾巴）才推进。</summary>
        private IEnumerator PlayVoicedLine(string speaker, string text, float voLength, float delay, Func<bool> abort)
        {
            // 延迟期间先收起上一句字幕，避免“上一句还挂着、下一句还没开口”
            if (hud != null) hud.HideSubtitle();

            float revealStart = Mathf.Clamp(delay, 0f, Mathf.Max(voLength - 0.1f, 0f));
            float revealSpan = Mathf.Max((voLength - revealStart) * voicedRevealRatio, 0.2f);
            float t = 0f;
            int lastVisible = -1;

            while (t < voLength)
            {
                if (abort != null && abort())
                {
                    _aborted = true;
                    yield break;
                }

                t += Time.deltaTime;

                if (t >= revealStart && hud != null)
                {
                    int visible = Mathf.Clamp(Mathf.CeilToInt(text.Length * ((t - revealStart) / revealSpan)), 0, text.Length);
                    if (visible != lastVisible)
                    {
                        lastVisible = visible;
                        hud.ShowSubtitle(speaker, text.Substring(0, visible));
                    }
                }

                yield return null;
            }

            // 音频放完，补齐整句（避免因帧率漏掉最后几个字）
            if (lastVisible != text.Length && hud != null)
                hud.ShowSubtitle(speaker, text);
        }

        /// <summary>没有配音：逐字显示 + hold 停留。</summary>
        private IEnumerator PlayTypewriterLine(string speaker, string text, Func<bool> abort)
        {
            if (hud != null) hud.ShowSubtitle(speaker, string.Empty);

            float typed = 0f;
            int lastVisible = -1;

            while (typed < text.Length)
            {
                if (abort != null && abort())
                {
                    _aborted = true;
                    yield break;
                }

                typed += typeSpeed * Time.deltaTime;
                int visible = Mathf.Clamp(Mathf.CeilToInt(typed), 0, text.Length);

                if (visible != lastVisible)
                {
                    lastVisible = visible;
                    if (hud != null) hud.ShowSubtitle(speaker, text.Substring(0, visible));
                }

                yield return null;
            }

            yield return null; // 让最后一段字幕上屏
        }

        private IEnumerator Wait(float seconds, Func<bool> abort)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (abort != null && abort())
                {
                    _aborted = true;
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }
        }

        public void Stop()
        {
            IsPlaying = false;
            if (hud != null) hud.HideSubtitle();
            if (audio != null)
            {
                audio.StopVo();
                audio.SetDialogueDucking(false);
            }
        }
    }
}
