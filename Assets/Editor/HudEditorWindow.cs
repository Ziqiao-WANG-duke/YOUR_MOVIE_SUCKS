using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Combat;
using YourMovieSucks.UI;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// HUD 调整面板（编辑器窗口，Edit Mode 即可用，无需 Play Mode）：
    ///  · 「屏幕受伤」页：血浆抓痕的位置/抖动/尺寸/旋转/透明度/时序，并带实时预览
    ///  · 「HUD 布局」页：按真实画面比例预览各 HUD 元素，直接拖动改偏移，写入 HudLayout.asset
    /// 菜单：YourMovieSucks/UI/HUD Editor
    /// </summary>
    public sealed class HudEditorWindow : EditorWindow
    {
        private const string LayoutAssetPath = "Assets/UI/Layout/HudLayout.asset";

        private static readonly string[] LayoutIds = { "HealthUI", "DemoCanvas", "ViewfinderCanvas", "BloodOverlay" };
        private static readonly string[] LayoutTitles = { "生命值 HUD", "演出 HUD（轨道/提示/结算）", "取景框", "屏幕血效" };

        private int _tab;
        private HudLayoutAsset _layout;
        private int _selectedElement;
        private Vector2 _layoutScroll;
        [SerializeField] private float _canvasZoom = 0.5f;

        private ScreenBlood blood;
        private SerializedObject serialized;
        private Vector2 scroll;
        private float previewHeight = 260f;
        private float sampleAlpha = 1f;
        private int sampleRotation = 0;
        private bool showSplashPreview = true;
        private static readonly Color Background = new Color(0.10f, 0.10f, 0.12f, 1f);

        [MenuItem("YourMovieSucks/UI/HUD Editor")]
        public static void Open()
        {
            var window = GetWindow<HudEditorWindow>("HUD Editor");
            window.minSize = new Vector2(620f, 560f);
            window.TryAutoFind();
            window.Show();
        }

        /// <summary>直接打开到「HUD 布局」页（HudLayout 资产的 Inspector 上的按钮会调它）。</summary>
        public static void OpenLayoutTab()
        {
            var window = GetWindow<HudEditorWindow>("HUD Editor");
            window.minSize = new Vector2(620f, 560f);
            window.TryAutoFind();
            window._tab = 1;
            window.Show();
            window.Repaint();
        }

        private void OnEnable() => TryAutoFind();
        private void OnSelectionChange()
        {
            if (blood) return;
            TryAutoFind();
            Repaint();
        }

        private void TryAutoFind()
        {
            if (blood && serialized != null) return;
            blood = Resources.FindObjectsOfTypeAll<ScreenBlood>()
                .WhereAliveInScene()
                .FirstOrDefault();
            Bind();
        }

        private void Bind()
        {
            serialized = blood ? new SerializedObject(blood) : null;
        }

        private void OnGUI()
        {
            DrawToolbar();

            _tab = GUILayout.Toolbar(_tab, new[] { "屏幕受伤", "HUD 布局" });
            if (_tab == 1)
            {
                DrawLayoutTab();
                return;
            }

            if (!blood)
            {
                EditorGUILayout.HelpBox("当前打开的场景里没有 ScreenBlood（它在 Systems 物体上）。选中它，或打开 PolePosition 场景。", MessageType.Info);
                return;
            }

            if (serialized == null || serialized.targetObject != blood) Bind();
            if (serialized == null) return;
            serialized.Update();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawPreview();
            EditorGUILayout.Space(6f);
            DrawClaw();
            EditorGUILayout.Space(6f);
            DrawSplash();
            EditorGUILayout.Space(6f);
            DrawTiming();
            EditorGUILayout.EndScrollView();

            if (serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(blood);
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            bool layoutTab = _tab == 1;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (layoutTab)
                {
                    if (GUILayout.Button("打开 HudLayout 资产", EditorStyles.toolbarButton, GUILayout.Width(140f)) && _layout)
                    {
                        Selection.activeObject = _layout;
                        EditorGUIUtility.PingObject(_layout);
                    }
                    GUILayout.Label("偏移在这里改，运行时由 HudLayoutApplier 应用（无需 Play Mode）", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    return;
                }

                EditorGUI.BeginChangeCheck();
                var picked = (ScreenBlood)EditorGUILayout.ObjectField(blood, typeof(ScreenBlood), true, GUILayout.Width(240f));
                if (EditorGUI.EndChangeCheck()) { blood = picked; Bind(); }

                if (GUILayout.Button("从场景重新查找", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                {
                    blood = null; serialized = null; TryAutoFind();
                }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("预演一次受伤", EditorStyles.toolbarButton, GUILayout.Width(100f))) blood.ShowPlayerHit();
                }
                if (!Application.isPlaying)
                    GUILayout.Label("（预演需 Play Mode）", EditorStyles.miniLabel);
            }
        }

        // ---------------------------------------------------------------- 预览

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("画面预览（按当前 Game 视图比例）", EditorStyles.boldLabel);

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            float width = Mathf.Min(position.width - 28f, previewHeight * aspect);
            float height = width / Mathf.Max(aspect, 0.01f);

            var box = GUILayoutUtility.GetRect(position.width - 24f, height + 8f);
            EditorGUI.DrawRect(box, new Color(0.055f, 0.055f, 0.065f));
            var screen = new Rect(box.x + 4f, box.y + 4f, box.width - 8f, box.height - 8f);
            EditorGUI.DrawRect(screen, Background);

            // 中心十字与安全区参考线
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.12f);
            Handles.DrawLine(new Vector2(screen.center.x, screen.yMin), new Vector2(screen.center.x, screen.yMax));
            Handles.DrawLine(new Vector2(screen.xMin, screen.center.y), new Vector2(screen.xMax, screen.center.y));
            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            Handles.DrawSolidRectangleWithOutline(new Rect(screen.xMin + screen.width * 0.1f, screen.yMin + screen.height * 0.1f,
                screen.width * 0.8f, screen.height * 0.7f), new Color(0, 0, 0, 0), new Color(1f, 1f, 1f, 0.10f));
            Handles.EndGUI();

            // 飞溅（示意：左右各两个位置范围）与爪痕都裁剪在预览框内
            GUI.BeginClip(screen);
            if (showSplashPreview) DrawSplashGhosts(new Rect(0f, 0f, screen.width, screen.height));

            // 爪痕
            var clawTex = serialized.FindProperty("clawTexture").objectReferenceValue as Texture;
            var clawCenter = serialized.FindProperty("clawCenter").vector2Value;
            var clawWidth = serialized.FindProperty("clawScreenWidth").floatValue;
            var scaleRange = serialized.FindProperty("clawScaleRange").vector2Value;
            var rotRange = serialized.FindProperty("clawRotation").vector2Value;
            var alpha = serialized.FindProperty("clawAlpha").floatValue;

            float size = screen.width * clawWidth * Mathf.Lerp(scaleRange.x, scaleRange.y, 0.5f);
            var center = new Vector2(screen.width * 0.5f + clawCenter.x * screen.width,
                                     screen.height * 0.5f - clawCenter.y * screen.height);
            var drawRect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);

            float rotation = Mathf.Lerp(rotRange.x, rotRange.y, (sampleRotation + 1f) * 0.5f);
            var savedMatrix = GUI.matrix;
            if (Mathf.Abs(rotation) > 0.01f) GUIUtility.RotateAroundPivot(rotation, center);
            if (clawTex != null)
                EditorGUI.DrawPreviewTexture(drawRect, clawTex, null, ScaleMode.StretchToFill, alpha * sampleAlpha);
            else
                EditorGUI.DrawRect(drawRect, new Color(0.6f, 0f, 0f, alpha * sampleAlpha));
            GUI.matrix = savedMatrix;
            GUI.EndClip();

            // 拖动重心：在预览框内按住拖动即可改位置
            var evt = Event.current;
            if (evt.type == EventType.MouseDrag && screen.Contains(evt.mousePosition))
            {
                var normalized = new Vector2((evt.mousePosition.x - screen.center.x) / screen.width,
                                            -(evt.mousePosition.y - screen.center.y) / screen.height);
                serialized.FindProperty("clawCenter").vector2Value = new Vector2(
                    Mathf.Clamp(normalized.x, -0.5f, 0.5f), Mathf.Clamp(normalized.y, -0.5f, 0.5f));
                evt.Use();
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                sampleAlpha = EditorGUILayout.Slider(new GUIContent("预览透明度", "不影响游戏内数值，只看不透明度"), sampleAlpha, 0f, 1f);
                sampleRotation = EditorGUILayout.IntSlider(new GUIContent("预览旋转", "在旋转范围内取样"), sampleRotation, -1, 1);
                showSplashPreview = EditorGUILayout.ToggleLeft("显示飞溅示意", showSplashPreview, GUILayout.Width(110f));
            }
            EditorGUILayout.LabelField("在预览里按住拖动 = 直接改爪痕重心；下面滑杆微调", EditorStyles.miniLabel);
        }

        private void DrawSplashGhosts(Rect screen)
        {
            var sizeRange = serialized.FindProperty("splashSizeRange").vector2Value;
            float size = screen.width * Mathf.Lerp(sizeRange.x, sizeRange.y, 0.5f);
            var tint = new Color(1f, 1f, 1f, 0.07f);
            foreach (var side in new[] { -1f, 1f })
            {
                var center = new Vector2(screen.width * 0.5f + side * screen.width * 0.32f, screen.height * 0.55f);
                EditorGUI.DrawRect(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), tint);
            }
        }

        // ---------------------------------------------------------------- 参数

        private void DrawClaw()
        {
            EditorGUILayout.LabelField("爪痕（每次受伤必出）", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("clawTexture"), new GUIContent("贴图"));
                EditorGUILayout.PropertyField(serialized.FindProperty("clawCenter"), new GUIContent("重心（归一化）", "0,0 = 屏幕正中；Y 正值向上"));
                EditorGUILayout.PropertyField(serialized.FindProperty("clawJitter"), new GUIContent("位置抖动（归一化）"));
                EditorGUILayout.PropertyField(serialized.FindProperty("clawScreenWidth"), new GUIContent("宽度（屏幕比例）"));
                EditorGUILayout.PropertyField(serialized.FindProperty("clawScaleRange"), new GUIContent("大小随机范围"));
                EditorGUILayout.PropertyField(serialized.FindProperty("clawRotation"), new GUIContent("旋转范围（度）"));
                EditorGUILayout.PropertyField(serialized.FindProperty("clawAlpha"), new GUIContent("峰值透明度"));
            }
        }

        private void DrawSplash()
        {
            EditorGUILayout.LabelField("额外飞溅（随机）", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("splashTextures"), new GUIContent("贴图池"), true);
                EditorGUILayout.PropertyField(serialized.FindProperty("hitSplashMax"), new GUIContent("最多几个"));
                EditorGUILayout.PropertyField(serialized.FindProperty("hitSplashChance"), new GUIContent("每个出现概率"));
                EditorGUILayout.PropertyField(serialized.FindProperty("splashSizeRange"), new GUIContent("尺寸范围（屏幕比例）"));
                EditorGUILayout.PropertyField(serialized.FindProperty("hitSplashAlpha"), new GUIContent("峰值透明度"));
                EditorGUILayout.PropertyField(serialized.FindProperty("splatterAlpha"), new GUIContent("击杀飞溅透明度"));
            }
        }

        private void DrawTiming()
        {
            EditorGUILayout.LabelField("时序 / 层级", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("holdTime"), new GUIContent("停留（秒）"));
                EditorGUILayout.PropertyField(serialized.FindProperty("fadeDuration"), new GUIContent("渐隐（秒）"));
                EditorGUILayout.PropertyField(serialized.FindProperty("sortingOrder"), new GUIContent("画布层级"));
                EditorGUILayout.PropertyField(serialized.FindProperty("reactToPlayerDamage"), new GUIContent("绑定玩家受伤事件"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("抓痕回到画面中心"))
                    {
                        serialized.FindProperty("clawCenter").vector2Value = Vector2.zero;
                        serialized.ApplyModifiedProperties();
                    }
                    if (GUILayout.Button("抖动归零（固定位置）"))
                    {
                        serialized.FindProperty("clawJitter").vector2Value = Vector2.zero;
                        serialized.ApplyModifiedProperties();
                    }
                }
            }
        }

        // ================================================================ HUD 布局页

        private void DrawLayoutTab()
        {
            if (_layout == null)
                _layout = AssetDatabase.LoadAssetAtPath<HudLayoutAsset>(LayoutAssetPath);

            if (_layout == null)
            {
                EditorGUILayout.HelpBox($"没有找到 {LayoutAssetPath}（应随场景一起提交）。", MessageType.Warning);
                return;
            }

            _layoutScroll = EditorGUILayout.BeginScrollView(_layoutScroll, true, true);

            EditorGUILayout.LabelField("拖预览里的方块 = 移动对应 HUD 元素（相对默认位置的整体偏移）", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("画布缩放", GUILayout.Width(56f));
                _canvasZoom = GUILayout.HorizontalSlider(_canvasZoom, 0.2f, 1f, GUILayout.Width(150f));
                GUILayout.Label($"{_canvasZoom * 100f:0}%", GUILayout.Width(44f));
                if (GUILayout.Button("50%", EditorStyles.miniButton, GUILayout.Width(44f))) _canvasZoom = 0.5f;
                if (GUILayout.Button("100%", EditorStyles.miniButton, GUILayout.Width(52f))) _canvasZoom = 1f;
                GUILayout.Label("（只改预览显示倍数，不改画布逻辑尺寸）", EditorStyles.miniLabel);
            }

            DrawLayoutPreview();
            EditorGUILayout.Space(4f);

            // 元素选择
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < LayoutIds.Length; i++)
                {
                    bool on = _selectedElement == i;
                    if (GUILayout.Toggle(on, LayoutTitles[i], EditorStyles.miniButton, GUILayout.Height(24f)) != on)
                        _selectedElement = i;
                }
            }

            var element = _layout.Ensure(LayoutIds[_selectedElement]);
            float before = element.offset.x + element.offset.y + element.scale + element.alpha;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(LayoutTitles[_selectedElement], EditorStyles.boldLabel);
                element.offset = EditorGUILayout.Vector2Field("偏移（像素）", element.offset);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("X -40")) element.offset += new Vector2(-40f, 0f);
                    if (GUILayout.Button("X +40")) element.offset += new Vector2(40f, 0f);
                    if (GUILayout.Button("Y +40")) element.offset += new Vector2(0f, 40f);
                    if (GUILayout.Button("Y -40")) element.offset += new Vector2(0f, -40f);
                }
                element.scale = EditorGUILayout.Slider("缩放", element.scale, 0.2f, 2.5f);
                element.alpha = EditorGUILayout.Slider("透明度", element.alpha, 0f, 1f);
                element.visible = EditorGUILayout.Toggle("显示", element.visible);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重置这个元素")) _layout.Reset(LayoutIds[_selectedElement]);
                    if (GUILayout.Button("全部重置")) foreach (var id in LayoutIds) _layout.Reset(id);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("保存资产", GUILayout.Width(90f)))
                    {
                        EditorUtility.SetDirty(_layout);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            float after = element.offset.x + element.offset.y + element.scale + element.alpha;
            if (!Mathf.Approximately(before, after))
            {
                EditorUtility.SetDirty(_layout);
                Repaint();
            }

            EditorGUILayout.HelpBox("偏移值会在运行时由 HudLayoutApplier 应用（它会把偏移加到各 HUD 画布的子节点上）。\n" +
                                    "场景里的 Systems 已挂好 applier，不需要进 Play Mode 就能在这里调完。", MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 按 Game 视图的实际分辨率画出「固定逻辑尺寸」的 HUD 画布：
        /// 画布尺寸 = 游戏分辨率 × 固定缩放（_canvasZoom），与编辑器窗口大小无关；
        /// 窗口放不下时由外层 ScrollView 滚动，绝不拉伸/压缩画布。
        /// </summary>
        private void DrawLayoutPreview()
        {
            RefreshGameViewSize(out float screenW, out float screenH);
            float aspect = screenW / screenH;

            // ---- 固定画布尺寸（不随窗口变） ----
            float zoom = Mathf.Clamp(_canvasZoom, 0.2f, 1f);
            float canvasW = Mathf.Round(screenW * zoom);
            float canvasH = Mathf.Round(screenH * zoom);

            // 可用区域比画布宽时居中留白（画布外部就是空白，不拉伸）
            float avail = Mathf.Max(1f, position.width - 40f);
            float leftPad = Mathf.Max(0f, (avail - canvasW) * 0.5f);

            var screen = new Rect();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(leftPad);
                var rect = GUILayoutUtility.GetRect(canvasW, canvasH, GUILayout.Width(canvasW), GUILayout.Height(canvasH));
                // 保险：即便布局把格子压小了，也按固定尺寸居中绘制（不跟随压缩）
                screen = new Rect(rect.x, rect.y, canvasW, canvasH);
                EditorGUI.DrawRect(screen, new Color(0.10f, 0.11f, 0.14f));
            }

            float s = zoom;

            // ---- 游戏边界框（分辨率）----
            var frameColor = new Color(1f, 0.85f, 0.35f, 0.95f);
            var safeColor = new Color(1f, 1f, 1f, 0.16f);
            var safe = new Rect(screen.x + screen.width * 0.05f, screen.y + screen.height * 0.05f,
                                screen.width * 0.90f, screen.height * 0.90f);
            Handles.BeginGUI();
            Handles.color = frameColor;
            Handles.DrawSolidRectangleWithOutline(screen, new Color(0f, 0f, 0f, 0f), frameColor);
            Handles.DrawSolidRectangleWithOutline(safe, new Color(0f, 0f, 0f, 0f), safeColor);
            // 四个角角标
            const float tick = 14f;
            foreach (var corner in new[] { new Vector2(screen.xMin, screen.yMin), new Vector2(screen.xMax, screen.yMin), new Vector2(screen.xMin, screen.yMax), new Vector2(screen.xMax, screen.yMax) })
            {
                float sx = corner.x == screen.xMin ? 1f : -1f;
                float sy = corner.y == screen.yMin ? 1f : -1f;
                Handles.DrawLine(corner, corner + new Vector2(tick * sx, 0f));
                Handles.DrawLine(corner, corner + new Vector2(0f, tick * sy));
            }
            Handles.EndGUI();
            GUI.Label(new Rect(screen.x + 6f, screen.y + 3f, canvasW - 12f, 18f),
                      $"游戏画面  {screenW:0} × {screenH:0}   ({aspect:0.00}:1)", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(screen.x + 6f, screen.yMax - 18f, canvasW - 12f, 16f), "黄框 = 屏幕边界，细线 = 90% 安全区", EditorStyles.miniLabel);

            // 先画非选中元素（弱），再画选中元素（强）；整屏元素（血效）只画描边不填充
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = LayoutIds.Length - 1; i >= 0; i--)
                {
                    bool selected = i == _selectedElement;
                    if ((pass == 0) == selected) continue;

                    var e = _layout.Ensure(LayoutIds[i]);
                    if (!e.visible) continue;

                    var baseRect = HudLayoutTabShared.DefaultRect(i, screenW, screenH);
                    var rect = new Rect(screen.x + (baseRect.x + e.offset.x) * s,
                                        screen.y + (baseRect.y - e.offset.y) * s,
                                        baseRect.width * s * e.scale,
                                        baseRect.height * s * e.scale);

                    bool fullScreen = i == LayoutIds.Length - 1;
                    var fill = selected ? new Color(0.25f, 0.75f, 1f, fullScreen ? 0f : 0.28f) : new Color(1f, 1f, 1f, fullScreen ? 0f : 0.10f);
                    EditorGUI.DrawRect(rect, fill);
                    var outline = selected ? new Color(0.3f, 0.85f, 1f, 1f) : new Color(1f, 1f, 1f, 0.35f);
                    Handles.BeginGUI();
                    Handles.color = outline;
                    Handles.DrawSolidRectangleWithOutline(rect, new Color(0, 0, 0, 0), outline);
                    Handles.EndGUI();
                    GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, Mathf.Max(120f, rect.width - 12f), 20f), LayoutTitles[i], EditorStyles.miniBoldLabel);

                    if (!selected) continue;

                    // 拖动：按住这个元素即可移动
                    var evt = Event.current;
                    if (evt.type == EventType.MouseDrag && rect.Contains(evt.mousePosition))
                    {
                        e.offset += new Vector2(evt.delta.x / s, -evt.delta.y / s);
                        e.offset = new Vector2(Mathf.Round(e.offset.x), Mathf.Round(e.offset.y));
                        EditorUtility.SetDirty(_layout);
                        evt.Use();
                        Repaint();
                    }
                }
            }

            // 屏幕中心参考线
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.10f);
            Handles.DrawLine(new Vector2(screen.center.x, screen.yMin), new Vector2(screen.center.x, screen.yMax));
            Handles.DrawLine(new Vector2(screen.xMin, screen.center.y), new Vector2(screen.xMax, screen.center.y));
            Handles.EndGUI();

            EditorGUILayout.LabelField($"画布：{(int)screenW} × {(int)screenH} 逻辑像素 → 显示 {canvasW:0} × {canvasH:0} px @ {zoom * 100f:0}%" +
                                       (_gameSizeFromView ? "（Game 视图实时）" : "（Game 视图未开，用参考分辨率）"), EditorStyles.miniLabel);
        }

        // ---------------- Game 视图尺寸 ----------------

        private bool _gameSizeFromView;
        private float _nextGameSizeRefresh;
        private static System.Reflection.MethodInfo _mainGameViewSizeMethod;
        private static bool _mainGameViewSizeProbed;

        /// <summary>取 Game 视图的渲染尺寸；拿不到时用参考分辨率（不影响比例正确性，只影响绝对像素）。</summary>
        private void RefreshGameViewSize(out float width, out float height)
        {
            if ((float)EditorApplication.timeSinceStartup >= _nextGameSizeRefresh)
            {
                _nextGameSizeRefresh = (float)EditorApplication.timeSinceStartup + 0.5f;

                if (!_mainGameViewSizeProbed)
                {
                    _mainGameViewSizeProbed = true;
                    _mainGameViewSizeMethod = typeof(Handles).GetMethod("GetMainGameViewSize",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                }

                bool resolved = false;
                if (_mainGameViewSizeMethod != null)
                {
                    try
                    {
                        var size = (Vector2)_mainGameViewSizeMethod.Invoke(null, null);
                        if (size.x >= 64f && size.y >= 64f) { _cachedGameSize = size; resolved = true; }
                    }
                    catch { /* 反射失败就回落 */ }
                }

                _gameSizeFromView = resolved;
            }

            width = Mathf.Max(64f, _cachedGameSize.x);
            height = Mathf.Max(64f, _cachedGameSize.y);
        }

        private Vector2 _cachedGameSize = new Vector2(1920f, 1080f);
    }

    internal static class HudLayoutTabShared
    {
        /// <summary>各 HUD 元素在屏幕坐标下的“默认”矩形（与运行时布局一致，供预览绘制）。</summary>
        public static Rect DefaultRect(int index, float w, float h)
        {
            switch (index)
            {
                case 0: // HealthUI：左上 620 宽，内容带高 = 620 * 0.334
                    return new Rect(24f, 18f, 620f, 620f * 0.334f);
                case 1: // DemoCanvas：顶部中间（轨道标题 / 提示 / 横幅）
                    return new Rect(w * 0.28f, 12f, w * 0.44f, h * 0.28f);
                case 2: // Viewfinder：右上
                    return new Rect(w * 0.58f, 12f, w * 0.38f, h * 0.14f);
                default: // BloodOverlay：整屏
                    return new Rect(0f, 0f, w, h);
            }
        }
    }

    internal static class ScreenBloodQueryExtensions
    {
        /// <summary>只保留场景里真实存在的实例（排除预制件资源里的伪对象）。</summary>
        public static IEnumerable<ScreenBlood> WhereAliveInScene(this ScreenBlood[] all)
        {
            foreach (var item in all)
            {
                if (item == null) continue;
                if (EditorUtility.IsPersistent(item)) continue;
                if (!item.gameObject.scene.IsValid()) continue;
                yield return item;
            }
        }
    }
}
