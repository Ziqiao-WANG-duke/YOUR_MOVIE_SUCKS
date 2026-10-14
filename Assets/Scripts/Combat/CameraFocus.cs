using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 镜头失焦（占位：FOV 拉大模拟）。导演蓝词超时触发，持续 blurDuration 后恢复。
    /// </summary>
    public class CameraFocus : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float blurFov = 72f;
        [SerializeField] private float blurDuration = 1.5f;

        private float _blurTimer;

        public bool IsBlurred => _blurTimer > 0f;
        public event System.Action OnBlurStart;
        public event System.Action OnBlurEnd;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null) baseFov = targetCamera.fieldOfView;
        }

        public void Blur()
        {
            _blurTimer = blurDuration;
            OnBlurStart?.Invoke();
        }

        private void Update()
        {
            if (targetCamera == null) return;

            if (_blurTimer > 0f)
            {
                _blurTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_blurTimer / blurDuration);
                targetCamera.fieldOfView = Mathf.Lerp(baseFov, blurFov, t);

                if (_blurTimer <= 0f)
                {
                    targetCamera.fieldOfView = baseFov;
                    OnBlurEnd?.Invoke();
                }
            }
        }
    }
}
