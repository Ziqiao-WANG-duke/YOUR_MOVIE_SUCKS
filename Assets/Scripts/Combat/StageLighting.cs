using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 舞台灯光：可切换点亮/熄灭，点亮状态供特工伤害增益查询。
    /// </summary>
    public class StageLighting : MonoBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private bool litAtStart = false;

        public bool IsLit { get; private set; }
        public event System.Action<bool> OnLitChanged;

        private void Awake()
        {
            if (targetLight == null) targetLight = GetComponent<Light>();
            SetLit(litAtStart);
        }

        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (targetLight != null) targetLight.enabled = lit;
            OnLitChanged?.Invoke(lit);
        }
    }
}
