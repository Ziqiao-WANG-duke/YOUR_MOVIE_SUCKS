using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YourMovieSucks.Combat;
using YourMovieSucks.Demo;
using YourMovieSucks.Input;
using YourMovieSucks.Typing;

namespace YourMovieSucks.LabB
{
    /// <summary>
    /// Lab B 遥测：记录每局（时长/分数/失败方式与位置/击杀/重试）、轨道切换（含是否有效）与变体切换，
    /// 按 T 导出 JSON（无网络请求、无第三方库），并在角落显示一行摘要（局数 / 中位时长 / 重开率）。
    ///
    /// 设计目的：让 Prototype Card 的 Watch 数字全部可由遥测算出——
    ///   无提示自发切换率、首次成功切换耗时、误轨输入率、局间学习曲线、失败后 ≤2s 重开率。
    /// </summary>
    public class LabBTelemetry : MonoBehaviour
    {
        [Serializable]
        public class RunRecord
        {
            public int index;
            public float startTime;
            public float endTime;
            public float duration;
            public int score;
            public string cause;
            public string failWord;
            public string failTrack;
            public float failX;
            public float failY;
            public int wordsTyped;
            public int lettersTyped;
            public int mistypes;
            public int kills;
            public int accuracy;
            public int wpm;
        }

        [Serializable]
        public class EventRecord
        {
            public float t;
            public string type;
            public string detail;
        }

        [Serializable]
        public class SwitchRecord
        {
            public float t;
            public string from;
            public string to;
            public bool effective;
        }

        [Serializable]
        private class Payload
        {
            public string appVersion;
            public string scene;
            public string sessionStart;
            public List<RunRecord> runs = new List<RunRecord>();
            public List<EventRecord> events = new List<EventRecord>();
            public List<SwitchRecord> switches = new List<SwitchRecord>();
        }

        public static LabBTelemetry I { get; private set; }

        [SerializeField] private TypingController typing;
        [SerializeField] private TrackInput trackInput;
        [SerializeField] private RunStats stats;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private DirectorStage directorStage;
        [SerializeField] private bool showSummary = true;

        /// <summary>切换后多久内成功打出同轨字母，算作“有效切换”。</summary>
        public const float SwitchEffectiveWindow = 5f;

        private readonly Payload _payload = new Payload();
        private RunRecord _current;
        private SwitchRecord _pendingSwitch;
        private float _pendingSwitchDeadline = -1f;
        private float _awaitRetrySince = -1f;
        private string _failWord = string.Empty;
        private string _failTrack = string.Empty;
        private float _failX;
        private float _failY;
        private string _message;
        private float _messageTimer;

        public int RunCount => _payload.runs.Count;

        private void Awake()
        {
            I = this;
            _payload.appVersion = $"{Application.version} / Unity {Application.unityVersion}";
            _payload.scene = SceneManager.GetActiveScene().name;
            _payload.sessionStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Resolve();
        }

        private void OnEnable()
        {
            Resolve();

            if (stats != null)
            {
                stats.OnRunStarted += HandleRunStarted;
                stats.OnRunEnded += HandleRunEnded;
            }
            if (typing != null) typing.OnLetterMatched += HandleLetterMatched;
            if (trackInput != null) trackInput.OnTrackChanged += HandleTrackChanged;
            if (playerHealth != null) playerHealth.OnDeath += HandlePlayerDeath;
            if (LabBTuning.I != null) LabBTuning.I.OnVariantChanged += HandleVariantChanged;
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.OnRunStarted -= HandleRunStarted;
                stats.OnRunEnded -= HandleRunEnded;
            }
            if (typing != null) typing.OnLetterMatched -= HandleLetterMatched;
            if (trackInput != null) trackInput.OnTrackChanged -= HandleTrackChanged;
            if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDeath;
            if (LabBTuning.I != null) LabBTuning.I.OnVariantChanged -= HandleVariantChanged;
        }

        private void Resolve()
        {
            if (typing == null) typing = UnityEngine.Object.FindFirstObjectByType<TypingController>();
            if (trackInput == null) trackInput = UnityEngine.Object.FindFirstObjectByType<TrackInput>();
            if (stats == null) stats = UnityEngine.Object.FindFirstObjectByType<RunStats>();
            if (playerHealth == null) playerHealth = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
            if (directorStage == null) directorStage = UnityEngine.Object.FindFirstObjectByType<DirectorStage>();
        }

        private void Update()
        {
            if (_messageTimer > 0f) _messageTimer -= Time.unscaledDeltaTime;
            if (_pendingSwitchDeadline > 0f && Time.unscaledTime > _pendingSwitchDeadline)
            {
                _pendingSwitch = null;   // 窗口内没打出有效输入 → 保持 effective=false
                _pendingSwitchDeadline = -1f;
            }

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tKey.wasPressedThisFrame) Export();
        }

        // ---------- 记录入口（演出层可直接调用） ----------

        public void NoteEvent(string type, string detail = "")
            => _payload.events.Add(new EventRecord { t = Time.unscaledTime, type = type, detail = detail });

        public void NoteSkipIntro() => NoteEvent("skip_intro");

        private void HandleRunStarted()
        {
            if (_awaitRetrySince > 0f)
            {
                float latency = Time.unscaledTime - _awaitRetrySince;
                NoteEvent("retry", $"latency={latency:0.00}");
                _awaitRetrySince = -1f;
            }

            _failWord = string.Empty;
            _failTrack = string.Empty;

            _current = new RunRecord
            {
                index = stats != null ? stats.RunIndex : _payload.runs.Count + 1,
                startTime = Time.unscaledTime,
                score = CurrentScore()
            };

            NoteEvent("run_start", $"index={_current.index} variant={VariantName()}");
        }

        private void HandleRunEnded(RunEndCause cause)
        {
            if (_current == null) return;

            _current.endTime = Time.unscaledTime;
            _current.duration = stats != null ? stats.LastDuration : _current.endTime - _current.startTime;
            _current.cause = cause.ToString();
            _current.score = CurrentScore();
            _current.wordsTyped = stats != null ? stats.WordsTyped : 0;
            _current.lettersTyped = stats != null ? stats.LettersTyped : 0;
            _current.mistypes = stats != null ? stats.Mistypes : 0;
            _current.kills = stats != null ? stats.Kills : 0;
            _current.accuracy = stats != null ? stats.AccuracyPercent : 0;
            _current.wpm = stats != null ? stats.Wpm : 0;
            _current.failWord = _failWord;
            _current.failTrack = _failTrack;
            _current.failX = _failX;
            _current.failY = _failY;

            _payload.runs.Add(_current);
            NoteEvent("run_end", $"cause={_current.cause} duration={_current.duration:0.00} score={_current.score}");
            _current = null;

            if (cause == RunEndCause.Died) _awaitRetrySince = Time.unscaledTime;
        }

        private void HandleTrackChanged(CharacterTrack track)
        {
            // 演出层强制设定的轨道（如开场默认落在导演轨）不是“玩家自发切换”，单独记事件，
            // 否则 Prototype Card 的“无提示自发切换率”口径会被污染。
            if (trackInput != null && trackInput.LastChangeWasForced)
            {
                NoteEvent("track_forced", $"to={track}");
                return;
            }

            var previous = _pendingSwitch != null ? _pendingSwitch.to : OtherTrack(track);
            var record = new SwitchRecord
            {
                t = Time.unscaledTime,
                from = previous,
                to = track.ToString(),
                effective = false
            };
            _payload.switches.Add(record);
            _pendingSwitch = record;
            _pendingSwitchDeadline = Time.unscaledTime + SwitchEffectiveWindow;
            NoteEvent("track_switch", $"to={record.to}");
        }

        private void HandleLetterMatched(TypingWord word, int progress)
        {
            // 有效切换判定：切换后 5 秒内，在**新轨道**上打出一个正确字母
            if (_pendingSwitch == null || word == null) return;
            if (word.Track.ToString() != _pendingSwitch.to) return;

            _pendingSwitch.effective = true;
            _pendingSwitchDeadline = -1f;
            NoteEvent("switch_effective", $"to={_pendingSwitch.to} after={Time.unscaledTime - _pendingSwitch.t:0.00}s");
            _pendingSwitch = null;
        }

        private void HandlePlayerDeath()
        {
            CaptureFailLocation();
            if (stats != null && stats.IsRunning) stats.EndRun(RunEndCause.Died);
        }

        private void HandleVariantChanged(int mode, string name)
            => NoteEvent("variant_switch", $"mode={mode} name={name}");

        /// <summary>失败方式与位置：最近的在攻击/存活的怪物 + 它头上的词条。</summary>
        private void CaptureFailLocation()
        {
            var camera = Camera.main;
            Vector3 from = camera != null ? camera.transform.position : Vector3.zero;

            TypingTarget nearest = null;
            float best = float.MaxValue;
            foreach (var target in UnityEngine.Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None))
            {
                if (target == null || target.IsDead) continue;
                float d = Vector3.Distance(from, target.transform.position);
                if (target.IsAttacking) d -= 100f; // 攻击中的优先
                if (d < best) { best = d; nearest = target; }
            }

            if (nearest == null) return;
            _failX = nearest.transform.position.x;
            _failY = nearest.transform.position.y;

            if (typing != null)
            {
                foreach (var word in typing.Words)
                {
                    if (word.Target == nearest.gameObject)
                    {
                        _failWord = word.DisplayText;
                        _failTrack = word.Track.ToString();
                        break;
                    }
                }
            }
        }

        // ---------- 导出 ----------

        public string Export()
        {
            string json = JsonUtility.ToJson(_payload, true);
            string fileName = $"telemetry_{_payload.scene}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var written = new List<string>();

            foreach (var dir in CandidateDirectories())
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, fileName);
                    File.WriteAllText(path, json, new UTF8Encoding(false));
                    written.Add(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LabBTelemetry] 写入失败 {dir}: {e.Message}");
                }
            }

            _message = written.Count > 0
                ? $"TELEMETRY: {written[0]}"
                : "TELEMETRY EXPORT FAILED (see Console)";
            _messageTimer = 12f;

            if (written.Count > 0) Debug.Log($"[LabBTelemetry] 已导出 {written.Count} 份：{string.Join(" | ", written)}");
            return written.Count > 0 ? written[0] : null;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            // 1) 游戏目录旁边的 telemetry/（提交时直接可用；编辑器里就是工程根/telemetry）
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            yield return Path.Combine(root, "telemetry");
            // 2) 持久化目录（游戏目录只读时的回退）
            yield return Path.Combine(Application.persistentDataPath, "telemetry");
        }

        // ---------- 角落摘要 ----------

        private void OnGUI()
        {
            // 遥测摘要只在编辑器/开发版里显示（正式包不显示）
            if (!showSummary || !(Application.isEditor || Debug.isDebugBuild)) return;

            int runs = _payload.runs.Count;
            if (runs == 0 && _messageTimer <= 0f) return;

            float median = MedianDuration();
            float retryPercent = RetryPercent();

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperRight
            };

            const float w = 620f;
            float x = Screen.width - w - 12f;
            GUI.Label(new Rect(x, 8f, w, 22f),
                $"LAB B TELEMETRY | RUNS {runs} | MEDIAN {median:0.0}s | RETRY {retryPercent:0}% | SWITCHES {_payload.switches.Count} (T = export)",
                style);

            if (_messageTimer > 0f)
            {
                GUI.Label(new Rect(x, 30f, w, 22f), _message, style);
            }
        }

        private float MedianDuration()
        {
            if (_payload.runs.Count == 0) return 0f;
            var durations = new List<float>();
            foreach (var run in _payload.runs) durations.Add(run.duration);
            durations.Sort();
            int mid = durations.Count / 2;
            return durations.Count % 2 == 1
                ? durations[mid]
                : (durations[mid - 1] + durations[mid]) * 0.5f;
        }

        private float RetryPercent()
        {
            if (_payload.runs.Count == 0) return 0f;
            int retries = 0;
            foreach (var e in _payload.events)
            {
                if (e.type == "retry") retries++;
            }
            return 100f * retries / _payload.runs.Count;
        }

        private int CurrentScore()
            => directorStage != null ? directorStage.Score : 0;

        private string VariantName()
            => LabBTuning.I != null ? LabBTuning.I.VariantName : "n/a";

        private static string OtherTrack(CharacterTrack track)
            => track == CharacterTrack.Agent ? CharacterTrack.Director.ToString() : CharacterTrack.Agent.ToString();
    }
}
