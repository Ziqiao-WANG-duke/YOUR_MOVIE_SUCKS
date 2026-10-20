using UnityEngine;
using UnityEngine.UI;
using YourMovieSucks.Combat;
using YourMovieSucks.LabB;
using YourMovieSucks.Typing;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 怪物头顶词条（World Space Canvas + Text）：红小写 / 蓝大写 / 绿进度。
    /// 显示保留原文空格与标点（my pole 就画 my pole），只有字母参与进度。
    /// 使用 alwaysOnTopMaterial（ZTest Always + Overlay 队列）时永远显示在最上层，不被钢管等几何挡住。
    ///
    /// 威胁配色（白 → 黄 → 红）：字体颜色表示这只怪有多危险，按“怪物到相机的距离”连续渐变：
    ///   白 = 还在远处、没有危险 / 黄 = 正在逐步靠近 / 红 = 已经在攻击环上（准备攻击）；
    ///   怪物进入攻击态时锁成红色并正弦闪烁（越快靠近 → 越红 → 越显眼）。
    /// 颜色用指数平滑跟随目标值，所以阶段切换不会“跳色”。
    /// </summary>
    public class WordLabel : MonoBehaviour
    {
        // 未打字母用白色（作者 2026-10-07：暗背景下红/蓝偏暗看不清）；已完成=绿。
        // 注：旧版把「当前待输入字母」染成黄色，进度 0 时首字母就是黄的，玩家会以为已经打过了，
        // 所以那一机制已删——现在由箭头（TextCaretArrow）指出当前待输入的位置。
        private const string DoneHex = "2bff6a";
        private const string AgentTodoHex = "ffffff";
        private static readonly Color ArrowColor = new Color(1f, 0.878f, 0.4f, 1f); // #ffe066

        /// <summary>词条字体：导入到 Assets/Resources/Fonts/ 的打字机字体（作者安装的 Special Elite）；没导入就退回内置 Arial。</summary>
        private const string LabelFontResourcePath = "Fonts/SpecialElite";

        /// <summary>威胁色阶的量化级数（每通道）：既保证过渡看不出来，又不会每帧重建文字网格。</summary>
        private const int TintSteps = 32;

        private TypingWord _word;
        private Transform _follow;
        private Text _text;
        private Vector3 _offset = new Vector3(0f, DefaultLabelHeight, 0f);
        private Vector3 _fixedWorldPos;
        private int _lastProgress = -1;
        private Vector3 _baseScale = new Vector3(0.005f, 0.005f, 0.005f);
        private Image _arrow;

        [Header("字体")]
        [Tooltip("字号（Special Elite 与 Arial 的字宽/字高不同，换字体后可能需要重调）")]
        [SerializeField] private int fontSize = 44;

        [Header("威胁配色（白 → 黄 → 红）")]
        [Tooltip("远处（毫无危险）的字体颜色")]
        [SerializeField] private Color farTintColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("正在逐步靠近时的字体颜色（黄色）")]
        [SerializeField] private Color approachTintColor = new Color(1f, 0.82f, 0.16f, 1f);

        [Tooltip("已经在攻击环上、准备攻击的字体颜色（红色）")]
        [SerializeField] private Color dangerTintColor = new Color(1f, 0.16f, 0.12f, 1f);

        [Tooltip("怪物到这个三维距离（米）以外 = 纯白。实测刷怪点离 WP2 相机约 8.2–8.6 m、攻击环约 2.4 m")]
        [SerializeField] private float farDistance = 9f;

        [Tooltip("怪物进到这个三维距离（米）= 纯红，之后进入攻击态就只闪不变色")]
        [SerializeField] private float dangerDistance = 3f;

        [Tooltip("黄色出现在危险度的哪个位置（0=刚离开白、1=已到红）")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float yellowPoint = 0.6f;

        [Tooltip("颜色过渡的指数平滑时间常数（秒）：越大越柔、越慢")]
        [SerializeField] private float tintSmoothTime = 0.35f;

        [Header("攻击中闪烁")]
        [Tooltip("闪烁频率（次/秒）")]
        [SerializeField] private float flashHz = 3.2f;

        [Tooltip("闪烁最暗时的亮度比例（1 = 不闪）")]
        [Range(0f, 1f)]
        [SerializeField] private float flashMinBrightness = 0.35f;

        /// <summary>当前实际使用的威胁色（未打字母的颜色，含闪烁）。供自动验收读取。</summary>
        public Color ThreatTint { get; private set; } = Color.white;

        /// <summary>当前写进富文本的量化颜色（十六进制）。</summary>
        public string ThreatTintHex => _tintHex;

        private static Font _cachedFont;
        private static bool _fontResolved;

        /// <summary>这只词条所属的怪物（能拿到才有威胁配色；输入门/无目标词条没有）。</summary>
        private TypingTarget _threatTarget;

        /// <summary>平滑后的危险度 0（远）..1（已经在攻击环上）。</summary>
        private float _threat;

        /// <summary>当前实际写进富文本的未打字母颜色（量化过的十六进制）。</summary>
        private string _tintHex = AgentTodoHex;

        /// <summary>找不到 Renderer 时的兵底高度（旧行为）。</summary>
        private const float DefaultLabelHeight = 1.7f;

        [Header("近身限尺")]
        [Tooltip("词条是 World Space 画布，怪走近时会跟着放大。到这个**三维距离**时把表观大小压到 closeScaleFactor；\n" +
                 "比它更近按比例继续缩小（表观大小封顶），更远则完全不受影响。\n" +
                 "2.0 ≈ 怪贴到攻击环时词条离相机的三维距离（当前 attackRange=1.7 + 2.2 m 体高 + 相机高 2.1）")]
        [SerializeField] private float closeReferenceDistance = 2f;

        [Tooltip("最近距离处的表观缩放（0.5 = 当前尺寸的一半）")]
        [SerializeField] private float closeScaleFactor = 0.5f;

        [Header("屏幕安全区（视口比例）")]
        [Tooltip("词条允许出现的屏幕范围（按**文字实际尺寸**夹，不是画布尺寸）：\n" +
                 "上边留出顶部 HUD——左上血条块是 IMGUI 固定像素，视口占位随窗口大小变\n" +
                 "（1024×768 时下沿到 0.844，1544×837 时到 0.857），所以取 0.82")]
        [SerializeField] private Vector2 safeMin = new Vector2(0.10f, 0.10f);
        [SerializeField] private Vector2 safeMax = new Vector2(0.90f, 0.82f);

        public void Init(TypingWord word, Material alwaysOnTopMaterial = null)
        {
            _word = word;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            var rt = gameObject.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 100f);
            transform.localScale = _baseScale;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(transform, false);
            _text = textGo.GetComponent<Text>();

            var font = ResolveLabelFont();
            if (font != null) _text.font = font;

            // ZTest Always + Overlay 队列：词条永远画在场景几何（钢管）与血浆层之上
            if (alwaysOnTopMaterial != null) _text.material = alwaysOnTopMaterial;

            // 细投影（不是四向描边）：压在亮黄铜/氖灯上时也有暗边衬托；
            // 四向粗描边会把细笔画糊成“黑字”（上一版就是这个原因）
            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;

            _text.fontSize = fontSize;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;

            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // 当前待输入字母上方的向下箭头（挂在 Text 下，共用字符坐标）
            _arrow = TextCaretArrow.CreateArrow(textGo.transform, alwaysOnTopMaterial, 30f, 20f, ArrowColor);

            if (word.Target != null)
            {
                _follow = word.Target.transform;
                // 弱点词条：锚点本身就是“弱点上方一点”，用给定的固定偏移；
                // 其它词条仍按目标包围盒自动算（方块/骨骼模型都能用）
                _offset = word.LabelOffsetY >= 0f
                    ? new Vector3(0f, word.LabelOffsetY, 0f)
                    : new Vector3(0f, ResolveLabelHeight(_follow), 0f);
            }
            else
            {
                var cam = Camera.main;
                _fixedWorldPos = cam != null
                    ? cam.transform.position + cam.transform.forward * 6f + Vector3.up * 0.5f
                    : new Vector3(0f, 2f, 6f);
            }

            Refresh();
            _lastProgress = _word.Progress;

            // 弱点词条的目标是骨骼上的锚点，所以向上找；找不到就没有威胁配色（例如输入门）
            _threatTarget = word.Target != null ? word.Target.GetComponentInParent<TypingTarget>() : null;
            _threat = _threatTarget != null ? ResolveDistanceThreat() : 0f;   // 出生瞬间直接用真实值，不从白色起步
        }

        private void LateUpdate()
        {
            if (_word == null) return;

            // 箭头指向当前待输入字母（文本布局完才能量到宽度，所以放在 LateUpdate）
            TextCaretArrow.Place(_arrow, _text, _word.CasedDisplayText, _word.CurrentDisplayIndex, 30f, 20f, 6f);

            Vector3 world = _follow != null ? _follow.position + _offset : _fixedWorldPos;
            var cam = Camera.main;

            if (cam != null)
            {
                ApplyCloseRangeSizeCap(world, cam);
                world = ClampToSafeArea(world, cam);
                transform.rotation = cam.transform.rotation;
            }

            transform.position = world;

            if (_word.Progress != _lastProgress)
            {
                _lastProgress = _word.Progress;
                Refresh();
            }

            UpdateThreatTint(Time.deltaTime);
        }

        /// <summary>
        /// 威胁配色：把“离相机多远 + 是否在攻击”化成一条连续色阶，用指数平滑跟随，避免跳色。
        /// 只有量化后的颜色真的变了才重建文字（否则每帧重建富文本会很浪费）。
        /// </summary>
        private void UpdateThreatTint(float deltaTime)
        {
            if (_threatTarget == null || _text == null) return;

            float target = ResolveDistanceThreat();
            if (_threatTarget.IsAttacking) target = 1f;   // 攻击态一律最危险（红色），具体由闪烁表达

            // 指数平滑：与帧率无关，阶段切换看起来是渐变而不是跳变
            float k = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.01f, tintSmoothTime));
            _threat = Mathf.Lerp(_threat, target, k);

            float yellow = Mathf.Clamp(yellowPoint, 0.0001f, 0.9999f);
            Color tint = _threat < yellow
                ? Color.Lerp(farTintColor, approachTintColor, _threat / yellow)
                : Color.Lerp(approachTintColor, dangerTintColor, (_threat - yellow) / (1f - yellow));

            if (_threatTarget.IsAttacking)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * flashHz);
                float brightness = Mathf.Lerp(flashMinBrightness, 1f, pulse);
                tint = new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, tint.a);
            }

            string hex = ToQuantizedHex(tint);
            ThreatTint = tint;
            if (hex == _tintHex) return;

            _tintHex = hex;
            Refresh();
        }

        /// <summary>危险度：远处 0、攻击环处 1（用怪物到相机的三维距离，怪走近是一条连续曲线）。</summary>
        private float ResolveDistanceThreat()
        {
            if (_threatTarget == null) return 0f;

            var cam = Camera.main;
            if (cam == null) return 0f;

            float distance = Vector3.Distance(_threatTarget.transform.position, cam.transform.position);
            return Mathf.Clamp01(Mathf.InverseLerp(farDistance, dangerDistance, distance));
        }

        /// <summary>量化成十六进制（TintSteps 级）：颜色变化看不出来，但重建次数掉到十几分之一。</summary>
        private static string ToQuantizedHex(Color color)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * (TintSteps - 1)) * 255 / (TintSteps - 1);
            int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * (TintSteps - 1)) * 255 / (TintSteps - 1);
            int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * (TintSteps - 1)) * 255 / (TintSteps - 1);

            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        /// <summary>词条字体：优先用导入的 Special Elite，没导入就退回内置 Arial（并只警告一次）。</summary>
        private static Font ResolveLabelFont()
        {
            if (!_fontResolved || _cachedFont == null)
            {
                _fontResolved = true;
                _cachedFont = Resources.Load<Font>(LabelFontResourcePath);

                if (_cachedFont == null)
                    Debug.LogWarning($"[WordLabel] 没找到字体 Resources/{LabelFontResourcePath}（.ttf 是否已导入？），词条回退内置 Arial");
            }

            return _cachedFont != null ? _cachedFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// 近身限尺：世界空间词条的表观大小 ∝ 1/距离，怪贴脸时会大到认不出。
        /// 这里把“最大表观尺寸”封顶：距离 ≥ closeReferenceDistance 完全不变（远处依旧好读），
        /// 比它近就按比例缩小，最近处的表观大小 = 当前的 closeScaleFactor 倍。
        /// </summary>
        private void ApplyCloseRangeSizeCap(Vector3 labelWorld, Camera cam)
        {
            var tuning = LabBTuning.I;
            bool tuned = tuning != null && tuning.Enabled;

            float reference = Mathf.Max(tuned ? tuning.labelCloseReferenceDistance : closeReferenceDistance, 0.01f);
            float cap = tuned ? tuning.labelCloseScaleFactor : closeScaleFactor;

            float distance = Vector3.Distance(labelWorld, cam.transform.position);
            float factor = Mathf.Min(1f, cap * distance / reference);
            transform.localScale = _baseScale * factor;
        }

        /// <summary>
        /// 屏幕安全区：把词条的屏幕位置夹回 safeMin..safeMax（夹的是整个词条包围框，不是中心点）。
        /// 不夹的话，左侧刷出的怪贴脸时词条会伸到左上角，被血条/顶部 HUD 盖住（UGUI/IMGUI 总在世界空间之上）。
        /// </summary>
        private Vector3 ClampToSafeArea(Vector3 labelWorld, Camera cam)
        {
            Vector3 viewport = cam.WorldToViewportPoint(labelWorld);
            if (viewport.z <= 0.01f) return labelWorld; // 在相机后面：不动

            Vector2 size = ResolveTextSize();
            Vector3 scale = transform.localScale;
            float halfHeight = size.y * scale.y * 0.5f;
            float halfWidth = size.x * scale.x * 0.5f;

            float fullHeight = 2f * viewport.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfHeightVp = halfHeight / fullHeight;
            float halfWidthVp = halfWidth / (fullHeight * Mathf.Max(cam.aspect, 0.01f));

            // 词条比安全区还大时退回正中，避免 Clamp 的上下限反了
            var tuning = LabBTuning.I;
            float safeTop = tuning != null && tuning.Enabled ? tuning.labelSafeTop : safeMax.y;

            float minX = Mathf.Min(safeMin.x + halfWidthVp, 0.5f);
            float maxX = Mathf.Max(safeMax.x - halfWidthVp, 0.5f);
            float minY = Mathf.Min(safeMin.y + halfHeightVp, 0.5f);
            float maxY = Mathf.Max(safeTop - halfHeightVp, 0.5f);

            float x = Mathf.Clamp(viewport.x, minX, maxX);
            float y = Mathf.Clamp(viewport.y, minY, maxY);
            if (Mathf.Approximately(x, viewport.x) && Mathf.Approximately(y, viewport.y)) return labelWorld;

            return cam.ViewportToWorldPoint(new Vector3(x, y, viewport.z));
        }

        /// <summary>
        /// 要夹的尺寸 = 文字实际占的像素（画布 400×100 里上下各有近 50px 空白，
        /// 按画布夹会把词条多压下去半条画布的高度）。
        /// </summary>
        private Vector2 ResolveTextSize()
        {
            if (_text != null)
            {
                float width = Mathf.Max(_text.preferredWidth, 1f);
                float height = Mathf.Max(_text.preferredHeight, 1f);
                return new Vector2(width, height);
            }

            return ((RectTransform)transform).sizeDelta;
        }

        private void Refresh()
        {
            if (_text == null || _word == null) return;

            // 未打字母全部同色（不再把“当前”单独染色），位置信息交给箭头；
            // 威胁配色接管未打字母：白（远）→ 黄（靠近）→ 红（要打你了），攻击时闪烁
            string todoHex = _tintHex;
            _text.text = _word.BuildDisplayRichText(DoneHex, todoHex, todoHex);
        }

        /// <summary>
        /// 词条挂在目标头顶（模型可见最高点 + 0.35）。骨骼网格有两个坑：
        /// ① `SkinnedMeshRenderer.bounds` 是「绑定盒 × 根骨骼」，会随姿势起伏（实测同一只怪在 2.12–2.70 m 之间漂）；
        /// ② `localToWorldMatrix` 对骨骼网格是无效的（顶点由骨骼矩阵变换），不能用它把 mesh.bounds 转出来。
        /// 所以取「实时包围盒」与「根骨骼位置 + 绑定盒上界×缩放」两者最大值：前者在绑定姿势下准，
        /// 后者不随姿势变，两个一起用就稳且准。没骨骼的灰盒方块走 Renderer.bounds（旧行为）。
        /// </summary>
        private static float ResolveLabelHeight(Transform target)
        {
            float top = float.NegativeInfinity;

            foreach (var skinned in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinned == null) continue;

                top = Mathf.Max(top, skinned.bounds.max.y);

                // 绑定姿势的盒子尺寸不随姿势变（localBounds.size.y = 模型身高），按根骨骼的缩放换算成世界身高。
                // 用尺寸而不是“根骨骼位置 + 上界”：根骨骼高度会跟着动画起落（实测差 0.3–0.5 m），
                // 会让词条一会儿压头一会儿飘高。
                var root = skinned.rootBone != null ? skinned.rootBone : skinned.transform;
                top = Mathf.Max(top, target.position.y + skinned.localBounds.size.y * Mathf.Abs(root.lossyScale.y));
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || renderer is SkinnedMeshRenderer) continue;
                top = Mathf.Max(top, renderer.bounds.max.y);
            }

            if (float.IsNegativeInfinity(top)) return DefaultLabelHeight;
            return Mathf.Max(DefaultLabelHeight, top - target.position.y + 0.35f);
        }
    }
}
