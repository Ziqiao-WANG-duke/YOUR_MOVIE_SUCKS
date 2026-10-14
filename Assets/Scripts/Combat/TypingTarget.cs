using UnityEngine;
using YourMovieSucks.LabB;

namespace YourMovieSucks.Combat
{
    public enum TargetState
    {
        Approaching, // 缓慢靠近
        Rushing,     // 加速靠近
        Attacking,   // 到达，攻击倒计时
        Dead
    }

    /// <summary>
    /// 怪物：缓慢靠近玩家，到达后请求攻击（一次一只），1.5s 倒计时后造成伤害。
    /// 无血量：逐字母仅做视觉进度，打完词由 AgentShooter 调用 Kill() 直接击杀。
    /// </summary>
    public class TypingTarget : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float baseSpeed = 0.5f;
        [SerializeField] private float rushSpeed = 1.6f;
        [SerializeField] private float attackRange = 4f;
        [SerializeField] private float attackCountdown = 1.5f;
        [SerializeField] private float attackDamage = 35f;

        [Tooltip("接近/攻击位置相对玩家的横向车道偏移（沿相机右方向，世界单位）。\n" +
                 "不同怪走不同车道，头顶词条才不会横向叠在一起；0 = 正对相机。")]
        [SerializeField] private float laneOffset;

        [Header("近身收敛（可选，默认关）")]
        [Tooltip("怪物离相机的**前向距离**小于这个值后，开始把车道往中间收。\n" +
                 "为什么要收：车道 ±2.6 在远处能让词条分开，但近身（1–2 m）时横向 2.6 m 已经出了视野，\n" +
                 "怪会停在画面外打你。<=0 = 关闭（灰盒方块行为）")]
        [SerializeField] private float laneConvergeStart;

        [Tooltip("收到攻击距离时的车道横向偏移绝对值（配合 laneConvergeStart 生效）。0 = 收到正中")]
        [SerializeField] private float closeLaneOffset;

        [Header("外观反馈（缩放）")]
        [Tooltip("被打错字母时整体放大多少倍。灰盒方块靠它表达“它冲过来了”；骨骼模型有奔跑动画 + 橘色染色，设 1 才不会在“由 Rushing 转成 Attacking”时突然缩回原尺寸（那个回缩会表现为模型突然变小/下移）")]
        [SerializeField] private float rushScaleMultiplier = 1.3f;

        [Tooltip("每次攻击结算时“前扑”的放大倍数（0.1 秒脉冲）。骨骼模型靠攻击动画表现，可设 1")]
        [SerializeField] private float attackLungeScaleMultiplier = 1.5f;

        [Tooltip("Kill() 后延迟销毁（秒）：0.05 = 几乎立即（灰盒方块），丧尸用它留出死亡动画的时间")]
        [SerializeField] private float deathLinger = 0.05f;

        /// <summary>已到达攻击站位：站住不动（镜头推进不再让它倒退）。</summary>
        private bool _arrived;

        /// <summary>
        /// 当前“持久”的整体缩放（被打错后放大，之后一直保持）。
        /// 不能再用“当前是不是 Rushing 状态”去重算：怪进攻击态后状态会变，重算就会把它从 1.3 缩回 1.0，
        /// 表现为第一次攻击后模型突然变小/下移。
        /// </summary>
        private float _rushScaleFactor = 1f;

        /// <summary>命中闪光的颜色：>1 会提亮贴图，贴了贴图的模型才看得出「打中了一下」。</summary>
        private static readonly Color HitFlashColor = new Color(2.5f, 2.5f, 2.5f);

        private Renderer[] _renderers;
        private Color _tintColor = Color.white;
        private Transform _player;
        private MonsterAttackCoordinator _coordinator;
        private PlayerHealth _playerHealth;
        private float _attackTimer;
        private Vector3 _baseScale;

        // 导演轨控场状态（不扣血不删字母）
        private float _stunTimer;
        private float _slowTimer;
        private float _slowMultiplier = 1f;

        public bool IsDead => State == TargetState.Dead;
        public TargetState State { get; private set; } = TargetState.Approaching;
        public bool IsAttacking => State == TargetState.Attacking;

        /// <summary>演出定格：入场/抱杆演出期间由演出层置位，怪物不走不动不攻击（仍可被打字击杀）。</summary>
        public bool Frozen { get; set; }

        public event System.Action OnDeath;
        public event System.Action OnAttackHit;
        public event System.Action OnAttackStart;

        private void Awake()
        {
            _baseScale = transform.localScale;
            ApplyTuning();

            // 怪物可能是「根上就是 Renderer」的灰盒方块，也可能是骨骼模型（Renderer 在子物体上）
            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _tintColor = _renderers[i].material.color; // .material = 每只怪一份实例，才能各自染色
                break;
            }

            _player = Camera.main != null ? Camera.main.transform : null;
            _coordinator = Object.FindFirstObjectByType<MonsterAttackCoordinator>();
            _playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        }

        private void Update()
        {
            if (IsDead || _player == null || Frozen) return;

            // 控场计时
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f) _slowMultiplier = 1f;
            }

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                return; // 定格：不动、不攻击、攻击倒计时暂停
            }

            if (State == TargetState.Attacking)
            {
                _attackTimer -= Time.deltaTime * _slowMultiplier;
                if (_attackTimer <= 0f)
                {
                    OnAttackHit?.Invoke();
                    if (_playerHealth != null) _playerHealth.TakeDamage(attackDamage);
                    _attackTimer = attackCountdown; // 继续下一轮攻击
                    AttackLunge();
                }
                return;
            }

            // 接近目标 = “攻击环”上的站位点：离相机 attackRange、沿相机右方向偏移 lane。
            // 为什么不沿用“相机平面上的车道点、走到离它 attackRange 就停”：那样怪是追一个**会跟着收窄的目标**，
            // 最后一段是斜着追上去的，停下时会留下最大一个车道的横向残差（实测 1.84 m ≈ 53°，怪贴在画面边上）。
            Vector3 approach = ResolveAttackPosition();
            approach.y = transform.position.y;

            float dist = Vector3.Distance(transform.position, approach);
            float cameraDistance = Vector3.Distance(transform.position, _player.position);

            // 到站就“站住”，不再追着镜头跑：攻击环是跟着相机走的，镜头往前推（战斗慢推 WP1→WP2）
            // 时环会跟着前移 1.2 m，怪只能倒着退——那才是“已经贴脸了又突然回退”的原因，
            // 而它此时在攻击态、播的是站立动画，看起来就像卡在姿势里倒滑。
            if (!_arrived && dist <= ArrivalRadius) _arrived = true;
            if (_arrived && cameraDistance > attackRange * ReacquireRangeFactor) _arrived = false; // 机位/波次推进很远后才重新接近

            if (_arrived)
            {
                // 站住不动：镜头推进、攻击环前移都不再让它动（离得超过 attackRange×3 才重新接近）
                RequestAttack();
                return;
            }

            float speed = (State == TargetState.Rushing ? rushSpeed : baseSpeed) * _slowMultiplier;
            transform.position = Vector3.MoveTowards(transform.position, approach, speed * Time.deltaTime);
        }

        /// <summary>相机离得超过攻击距离的这个倍数后，认为“机位换了”：解除到站状态重新接近。</summary>
        private const float ReacquireRangeFactor = 3f;

        /// <summary>到站判定半径（米）：够小才不会停在离相机很远的地方，够大才不会在站位点抖。</summary>
        private float ArrivalRadius => Mathf.Max(0.1f, attackRange * 0.12f);

        /// <summary>攻击环上的站位点：距相机 attackRange、横向 lane（y 由调用方决定）。</summary>
        private Vector3 ResolveAttackPosition()
        {
            Vector3 forward = _player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;

            float lane = ResolveLane();
            float ahead = Mathf.Sqrt(Mathf.Max(attackRange * attackRange - lane * lane, 0.01f));

            return _player.position + forward * ahead + _player.right * lane;
        }

        /// <summary>演出/编排层：指定本怪守住的横向车道（沿相机右方向，世界单位）。</summary>
        public void SetLane(float offset) => laneOffset = offset;

        /// <summary>
        /// 当前生效的车道偏移：远处用 laneOffset（词条分开），进入 laneConvergeStart 后按前向距离
        /// 线性收到 closeLaneOffset。用前向距离而不是实际距离，避免“收了车道→距离变近→又收更多”的自激。
        /// </summary>
        private float ResolveLane()
        {
            if (laneConvergeStart <= attackRange || _player == null) return laneOffset;

            Vector3 forward = _player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return laneOffset;
            forward.Normalize();

            float ahead = Vector3.Dot(transform.position - _player.position, forward);
            float t = Mathf.Clamp01(Mathf.InverseLerp(laneConvergeStart, attackRange, ahead));
            if (t <= 0f) return laneOffset;

            float side = laneOffset > 0f ? 1f : (laneOffset < 0f ? -1f : 0f); // Mathf.Sign(0)=1，会正中怪自己跑偏
            return Mathf.Lerp(laneOffset, side * closeLaneOffset, t);
        }

        /// <summary>编排/外观层：指定击杀后延迟多久销毁（丧尸死亡动画用）。</summary>
        public void SetDeathLinger(float seconds) => deathLinger = Mathf.Max(0f, seconds);

        /// <summary>加速靠近（打错触发）。</summary>
        public void Rush()
        {
            if (IsDead || State == TargetState.Attacking) return;
            State = TargetState.Rushing;

            _tintColor = new Color(1f, 0.4f, 0f);
            ApplyTint(_tintColor);
            _rushScaleFactor = rushScaleMultiplier;
            transform.localScale = _baseScale * _rushScaleFactor;
        }

        /// <summary>打完词直接击杀（无血量）。</summary>
        public void Kill()
        {
            if (IsDead) return;
            State = TargetState.Dead;
            if (_coordinator != null) _coordinator.ReleaseAttack(this);
            OnDeath?.Invoke();
            Destroy(gameObject, Mathf.Max(0f, deathLinger));
        }

        /// <summary>逐字母命中反馈：提亮一下，不造成伤害。</summary>
        public void Hit()
        {
            if (IsDead || _renderers == null || _renderers.Length == 0) return;
            ApplyTint(HitFlashColor);
            CancelInvoke(nameof(RestoreColor));
            Invoke(nameof(RestoreColor), 0.06f);
        }

        /// <summary>定格震慑：不动、不攻击、攻击倒计时暂停。</summary>
        public void Stun(float duration)
        {
            if (IsDead) return;
            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        /// <summary>慢动作：移动与攻击倒计时乘以 factor（如 0.3 = 慢 70%）。</summary>
        public void Slow(float duration, float factor)
        {
            if (IsDead) return;
            _slowTimer = duration;
            _slowMultiplier = factor;
        }

        /// <summary>重置攻击倒计时并后仰小硬直（PAN）。</summary>
        public void ResetAttack()
        {
            if (IsDead || State != TargetState.Attacking) return;
            _attackTimer = attackCountdown;
            Stun(0.25f);
            AttackLunge();
        }

        public void StartAttack()
        {
            if (State == TargetState.Attacking || IsDead) return;
            ApplyTuning();
            State = TargetState.Attacking;
            _attackTimer = attackCountdown;
            OnAttackStart?.Invoke();
        }

        private void AttackLunge()
        {
            // 攻击前扑：瞬间放大再恢复（叠在持久的 Rush 缩放上）
            transform.localScale = _baseScale * _rushScaleFactor * attackLungeScaleMultiplier;
            CancelInvoke(nameof(RestoreScale));
            Invoke(nameof(RestoreScale), 0.1f);
        }

        private void RestoreScale()
        {
            if (IsDead) return;
            transform.localScale = _baseScale * _rushScaleFactor;
        }

        private void RequestAttack()
        {
            if (_coordinator == null) return;
            if (_coordinator.RequestAttack(this))
            {
                StartAttack();
            }
        }

        private void RestoreColor()
        {
            ApplyTint(_tintColor);
        }

        /// <summary>给所有 Renderer 染色（同一只怪可能有多个 Renderer）。</summary>
        private void ApplyTint(Color color)
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].material.color = color;
            }
        }

        /// <summary>Lab B 调参面板生效时，覆盖序列化默认值（面板关闭则用场景里的值）。</summary>
        private void ApplyTuning()
        {
            var tuning = LabBTuning.I;
            if (tuning == null || !tuning.Enabled) return;

            baseSpeed = tuning.approachSpeed;
            rushSpeed = tuning.rushSpeed;
            attackRange = tuning.attackRange;
            attackCountdown = tuning.attackCountdown;
            attackDamage = tuning.attackDamage;

            // 面板上能实时调的外观/站位参数（作业要求：Lab B 期间调过的每个数字都要在面板上）
            deathLinger = tuning.enemyDeathHold;
            rushScaleMultiplier = tuning.rushScaleMultiplier;
            attackLungeScaleMultiplier = tuning.attackLungeScaleMultiplier;
            laneConvergeStart = tuning.laneConvergeStart;
            closeLaneOffset = tuning.closeLaneOffset;
        }
    }
}
