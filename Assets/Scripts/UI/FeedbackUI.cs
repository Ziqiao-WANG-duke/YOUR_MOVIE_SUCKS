using UnityEngine;
using UnityEngine.InputSystem;
using YourMovieSucks.Combat;
using YourMovieSucks.Typing;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 游戏反馈：打错显示 WRONG! 文字、受击屏幕红闪。
    /// </summary>
    public class FeedbackUI : MonoBehaviour
    {
        [SerializeField] private TypingController typing;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private CameraShake cameraShake;

        private string _flashText;
        private Color _flashColor = Color.white;
        private float _flashTimer;
        private float _damageFlashTimer;

        private void OnEnable()
        {
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
            if (typing != null) typing.OnLetterMismatch += HandleMismatch;
            if (playerHealth != null) playerHealth.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (typing != null) typing.OnLetterMismatch -= HandleMismatch;
            if (playerHealth != null) playerHealth.OnDamaged -= HandleDamaged;
        }

        private void HandleMismatch(TypingWord word, Key key)
        {
            _flashText = "WRONG!";
            _flashColor = new Color(1f, 0.2f, 0.1f);
            _flashTimer = 0.5f;
        }

        private void HandleDamaged()
        {
            _damageFlashTimer = 0.35f;
            if (cameraShake == null) cameraShake = Object.FindFirstObjectByType<CameraShake>();
            if (cameraShake != null) cameraShake.Shake();
        }

        private void Update()
        {
            if (_flashTimer > 0f) _flashTimer -= Time.deltaTime;
            if (_damageFlashTimer > 0f) _damageFlashTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            // 受击红闪（VFX 关闭时不画，避免“关了还有效果”）
            if (_damageFlashTimer > 0f && GameOptions.VfxEnabled)
            {
                float alpha = Mathf.Clamp01(_damageFlashTimer / 0.35f) * 0.5f;
                var prev = GUI.color;
                GUI.color = new Color(1f, 0f, 0f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // WRONG! 提示
            if (_flashTimer > 0f)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 64,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                style.normal.textColor = _flashColor;
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 110), _flashText, style);
            }
        }
    }
}
