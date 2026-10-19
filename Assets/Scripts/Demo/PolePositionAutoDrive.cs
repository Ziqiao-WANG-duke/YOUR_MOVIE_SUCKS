using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YourMovieSucks.Combat;
using YourMovieSucks.Input;
using YourMovieSucks.Level;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 关卡「POLE POSITION」自检驱动器：自动把整关打完并输出逐项校验结果，用于改动后一键回归。
    ///
    /// 输入路径：优先用合成的 InputSystem 设备事件（等价于真按键盘）；若该路径不可用
    /// （批处理/无焦点时常见），自动降级到 <see cref="TrackInput.InjectLetter"/> /
    /// <see cref="TrackInput.InjectSwitch"/> 注入通道，并在报告里写明实际用了哪条路径。
    ///
    /// 触发方式：组件上勾 autoStartOnPlay；或游戏内按 F2；或外部调用 <see cref="Run"/>。
    /// 结果打印到 Console 并写入 `Library/Locus/tmp/poleposition-selftest.txt`。
    /// </summary>
    public class PolePositionAutoDrive : MonoBehaviour
    {
        [Header("Refs（留空则自动查找）")]
        [SerializeField] private TypingController typing;
        [SerializeField] private TrackInput trackInput;
        [SerializeField] private RailCameraController rail;
        [SerializeField] private ScriptedEncounter encounter;
        [SerializeField] private DemoHud hud;
        [SerializeField] private RunStats stats;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("开关")]
        [Tooltip("勾上则进入 Play 后立即自动跑完整关")]
        [SerializeField] private bool autoStartOnPlay;

        [Tooltip("跑完自动退出编辑器（批处理自检用；构建版为 Application.Quit）。默认关，避免误关编辑器")]
        [SerializeField] private bool exitWhenFinished;

        [Tooltip("启用后按该键启动自检")]
        [SerializeField] private Key activationKey = Key.F2;

        [Tooltip("先按 Enter 跳过开场演出，只验证战斗与结算")]
        [SerializeField] private bool skipOpening;

        [Header("节奏")]
        [Tooltip("每个按键之后的稳定帧数（一帧只发一个键）")]
        [SerializeField] private int framesPerKey = 3;

        [Tooltip("主战斗阶段每打一个词后的停顿：要长于 2 个刷怪间隔，才能稳定出现“同屏两只怪”供重叠断言与截图检验")]
        [SerializeField] private float mainPhaseRestSeconds = 2.6f;

        [SerializeField] private float overallTimeout = 300f;

        [Header("截图（批处理也生效，用于无 GUI 的画面核对）")]
        [SerializeField] private bool captureScreenshots = true;
        [SerializeField] private int captureWidth = 960;
        [SerializeField] private int captureHeight = 540;

        private readonly List<string> _report = new();
        private readonly List<string> _waveOrder = new();

        private TypingWord _gate;
        private bool _running;
        private bool _useDeviceEvents = true;
        private string _inputPath = "synthetic InputSystem device events";
        private bool _clashReported;
        private bool _behindPoleCaptured;
        private bool _overlapReported;
        private YourMovieSucks.Combat.ScreenBlood _blood;
        private DemoAudioDirector _audioDirector;
        private TypingSfxPlayer _typingSfx;
        private float _bgmBaseline;

        private DemoAudioDirector AudioDirector
        {
            get
            {
                if (_audioDirector == null) _audioDirector = UnityEngine.Object.FindFirstObjectByType<DemoAudioDirector>();
                return _audioDirector;
            }
        }

        private TypingSfxPlayer TypingSfx
        {
            get
            {
                if (_typingSfx == null) _typingSfx = UnityEngine.Object.FindFirstObjectByType<TypingSfxPlayer>();
                return _typingSfx;
            }
        }

        public bool IsRunning => _running;

        /// <summary>供外部（编辑器 / 构建启动参数）直接触发。</summary>
        public static bool Run()
        {
            var drive = Object.FindFirstObjectByType<PolePositionAutoDrive>();
            if (drive == null)
            {
                Debug.LogWarning("[PolePositionAutoDrive] no driver found in the active scene");
                return false;
            }

            drive.Begin();
            return true;
        }

        private void Awake() => Resolve();

        private void Start()
        {
            if (autoStartOnPlay) Begin();
        }

        private void Update()
        {
            if (_running) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[activationKey].wasPressedThisFrame) Begin();
        }

        public void Begin()
        {
            if (_running) return;
            _running = true;
            StartCoroutine(Drive());
        }

        private void Resolve()
        {
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (trackInput == null) trackInput = Object.FindFirstObjectByType<TrackInput>();
            if (rail == null) rail = Object.FindFirstObjectByType<RailCameraController>();
            if (encounter == null) encounter = Object.FindFirstObjectByType<ScriptedEncounter>();
            if (hud == null) hud = Object.FindFirstObjectByType<DemoHud>();
            if (stats == null) stats = Object.FindFirstObjectByType<RunStats>();
            if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        }

        // ---------------------------------------------------------------- 主流程

        private IEnumerator Drive()
        {
            _report.Clear();
            _waveOrder.Clear();
            _gate = null;
            _useDeviceEvents = true;
            _clashReported = false;
            _behindPoleCaptured = false;
            _overlapReported = false;

            EnsureKeyboard();

            float deadline = Time.unscaledTime + overallTimeout;

            Log("== POLE POSITION self-test ==");
            Log($"start: track={TrackName()} letters={LettersEnabled()} switch={SwitchEnabled()} hp={Hp()}");
            CheckVoiceWiring();

            if (skipOpening)
            {
                if (_useDeviceEvents)
                {
                    yield return PressSwitch(Key.Enter);
                    yield return WaitFrames(2);
                    Log("skip requested (Enter)");
                }
                else
                {
                    Warn("skipOpening needs device events; continuing without skip");
                }
            }

            // ---- 第 2 段：ZOOM 输入门
            yield return WaitForGate(60f);
            if (_gate == null)
            {
                Fail("ZOOM input gate never appeared");
                yield return Finish();
                yield break;
            }

            Log($"gate word: {_gate.DisplayText}");
            yield return TypeWord(_gate);
            yield return WaitFrames(20);

            CheckAudioFirstPass();

            Check(rail != null && rail.CurrentIndex >= 1, $"camera pushed to waypoint 1 after ZOOM (index={RailIndex()})");
            Capture("01_after_zoom_push");

            // ---- 第 4 段：Ctrl 切轨门
            float switchDeadline = Time.unscaledTime + 45f;
            while (!SwitchEnabled() && Time.unscaledTime < switchDeadline)
                yield return null;

            Check(SwitchEnabled(), "switch gate opened (letters off / switch on)");
            yield return ToggleTrack();
            Check(trackInput != null && trackInput.CurrentTrack == CharacterTrack.Agent,
                $"switch key (CapsLock) toggled to AGENT track (now {TrackName()})");

            // 对白刚结束：等闪避恢复的过渡跑完再取基准（否则采到过渡中间值）
            yield return WaitSeconds(0.6f);
            _bgmBaseline = AudioDirector != null ? AudioDirector.BgmVolume : 0f;
            Check(_bgmBaseline > 0.001f, $"BGM audible outside dialogue (volume={_bgmBaseline:0.000})");

            // ---- 第 5–6 段：入场 → COMBAT START → 4 个脚本波次 → 主战斗
            yield return WaitUntil(() => AnyAgentWord() != null, 60f);
            Check(AnyAgentWord() != null, "wave 1 word appeared after the entrance beat");
            Capture("02_wave1_entrance");

            // 局内切轨必须可持续（曾经把切轨在开场后永久关掉，导致按 CapsLock 没反应）
            yield return VerifyMidCombatTrackSwitching();

            int guardFrames = 0;
            int agentWordsTyped = 0;
            int maxAlive = 0;
            bool combatShotDone = false;
            bool combatShotA = false;
            bool combatShotB = false;
            bool combatShotC = false;
            while (Time.unscaledTime < deadline && guardFrames++ < 8000)
            {
                if (encounter != null && encounter.IsCleared) break;

                maxAlive = Mathf.Max(maxAlive, Alive());

                TryCaptureWordBehindPole();
                CheckNoInitialClash();
                CheckNoWordLabelOverlap();

                // 两只怪同时在时多拍几张（不筛血浆，总有干净的），用于人工核对“两条词条不重叠、可读”
                if (Alive() >= 2)
                {
                    if (!combatShotA && agentWordsTyped >= 4) { combatShotA = true; Capture("06_two_alive_a"); }
                    if (!combatShotB && agentWordsTyped >= 8) { combatShotB = true; Capture("07_two_alive_b"); }
                    if (!combatShotC && agentWordsTyped >= 11) { combatShotC = true; Capture("08_two_alive_c"); }
                }

                if (!combatShotDone && (Alive() >= 2 || agentWordsTyped >= 6))
                {
                    combatShotDone = true;
                    Capture("03_main_combat");
                }

                var word = AnyAgentWord();
                if (word == null)
                {
                    yield return null;
                    continue;
                }

                if (_waveOrder.Count < 4) _waveOrder.Add(word.MatchText);
                agentWordsTyped++;
                yield return TypeWord(word);

                // 人类节奏：主战斗阶段留出间隔，让第二只怪能同时在场（否则驱动器把它们一秒一只枪毙，永不同屏）
                if (agentWordsTyped > 4 && mainPhaseRestSeconds > 0f)
                    yield return WaitSeconds(mainPhaseRestSeconds);

                yield return WaitFrames(1);
            }

            float clearDeadline = Time.unscaledTime + 240f;
            while (encounter != null && !encounter.IsCleared && Time.unscaledTime < clearDeadline)
            {
                var word = AnyAgentWord();
                if (word != null) yield return TypeWord(word);
                else yield return null;
            }

            Check(encounter != null && encounter.IsCleared, $"main combat cleared (alive={Alive()})");
            Check(!_clashReported, "no two active words ever shared the same initial letter");
            Check(!_overlapReported, "no two word labels ever overlapped on screen");
            Check(maxAlive >= 2, $"test actually exercised two-enemies-at-once (max alive={maxAlive})");
            Capture("04_after_last_kill");

            var expected = new[] { "MINE", "MYPOLE", "GETOFF", "WAITYOURTURN" };
            bool orderOk = _waveOrder.Count >= 4;
            for (int i = 0; orderOk && i < expected.Length; i++)
                orderOk = _waveOrder[i] == expected[i];

            Check(orderOk, $"scripted wave order = mine/my pole/get off/wait your turn (seen: {string.Join(" | ", _waveOrder)})");

            // ---- 第 9 段：结算屏（同时采样 BGM 音量，验证对话时被压低）
            float minBgm = float.MaxValue;
            float minDuck = float.MaxValue;
            int duckedFrames = 0;
            int sampledFrames = 0;
            float resultsDeadline = Time.unscaledTime + 60f;
            while (Time.unscaledTime < resultsDeadline)
            {
                if (hud != null && hud.IsResultsVisible) break;
                if (AudioDirector != null)
                {
                    minBgm = Mathf.Min(minBgm, AudioDirector.BgmVolume);
                    minDuck = Mathf.Min(minDuck, AudioDirector.DuckFactor);
                    sampledFrames++;
                    if (AudioDirector.DialogueDucking) duckedFrames++;
                }

                yield return null;
            }

            Log($"during closing: duckedFrames={duckedFrames}/{sampledFrames}, minBgm={minBgm:0.000}, baseline={_bgmBaseline:0.000}, minDuckFactor={minDuck:0.00}");

            Check(hud != null && hud.IsResultsVisible, "PRINT IT! results screen shown");
            Check(AudioDirector != null && !AudioDirector.IsBgmPlaying, "BGM stopped before the results screen");
            Check(minDuck <= 0.5f, $"duck factor actually dropped during dialogue (min={minDuck:0.00})");
            Check(minBgm < _bgmBaseline * 0.6f,
                $"BGM ducked during the closing dialogue (min={minBgm:0.000} vs baseline={_bgmBaseline:0.000})");
            CheckTypingSfx();

            if (stats != null)
            {
                Log($"results: accuracy={stats.AccuracyPercent}% words={stats.WordsTyped} wpm={stats.Wpm} kills={stats.Kills} mistypes={stats.Mistypes} duration={stats.LastDuration:0.0}s");
                Check(stats.Kills >= 12, $"kills >= 12 (kills={stats.Kills}; expected 4 scripted + 8 main)");
                Check(stats.WordsTyped >= 12, $"combat words typed >= 12 (words={stats.WordsTyped})");
            }

            Check(playerHealth == null || !playerHealth.IsDead, "player survived the run");

            var voDirector = UnityEngine.Object.FindFirstObjectByType<DemoAudioDirector>();
            int voPlayed = voDirector != null ? voDirector.VoPlayCount : -1;
            Log($"VO played during run: {voPlayed}");
            Check(voPlayed >= RequiredVoIds.Length, $"every scripted dialogue line actually played its VO ({voPlayed}/{RequiredVoIds.Length})");

            yield return Finish();
        }

        private IEnumerator Finish()
        {
            int pass = 0;
            int fail = 0;
            foreach (var line in _report)
            {
                if (line.StartsWith("[PASS]")) pass++;
                else if (line.StartsWith("[FAIL]")) fail++;
            }

            Log($"input path used: {_inputPath}");
            Log($"== summary: {pass} pass / {fail} fail ==");
            WriteReport();

            _running = false;

            if (exitWhenFinished)
            {
                Debug.Log($"[PolePositionAutoDrive] exiting (fail={fail})");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.Exit(fail > 0 ? 1 : 0);
#else
                Application.Quit(fail > 0 ? 1 : 0);
#endif
            }

            yield break;
        }

        // ---------------------------------------------------------------- 输入（设备事件 → 注入通道自动降级）

        private void EnsureKeyboard()
        {
            if (Keyboard.current != null) return;

            InputSystem.AddDevice<Keyboard>();
            Log("no physical keyboard: added a synthetic Keyboard device");
        }

        private IEnumerator PressLetter(Key key)
        {
            if (!_useDeviceEvents)
            {
                trackInput?.InjectLetter(key);
                yield return WaitFrames(FramesPerKey);
                yield break;
            }

            EnsureKeyboard();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null;

            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return WaitFrames(FramesPerKey);
        }

        private IEnumerator PressSwitch(Key key)
        {
            if (!_useDeviceEvents)
            {
                trackInput?.InjectSwitch();
                yield return WaitFrames(FramesPerKey);
                yield break;
            }

            EnsureKeyboard();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null;

            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return WaitFrames(FramesPerKey);
        }

        /// <summary>切到特工轨：先试真实 Ctrl，无效则降级到注入通道（带重试，防止异步事件造成双次 toggle）。</summary>
        private IEnumerator ToggleTrack()
        {
            if (_useDeviceEvents)
            {
                yield return PressSwitch(Key.CapsLock);
                yield return WaitFrames(3);

                if (trackInput != null && trackInput.CurrentTrack != CharacterTrack.Agent)
                {
                    Warn("synthetic CapsLock was not observed by TrackInput — falling back to the injection seam");
                    _useDeviceEvents = false;
                    _inputPath = "TrackInput injection seam (device events unusable in this session)";
                }
            }

            int attempts = 0;
            while (trackInput != null && trackInput.CurrentTrack != CharacterTrack.Agent && attempts++ < 4)
            {
                trackInput.InjectSwitch();
                yield return WaitFrames(3);
            }

            yield return WaitFrames(2);
        }

        /// <summary>战斗中来回切轨：先走真实按键，不行再用注入通道验逻辑（两者都必须能双向切）。</summary>
        private IEnumerator VerifyMidCombatTrackSwitching()
        {
            if (trackInput == null) yield break;

            Check(trackInput.SwitchEnabled, "switch input stays enabled after the opening gate");

            yield return PressSwitch(Key.CapsLock);
            yield return WaitFrames(3);
            bool toDirector = trackInput.CurrentTrack == CharacterTrack.Director;
            Check(toDirector, $"mid-combat CapsLock switched to DIRECTOR (now {TrackName()})");

            yield return PressSwitch(Key.CapsLock);
            yield return WaitFrames(3);
            bool backToAgent = trackInput.CurrentTrack == CharacterTrack.Agent;
            Check(backToAgent, $"mid-combat CapsLock switched back to AGENT (now {TrackName()})");

            // 真实按键在批处理里可能不可用：用注入通道（同一条逻辑路径）再验一次双向切换
            if (!toDirector || !backToAgent)
            {
                Warn("device-event mid-combat switching incomplete — re-checking via the injection seam");

                trackInput.InjectSwitch();
                yield return WaitFrames(2);
                bool seamDirector = trackInput.CurrentTrack == CharacterTrack.Director;

                trackInput.InjectSwitch();
                yield return WaitFrames(2);
                bool seamBack = trackInput.CurrentTrack == CharacterTrack.Agent;

                Check(seamDirector && seamBack, $"injection seam: DIRECTOR <-> AGENT both ways (director={seamDirector} back={seamBack})");
            }
        }

        private IEnumerator TypeWord(TypingWord word)
        {
            if (word == null) yield break;

            string match = word.MatchText;
            for (int i = word.Progress; i < match.Length; i++)
            {
                var key = CharToKey(match[i]);
                if (key == Key.None) continue;

                int before = word.Progress;
                yield return PressLetter(key);

                if (word.Progress > before) continue;

                if (_useDeviceEvents)
                {
                    Warn($"letter '{match[i]}' of \"{word.DisplayText}\" not accepted via device events — falling back to the injection seam");
                    _useDeviceEvents = false;
                    _inputPath = "TrackInput injection seam (device events unusable in this session)";

                    yield return PressLetter(key);
                    if (word.Progress > before) continue;
                }

                Fail($"letter '{match[i]}' of \"{word.DisplayText}\" not accepted (progress={word.Progress})");
                yield break;
            }

            yield return WaitFrames(FramesPerKey);
        }

        private static Key CharToKey(char c)
        {
            c = char.ToUpperInvariant(c);
            return c < 'A' || c > 'Z' ? Key.None : Key.A + (c - 'A');
        }

        // ---------------------------------------------------------------- 查询

        private int FramesPerKey => Mathf.Max(framesPerKey, 1);

        private TypingWord AnyAgentWord()
        {
            if (typing == null) return null;

            foreach (var word in typing.Words)
            {
                if (word == null || word.IsGate) continue;
                if (word.Track != CharacterTrack.Agent) continue;
                return word;
            }

            return null;
        }

        private IEnumerator WaitForGate(float timeout)
        {
            float deadline = Time.unscaledTime + timeout;

            while (Time.unscaledTime < deadline)
            {
                if (typing != null)
                {
                    foreach (var word in typing.Words)
                    {
                        if (word == null || !word.IsGate) continue;

                        _gate = word;
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
        {
            float deadline = Time.unscaledTime + timeout;

            while (Time.unscaledTime < deadline)
            {
                if (condition()) yield break;
                yield return null;
            }
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private bool LettersEnabled() => trackInput == null || trackInput.LettersEnabled;
        private bool SwitchEnabled() => trackInput != null && trackInput.SwitchEnabled;
        private string TrackName() => trackInput != null ? trackInput.CurrentTrack.ToString() : "-";
        private int RailIndex() => rail != null ? rail.CurrentIndex : -1;
        private int Alive() => encounter != null ? encounter.AliveCount : -1;
        private string Hp() => playerHealth != null ? $"{playerHealth.CurrentHp:0}" : "-";

        // ---------------------------------------------------------------- 报告

        /// <summary>开场阶段：BGM 是否从黑幕就起了、打字音效候选池是否接好。</summary>
        private void CheckAudioFirstPass()
        {
            var director = AudioDirector;
            Check(director != null && director.IsBgmPlaying, "BGM playing from the opening black screen");

            var sfx = TypingSfx;
            Check(sfx != null && sfx.TypingPoolSize == 7,
                $"typing SFX pool = typewriter1..7 (size={(sfx != null ? sfx.TypingPoolSize : -1)})");
            Check(sfx != null && sfx.SwitchSfxId == "typewriter8",
                $"track-switch SFX = typewriter8 (id={(sfx != null ? sfx.SwitchSfxId : "-")})");
            Check(sfx != null && sfx.GunshotPoolSize == 2,
                $"gunshot pool wired (size={(sfx != null ? sfx.GunshotPoolSize : -1)})");
        }

        /// <summary>打字/切轨音效是否真的响了。</summary>
        private void CheckTypingSfx()
        {
            var sfx = TypingSfx;
            if (sfx == null)
            {
                Fail("TypingSfxPlayer not found in scene");
                return;
            }

            Log($"typing SFX played: {sfx.TypingPlayCount}, switch SFX played: {sfx.SwitchPlayCount}, gunshots: {sfx.GunshotPlayCount} (last={sfx.LastGunshotId}), last typing={sfx.LastTypingSfxId}");
            Check(sfx.TypingPlayCount >= 20, $"typing SFX actually played on keystrokes ({sfx.TypingPlayCount} > 20)");
            Check(sfx.SwitchPlayCount >= 2, $"track-switch SFX played on CapsLock ({sfx.SwitchPlayCount} >= 2)");
            Check(!string.IsNullOrEmpty(sfx.LastTypingSfxId) && sfx.LastTypingSfxId != "typewriter8",
                $"typing SFX never picks the switch sound (last={sfx.LastTypingSfxId})");
            Check(sfx.GunshotPlayCount >= 20 && (sfx.LastGunshotId == "cg1" || sfx.LastGunshotId == "pistol"),
                $"gunshot layered on every keystroke ({sfx.GunshotPlayCount}, last={sfx.LastGunshotId})");
        }

        /// <summary>剧本里带配音的台词（与 PolePositionVoiceTools 的 LineIds 表一致）。</summary>
        private static readonly string[] RequiredVoIds =
        {
            "pp_open_dir_01_production_value",
            "pp_open_dir_02_get_on_the_pole",
            "pp_open_agt_03_no",
            "pp_open_dir_04_ten",
            "pp_open_dir_05_nine",
            "pp_open_agt_06_fuck_you",
            "pp_ent_agt_07_my_stage",
            "pp_end_agt_08_there",
            "pp_end_agt_09_happy_now",
            "pp_end_dir_10_got_the_shot",
            "pp_end_agt_11_hate_this_job",
            "pp_end_dir_12_great",
            "pp_end_dir_13_print_it"
        };

        /// <summary>台词配音接线自检：VoLibrary 是否建好、13 条脚本台词是否都有音频。</summary>
        private void CheckVoiceWiring()
        {
            var director = UnityEngine.Object.FindFirstObjectByType<DemoAudioDirector>();
            var library = director != null ? director.Library : null;

            Log($"VO library: {(library != null ? library.Count + " clip(s)" : "NOT WIRED")}");

            if (library == null)
            {
                Fail("VO library not wired (DemoAudioDirector.voLibrary is null)");
                return;
            }

            var missing = new List<string>();
            foreach (var id in RequiredVoIds)
            {
                if (!library.Has(id)) missing.Add(id);
            }

            Check(missing.Count == 0,
                $"all {RequiredVoIds.Length} scripted dialogue VO clips are wired" +
                (missing.Count > 0 ? $" (missing: {string.Join(",", missing)})" : string.Empty));

            CheckPerLineSettings(director, library);
        }

        /// <summary>逐条参数（速度/延迟/偏移）真的进了生效时长公式——临时设值验证后还原。</summary>
        private void CheckPerLineSettings(DemoAudioDirector director, VoLibrary library)
        {
            if (director.LineSettings == null)
            {
                Fail("per-line audio settings asset not wired (DemoAudioDirector.lineSettings is null)");
                return;
            }

            const string sampleId = "pp_open_dir_01_production_value";
            var clip = library.Get(sampleId);
            if (clip == null) return;

            var entry = director.LineSettings.EnsureEntry(sampleId);
            float savedSpeed = entry.speed;
            float savedDelay = entry.delaySeconds;
            float savedOffset = entry.startOffsetSeconds;

            entry.speed = 2f;
            entry.delaySeconds = 0.5f;
            entry.startOffsetSeconds = 0f;

            float expected = 0.5f + clip.length / 2f;
            float actual = director.GetEffectiveDuration(sampleId, includeTail: false);

            entry.speed = savedSpeed;
            entry.delaySeconds = savedDelay;
            entry.startOffsetSeconds = savedOffset;

            Check(Mathf.Abs(expected - actual) < 0.03f,
                $"per-line speed/delay feed the pacing formula (expected {expected:0.00}s, got {actual:0.00}s)");
        }

        /// <summary>同首字母词条不得同时在场上（锁定按首字母吸附，会出现歧义）。</summary>
        private void CheckNoInitialClash()
        {
            if (_clashReported || typing == null) return;

            // 首字母吸附是**按轨**的：同一轨内不能有两个同首字母的词；
            // 跨轨（导演轨弱点词 vs 特工轨战斗词）同时存在是设计允许的，不算冲突。
            var initials = new Dictionary<CharacterTrack, List<char>>();
            foreach (var word in typing.Words)
            {
                if (word == null || word.IsGate || word.MatchText.Length == 0) continue;

                if (!initials.TryGetValue(word.Track, out var list))
                {
                    list = new List<char>();
                    initials[word.Track] = list;
                }

                char initial = char.ToUpperInvariant(word.MatchText[0]);
                if (list.Contains(initial))
                {
                    _clashReported = true;
                    Fail($"two active words on the same track share the initial '{initial}'");
                    return;
                }

                list.Add(initial);
            }
        }

        private bool IsBloody()
        {
            if (_blood == null) _blood = UnityEngine.Object.FindFirstObjectByType<YourMovieSucks.Combat.ScreenBlood>();
            return _blood != null && _blood.IsBloody;
        }

        /// <summary>两条活动词条不应在屏幕上重叠（同点叠怪 / 横向收敛会把词条挤成一团看不清）。</summary>
        private void CheckNoWordLabelOverlap()
        {
            if (_overlapReported || typing == null) return;

            var camera = Camera.main;
            if (camera == null) return;

            var labels = new List<(Vector3 pos, float width)>();

            foreach (var word in typing.Words)
            {
                if (word == null || word.IsGate || word.Target == null) continue;

                Vector3 labelWorld = word.Target.transform.position + new Vector3(0f, 2.1f, 0f);
                Vector3 screen = camera.WorldToScreenPoint(labelWorld);
                if (screen.z <= 0f) continue;

                float width = LabelScreenWidth(camera, labelWorld);
                labels.Add((screen, width));
            }

            for (int i = 0; i < labels.Count; i++)
            {
                for (int j = i + 1; j < labels.Count; j++)
                {
                    float dx = Mathf.Abs(labels[i].pos.x - labels[j].pos.x);
                    float dy = Mathf.Abs(labels[i].pos.y - labels[j].pos.y);

                    // 词条画布是 400x100（宽:高 = 4:1），换算成屏幕像素后判断是否相交
                    float requiredDx = (labels[i].width + labels[j].width) * 0.5f * 0.85f;
                    float requiredDy = Mathf.Max(labels[i].width, labels[j].width) * 0.28f;

                    if (dx >= requiredDx || dy >= requiredDy) continue;

                    _overlapReported = true;
                    Fail($"two active word labels overlap on screen (dx={dx:0}px < {requiredDx:0}px, dy={dy:0}px < {requiredDy:0}px)");
                    return;
                }
            }
        }

        /// <summary>词条世界宽度 2 单位（400px × 0.005 缩放）→ 投影到屏幕像素宽度。</summary>
        private static float LabelScreenWidth(Camera camera, Vector3 labelWorld)
        {
            Vector3 right = camera.transform.right;
            Vector3 a = camera.WorldToScreenPoint(labelWorld - right);
            Vector3 b = camera.WorldToScreenPoint(labelWorld + right);
            return Mathf.Abs(b.x - a.x);
        }

        /// <summary>当某条词条位于钢管之后且屏幕横向重叠时截一张图，用于核对“词条永远显示在最上层”。</summary>
        private void TryCaptureWordBehindPole()
        {
            if (!captureScreenshots || _behindPoleCaptured || typing == null) return;

            var camera = Camera.main;
            if (camera == null) return;

            var pole = GameObject.Find("BrassPole");
            if (pole == null) return;

            Vector3 poleScreen = camera.WorldToScreenPoint(pole.transform.position);
            if (poleScreen.z <= 0f) return;

            foreach (var word in typing.Words)
            {
                if (word == null || word.IsGate || word.Target == null) continue;

                Vector3 labelWorld = word.Target.transform.position + new Vector3(0f, 2.1f, 0f);
                if (labelWorld.z <= pole.transform.position.z) continue; // 必须在钢管之后（更远）

                Vector3 labelScreen = camera.WorldToScreenPoint(labelWorld);
                if (labelScreen.z <= 0f) continue;
                if (Mathf.Abs(labelScreen.x - poleScreen.x) > 200f) continue;

                _behindPoleCaptured = true;
                Capture("05_word_behind_pole");
                return;
            }
        }

        /// <summary>把主相机渲进 RenderTexture 存 PNG（屏空间 UI 拍不到，但能核对场地/机位/敌人与头顶词条）。</summary>
        private void Capture(string name)
        {
            if (!captureScreenshots) return;

            var camera = Camera.main;
            if (camera == null)
            {
                Warn("no main camera; skipping capture " + name);
                return;
            }

            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                rt = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = previousTarget;

                RenderTexture.active = rt;
                var tex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, captureWidth, captureHeight), 0, 0);
                tex.Apply();

                string dir = Path.Combine(Application.dataPath, "../Library/Locus/tmp/selftest-shots");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"pp_{name}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());

                Object.Destroy(tex);
                Log($"capture: {path}");
            }
            catch (System.Exception e)
            {
                Warn($"capture '{name}' failed: {e.Message}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (rt != null) rt.Release();
            }
        }

        private void Check(bool ok, string label)
        {
            if (ok) Log("[PASS] " + label);
            else Fail(label);
        }

        private void Fail(string label)
        {
            Log("[FAIL] " + label);
            Debug.LogWarning("[PolePositionAutoDrive] " + label);
        }

        private void Warn(string message)
        {
            Log("[WARN] " + message);
            Debug.LogWarning("[PolePositionAutoDrive] " + message);
        }

        private void Log(string line)
        {
            _report.Add(line);
            Debug.Log("[PolePositionAutoDrive] " + line);
        }

        private void WriteReport()
        {
            try
            {
                string dir = Path.Combine(Application.dataPath, "../Library/Locus/tmp");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "poleposition-selftest.txt");

                var sb = new StringBuilder();
                sb.AppendLine($"scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                sb.AppendLine($"time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                foreach (var line in _report) sb.AppendLine(line);

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Debug.Log($"[PolePositionAutoDrive] report written: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PolePositionAutoDrive] report write failed: {e.Message}");
            }
        }
    }
}
