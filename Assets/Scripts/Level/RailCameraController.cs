using UnityEngine;

namespace YourMovieSucks.Level
{
    /// <summary>
    /// 光枪式相机驱动：沿 DollyTrack 机位节点自动推进，位置/旋转平滑插值，
    /// 到达节点后停留（dwell），随后推进到下一节点。
    /// 战斗场景可将 autoAdvance 设为 false，由战斗完成事件调用 Advance()。
    /// </summary>
    public class RailCameraController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private DollyTrack track;

        [Header("Movement")]
        [SerializeField] private float moveDuration = 2f;
        [SerializeField] private float dwellDuration = 1f;
        [SerializeField] private bool autoAdvance = true;
        [SerializeField] private AnimationCurve moveEase;

        private int _fromIndex = -1;
        private int _toIndex;
        private float _t;
        private float _dwellTimer;
        private bool _dwelling;

        public bool IsMoving { get; private set; }
        public int CurrentIndex => _toIndex;
        public bool IsFinished { get; private set; }

        public event System.Action<int> OnWaypointReached;

        /// <summary>演出层：改推镜速度（开场 ZOOM 快推 vs 战斗中的慢推）。</summary>
        public void SetMoveDuration(float seconds) => moveDuration = Mathf.Max(seconds, 0.01f);

        /// <summary>演出层：改机位停留时长（dwell）。</summary>
        public void SetDwellDuration(float seconds) => dwellDuration = Mathf.Max(seconds, 0f);

        private void Start()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (moveEase == null || moveEase.keys.Length == 0)
                moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            if (track == null || track.Count == 0)
            {
                Debug.LogWarning("[RailCamera] no track or empty waypoints; stopped.");
                IsFinished = true;
                return;
            }

            BeginSegment(0);
        }

        private void Update()
        {
            if (IsFinished) return;

            if (_dwelling)
            {
                _dwellTimer -= Time.deltaTime;
                if (_dwellTimer <= 0f)
                {
                    _dwelling = false;
                    if (autoAdvance) Advance();
                }
                return;
            }

            if (IsMoving)
            {
                _t += Time.deltaTime / Mathf.Max(moveDuration, 0.001f);
                float eased = moveEase.Evaluate(Mathf.Clamp01(_t));
                ApplySegment(eased);

                if (_t >= 1f)
                {
                    Arrive();
                }
            }
        }

        /// <summary>推进到下一机位（战斗完成后可手动调用）。</summary>
        public void Advance()
        {
            if (IsFinished) return;

            int next = _toIndex + 1;
            if (next >= track.Count)
            {
                IsMoving = false;
                IsFinished = true;
                Debug.Log("[RailCamera] track finished.");
                return;
            }

            BeginSegment(next);
        }

        private void BeginSegment(int toIndex)
        {
            _fromIndex = toIndex - 1;
            _toIndex = toIndex;
            _t = 0f;
            _dwelling = false;   // 演出层可能在上一次停留未结束时就叫 Advance()
            IsMoving = true;
            IsFinished = false;

            // 首段：直接落到第一个机位并进入停留
            if (_fromIndex < 0)
            {
                ApplyWaypoint(_toIndex);
                Arrive();
            }
        }

        private void ApplySegment(float eased)
        {
            if (targetCamera == null) return;

            Transform from = _fromIndex >= 0 ? track.GetWaypoint(_fromIndex) : null;
            Transform to = track.GetWaypoint(_toIndex);
            if (to == null) return;

            if (from == null)
            {
                ApplyWaypoint(_toIndex);
                return;
            }

            targetCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(from.position, to.position, eased),
                Quaternion.Slerp(from.rotation, to.rotation, eased));
        }

        private void ApplyWaypoint(int index)
        {
            if (targetCamera == null) return;
            var w = track.GetWaypoint(index);
            if (w == null) return;
            targetCamera.transform.SetPositionAndRotation(w.position, w.rotation);
        }

        private void Arrive()
        {
            IsMoving = false;
            _dwelling = true;
            _dwellTimer = dwellDuration;
            ApplyWaypoint(_toIndex);
            OnWaypointReached?.Invoke(_toIndex);
        }
    }
}
