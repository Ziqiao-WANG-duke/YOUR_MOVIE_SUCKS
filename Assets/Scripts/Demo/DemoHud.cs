using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YourMovieSucks.Input;
using YourMovieSucks.Typing;
using YourMovieSucks.UI;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// Demo 演出 UI（运行期构建，与工程既有 UI 约定一致：GameOverController / ScreenBlood 同样手动建 Canvas）。
    /// 负责：黑屏与淡入淡出、中央输入提示（逐字母进度）、COMBAT START 横幅、对白字幕、
    /// 轨道徽章（切轨即时视觉反馈，AGENTS 第 10 条要求）、PRINT IT! 结算屏。
    /// </summary>
    public class DemoHud : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TrackInput trackInput;

        [Header("结算屏文案")]
        [SerializeField] private string resultsTitle = "PRINT IT!";
        [SerializeField] private string resultsSubtitle = "POLE POSITION — WRAPPED";
        [SerializeField] private string resultsFooter = "THAT'S A WRAP.";
        [SerializeField] private string retryHint = "PRESS R TO RETRY";

        [Header("节奏")]
        [SerializeField] private float bannerDuration = 1.8f;

        private static readonly Color AgentColor = new Color(1f, 0.25f, 0.25f);
        private static readonly Color DirectorColor = new Color(0.32f, 0.56f, 1f);

        // 与 WordLabel 保持一致：未打=白、已完成=绿。当前待输入字母不再染色，改由箭头指出
        // （旧版把首字母染黄，玩家会以为已经打过、从第二个字母开始输）。
        private const string DoneHex = "2bff6a";
        private const string TodoAgentHex = "ffffff";
        private const string TodoDirectorHex = "ffffff";
        private static readonly Color ArrowColor = new Color(1f, 0.878f, 0.4f, 1f); // #ffe066

        private Font _font;
        private Canvas _canvas;
        private Image _blackout;
        private Image _trackBar;
        private Text _trackBadge;
        private GameObject _promptRoot;
        private Text _promptText;
        private Text _promptHint;
        private Image _promptArrow;
        private TypingWord _promptWord;
        private string _promptPlainText;
        private int _promptPlainPrefixLength;
        private GameObject _subtitleRoot;
        private Text _subtitleSpeaker;
        private Text _subtitleText;
        private Text _banner;
        private GameObject _resultsRoot;
        private Text _resultsBody;

        private float _bannerTimer;

        /// <summary>结算屏是否已显示（演出层据此开放 R 重开）。</summary>
        public bool IsResultsVisible => _resultsRoot != null && _resultsRoot.activeSelf;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
            Blackout(1f);
        }

        private void OnEnable()
        {
            if (trackInput != null) trackInput.OnTrackChanged += HandleTrackChanged;
            RefreshTrack(trackInput != null ? trackInput.CurrentTrack : CharacterTrack.Agent);
        }

        private void OnDisable()
        {
            if (trackInput != null) trackInput.OnTrackChanged -= HandleTrackChanged;
        }

        private void Update()
        {
            if (_bannerTimer <= 0f) return;

            _bannerTimer -= Time.deltaTime;
            if (_bannerTimer <= 0f && _banner != null)
                _banner.gameObject.SetActive(false);
        }

        /// <summary>箭头摆在当前待输入字母上方（文本布局完才能量宽度，所以放 LateUpdate）。</summary>
        private void LateUpdate()
        {
            if (_promptWord == null || _promptArrow == null) return;

            TextCaretArrow.Place(_promptArrow, _promptText, _promptPlainText,
                _promptPlainPrefixLength + _promptWord.CurrentDisplayIndex, 34f, 22f, 8f);
        }

        // ---------------------------------------------------------------- 黑屏 / 淡入

        public void Blackout(float alpha)
        {
            if (_blackout == null) return;
            _blackout.gameObject.SetActive(alpha > 0.001f);
            var c = _blackout.color;
            c.a = Mathf.Clamp01(alpha);
            _blackout.color = c;
        }

        public IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
        {
            if (duration <= 0f)
            {
                Blackout(toAlpha);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                Blackout(Mathf.Lerp(fromAlpha, toAlpha, t / duration));
                yield return null;
            }

            Blackout(toAlpha);
        }

        // ---------------------------------------------------------------- 中央输入提示

        private string _promptPrefix = "TYPE:";

        /// <summary>显示中央提示（纯文案）：大标题 + 下方小提示。</summary>
        public void ShowPromptText(string bigText, string hint)
        {
            if (_promptRoot == null) return;

            _promptPrefix = bigText;
            _promptRoot.SetActive(true);
            if (_promptText != null) _promptText.text = $"<color=#ffffff><b>{RichEscape(bigText)}</b></color>";
            if (_promptHint != null) _promptHint.text = hint;
        }

        /// <summary>用当前词条的输入进度刷新提示（已完成绿、未打白；当前待输入字母由箭头指出）。</summary>
        public void SetPromptWord(TypingWord word)
        {
            if (_promptText == null || word == null) return;

            string todoHex = word.Track == CharacterTrack.Agent ? TodoAgentHex : TodoDirectorHex;
            string rich = word.BuildDisplayRichText(DoneHex, todoHex, todoHex);
            string openTag = "<color=#e6e6e6>";
            string prefix = $"{openTag}{RichEscape(_promptPrefix)} </color>";
            _promptText.text = $"{prefix}<size=76>{rich}</size>";

            // 箭头要加上前缀的可见字符数（颜色/大小标签不算字符）
            _promptWord = word;
            _promptPlainText = $"{_promptPrefix} {word.CasedDisplayText}";
            _promptPlainPrefixLength = _promptPrefix.Length + 1;
        }

        public void HidePrompt()
        {
            _promptWord = null;
            if (_promptRoot != null) _promptRoot.SetActive(false);
        }

        // ---------------------------------------------------------------- 字幕 / 横幅

        public void ShowSubtitle(string speaker, string line)
        {
            if (_subtitleRoot == null) return;
            _subtitleRoot.SetActive(true);

            bool director = speaker != null && speaker.ToUpperInvariant().StartsWith("DIRECTOR");
            if (_subtitleSpeaker != null)
            {
                _subtitleSpeaker.text = speaker;
                _subtitleSpeaker.color = director ? DirectorColor : AgentColor;
            }
            if (_subtitleText != null) _subtitleText.text = line;
        }

        public void HideSubtitle()
        {
            if (_subtitleRoot != null) _subtitleRoot.SetActive(false);
        }

        public void ShowBanner(string text)
        {
            if (_banner == null) return;
            _banner.text = text;
            _banner.gameObject.SetActive(true);
            _bannerTimer = bannerDuration;
        }

        // ---------------------------------------------------------------- 轨道反馈

        private void HandleTrackChanged(CharacterTrack track) => RefreshTrack(track);

        /// <summary>切轨即时反馈：顶部色条 + 徽章文案（大小写/颜色双重编码）。</summary>
        public void RefreshTrack(CharacterTrack track)
        {
            bool agent = track == CharacterTrack.Agent;
            Color color = agent ? AgentColor : DirectorColor;

            if (_trackBar != null) _trackBar.color = color;
            if (_trackBadge != null)
            {
                _trackBadge.color = color;
                _trackBadge.text = agent
                    ? "AGENT TRACK   —   lowercase words / red targets"
                    : "DIRECTOR TRACK   —   UPPERCASE words / viewfinder on";
            }
        }

        // ---------------------------------------------------------------- 结算屏

        public void ShowResults(RunStats stats)
        {
            if (_resultsRoot == null) return;

            string body =
                $"<size=110><b>{resultsTitle}</b></size>\n" +
                $"<size=34><color=#d8d8d8>{resultsSubtitle}</color></size>\n\n" +
                $"<size=38>ACCURACY          {stats.AccuracyPercent}%</size>\n" +
                $"<size=38>WORDS TYPED       {stats.WordsTyped}</size>\n" +
                $"<size=38>TYPING SPEED      {stats.Wpm} WPM</size>\n" +
                $"<size=38>ENEMIES KILLED    {stats.Kills}</size>\n" +
                $"<size=38>MISSTYPES         {stats.Mistypes}</size>\n\n" +
                $"<size=40><color=#ffe066>{resultsFooter}</color></size>\n" +
                $"<size=28><color=#9a9a9a>{retryHint}</color></size>";

            if (_resultsBody != null) _resultsBody.text = body;
            _resultsRoot.SetActive(true);
        }

        // ---------------------------------------------------------------- 构建

        private void BuildUi()
        {
            var canvasGo = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // 绘制顺序 = 子物体顺序（后建的在上）
            BuildTrackFeedback(canvasGo.transform);
            BuildPrompt(canvasGo.transform);
            BuildSubtitle(canvasGo.transform);
            BuildBanner(canvasGo.transform);
            BuildBlackout(canvasGo.transform);
            BuildResults(canvasGo.transform);
        }

        private void BuildTrackFeedback(Transform parent)
        {
            _trackBar = CreateImage(parent, "TrackBar", new Color(1f, 0.25f, 0.25f, 0.9f));
            var barRect = (RectTransform)_trackBar.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(0f, -8f);
            barRect.offsetMax = new Vector2(0f, 0f);

            _trackBadge = CreateText(parent, "TrackBadge", 30, TextAnchor.UpperCenter, new Color(1f, 0.25f, 0.25f));
            var badgeRect = (RectTransform)_trackBadge.transform;
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 1f);
            badgeRect.offsetMin = new Vector2(0f, -52f);
            badgeRect.offsetMax = new Vector2(0f, -16f);
        }

        private void BuildPrompt(Transform parent)
        {
            var root = new GameObject("Prompt", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1400f, 220f);
            rect.anchoredPosition = new Vector2(0f, 90f);

            _promptText = CreateText(root.transform, "PromptText", 64, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)_promptText.transform, 0f, 0f, 0f, 100f);

            // 当前待输入字母上方的箭头（Screen Space 画布，不需要“永远最上层”材质）
            _promptArrow = TextCaretArrow.CreateArrow(_promptText.transform, null, 34f, 22f, ArrowColor);

            _promptHint = CreateText(root.transform, "PromptHint", 30, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
            Stretch((RectTransform)_promptHint.transform, 0f, 0f, 140f, 0f);

            _promptRoot = root;
            _promptRoot.SetActive(false);
        }

        private void BuildSubtitle(Transform parent)
        {
            var root = new GameObject("Subtitle", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(1500f, 170f);
            rect.anchoredPosition = new Vector2(0f, 90f);

            var box = CreateImage(root.transform, "SubtitleBox", new Color(0f, 0f, 0f, 0.55f));
            Stretch((RectTransform)box.transform, 0f, 0f, 0f, 0f);

            _subtitleSpeaker = CreateText(root.transform, "Speaker", 34, TextAnchor.UpperLeft, AgentColor);
            Stretch((RectTransform)_subtitleSpeaker.transform, 24f, 24f, 0f, 62f);

            _subtitleText = CreateText(root.transform, "Line", 42, TextAnchor.MiddleLeft, Color.white);
            Stretch((RectTransform)_subtitleText.transform, 24f, 24f, 58f, 0f);

            _subtitleRoot = root;
            _subtitleRoot.SetActive(false);
        }

        private void BuildBanner(Transform parent)
        {
            _banner = CreateText(parent, "Banner", 120, TextAnchor.MiddleCenter, Color.white);
            var rect = (RectTransform)_banner.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1400f, 200f);
            rect.anchoredPosition = new Vector2(0f, -40f);

            var outline = _banner.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.2f, 0.25f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);

            _banner.gameObject.SetActive(false);
        }

        private void BuildBlackout(Transform parent)
        {
            _blackout = CreateImage(parent, "Blackout", Color.black);
            Stretch((RectTransform)_blackout.transform, 0f, 0f, 0f, 0f);
        }

        private void BuildResults(Transform parent)
        {
            var root = new GameObject("Results", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform, 0f, 0f, 0f, 0f);

            var bg = CreateImage(root.transform, "ResultsBg", new Color(0f, 0f, 0f, 0.92f));
            Stretch((RectTransform)bg.transform, 0f, 0f, 0f, 0f);

            _resultsBody = CreateText(root.transform, "ResultsText", 40, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)_resultsBody.transform, 80f, 80f, 60f, 60f);

            _resultsRoot = root;
            _resultsRoot.SetActive(false);
        }

        // ---------------------------------------------------------------- UI 小工具

        private Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            if (_font != null) text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static string RichEscape(string raw)
            => string.IsNullOrEmpty(raw) ? string.Empty : raw.Replace("<", "&lt;");
    }
}
