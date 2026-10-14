using System.Collections;
using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 敌人入场演出：从侧幕冲入舞台 → 抱住钢管（定格）→ 松开后回到常规接近/攻击行为。
    /// 对应脚本第 5 段「STRIPPER ZOMBIE 冲入舞台，抢先一步抱住钢管」。
    /// </summary>
    [RequireComponent(typeof(TypingTarget))]
    public class StripperZombieEntrance : MonoBehaviour
    {
        [SerializeField] private Transform poleAnchor;

        [Tooltip("冲入舞台的耗时")]
        [SerializeField] private float dashDuration = 0.45f;

        [Tooltip("抱住钢管的定格时长（之后松手恢复常规行为）")]
        [SerializeField] private float hugHold = 2.5f;

        [Tooltip("抱杆抖动幅度")]
        [SerializeField] private float hugShake = 0.05f;

        private TypingTarget _target;
        private Vector3 _hugPosition;

        private void Awake()
        {
            _target = GetComponent<TypingTarget>();
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

            // 2) 抱住钢管定格（带轻微抖动）
            float held = 0f;
            while (held < hugHold)
            {
                if (_target != null && _target.IsDead) yield break;

                held += Time.deltaTime;
                transform.position = _hugPosition + new Vector3(
                    Mathf.Sin(Time.time * 26f) * hugShake,
                    Mathf.Abs(Mathf.Sin(Time.time * 14f)) * hugShake,
                    0f);
                yield return null;
            }

            // 3) 松手：恢复常规接近/攻击
            if (_target != null) _target.Frozen = false;
            enabled = false;
        }
    }
}
