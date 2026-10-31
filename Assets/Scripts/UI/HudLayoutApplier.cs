using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 把 <see cref="HudLayoutAsset"/> 里的偏移/缩放/透明度应用到运行时生成的 HUD 画布上。
    /// 做法：Overlay 画布根节点的 RectTransform 由 Canvas 驱动（改不动），
    /// 所以偏移施加到它的直接子节点上，缩放/透明度作用在画布本体上。
    /// 面板（HudLayoutPanel，F3）实时改资产并调 <see cref="Apply"/>。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class HudLayoutApplier : MonoBehaviour
    {
        [Tooltip("HUD 布局资产（留空则用默认值 = 不动）")]
        public HudLayoutAsset layout;

        [Tooltip("额外重扫间隔（秒）：HUD 是运行时创建的，晚于本组件出现时要重试")]
        [Min(0.05f)] public float rescanInterval = 0.5f;

        private sealed class Entry
        {
            public Canvas canvas;
            /// <summary>布局用的 CanvasGroup 挂在容器上（不碰画布自己的 CanvasGroup，否则会和取景框的淡入淡出打架）。</summary>
            public CanvasGroup layoutGroup;
            public RectTransform container;
            /// <summary>内容中心（容器本地坐标）：缩放时以它为基准，元素就不会滑走。</summary>
            public Vector2 contentCenter;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private float _nextScan;

        private static readonly string[] KnownCanvases =
        {
            "HealthUI",        // 生命值（HealthUI）
            "DemoCanvas",      // 演出 HUD：轨道标题 / TYPE 提示 / COMBAT START / 结算
            "ViewfinderCanvas",// 16MM 取景框
            "BloodOverlay"     // 屏幕血效
        };

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + rescanInterval;
            Scan();
            Apply();
        }

        /// <summary>重新扫描还没被接管的 HUD 画布（并把内容放进一个可自由变换的全屏容器里）。</summary>
        private void Scan()
        {
            foreach (var name in KnownCanvases)
            {
                if (_entries.ContainsKey(name)) continue;
                var canvas = FindCanvas(name);
                if (canvas == null) continue;

                var entry = new Entry { canvas = canvas };

                // Overlay 画布的根节点由 Canvas 驱动（localScale 会被重置、anchoredPosition 也改不动），
                // 所以把所有子节点装进一个全屏容器，偏移/缩放都作用在这个容器上。
                var containerGo = new GameObject("HudLayoutRoot", typeof(RectTransform));
                var container = (RectTransform)containerGo.transform;
                container.SetParent(canvas.transform, false);
                container.anchorMin = Vector2.zero;
                container.anchorMax = Vector2.one;
                container.offsetMin = Vector2.zero;
                container.offsetMax = Vector2.zero;
                container.pivot = new Vector2(0.5f, 0.5f);
                container.localScale = Vector3.one;
                container.anchoredPosition = Vector2.zero;

                // 透明度作用在这个容器上：画布根节点上的 CanvasGroup 可能被别的组件（如取景框）
                // 每帧驱动做淡入淡出，绝不能覆盖它，否则会出现“本该隐藏却常驻且闪烁”。
                entry.layoutGroup = containerGo.GetComponent<CanvasGroup>();
                if (entry.layoutGroup == null) entry.layoutGroup = containerGo.AddComponent<CanvasGroup>();
                entry.layoutGroup.blocksRaycasts = false;
                entry.layoutGroup.interactable = false;
                entry.layoutGroup.alpha = 1f;

                var existing = new List<Transform>();
                foreach (Transform child in canvas.transform)
                    if (child != container.transform) existing.Add(child);
                foreach (var child in existing) child.SetParent(container, false);

                entry.container = container;
                entry.contentCenter = ComputeContentCenter(container);
                _entries[name] = entry;
            }
        }

        private static Canvas FindCanvas(string objectName)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas && canvas.name == objectName) return canvas;
            return null;
        }

        /// <summary>算容器内所有子内容的包围盒中心（容器本地坐标）。</summary>
        private static Vector2 ComputeContentCenter(RectTransform container)
        {
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            var corners = new Vector3[4];

            foreach (Transform child in container)
            {
                var rt = child as RectTransform;
                if (rt == null) continue;
                rt.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    var local = (Vector2)container.InverseTransformPoint(corners[i]);
                    if (!any) { min = max = local; any = true; }
                    else { min = Vector2.Min(min, local); max = Vector2.Max(max, local); }
                }
            }

            return any ? (min + max) * 0.5f : Vector2.zero;
        }

        /// <summary>把当前资产值应用一遍（面板拖动时每帧调用）。</summary>
        public void Apply()
        {
            if (layout == null) return;

            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry.canvas == null || entry.container == null) continue;

                var data = layout.Get(pair.Key) ?? layout.Ensure(pair.Key);

                bool active = data.visible;
                if (entry.canvas.gameObject.activeSelf != active) entry.canvas.gameObject.SetActive(active);
                if (!active) continue;

                float k = Mathf.Max(0.05f, data.scale);
                // 以内容中心为基准缩放：缩放前后该点位置不变（否则会向屏幕中心滑）
                entry.container.localScale = Vector3.one * k;
                entry.container.anchoredPosition = data.offset + entry.contentCenter * (1f - k);
                if (entry.layoutGroup != null) entry.layoutGroup.alpha = Mathf.Clamp01(data.alpha);
            }
        }

        public HudLayoutAsset Layout => layout;
    }
}
