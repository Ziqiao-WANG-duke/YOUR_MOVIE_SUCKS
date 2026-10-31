using UnityEditor;
using UnityEngine;
using YourMovieSucks.UI;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// HudLayout 资产的自定义 Inspector：点开资产就能看到（并能直接调）HUD 布局页面，
    /// 不用先去找菜单。画布同样按「固定逻辑尺寸 + 固定倍率」绘制，窗口/面板拉伸不会改变比例。
    /// </summary>
    [CustomEditor(typeof(HudLayoutAsset))]
    public sealed class HudLayoutAssetEditor : UnityEditor.Editor
    {
        private static readonly string[] Ids = { "HealthUI", "DemoCanvas", "ViewfinderCanvas", "BloodOverlay" };
        private static readonly string[] Titles = { "生命值 HUD", "演出 HUD（轨道/提示/结算）", "取景框", "屏幕血效" };

        private int _selected;
        private float _zoom = 0.35f;
        private static System.Reflection.MethodInfo _gameViewSize;
        private static bool _probed;
        private Vector2 _cachedSize = new Vector2(1920f, 1080f);
        private float _nextRefresh;

        public override void OnInspectorGUI()
        {
            var layout = (HudLayoutAsset)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("这是 HUD 位置数据。下面就是可视化编辑页（拖方块改偏移）；" +
                                        "也可以点右边的按钮打开完整窗口（带缩放/滚动）。", MessageType.Info);
                if (GUILayout.Button("打开完整窗口", GUILayout.Width(110f), GUILayout.Height(38f)))
                    HudEditorWindow.OpenLayoutTab();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("画布缩放", GUILayout.Width(56f));
                _zoom = GUILayout.HorizontalSlider(_zoom, 0.2f, 0.8f, GUILayout.Width(120f));
                GUILayout.Label($"{_zoom * 100f:0}%", GUILayout.Width(40f));
                GUILayout.Label($"画布 {(int)_cachedSize.x} × {(int)_cachedSize.y} 逻辑像素", EditorStyles.miniLabel);
            }

            RefreshGameViewSize();
            DrawCanvas(layout);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < Ids.Length; i++)
                {
                    bool on = _selected == i;
                    if (GUILayout.Toggle(on, Titles[i], EditorStyles.miniButton, GUILayout.Height(22f)) != on)
                        _selected = i;
                }
            }

            var element = layout.Ensure(Ids[_selected]);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(Titles[_selected], EditorStyles.boldLabel);
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
                    if (GUILayout.Button("重置这个元素")) layout.Reset(Ids[_selected]);
                    if (GUILayout.Button("全部重置")) foreach (var id in Ids) layout.Reset(id);
                }
            }

            if (GUI.changed)
            {
                // 只标脏，不自动写盘：避免误触直接改掉资产（需要落盘时点「保存资产」或 Ctrl+S）
                EditorUtility.SetDirty(layout);
            }
        }

        private void DrawCanvas(HudLayoutAsset layout)
        {
            float zoom = Mathf.Clamp(_zoom, 0.2f, 1f);
            float canvasW = Mathf.Round(_cachedSize.x * zoom);
            float canvasH = Mathf.Round(_cachedSize.y * zoom);

            float avail = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 40f);
            float pad = Mathf.Max(0f, (avail - canvasW) * 0.5f);

            var outer = GUILayoutUtility.GetRect(avail, canvasH + 4f);
            var canvas = new Rect(outer.x + pad, outer.y + 2f, canvasW, canvasH);
            EditorGUI.DrawRect(new Rect(outer.x, outer.y, outer.width, outer.height), new Color(0.16f, 0.16f, 0.18f));
            EditorGUI.DrawRect(canvas, new Color(0.10f, 0.11f, 0.14f));

            // 游戏边界 + 安全区
            var frame = new Color(1f, 0.85f, 0.35f, 0.95f);
            var safe = new Rect(canvas.x + canvas.width * 0.05f, canvas.y + canvas.height * 0.05f,
                                canvas.width * 0.90f, canvas.height * 0.90f);
            Handles.BeginGUI();
            Handles.color = frame;
            Handles.DrawSolidRectangleWithOutline(canvas, new Color(0, 0, 0, 0), frame);
            Handles.DrawSolidRectangleWithOutline(safe, new Color(0, 0, 0, 0), new Color(1f, 1f, 1f, 0.15f));
            Handles.EndGUI();
            GUI.Label(new Rect(canvas.x + 4f, canvas.y + 2f, canvasW - 8f, 16f),
                      $"游戏画面 {(int)_cachedSize.x} × {(int)_cachedSize.y}", EditorStyles.miniBoldLabel);

            // 元素方块（整屏元素只描边）
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = Ids.Length - 1; i >= 0; i--)
                {
                    bool selected = i == _selected;
                    if ((pass == 0) == selected) continue;

                    var e = layout.Ensure(Ids[i]);
                    if (!e.visible) continue;

                    var baseRect = HudLayoutTabShared.DefaultRect(i, _cachedSize.x, _cachedSize.y);
                    var rect = new Rect(canvas.x + (baseRect.x + e.offset.x) * zoom,
                                        canvas.y + (baseRect.y - e.offset.y) * zoom,
                                        baseRect.width * zoom * e.scale,
                                        baseRect.height * zoom * e.scale);

                    bool fullScreen = i == Ids.Length - 1;
                    EditorGUI.DrawRect(rect, selected
                        ? new Color(0.25f, 0.75f, 1f, fullScreen ? 0f : 0.28f)
                        : new Color(1f, 1f, 1f, fullScreen ? 0f : 0.10f));

                    Handles.BeginGUI();
                    Handles.color = selected ? new Color(0.3f, 0.85f, 1f, 1f) : new Color(1f, 1f, 1f, 0.35f);
                    Handles.DrawSolidRectangleWithOutline(rect, new Color(0, 0, 0, 0), Handles.color);
                    Handles.EndGUI();
                    GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, Mathf.Max(80f, rect.width - 8f), 16f),
                              Titles[i], EditorStyles.miniLabel);

                    if (!selected) continue;
                    var evt = Event.current;
                    if (evt.type == EventType.MouseDrag && rect.Contains(evt.mousePosition))
                    {
                        e.offset += new Vector2(evt.delta.x / zoom, -evt.delta.y / zoom);
                        e.offset = new Vector2(Mathf.Round(e.offset.x), Mathf.Round(e.offset.y));
                        EditorUtility.SetDirty(layout);
                        evt.Use();
                        Repaint();
                    }
                }
            }
        }

        private void RefreshGameViewSize()
        {
            if ((float)EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = (float)EditorApplication.timeSinceStartup + 0.5f;

            if (!_probed)
            {
                _probed = true;
                _gameViewSize = typeof(Handles).GetMethod("GetMainGameViewSize",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
            }
            if (_gameViewSize == null) return;

            try
            {
                var size = (Vector2)_gameViewSize.Invoke(null, null);
                if (size.x >= 64f && size.y >= 64f) _cachedSize = size;
            }
            catch { /* 拿不到就用上次/参考分辨率 */ }
        }
    }
}
