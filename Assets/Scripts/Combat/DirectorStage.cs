using UnityEngine;
using YourMovieSucks.Input;
using YourMovieSucks.Typing;
using YourMovieSucks.UI;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 导演轨（Hype Director）：控场、控时、增益。纯正向，无扣分惩罚。
    /// 词条：NEON/LIGHT 打光、ZOOM 子弹时间、PAN 打断、SNAP/CUE/PRINT 抓拍、WIPE 手动擦镜。
    /// </summary>
    public class DirectorStage : MonoBehaviour
    {
        [SerializeField] private TypingController typing;
        [SerializeField] private StageLighting lighting;
        [SerializeField] private ScreenBlood blood;
        [SerializeField] private Camera targetCamera;

        [Header("Score")]
        [SerializeField] private int scorePerWord = 100;
        [SerializeField] private int moneyShotBonus = 250;

        [Header("Zoom (子弹时间)")]
        [SerializeField] private float zoomFov = 38f;
        [SerializeField] private float zoomDuration = 2.5f;
        [SerializeField] private float slowFactor = 0.3f; // 慢 70%

        [Header("Flash")]
        [SerializeField] private float flashDuration = 0.15f;

        [Tooltip("演出层用：关闭后不刷导演 track 词条、也不结算导演词条效果（Demo 开场演出期间）")]
        [SerializeField] private bool tricksEnabled = true;

        private static readonly string[] SnapshotWords = { "SNAP", "CUE", "PRINT" };

        private float _baseFov;
        private float _zoomTimer;
        private float _flashTimer;
        private TypingWord _snapshotWord;

        public int Score { get; private set; }
        public event System.Action<int> OnScoreChanged;

        /// <summary>演出层：开关导演 track 词条生成与效果（Demo 开场禁用以保证 ZOOM 取“过场推镜”语义）。</summary>
        public void SetTricksEnabled(bool enabled) => tricksEnabled = enabled;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null) _baseFov = targetCamera.fieldOfView;
        }

        private void OnEnable()
        {
            if (typing == null) typing = GetComponent<TypingController>();
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (typing == null)
            {
                Debug.LogError("[DirectorStage] no TypingController.");
                return;
            }

            typing.OnWordCompleted += HandleWordCompleted;
            typing.OnWordFailed += HandleWordFailed;
        }

        private void OnDisable()
        {
            if (typing != null)
            {
                typing.OnWordCompleted -= HandleWordCompleted;
                typing.OnWordFailed -= HandleWordFailed;
            }
        }

        private void Update()
        {
            if (tricksEnabled) UpdateTrickAvailability();
            UpdateZoom();
            UpdateFlash();
        }

        /// <summary>怪物击杀后开启 1.5s 抓拍窗口（Money Shot）。</summary>
        public void OpenSnapshotWindow()
        {
            if (typing == null || _snapshotWord != null) return;
            string word = SnapshotWords[Random.Range(0, SnapshotWords.Length)];
            _snapshotWord = typing.SpawnWord(word, CharacterTrack.Director, 1.5f);
        }

        private void UpdateTrickAvailability()
        {
            var targets = Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None);
            int alive = 0;
            bool anyAttacking = false;
            foreach (var t in targets)
            {
                if (t == null || t.IsDead) continue;
                alive++;
                if (t.IsAttacking) anyAttacking = true;
            }

            // ZOOM：场上 2 只及以上怪物时可用
            if (alive >= 2 && !DirectorWordActive("ZOOM"))
                typing.SpawnWord("ZOOM", CharacterTrack.Director, 6f);

            // PAN：有怪物进入攻击倒计时时可用
            if (anyAttacking && !DirectorWordActive("PAN"))
                typing.SpawnWord("PAN", CharacterTrack.Director, 2.5f);
        }

        private bool DirectorWordActive(string matchText)
        {
            foreach (var w in typing.Words)
            {
                if (w.Track == CharacterTrack.Director && w.MatchText == matchText)
                    return true;
            }
            return false;
        }

        private void HandleWordCompleted(TypingWord word)
        {
            if (word.Track != CharacterTrack.Director) return;
            if (!tricksEnabled) return;

            Score += scorePerWord;
            OnScoreChanged?.Invoke(Score);

            switch (word.MatchText)
            {
                case "NEON":
                case "LIGHT":
                    if (lighting == null) lighting = Object.FindFirstObjectByType<StageLighting>();
                    if (lighting != null) lighting.SetLit(true);
                    break;

                case "ZOOM":
                    ApplyZoom();
                    break;

                case "PAN":
                    ApplyPan();
                    break;

                case "WIPE":
                    if (blood == null) blood = Object.FindFirstObjectByType<ScreenBlood>();
                    if (blood != null) blood.ClearBlood();
                    break;

                case "SNAP":
                case "CUE":
                case "PRINT":
                    if (word == _snapshotWord)
                    {
                        _snapshotWord = null;
                        ApplyMoneyShot();
                    }
                    break;
            }
        }

        private void HandleWordFailed(TypingWord word, WordFailReason reason)
        {
            if (word.Track != CharacterTrack.Director) return;
            if (word == _snapshotWord) _snapshotWord = null;
        }

        private void ApplyZoom()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null) targetCamera.fieldOfView = zoomFov;

            _zoomTimer = zoomDuration;

            foreach (var t in Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None))
            {
                if (t != null && !t.IsDead)
                    t.Slow(zoomDuration, slowFactor);
            }
        }

        private void UpdateZoom()
        {
            if (_zoomTimer <= 0f) return;
            _zoomTimer -= Time.deltaTime;
            if (_zoomTimer <= 0f && targetCamera != null)
                targetCamera.fieldOfView = _baseFov;
        }

        private void ApplyPan()
        {
            foreach (var t in Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None))
            {
                if (t != null && !t.IsDead)
                    t.ResetAttack();
            }

            // 镜头横切震动
            var shake = Object.FindFirstObjectByType<CameraShake>();
            if (shake != null) shake.Shake(0.25f, 0.3f);
        }

        private void ApplyMoneyShot()
        {
            _flashTimer = flashDuration;
            Score += moneyShotBonus;
            OnScoreChanged?.Invoke(Score);

            foreach (var t in Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None))
            {
                if (t == null || t.IsDead || t.IsAttacking) continue;
                t.Stun(1f); // Flash Stun：逼近中的怪物定格 1s
            }
        }

        private void UpdateFlash()
        {
            if (_flashTimer > 0f) _flashTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (_flashTimer <= 0f) return;
            float alpha = Mathf.Clamp01(_flashTimer / flashDuration) * 0.9f;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
