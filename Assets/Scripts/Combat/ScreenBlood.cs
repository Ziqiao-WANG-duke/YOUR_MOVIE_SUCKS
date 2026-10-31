using UnityEngine;
using UnityEngine.UI;
using YourMovieSucks.LabB;
using YourMovieSucks.UI;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 屏幕受伤效果（Screen Space Overlay）：
    /// ① 玩家被怪打到时 → 必出三道爪痕（T_Blood0）+ 随机 0–2 个飞溅（T_Blood1/2）；
    /// ② 击杀怪物时 → 只有轻微飞溅（保留旧的 Splatter 接口）。
    /// 三层都只在屏幕空间叠加，进不了场景也遮不住词条以外的 UI；停留后渐隐，可连击重复触发。
    /// </summary>
    public class ScreenBlood : MonoBehaviour
    {
        [Header("贴图")]
        [Tooltip("三道爪痕（每次玩家受伤必出）")]
        [SerializeField] private Texture clawTexture;
        [Tooltip("额外飞溅池（随机取用，可只出一张也可同时出）")]
        [SerializeField] private Texture[] splashTextures = new Texture[0];

        [Header("玩家受伤")]
        [Tooltip("是否自动挂到 PlayerHealth.OnDamaged 上（只在真正扣血时触发）")]
        [SerializeField] private bool reactToPlayerDamage = true;
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("爪痕峰值透明度")] [Range(0f, 1f)] [SerializeField] private float clawAlpha = 0.85f;
        [Tooltip("爪痕重心（归一化：0=屏幕中心，x 向右、y 向上，按屏宽/屏高计）")] [SerializeField] private Vector2 clawCenter = new Vector2(0f, 0.10f);
        [Tooltip("爪痕位置随机抖动（归一化）")] [SerializeField] private Vector2 clawJitter = new Vector2(0.10f, 0.06f);
        [Tooltip("爪痕宽度（屏幕宽度的比例）")] [Range(0.2f, 1.2f)] [SerializeField] private float clawScreenWidth = 0.72f;
        [Tooltip("爪痕旋转范围（度）")] [SerializeField] private Vector2 clawRotation = new Vector2(-7f, 7f);
        [Tooltip("爪痕大小随机范围（倍率）")] [SerializeField] private Vector2 clawScaleRange = new Vector2(0.92f, 1.12f);
        [Tooltip("额外飞溅峰值透明度")] [Range(0f, 1f)] [SerializeField] private float hitSplashAlpha = 0.6f;
        [Tooltip("最多同时出现几个飞溅")] [Range(0, 4)] [SerializeField] private int hitSplashMax = 2;
        [Tooltip("每个飞溅出现的概率")] [Range(0f, 1f)] [SerializeField] private float hitSplashChance = 0.8f;
        [Tooltip("飞溅尺寸范围（屏幕宽度的比例）")] [SerializeField] private Vector2 splashSizeRange = new Vector2(0.22f, 0.45f);

        [Header("击杀反馈（旧接口）")]
        [Tooltip("击杀时的血幕峰值透明度")] [Range(0f, 1f)] [SerializeField] private float splatterAlpha = 0.32f;

        [Header("时序")]
        [Tooltip("保持满透明度的时长（秒）")] [Min(0f)] [SerializeField] private float holdTime = 0.35f;
        [Tooltip("渐隐时长（秒）")] [Min(0.01f)] [SerializeField] private float fadeDuration = 0.75f;

        [Header("层级")]
        [Tooltip("越低越靠后（画面之上、其他 UI 之下）")] [SerializeField] private int sortingOrder = -10;

        private CanvasGroup _group;
        private RectTransform _claw;
        private RectTransform[] _splashes;
        private CameraShake _cameraShake;
        private float _timer;
        private float _peakAlpha;

        public bool IsBloody => _timer > 0f;
        public event System.Action OnSplatter;
        public event System.Action OnCleared;

        private void Awake()
        {
            EnsureOverlay();
        }

        private void OnEnable()
        {
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null) playerHealth.OnDamaged += HandlePlayerDamaged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDamaged -= HandlePlayerDamaged;
        }

        /// <summary>屏幕空间画布 + 一层爪痕 + 一池飞溅。全部延迟建，避免场景里多出空物体。</summary>
        private void EnsureOverlay()
        {
            if (_group != null) return;

            var go = new GameObject("BloodOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;   // 3D 之上、其他 UI 画布之下
            _group = go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _claw = CreateLayer(go.transform, "Claw");
            int count = Mathf.Max(0, hitSplashMax);
            _splashes = new RectTransform[count];
            for (int i = 0; i < count; i++) _splashes[i] = CreateLayer(go.transform, "Splash" + i);
        }

        private static RectTransform CreateLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<RawImage>();
            img.raycastTarget = false;
            img.enabled = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private void HandlePlayerDamaged()
        {
            if (!reactToPlayerDamage) return;
            ShowPlayerHit();
        }

        /// <summary>玩家真正被扣血时的固定反馈：爪痕必出 + 随机飞溅。</summary>
        public void ShowPlayerHit()
        {
            if (!BloodAllowed()) return;
            EnsureOverlay();

            float width = Mathf.Max(Screen.width, 320f);
            float height = Mathf.Max(Screen.height, 240f);

            ShowClaw(width, height);

            for (int i = 0; i < _splashes.Length; i++)
            {
                bool show = Random.value <= hitSplashChance;
                ShowSplash(_splashes[i], width, height, show);
            }

            StartFade(clawAlpha);
            OnSplatter?.Invoke();
        }

        private void ShowClaw(float width, float height)
        {
            var img = _claw.GetComponent<RawImage>();
            img.texture = clawTexture ? clawTexture : Texture2D.whiteTexture;
            bool hasArt = clawTexture != null;
            img.color = hasArt ? Color.white : new Color(0.6f, 0f, 0f, 1f);
            img.enabled = true;

            float size = width * clawScreenWidth * Random.Range(clawScaleRange.x, clawScaleRange.y);
            _claw.sizeDelta = new Vector2(size, size);
            // 位置 = 可调重心 + 可调抖动（都按归一化屏幕坐标，HUD Editor 可直接拖）
            float px = (clawCenter.x + Random.Range(-clawJitter.x, clawJitter.x)) * width;
            float py = (clawCenter.y + Random.Range(-clawJitter.y, clawJitter.y)) * height;
            _claw.anchoredPosition = new Vector2(px, py);
            _claw.localRotation = Quaternion.Euler(0f, 0f, Random.Range(clawRotation.x, clawRotation.y));
        }

        private void ShowSplash(RectTransform rt, float width, float height, bool show)
        {
            var img = rt.GetComponent<RawImage>();
            if (!show || splashTextures == null || splashTextures.Length == 0)
            {
                img.enabled = false;
                return;
            }

            img.texture = splashTextures[Random.Range(0, splashTextures.Length)];
            img.color = Color.white;
            img.enabled = true;

            float size = width * Random.Range(splashSizeRange.x, splashSizeRange.y);
            rt.sizeDelta = new Vector2(size, size);
            // 飞溅推到画面边缘，不压中心视野
            bool left = Random.value < 0.5f;
            float x = (left ? -1f : 1f) * Random.Range(0.22f, 0.42f) * width;
            float y = Random.Range(-0.20f, 0.30f) * height;
            rt.anchoredPosition = new Vector2(x, y);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
        }

        private void StartFade(float peak)
        {
            _peakAlpha = Mathf.Clamp01(peak);
            _timer = Mathf.Max(0.01f, holdTime + fadeDuration);
            _group.alpha = _peakAlpha;
        }

        /// <summary>击杀反馈（旧接口）：只出飞溅，不出爪痕。</summary>
        public void Splatter()
        {
            if (!BloodAllowed()) return;
            EnsureOverlay();

            var tuning = LabBTuning.I;
            float peak = tuning != null && tuning.Enabled ? tuning.splatterAlpha : splatterAlpha;
            float width = Mathf.Max(Screen.width, 320f);
            float height = Mathf.Max(Screen.height, 240f);

            bool any = false;
            for (int i = 0; i < _splashes.Length; i++)
            {
                bool show = Random.value <= 0.75f;
                ShowSplash(_splashes[i], width, height, show);
                any |= show;
            }
            if (!any && _splashes.Length > 0) ShowSplash(_splashes[0], width, height, true);
            if (_claw) _claw.GetComponent<RawImage>().enabled = false;

            StartFade(peak);

            if (_cameraShake == null) _cameraShake = Object.FindFirstObjectByType<CameraShake>();
            if (_cameraShake != null) _cameraShake.Shake();

            OnSplatter?.Invoke();
        }

        private static bool BloodAllowed()
        {
            if (!GameOptions.VfxEnabled) return false;   // 玩家在标题界面/选项里关了 VFX
            var tuning = LabBTuning.I;
            return tuning == null || !tuning.Enabled || tuning.screenBloodOn;
        }

        public void ClearBlood()
        {
            if (_timer <= 0f) return;
            _timer = 0f;
            if (_group) _group.alpha = 0f;
            HideAll();
            OnCleared?.Invoke();
        }

        private void Update()
        {
            if (_timer <= 0f) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                if (_group) _group.alpha = 0f;
                HideAll();
                OnCleared?.Invoke();
                return;
            }

            // 停留期保持峰值，之后线性淡出
            float fade = Mathf.Clamp01(_timer / Mathf.Max(fadeDuration, 0.01f));
            if (_group) _group.alpha = _peakAlpha * fade;
        }

        private void HideAll()
        {
            if (_claw) _claw.GetComponent<RawImage>().enabled = false;
            if (_splashes == null) return;
            for (int i = 0; i < _splashes.Length; i++)
                if (_splashes[i]) _splashes[i].GetComponent<RawImage>().enabled = false;
        }
    }
}
