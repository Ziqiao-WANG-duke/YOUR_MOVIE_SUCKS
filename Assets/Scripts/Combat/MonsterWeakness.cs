using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 单只怪的弱点：配置（哪条 entry）+ 运行时状态（是否已识别）+ 两个运行时占位物：
    ///   ① 红色发光标记（挂在弱点骨骼上，轻微脉冲，只在导演轨显示）
    ///   ② 弱点词条锚点（骨骼上方一点，世界空间词条挂在这里，不按包围盒算高度）
    ///
    /// 状态天然是「每只怪一份」（一个实例一份），不是全局状态。
    /// 识别不会击杀怪物，只把它的战斗词换成战斗编辑器里该类型「弱点后词库」里的新词。
    /// </summary>
    [RequireComponent(typeof(TypingTarget))]
    public class MonsterWeakness : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private MonsterWeaknessSet set;

        [Header("占位表现")]
        [SerializeField] private float markerSize = 0.17f;
        [SerializeField] private Color markerColor = new Color(1f, 0.12f, 0.12f, 1f);
        [SerializeField] private float markerPulseHz = 2.2f;

        /// <summary>场上所有还活着的弱点实例（导演轨视图用来枚举；同屏 ≤5 只）。</summary>
        private static readonly List<MonsterWeakness> Live = new();
        public static IReadOnlyList<MonsterWeakness> All => Live;

        private MonsterWeaknessSet.Entry _entry;
        private Transform _bone;
        private GameObject _marker;
        private GameObject _anchor;
        private Renderer _markerRenderer;
        private Material _markerMaterial;
        private float _pulse;
        private bool _visible;

        public bool Recognized { get; private set; }

        /// <summary>导演模式可把这个放大一点让弱点更醒目（1 = 不放大）。</summary>
        public static float GlobalMarkerScale = 1f;

        /// <summary>本局这只怪的原始战斗词（由刷怪方写入）。</summary>
        public string OriginalWord { get; set; } = string.Empty;

        /// <summary>
        /// 战斗编排资产按怪物类型指定的「弱点被破坏后的新词库」（由刷怪方写入）。
        /// 这是弱点被破坏后唯一的换词来源：空 = 不换词（保持原战斗词）。
        /// </summary>
        public string[] PostWeakWords { get; set; } = System.Array.Empty<string>();

        public string PartLabel => _entry != null ? _entry.partLabel : string.Empty;
        public string WeakWord => _entry != null ? _entry.weakWord : string.Empty;
        public Transform WeakPoint => _bone != null ? _bone : transform;
        public GameObject LabelAnchor => _anchor != null ? _anchor : gameObject;

        public event System.Action<MonsterWeakness> OnRecognized;

        private void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        private void OnDisable()
        {
            Live.Remove(this);
        }

        private void Awake()
        {
            _entry = PickEntry();
        }

        private void Start()
        {
            BuildVisuals();
        }

        private void Update()
        {
            if (!_visible || Recognized || _marker == null) return;

            _pulse += Time.deltaTime;
            float scale = markerSize * (1f + Mathf.Sin(_pulse * Mathf.PI * 2f * markerPulseHz) * 0.14f) * GlobalMarkerScale;
            _marker.transform.localScale = Vector3.one * scale;
        }

        // ---------------------------------------------------------------- 对外

        /// <summary>导演出/入导演轨时由视图调用：红光只在导演轨出现。</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible && !Recognized;
            if (_marker != null) _marker.SetActive(_visible);
        }

        /// <summary>弱点识别成功（不击杀）。</summary>
        public void MarkRecognized()
        {
            if (Recognized) return;

            Recognized = true;
            _visible = false;

            StartCoroutine(RecognitionFlash());
            OnRecognized?.Invoke(this);
        }

        // ---------------------------------------------------------------- 内部

        /// <summary>挑一条「没被别的活怪占用」的弱点：同屏最多 5 只、池子 5 条 → 不会重复。</summary>
        private MonsterWeaknessSet.Entry PickEntry()
        {
            if (set == null || set.Count == 0)
            {
                Debug.LogWarning($"[MonsterWeakness] {name} 没有配置 MonsterWeaknessSet");
                return null;
            }

            for (int i = 0; i < set.Count; i++)
            {
                var candidate = set.Get(i);
                if (candidate == null || string.IsNullOrEmpty(candidate.weakWord)) continue;
                if (!IsEntryInUse(candidate)) return candidate;
            }

            // 理论上不会走到：多于池子里的怪。退回第一条，交给首字母吸附去处理歧义。
            Debug.LogWarning("[MonsterWeakness] 弱点池已全部被占用，可能出现同首字母弱点词");
            return set.Get(0);
        }

        private bool IsEntryInUse(MonsterWeaknessSet.Entry entry)
        {
            foreach (var other in Live)
            {
                if (other == null || other == this || other.Recognized) continue;
                if (other._entry != null && other._entry.weakWord == entry.weakWord) return true;
            }

            return false;
        }

        private void BuildVisuals()
        {
            _bone = ResolveBone(_entry != null ? _entry.boneName : null);
            if (_bone == null)
            {
                _bone = transform;
                Debug.LogWarning($"[MonsterWeakness] {name} 找不到骨骼 '{( _entry != null ? _entry.boneName : "null")}'，弱点用根节点代替");
            }

            // ① 红色发光标记（占位：发光小球，挂在骨骼上跟着动画走）
            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _marker.name = "WeakPointMarker";
            var collider = _marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            _marker.transform.SetParent(_bone, false);
            _marker.transform.localPosition = Vector3.zero;
            _marker.transform.localScale = Vector3.one * markerSize;

            _markerRenderer = _marker.GetComponent<Renderer>();            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _markerMaterial = new Material(shader) { name = "M_WeakPointMarker" };
            if (_markerMaterial.HasProperty("_BaseColor")) _markerMaterial.SetColor("_BaseColor", markerColor);
            if (_markerMaterial.HasProperty("_Color")) _markerMaterial.SetColor("_Color", markerColor);
            _markerRenderer.sharedMaterial = _markerMaterial;
            // 弱点标记单独一层：导演模式下由原生分辨率相机绘制，不经过黑白滤镜，永远保持纯红。
            _marker.layer = YourMovieSucks.UI.DirectorModeLayers.WeakPoint;
            _marker.SetActive(false);

            // ② 词条锚点（骨骼上方一点；世界空间词条挂这里，用固定偏移而不是包围盒）
            _anchor = new GameObject("WeakPointLabelAnchor");
            _anchor.transform.SetParent(_bone, false);
            _anchor.transform.localPosition = new Vector3(0f, markerSize * 1.2f, 0f);
        }

        private Transform ResolveBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;

            foreach (var candidate in GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == boneName) return candidate;
            }

            return null;
        }

        private IEnumerator RecognitionFlash()
        {
            if (_marker != null)
            {
                _marker.SetActive(true);
                float t = 0f;
                const float duration = 0.25f;

                while (t < duration)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / duration);
                    _marker.transform.localScale = Vector3.one * (markerSize * (1f + 2.4f * k));
                    yield return null;
                }

                _marker.SetActive(false);
            }
        }
    }
}
