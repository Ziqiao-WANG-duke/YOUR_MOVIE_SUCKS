using UnityEngine;
using YourMovieSucks.LabB;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 镜头晃动：受击时调用 Shake()，短暂随机偏移后复位。
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float magnitude = 0.15f;
        [SerializeField] private float duration = 0.25f;

        private Vector3 _appliedOffset;
        private float _timer;
        private float _activeMagnitude;
        private float _activeDuration;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;
        }

        public void Shake()
        {
            Shake(magnitude, duration);
        }

        public void Shake(float magnitude, float duration)
        {
            if (targetCamera == null) return;

            // F1 面板的 juice 开关（作业要求：加了的 juice 必须能关）
            var tuning = LabBTuning.I;
            if (tuning != null && tuning.Enabled)
            {
                if (!tuning.screenShakeOn) return;
                magnitude *= tuning.shakeStrength;
            }

            _activeMagnitude = magnitude;
            _activeDuration = Mathf.Max(duration, 0.001f);
            _timer = _activeDuration;
        }

        private void OnDisable()
        {
            // 别把偏移留在相机上
            if (targetCamera != null) targetCamera.transform.position -= _appliedOffset;
            _appliedOffset = Vector3.zero;
            _timer = 0f;
        }

        /// <summary>
        /// 相机位置的所有权归轨道镜头（RailCameraController），这里只施加**临时偏移**：
        /// 每帧先把上一帧的偏移撤掉再加新的，且**永不写回捕获的基准位置**。
        /// 以前是“晃开始时记下基准位置 → 期间写基准+偏移 → 结束写回基准”：连续击键/连续受击时
        /// 基准只在空档（>duration 不晃）才刷新，一旦基准过期，晃动结束那一瞬会把相机拽回旧位置，
        /// 整个画面（包括已经贴脸的怪）会“突然位置回退”。
        /// 放在 LateUpdate，保证在轨道镜头写完位置之后施加。
        /// </summary>
        private void LateUpdate()
        {
            if (targetCamera == null) return;

            targetCamera.transform.position -= _appliedOffset;
            _appliedOffset = Vector3.zero;

            if (_timer <= 0f) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f) return;

            float strength = _activeMagnitude * (_timer / _activeDuration);
            _appliedOffset = Random.insideUnitSphere * strength;
            targetCamera.transform.position += _appliedOffset;
        }
    }
}
