using UnityEngine;
using UnityEngine.UI;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 把一个小三角箭头（↓）摆到 <see cref="Text"/> 里某个字符的正上方。
    ///
    /// 为什么要它：词条原来把「当前待输入字母」染成黄色，进度为 0 时首字母就是黄的，
    /// 玩家会误以为这个字母已经打过了，于是从第二个字母开始输。改成：
    /// 未打的字母一律白色（打完变绿），当前待输入的位置由这个箭头指出。
    ///
    /// 字符位置取 <see cref="Text"/> 的 cachedTextGenerator.characters（富文本标签不算字符，
    /// 所以传进来的下标要按**去掉标签后的可见文本**算）。生成器坐标系：原点在文本矩形左上角，
    /// x 向右、y 向下，所以摆成 anchoredPosition 时 y 要取负。
    /// </summary>
    public static class TextCaretArrow
    {
        /// <summary>调试用：打印每次摆放的坐标（仅状态变化时打印一次）。默认关。</summary>
        public static bool DebugCaret;
        private static string _lastDebugKey;

        private static Sprite _downTriangle;

        /// <summary>向下的小三角（运行时生成，不依赖字体，避免缺字形变成方块）。</summary>
        public static Sprite DownTriangle
        {
            get
            {
                if (_downTriangle != null) return _downTriangle;

                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "CaretDownTriangle",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    // y = 0 是图像底部：顶部整行宽、向下收成一点 = 向下箭头
                    float t = y / (float)(size - 1);
                    float halfWidth = t * (size * 0.5f);
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Mathf.Abs(x - (size - 1) * 0.5f);
                        float alpha = Mathf.Clamp01(halfWidth - distance + 0.5f); // 1px 羽化边
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _downTriangle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                _downTriangle.name = "CaretDownTriangle";
                return _downTriangle;
            }
        }

        /// <summary>造一个箭头 Image（挂在要对照的 Text 的 RectTransform 下，才能共用字符坐标）。</summary>
        public static Image CreateArrow(Transform parent, Material material, float width, float height, Color color)
        {
            var go = new GameObject("CaretArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = DownTriangle;
            image.color = color;
            image.raycastTarget = false;
            if (material != null) image.material = material;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            return image;
        }

        /// <summary>
        /// 把箭头摆到第 characterIndex 个字符正上方。
        /// 位置用**量宽度**算（TextGenerator.GetPreferredWidth），不用 characters 的逐字符坐标：
        /// 实测 cachedTextGenerator.characters 这里拿不到有效数据（每个字符 cursorPos 都一样、Count 还等于带标签的原始串长），
        /// 而 preferredWidth / GetPreferredWidth 是准的。
        /// plainText 传“真实显示出来的纯文本”（无富文本标签），宽度要按实际渲染串量。
        /// </summary>
        public static bool Place(Image arrow, Text text, string plainText, int characterIndex, float width, float height, float gap)
        {
            if (arrow == null || text == null || string.IsNullOrEmpty(plainText)) return false;
            if (characterIndex < 0 || characterIndex >= plainText.Length)
            {
                arrow.enabled = false;
                return false;
            }

            ResolveAlignmentOffset(text, out float offsetX, out float offsetY);
            ResolvePrefixWidth(text, plainText, characterIndex, out float prefixWidth, out float advance);

            float x = offsetX + prefixWidth + advance * 0.5f;
            float y = -offsetY + gap + height * 0.5f;   // 单行：字形顶 ≈ 文字块顶；y 向上，所以往上让 gap

            if (DebugCaret)
            {
                string key = text.GetInstanceID() + ":" + characterIndex;
                if (key != _lastDebugKey)
                {
                    _lastDebugKey = key;
                    Debug.Log($"[CaretArrow] index={characterIndex}/{plainText.Length} plain='{plainText}' " +
                              $"prefixW={prefixWidth:0.0} advance={advance:0.0} offsetX={offsetX:0.0} offsetY={offsetY:0.0} " +
                              $"rect={text.rectTransform.rect.width:0.0}x{text.rectTransform.rect.height:0.0} " +
                              $"pref={text.preferredWidth:0.0}x{text.preferredHeight:0.0} align={text.alignment} result=({x:0.0},{y:0.0})");
                }
            }

            var rect = (RectTransform)arrow.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);

            arrow.enabled = true;
            return true;
        }

        /// <summary>量出「当前字符左边的宽度」与该字符的宽度（按实际渲染设置量，含富文本大小）。</summary>
        private static void ResolvePrefixWidth(Text text, string plainText, int characterIndex, out float prefixWidth, out float advance)
        {
            var generator = text.cachedTextGeneratorForLayout;
            TextGenerationSettings settings = text.GetGenerationSettings(
                new Vector2(100000f, Mathf.Max(text.rectTransform.rect.height, 1f)));

            float perUnit = Mathf.Max(text.pixelsPerUnit, 0.0001f);
            prefixWidth = generator.GetPreferredWidth(plainText.Substring(0, characterIndex), settings) / perUnit;
            float withChar = generator.GetPreferredWidth(plainText.Substring(0, characterIndex + 1), settings) / perUnit;
            advance = Mathf.Max(withChar - prefixWidth, text.fontSize * 0.25f);
        }

        /// <summary>把文字块在文本框里的对齐偏移算出来（生成器坐标不含这一部分）。</summary>
        private static void ResolveAlignmentOffset(Text text, out float offsetX, out float offsetY)
        {
            offsetX = 0f;
            offsetY = 0f;

            Rect rect = text.rectTransform.rect;
            float contentWidth = text.preferredWidth;
            float contentHeight = text.preferredHeight;

            switch (text.alignment)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    offsetX = Mathf.Max(0f, (rect.width - contentWidth) * 0.5f);
                    break;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    offsetX = Mathf.Max(0f, rect.width - contentWidth);
                    break;
            }

            switch (text.alignment)
            {
                case TextAnchor.MiddleLeft:
                case TextAnchor.MiddleCenter:
                case TextAnchor.MiddleRight:
                    offsetY = Mathf.Max(0f, (rect.height - contentHeight) * 0.5f);
                    break;
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerRight:
                    offsetY = Mathf.Max(0f, rect.height - contentHeight);
                    break;
            }
        }
    }
}
