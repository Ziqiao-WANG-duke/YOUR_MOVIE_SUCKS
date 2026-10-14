using UnityEngine;
using UnityEngine.UI;
using YourMovieSucks.LabB;
using YourMovieSucks.UI;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 血浆层：相机前置 WorldSpace 画布，只遮场景不遮词条；爆浆后自动渐隐。
    /// </summary>
    public class ScreenBlood : MonoBehaviour
    {
        [SerializeField] private float splatterAlpha = 0.5f;
        [SerializeField] private float fadeDuration = 1.2f;

        private RawImage _image;
        private CameraShake _cameraShake;
        private float _fadeTimer;

        public bool IsBloody => _fadeTimer > 0f;
        public event System.Action OnSplatter;
        public event System.Action OnCleared;

        private void Awake()
        {
            EnsureOverlay();
        }

        private void EnsureOverlay()
        {
            if (_image != null) return;

            var cam = Camera.main;
            if (cam == null) return;

            var go = new GameObject("BloodOverlay", typeof(RectTransform), typeof(Canvas), typeof(RawImage));
            go.transform.SetParent(cam.transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10; // 低于头顶词条(20)，高于场景

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1920f, 1080f);
            rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            rt.localPosition = new Vector3(0f, 0f, 2f);
            rt.localRotation = Quaternion.identity;

            _image = go.GetComponent<RawImage>();
            _image.texture = Texture2D.whiteTexture;
            _image.color = new Color(1f, 0f, 0f, 0f);
            _image.raycastTarget = false;
        }

        public void Splatter()
        {
            var tuning = LabBTuning.I;
            if (tuning != null && tuning.Enabled && !tuning.screenBloodOn) return;

            EnsureOverlay();
            _fadeTimer = fadeDuration;
            SetAlpha(tuning != null && tuning.Enabled ? tuning.splatterAlpha : splatterAlpha);

            if (_cameraShake == null) _cameraShake = Object.FindFirstObjectByType<CameraShake>();
            if (_cameraShake != null) _cameraShake.Shake();

            OnSplatter?.Invoke();
        }

        public void ClearBlood()
        {
            if (_fadeTimer <= 0f) return;
            _fadeTimer = 0f;
            SetAlpha(0f);
            OnCleared?.Invoke();
        }

        private void Update()
        {
            if (_fadeTimer <= 0f) return;

            _fadeTimer -= Time.deltaTime;
            if (_fadeTimer <= 0f)
            {
                SetAlpha(0f);
                OnCleared?.Invoke();
                return;
            }

            float t = _fadeTimer / fadeDuration;
            SetAlpha(Mathf.Lerp(0f, splatterAlpha, t));
        }

        private void SetAlpha(float a)
        {
            if (_image == null) return;
            var c = _image.color;
            c.a = a;
            _image.color = c;
        }
    }
}
