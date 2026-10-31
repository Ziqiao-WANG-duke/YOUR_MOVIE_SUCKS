using UnityEngine;
using UnityEngine.UI;
using YourMovieSucks.Combat;
using YourMovieSucks.LabB;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 生命值 HUD（替换旧的 IMGUI HPUI）：
    ///  · 底图 = 空血贴图（胶片条纹），上层 = 满血贴图按格遮罩（只显示前 N 格）→ N/9 格血条
    ///  · 特工与导演用同一套贴图、同一条血条（不再按轨道换皮）
    ///  · 不显示数字（格数只靠胶片亮格表达）
    /// 参数全部在 Inspector；贴图 UV 与血格范围有默认值（来自当前素材实测），换素材时改 contentUv / cellsStartU / cellsEndU。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthUI : MonoBehaviour
    {
        [Header("引用（留空自动查找）")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("贴图（两位角色共用）")]
        [Tooltip("满血白模：胶片上发亮的画面")]
        [SerializeField] private Texture full;
        [Tooltip("空血底图：同一胶片但画面是暗的")]
        [SerializeField] private Texture empty;

        [Header("排版")]
        [Tooltip("整块 UI 的屏幕锚点（0,1 = 左上角）")]
        [SerializeField] private Vector2 anchor = new Vector2(0f, 1f);
        [Tooltip("相对锚点的像素偏移")]
        [SerializeField] private Vector2 offset = new Vector2(24f, -18f);
        [Tooltip("血条可见宽度（像素）")]
        [Min(64f)] [SerializeField] private float width = 640f;
        [Tooltip("贴图里不透明内容的 UV 区域（裁掉透明留白）。默认来自当前素材：x 0.0439 y 0.415 w 0.9121 h 0.167")]
        [SerializeField] private Rect contentUv = new Rect(0.0439f, 0.415f, 0.9121f, 0.167f);

        [Header("血格（与贴图对齐）")]
        [Tooltip("血格数量，需与美术一致（当前胶片素材 9 格）")]
        [Min(1)] [SerializeField] private int cells = 9;
        [Tooltip("血格区域在贴图 U 方向的起止（当前素材：每道格边界都落在胶片黑边上）")]
        [SerializeField] private float cellsStartU = 0.0794f;
        [SerializeField] private float cellsEndU = 0.9187f;

        [Header("层级")]
        [SerializeField] private int sortingOrder = 40;

        private Canvas _canvas;
        private RawImage _emptyImage;
        private RawImage _fullImage;
        private Material _maskMaterial;
        private RectTransform _root;
        private int _lastCells = -1;

        private static readonly int FilledId = Shader.PropertyToID("_Filled");
        private static readonly int CellsStartId = Shader.PropertyToID("_CellsStart");
        private static readonly int CellsEndId = Shader.PropertyToID("_CellsEnd");
        private static readonly int CellCountId = Shader.PropertyToID("_CellCount");

        private void Awake()
        {
            Resolve();
            Build();
        }

        private void OnEnable()
        {
            Resolve();
            if (playerHealth != null) playerHealth.OnDamaged += HandleDamaged;
            _lastCells = -1;
            Refresh();
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDamaged -= HandleDamaged;
        }

        private void Resolve()
        {
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();

            var tuning = LabBTuning.I;
            if (tuning != null && tuning.Enabled && tuning.playerMaxCells > 0)
                cells = Mathf.Max(1, Mathf.RoundToInt(tuning.playerMaxCells));
        }

        private void HandleDamaged() => Refresh();

        // ---------------------------------------------------------------- 构建

        private void Build()
        {
            if (_canvas != null) return;

            var go = new GameObject("HealthUI");
            // 不挂到 Hud 下面：父画布的 CanvasScaler 会缩小/放大子画布，血条尺寸就不准了
            go.transform.SetParent(null, false);
            go.transform.position = Vector3.zero;
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            _root = (RectTransform)go.transform;
            // Overlay 画布的根 RectTransform 由 Canvas 驱动（会被强制铺满屏幕），
            // 所以布局放到子容器上。
            var content = new GameObject("Layout", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            _root = (RectTransform)content.transform;
            _root.anchorMin = anchor;
            _root.anchorMax = anchor;
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = offset;

            float bandHeight = width * Mathf.Max(contentUv.height, 0.01f) / Mathf.Max(contentUv.width, 0.01f);
            _root.sizeDelta = new Vector2(width, bandHeight);

            _emptyImage = CreateRawImage(_root, "HealthEmpty");
            _fullImage = CreateRawImage(_root, "HealthFull");

            var maskShader = Shader.Find("YourMovieSucks/UIHealthCellMask");
            if (maskShader != null)
            {
                _maskMaterial = new Material(maskShader) { name = "HealthCellMask (Runtime)" };
                _fullImage.material = _maskMaterial;
            }
            else Debug.LogWarning("[HealthUI] 找不到 YourMovieSucks/UIHealthCellMask，血格将无法逐格显示");
        }

        private RawImage CreateRawImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<RawImage>();
            img.raycastTarget = false;
            img.uvRect = contentUv;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        // ---------------------------------------------------------------- 刷新

        private void Refresh()
        {
            if (_canvas == null) return;

            if (_emptyImage.texture != empty) _emptyImage.texture = empty;
            if (_fullImage.texture != full) _fullImage.texture = full;
            if (_emptyImage.uvRect != contentUv) { _emptyImage.uvRect = contentUv; _fullImage.uvRect = contentUv; }

            int max = playerHealth != null ? playerHealth.MaxCells : cells;
            int current = playerHealth != null ? playerHealth.CurrentCells : max;
            if (max != cells) cells = Mathf.Max(1, max);

            if (_maskMaterial != null)
            {
                _maskMaterial.SetFloat(FilledId, Mathf.Clamp(current, 0, cells));
                _maskMaterial.SetFloat(CellsStartId, cellsStartU);
                _maskMaterial.SetFloat(CellsEndId, cellsEndU);
                _maskMaterial.SetFloat(CellCountId, cells);
            }

            _lastCells = current;
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (playerHealth == null) return;
            if (playerHealth.CurrentCells != _lastCells) Refresh();
        }
    }
}
