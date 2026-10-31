using UnityEngine;
using UnityEngine.UI;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 标题画面 + 一个菜单（Lab C 交付要求：Title screen / HUD / one menu）。
    ///
    /// 职责：挡住开场演出，等玩家按 ENTER 才开始；同一屏提供 静音(M) 与 VFX(V) 开关，
    /// ESC 退出。真正的开场演出仍由 PolePositionFlow 负责，这里只做「开始门」。
    /// 样式沿用工程既有做法：运行时建 UGUI Canvas + Special Elite 字体，纸纹/打字机气质。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        [Header("文案")]
        [SerializeField] private string title = "YOUR MOVIE SUCKS!";
        [SerializeField] private string subtitle = "THE NEON BUTCHERY  ·  A GRINDHOUSE TYPE-OFF";
        [Tooltip("操作提示（{0}=静音状态，{1}=VFX 状态）")]
        [SerializeField] private string hints = "[ENTER]  START      [M]  MUTE: {0}      [V]  VFX: {1}      [ESC]  QUIT";
        [SerializeField] private string footer = "TYPE THE WORDS · CAPS LOCK SWITCHES TRACK · AGENT = lowercase / DIRECTOR = UPPERCASE";

        [Header("层级")]
        [SerializeField] private int sortingOrder = 5000;

        /// <summary>是否有标题画面挡住开场（场景里没挂 TitleScreen 时为 false，流程直接开始）。</summary>
        public static bool GateShown { get; private set; }
        /// <summary>玩家是否已经按过开始。</summary>
        public static bool Started { get; private set; }
        /// <summary>PolePositionFlow 用这个门等开始。</summary>
        public static bool GateOpen => !GateShown || Started;

        private Canvas _canvas;
        private Text _hintsText;

        private void Awake()
        {
            GateShown = true;
            Started = false;
            GameOptions.Load();
            GameOptions.Changed += RefreshHints;
            Build();

            // 自动化用（截图/录屏/自检）：-autostart 跳过标题直接开始，不影响正常游玩
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autostart") StartCoroutine(AutoStart());
                else if (args[i] == "-shot" && i + 1 < args.Length) StartCoroutine(CaptureAndQuit(args[i + 1]));
            }
        }

        private System.Collections.IEnumerator AutoStart()
        {
            yield return null;
            BeginGame();
            Debug.Log("[TitleScreen] -autostart：已跳过标题画面");
        }

        /// <summary>自动化用：等两帧让 UI 布局完成后用引擎自己截图（比外部抓窗可靠），然后退出。</summary>
        private System.Collections.IEnumerator CaptureAndQuit(string path)
        {
            yield return null;
            yield return null;
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[TitleScreen] -shot -> " + path);
            yield return new WaitForSeconds(1.5f);
            Application.Quit();
        }

        private void OnDestroy()
        {
            GameOptions.Changed -= RefreshHints;
        }

        private void Update()
        {
            if (Started) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame)
            {
                BeginGame();
            }
            else if (keyboard.mKey.wasPressedThisFrame) GameOptions.ToggleMute();
            else if (keyboard.vKey.wasPressedThisFrame) GameOptions.ToggleVfx();
            else if (keyboard.escapeKey.wasPressedThisFrame) Application.Quit();
        }

        /// <summary>开始游戏：收起标题，放开 PolePositionFlow 的门。</summary>
        public void BeginGame()
        {
            if (Started) return;
            Started = true;
            GameOptions.Changed -= RefreshHints;
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
        }

        // ---------------------------------------------------------------- 构建

        private void Build()
        {
            if (_canvas != null) return;

            var go = new GameObject("TitleScreenCanvas");
            go.transform.SetParent(null, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            // 用像素坐标（和 HealthUI 一致）：标题文本用 5%–95% 锚点 + BestFit，任何窗口尺寸都不会被裁
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            go.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)go.transform;

            // 全黑底：标题期间不露出后面的场景
            var bg = CreateImage(root, "Backdrop", new Color(0.02f, 0.02f, 0.03f, 1f));
            Stretch(bg.rectTransform);

            Font font = Resources.Load<Font>("Fonts/SpecialElite");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var titleText = CreateText(root, "Title", 96, TextAnchor.MiddleCenter, new Color(0.92f, 0.16f, 0.16f));
            titleText.font = font;
            titleText.text = title;
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 24;
            titleText.resizeTextMaxSize = 96;
            PlaceStretch(titleText.rectTransform, 0.18f, 0.16f);

            var subText = CreateText(root, "Subtitle", 30, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f));
            subText.font = font;
            subText.text = subtitle;
            subText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PlaceStretch(subText.rectTransform, 0.06f, 0.07f);

            _hintsText = CreateText(root, "Hints", 30, TextAnchor.MiddleCenter, new Color(0.98f, 0.878f, 0.4f));
            _hintsText.font = font;
            _hintsText.resizeTextForBestFit = true;
            _hintsText.resizeTextMinSize = 14;
            _hintsText.resizeTextMaxSize = 30;
            PlaceStretch(_hintsText.rectTransform, -0.12f, 0.08f);

            var footerText = CreateText(root, "Footer", 22, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.6f));
            footerText.font = font;
            footerText.text = footer;
            footerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PlaceStretch(footerText.rectTransform, -0.32f, 0.09f);

            RefreshHints();
        }

        private void RefreshHints()
        {
            if (_hintsText == null) return;
            _hintsText.text = string.Format(hints,
                GameOptions.Muted ? "OFF" : "ON",
                GameOptions.VfxEnabled ? "ON" : "OFF");
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>横向 5%–95% 铺开、纵向按屏幕高度百分比定位（offsetRatio/heightRatio 都是 0–1）。</summary>
        private static void PlaceStretch(RectTransform rt, float offsetRatio, float heightRatio)
        {
            rt.anchorMin = new Vector2(0.05f, 0.5f + offsetRatio - heightRatio * 0.5f);
            rt.anchorMax = new Vector2(0.95f, 0.5f + offsetRatio + heightRatio * 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
