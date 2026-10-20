using System.Collections;
using UnityEngine;

namespace YourMovieSucks.Level
{
    /// <summary>
    /// 弱点特写：短暂接管相机 —— 快速推到怪物弱点、停一下、平滑回位。
    ///
    /// 相机位置的所有权属于轨道镜头（RailCameraController）：
    ///   · 只在它停稳（!IsMoving）时接管，正在推轨时直接放弃本次特写；
    ///   · 结束把相机恢复到接管前的精确 pose，**不写回任何“基准位置”**
    ///     （之前 CameraShake 抢所有权导致“突然位置回退”，这里刻意避开同样的坑）；
    ///   · 保持期间若轨道镜头开始推进，立刻提前结束让位。
    /// </summary>
    public class WeakPointCloseUpCamera : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RailCameraController rail;

        [Tooltip("机位到弱点的距离（米）")]
        [SerializeField] private float distance = 0.9f;

        [Tooltip("抬高一点，避免正对骨骼球心")]
        [SerializeField] private float heightBias = 0.12f;

        [SerializeField] private float pushDuration = 0.18f;
        [SerializeField] private float holdDuration = 0.5f;
        [SerializeField] private float returnDuration = 0.28f;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (rail == null) rail = Object.FindFirstObjectByType<RailCameraController>();
        }

        /// <summary>
        /// 播一次弱点特写；轨道镜头正在推进或已在播时放弃本次特写（放弃时也会回调 onFinished）。
        /// onFinished 一定会被回调一次 —— 调用方拿它恢复“特写期间定格”的怪物状态，漏调就会把怪定死。
        /// </summary>
        public void Play(Transform focus, System.Action onFinished = null)
        {
            if (IsPlaying || focus == null || targetCamera == null)
            {
                onFinished?.Invoke();
                return;
            }

            // 只有“启用中的轨道镜头正在推进”才算冲突；它被禁用时不会写位置，可以安全接管
            if (RailOwnsCamera())
            {
                onFinished?.Invoke();
                return;
            }

            StartCoroutine(Run(focus, onFinished));
        }

        private bool RailOwnsCamera()
            => rail != null && rail.isActiveAndEnabled && rail.IsMoving;

        private IEnumerator Run(Transform focus, System.Action onFinished)
        {
            IsPlaying = true;

            Transform cameraTransform = targetCamera.transform;
            Vector3 fromPosition = cameraTransform.position;
            Quaternion fromRotation = cameraTransform.rotation;

            Vector3 focusPosition = focus.position;

            Vector3 away = fromPosition - focusPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -cameraTransform.forward;
            away.Normalize();

            Vector3 toPosition = focusPosition + away * distance + Vector3.up * heightBias;
            Quaternion toRotation = Quaternion.LookRotation((focusPosition - toPosition).normalized, Vector3.up);

            float t = 0f;
            while (t < pushDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / pushDuration));
                cameraTransform.SetPositionAndRotation(
                    Vector3.Lerp(fromPosition, toPosition, k),
                    Quaternion.Slerp(fromRotation, toRotation, k));
                yield return null;
            }

            cameraTransform.SetPositionAndRotation(toPosition, toRotation);

            // 保持：轨道镜头一旦开始推进就提前让位
            t = 0f;
            while (t < holdDuration)
            {
                if (RailOwnsCamera()) break;
                t += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / returnDuration));
                cameraTransform.SetPositionAndRotation(
                    Vector3.Lerp(toPosition, fromPosition, k),
                    Quaternion.Slerp(toRotation, fromRotation, k));
                yield return null;
            }

            cameraTransform.SetPositionAndRotation(fromPosition, fromRotation);
            IsPlaying = false;

            onFinished?.Invoke();
        }
    }
}
