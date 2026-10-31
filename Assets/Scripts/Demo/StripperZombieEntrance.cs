using System.Collections;
using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 敌人入场演出：从侧幕冲入舞台 → 站定咆哮（Scream 动画）→ 恢复常规接近/攻击行为。
    /// 对应脚本第 5 段「STRIPPER ZOMBIE 冲入舞台」；原来抱杆的节拍已改为丧尸咆哮。
    /// </summary>
    [RequireComponent(typeof(TypingTarget))]
    public class StripperZombieEntrance : MonoBehaviour
    {
        [SerializeField] private Transform poleAnchor;

        [Tooltip("冲入舞台的耗时")]
        [SerializeField] private float dashDuration = 0.45f;

        [Tooltip("咆哮定格时长（之后恢复常规行为）")]
        [SerializeField] private float hugHold = 2.5f;

        [Tooltip("咆哮时的轻微颤栗幅度（0 = 完全不动）")]
        [SerializeField] private float hugShake = 0.02f;

        [Tooltip("咆哮动画状态名（AnimatorController 里的状态）")]
        [SerializeField] private string screamStateName = "Scream";

        private TypingTarget _target;
        private Animator _animator;
        private Vector3 _hugPosition;

        private void Awake()
        {
            _target = GetComponent<TypingTarget>();
            _animator = GetComponentInChildren<Animator>();
            if (_target != null) _target.Frozen = true; // 入场演出期间不动不攻击
        }

        public void Init(Transform pole, float dash, float hold)
        {
            if (pole != null) poleAnchor = pole;
            dashDuration = Mathf.Max(dash, 0.01f);
            hugHold = Mathf.Max(hold, 0f);
        }

        private void Start()
        {
            _hugPosition = poleAnchor != null
                ? poleAnchor.position
                : transform.position + Vector3.forward * 3f;

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 1) 冲入舞台
            Vector3 from = transform.position;
            float t = 0f;
            while (t < dashDuration)
            {
                if (_target != null && _target.IsDead) yield break;

                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dashDuration));
                transform.position = Vector3.Lerp(from, _hugPosition, k);
                yield return null;
            }

            // 2) 站定咆哮（带轻微颤栗），播完自动回 Idle
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.Play(screamStateName, 0, 0f);
                _animator.Update(0f); // 立刻生效，避免下一帧才切
            }

            float held = 0f;
            while (held < hugHold)
            {
                if (_target != null && _target.IsDead) yield break;

                held += Time.deltaTime;
                float shake = hugShake <= 0f ? 0f : Mathf.Sin(Time.time * 24f) * hugShake;
                transform.position = _hugPosition + new Vector3(shake, 0f, 0f);
                yield return null;
            }

            // 3) 结束演出：恢复常规接近/攻击
            if (_target != null) _target.Frozen = false;
            enabled = false;
        }
    }
}
