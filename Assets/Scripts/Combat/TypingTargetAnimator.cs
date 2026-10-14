using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 怪物外观（骨骼动画版）：按 <see cref="TypingTarget"/> 的状态驱动 Animator：
    /// 站立 / 走 / 跑（被打错而加速时）/ 攻击 / 死亡。
    ///
    /// 位移仍由 TypingTarget 负责（Animator 不开 Apply Root Motion，动画片段也已把水平位移摘掉），
    /// 所以「看起来在走」和「实际移动速度/车道」是两件互不干扰的事。
    /// 灰盒方块没有 Animator 时该组件自动禁用，不影响老场景。
    /// </summary>
    [RequireComponent(typeof(TypingTarget))]
    public class TypingTargetAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("Animator 参数名")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string rushParameter = "Rush";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string deadTrigger = "Dead";

        [Header("Speed 取值")]
        [Tooltip("接近中")]
        [SerializeField] private float walkValue = 1f;
        [Tooltip("被打错后加速接近")]
        [SerializeField] private float runValue = 2f;

        [Tooltip("死亡动画开始后尸体再停留多久才销毁（秒）。0 = 立刻销毁（灰盒方块行为）；" +
                 "尸体不参与任何判定（TypingTarget 已死），只是留个倒地的画面")]
        [SerializeField] private float deathHold = 1.2f;

        private TypingTarget _target;
        private bool _dead;

        private int _speedId, _rushId, _attackId, _deadId;
        private bool _hasSpeed, _hasRush, _hasAttack, _hasDead;
        private float _appliedSpeed = float.NaN;
        private bool _appliedRush;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _target = GetComponent<TypingTarget>();
            _lastPosition = transform.position;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                enabled = false; // 灰盒方块：没有 Animator，本组件无事可做
                return;
            }

            // 位移是 TypingTarget 的职责，动画不接管根运动
            animator.applyRootMotion = false;

            _speedId = Animator.StringToHash(speedParameter);
            _rushId = Animator.StringToHash(rushParameter);
            _attackId = Animator.StringToHash(attackTrigger);
            _deadId = Animator.StringToHash(deadTrigger);

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == speedParameter) _hasSpeed = true;
                else if (parameter.name == rushParameter) _hasRush = true;
                else if (parameter.name == attackTrigger) _hasAttack = true;
                else if (parameter.name == deadTrigger) _hasDead = true;
            }

            if (deathHold > 0f) _target.SetDeathLinger(deathHold);

            _target.OnAttackStart += HandleAttackStart;
            _target.OnAttackHit += HandleAttackHit;
            _target.OnDeath += HandleDeath;
        }

        private void OnDestroy()
        {
            if (_target == null) return;
            _target.OnAttackStart -= HandleAttackStart;
            _target.OnAttackHit -= HandleAttackHit;
            _target.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (_dead || animator == null) return;

            // 动画跟着“实际有没有在动”走，而不仅仅看状态：
            // 站住不动 → Idle（包括贴脸站桩、定格演出），真的在移动 → Walk / Run。
            // （以前只看状态，怪因为镜头推进而侧滑时会一边滑一边摆站立姿势）
            Vector3 position = transform.position;
            bool moving = Vector3.Distance(position, _lastPosition) > Time.deltaTime * 0.05f;
            _lastPosition = position;

            bool rushing = _target.State == TargetState.Rushing;
            float speed = !moving ? 0f : (rushing ? runValue : walkValue);

            if (_hasSpeed && !Mathf.Approximately(speed, _appliedSpeed))
            {
                animator.SetFloat(_speedId, speed);
                _appliedSpeed = speed;
            }

            if (_hasRush && rushing != _appliedRush)
            {
                animator.SetBool(_rushId, rushing);
                _appliedRush = rushing;
            }
        }

        private void HandleAttackStart()
        {
            if (_dead || !_hasAttack) return;
            animator.SetTrigger(_attackId);
        }

        /// <summary>
        /// 每一轮攻击都重放一次撕咬动作。伤害是每 attackCountdown（1.5 s）结算一次，
        /// 而 Attack 片段长 2.6 s，只在 StartAttack 时触发的话就只有第一下才有动作，后面看着“站着掉血”。
        /// 触发器打在结算瞬间 → 伤害落在挥击过程中，手慼也更对。
        /// </summary>
        private void HandleAttackHit()
        {
            if (_dead || !_hasAttack) return;
            animator.SetTrigger(_attackId);
        }

        private void HandleDeath()
        {
            _dead = true;
            if (_hasDead) animator.SetTrigger(_deadId);
        }
    }
}
