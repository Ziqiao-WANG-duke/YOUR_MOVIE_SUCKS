using UnityEngine;
using YourMovieSucks.UI;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 局内 HUD 布局面板（F3 开关）：直接拖滑杆改每个 HUD 元素的位置/缩放/透明度/显隐，
    /// 立刻生效；「保存到资产」在编辑器 Play Mode 下会把数值写进 HudLayout.asset，下次运行沿用。
    /// 不改变任何玩法逻辑，只动 HUD 画布的偏移。
    /// </summary>
    public sealed class HudLayoutPanel : MonoBehaviour
    {
        [Header("引用")]
        public HudLayoutApplier applier;

        [Header("外观")]
        public int panelWidth = 360;
        public float stepPixels = 2f;

        private bool _visible;
        private Rect _window = new Rect(12f, 120f, 360f, 520f);
        private static readonly string[] Labels =
        {
            "HealthUI", "DemoCanvas", "ViewfinderCanvas", "BloodOverlay"
        };
        private static readonly string[] Titles =
        {
            "生命值 HUD", "演出 HUD（轨道/提示/结算）", "取景框", "屏幕血效"
        };

        private void Awake()
        {
            if (applier == null) applier = GetComponent<HudLayoutApplier>();
            if (applier == null) applier = Object.FindFirstObjectByType<HudLayoutApplier>();
        }

        private void Update()
        {
            // 用新输入系统（工程已禁用旧 Input 类）
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || applier == null || applier.Layout == null) return;

            _window.width = panelWidth;
            _window.height = Mathf.Min(Screen.height - 140f, 60f + Labels.Length * 108f + 44f);
            _window = GUI.Window(GetInstanceID(), _window, DrawWindow, "HUD 布局（F3 关闭）");
        }

        private void DrawWindow(int id)
        {
            var layout = applier.Layout;
            float y = 24f;
            float w = _window.width - 24f;

            for (int i = 0; i < Labels.Length; i++)
            {
                var e = layout.Ensure(Labels[i]);
                GUI.Label(new Rect(12f, y, w, 18f), Titles[i]);
                y += 18f;

                // 位置
                float x0 = e.offset.x;
                float y0 = e.offset.y;
                GUI.Label(new Rect(12f, y, 62f, 18f), $"X {x0:0}");
                e.offset.x = GUI.HorizontalSlider(new Rect(76f, y + 5f, w - 150f, 16f), x0, -600f, 600f);
                e.offset.x = Mathf.Round(e.offset.x / stepPixels) * stepPixels;
                if (GUI.Button(new Rect(_window.width - 66f, y, 24f, 18f), "◀")) e.offset.x = x0 - 8f;
                if (GUI.Button(new Rect(_window.width - 40f, y, 24f, 18f), "▶")) e.offset.x = x0 + 8f;
                y += 20f;

                GUI.Label(new Rect(12f, y, 62f, 18f), $"Y {e.offset.y:0}");
                e.offset.y = GUI.HorizontalSlider(new Rect(76f, y + 5f, w - 150f, 16f), e.offset.y, -600f, 600f);
                e.offset.y = Mathf.Round(e.offset.y / stepPixels) * stepPixels;
                if (GUI.Button(new Rect(_window.width - 66f, y, 24f, 18f), "▼")) e.offset.y = y0 - 8f;
                if (GUI.Button(new Rect(_window.width - 40f, y, 24f, 18f), "▲")) e.offset.y = y0 + 8f;
                y += 20f;

                // 缩放 / 透明度
                GUI.Label(new Rect(12f, y, 62f, 18f), $"缩放 {e.scale:0.00}");
                e.scale = GUI.HorizontalSlider(new Rect(76f, y + 5f, w - 90f, 16f), e.scale, 0.2f, 2.5f);
                y += 20f;

                GUI.Label(new Rect(12f, y, 62f, 18f), $"透明 {e.alpha:0.00}");
                e.alpha = GUI.HorizontalSlider(new Rect(76f, y + 5f, w - 90f, 16f), e.alpha, 0f, 1f);
                y += 20f;

                e.visible = GUI.Toggle(new Rect(12f, y, 90f, 18f), e.visible, "显示");
                if (GUI.Button(new Rect(110f, y, 70f, 18f), "重置")) layout.Reset(Labels[i]);
                y += 26f;
            }

            applier.Apply();

            if (GUI.Button(new Rect(12f, y, 120f, 24f), "保存到资产"))
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(layout);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[HudLayoutPanel] 已保存到 " + UnityEditor.AssetDatabase.GetAssetPath(layout));
#endif
            }
            if (GUI.Button(new Rect(140f, y, 120f, 24f), "全部重置"))
            {
                foreach (var name in Labels) layout.Reset(name);
            }

            GUI.DragWindow(new Rect(0f, 0f, _window.width, 20f));
        }
    }
}
