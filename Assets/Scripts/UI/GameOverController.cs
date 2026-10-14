using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YourMovieSucks.Combat;

namespace YourMovieSucks.UI
{
    /// <summary>
    /// 玩家死亡失败流程：HP 归零 → 暂停游戏 + 显示失败画面 → 按 R 重载场景重试。
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;

        private bool _gameOver;

        private void OnEnable()
        {
            if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null) playerHealth.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (!_gameOver) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Retry();
            }
        }

        private void HandleDeath()
        {
            if (_gameOver) return;
            _gameOver = true;
            Time.timeScale = 0f;
            BuildGameOverUI();
        }

        private void Retry()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void BuildGameOverUI()
        {
            var canvasGo = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var overlayGo = new GameObject("Overlay", typeof(Image));
            overlayGo.transform.SetParent(canvasGo.transform, false);
            var overlay = overlayGo.GetComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.75f);
            var oRect = (RectTransform)overlayGo.transform;
            oRect.anchorMin = Vector2.zero;
            oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero;
            oRect.offsetMax = Vector2.zero;

            CreateText(canvasGo.transform, font, "YOU DIED", 72, Color.red, new Vector2(0f, 60f), new Vector2(600f, 120f));
            CreateText(canvasGo.transform, font, "PRESS R TO RETRY", 32, Color.white, new Vector2(0f, -40f), new Vector2(600f, 60f));
        }

        private static void CreateText(Transform parent, Font font, string content, int size, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text_" + content, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPos;
        }
    }
}
