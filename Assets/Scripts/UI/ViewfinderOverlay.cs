using UnityEngine;
using UnityEngine.UI;
using YourMovieSucks.Input;
using YourMovieSucks.LabB;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 导演 16mm 取景框：全屏叠加滤镜（暗角 + 安全框 + 四角括号 + 准星 + 标识），非独立窗口（设计文档 4.5）。
    /// 切轨即时反馈：特工轨隐去、导演轨显示（AGENTS 第 10 条要求切换后 HUD/取景框立刻变化）。
    /// 同时承接 Lab B 的 Variant：LabBTuning.variantMode 0 = 取景框开、1 = 关。
    /// </summary>
    public class ViewfinderOverlay : MonoBehaviour
    {
        [SerializeField] private TrackInput trackInput;

        [Tooltip("显隐淡入淡出速度（alpha/秒），保证切轨反馈够快够明显")]
        [SerializeField] private float fadeSpeed = 9f;

        [SerializeField] private int vignetteSize = 256;

        private CanvasGroup _group;
        private bool _visible = true;
        private static readonly Color FrameColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color MarkColor = new Color(1f, 1f, 1f, 0.7f);

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            if (trackInput != null) trackInput.OnTrackChanged += HandleTrackChanged;
            if (LabBTuning.I != null) LabBTuning.I.OnVariantChanged += HandleVariantChanged;
            Apply();
            if (_group != null) _group.alpha = _visible ? 1f : 0f;
        }

        private void OnDisable()
        {
            if (trackInput != null) trackInput.OnTrackChanged -= HandleTrackChanged;
            if (LabBTuning.I != null) LabBTuning.I.OnVariantChanged -= HandleVariantChanged;
        }

        private void Update()
        {
            if (_group == null) return;
            float target = _visible ? 1f : 0f;
            if (!Mathf.Approximately(_group.alpha, target))
                _group.alpha = Mathf.MoveTowards(_group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        }

        private void HandleTrackChanged(CharacterTrack track) => Apply();

        private void HandleVariantChanged(int mode, string name) => Apply();

        private void Apply()
        {
            bool director = trackInput == null || trackInput.CurrentTrack == CharacterTrack.Director;
            bool variantOn = LabBTuning.I == null || LabBTuning.I.variantMode == 0;
            _visible = director && variantOn;
        }

        // ---------------------------------------------------------------- 构建

        private void Build()
        {
            var canvasGo = new GameObject("ViewfinderCanvas", typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45; // 低于 DemoHud(60)，高于场景

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // 暗角
            var vignette = CreateRawImage(canvasGo.transform, "Vignette", Color.white);
            var vignetteRect = (RectTransform)vignette.transform;
            Stretch(vignetteRect, 0f, 0f, 0f, 0f);
            vignette.texture = CreateVignetteTexture(vignetteSize);

            // 安全框（四边）
            AddBar(canvasGo.transform, "FrameTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -34f), new Vector2(0f, 0f));
            AddBar(canvasGo.transform, "FrameBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 34f));
            AddBar(canvasGo.transform, "FrameLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(34f, 0f));
            AddBar(canvasGo.transform, "FrameRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-34f, 0f), new Vector2(0f, 0f));

            // 四角括号
            AddCorner(canvasGo.transform, "CornerTL", 0f, 1f, 1f, 1f);
            AddCorner(canvasGo.transform, "CornerTR", 1f, 1f, -1f, 1f);
            AddCorner(canvasGo.transform, "CornerBL", 0f, 0f, 1f, -1f);
            AddCorner(canvasGo.transform, "CornerBR", 1f, 0f, -1f, -1f);

            // 中心准星
            AddBar(canvasGo.transform, "CrossH", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -1f), new Vector2(14f, 1f));

            // 标识
            // 标识放右上角：左上角是 HP（IMGUI），撞在一起看不清
            var label = CreateText(canvasGo.transform, "Label", 26, TextAnchor.UpperRight, MarkColor);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(1f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(1f, 1f);
            labelRect.sizeDelta = new Vector2(560f, 44f);
            labelRect.anchoredPosition = new Vector2(-52f, -44f);
            label.text = "16MM  ·  DIRECTOR TRACK  ·  SAFE FRAME";
        }

        private void AddBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var image = CreateImage(parent, name, FrameColor);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void AddCorner(Transform parent, string name, float anchorX, float anchorY, float dirX, float dirY)
        {
            var anchor = new Vector2(anchorX, anchorY);

            var horizontal = CreateImage(parent, name + "_H", MarkColor);
            var hRect = (RectTransform)horizontal.transform;
            hRect.anchorMin = anchor;
            hRect.anchorMax = anchor;
            hRect.pivot = new Vector2(dirX > 0 ? 0f : 1f, 0.5f);
            hRect.sizeDelta = new Vector2(70f, 4f);
            hRect.anchoredPosition = new Vector2(dirX * 52f, dirY * 52f);

            var vertical = CreateImage(parent, name + "_V", MarkColor);
            var vRect = (RectTransform)vertical.transform;
            vRect.anchorMin = anchor;
            vRect.anchorMax = anchor;
            vRect.pivot = new Vector2(0.5f, dirY > 0 ? 0f : 1f);
            vRect.sizeDelta = new Vector2(4f, 70f);
            vRect.anchoredPosition = new Vector2(dirX * 52f, dirY * 52f);
        }

        private Texture2D CreateVignetteTexture(int size)
        {
            size = Mathf.Clamp(size, 32, 1024);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142136f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.32f) / 0.68f));
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 220f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<RawImage>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
