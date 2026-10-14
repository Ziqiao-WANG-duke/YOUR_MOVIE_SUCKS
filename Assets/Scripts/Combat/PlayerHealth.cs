using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 玩家血量：怪物攻击造成伤害，归零失败。
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;

        private float _hp;

        public float HpPercent => Mathf.Clamp01(_hp / maxHp);
        public float CurrentHp => _hp;
        public float MaxHp => maxHp;
        public bool IsDead { get; private set; }

        public event System.Action OnDamaged;
        public event System.Action OnDeath;

        private void Awake()
        {
            _hp = maxHp;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            _hp -= damage;
            OnDamaged?.Invoke();
            if (_hp <= 0f)
            {
                IsDead = true;
                OnDeath?.Invoke();
            }
        }
    }
}
