using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// HP 显示：屏幕左上角大字号加粗数字 + 血条。
    /// </summary>
    public class HPUI : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;

        private void OnEnable()
        {
            if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        }

        private void OnGUI()
        {
            if (playerHealth == null) return;

            float hpPercent = playerHealth.HpPercent;
            float hp = playerHealth.CurrentHp;
            float maxHp = playerHealth.MaxHp;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            style.normal.textColor = hpPercent > 0.25f ? Color.white : Color.red;
            GUI.Label(new Rect(30, 18, 520, 52), $"HP {hp:0}/{maxHp:0}", style);

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(30, 80, 420, 40), Texture2D.whiteTexture);

            Color barColor = hpPercent > 0.5f ? Color.green : (hpPercent > 0.25f ? Color.yellow : Color.red);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(30, 80, 420 * hpPercent, 40), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
