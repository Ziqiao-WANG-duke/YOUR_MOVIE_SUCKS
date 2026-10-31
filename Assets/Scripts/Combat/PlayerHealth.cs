using UnityEngine;
using YourMovieSucks.LabB;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 玩家血量：按「血格」计算（默认 9 格，与血条贴图的 9 格胶片一致）。怪物每命中一次扣 1 格，
    /// 特定怪物（预制件 attackCells = 2）扣 2 格；归零失败。
    /// 旧的百分比接口（HpPercent / CurrentHp / MaxHp）保留给日志与验收工具，数值现在就是格数。
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Tooltip("总血格数（与生命值 HUD 的格子数一致）")]
        [Min(1)] [SerializeField] private int maxCells = 9;

        private int _cells;

        public int MaxCells => maxCells;
        public int CurrentCells => _cells;

        /// <summary>剩余比例（0..1）：日志、旧 UI、验收工具仍在用。</summary>
        public float HpPercent => maxCells > 0 ? Mathf.Clamp01((float)_cells / maxCells) : 0f;
        /// <summary>兼容旧接口：现在返回当前格数。</summary>
        public float CurrentHp => _cells;
        /// <summary>兼容旧接口：现在返回总格数。</summary>
        public float MaxHp => maxCells;
        public bool IsDead { get; private set; }

        public event System.Action OnDamaged;
        public event System.Action OnDeath;
        /// <summary>血量变化（参数：当前格数 / 总格数），HUD 用它刷新。</summary>
        public event System.Action<int, int> OnCellsChanged;

        private void Awake()
        {
            var tuning = LabBTuning.I;
            if (tuning != null && tuning.Enabled && tuning.playerMaxCells > 0)
                maxCells = Mathf.Max(1, Mathf.RoundToInt(tuning.playerMaxCells));

            _cells = maxCells;
            OnCellsChanged?.Invoke(_cells, maxCells);
        }

        /// <summary>扣血（单位：格）。不足 1 按 1 算；浮点参数保留给旧调用点。</summary>
        public void TakeDamage(float cells)
        {
            TakeCells(Mathf.Max(1, Mathf.RoundToInt(cells)));
        }

        public void TakeCells(int cells)
        {
            if (IsDead || cells <= 0) return;

            _cells = Mathf.Max(0, _cells - cells);
            OnDamaged?.Invoke();
            OnCellsChanged?.Invoke(_cells, maxCells);

            if (_cells <= 0)
            {
                IsDead = true;
                OnDeath?.Invoke();
            }
        }

        /// <summary>回血（格）：后续加分/治疗道具可直接用。</summary>
        public void Heal(int cells)
        {
            if (IsDead || cells <= 0) return;
            _cells = Mathf.Min(maxCells, _cells + cells);
            OnCellsChanged?.Invoke(_cells, maxCells);
        }

        /// <summary>重开一局时重置。</summary>
        public void ResetCells()
        {
            IsDead = false;
            _cells = maxCells;
            OnCellsChanged?.Invoke(_cells, maxCells);
        }
    }
}
