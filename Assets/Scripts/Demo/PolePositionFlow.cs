using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YourMovieSucks.Combat;
using YourMovieSucks.Input;
using YourMovieSucks.LabB;
using YourMovieSucks.Level;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// 关卡「POLE POSITION」总控（Demo 脚本第 1–9 段）：
    /// 黑屏淡入 → 导演台词 + TYPE: ZOOM 输入门 + 快速推镜 → 互喷台词 → Ctrl 切轨教学门
    /// → 怪物入场抱杆 + COMBAT START → 脚本波次 1–4 + 递减主战斗 → 收尾台词 → CUT TO BLACK + PRINT IT! 结算屏。
    ///
    /// 本轮范围（作者确认「先做可玩闭环」）不含：受击 bark、低血量一次性台词、
    /// BAD TAKE 死亡演出与音频分层——BGM/SFX 全部留给下一轮，失败态暂用 GameOverController 占位。
    /// </summary>
    public class PolePositionFlow : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TypingController typing;
        [SerializeField] private TrackInput trackInput;
        [SerializeField] private RailCameraController rail;
        [SerializeField] private ScriptedEncounter encounter;
        [SerializeField] private DemoHud hud;
        [SerializeField] private DemoDialogue dialogue;
        [SerializeField] private RunStats stats;
        [SerializeField] private DirectorStage directorStage;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("音乐")]
        [SerializeField] private DemoAudioDirector audio;

        [Tooltip("主 BGM 的 id（= Assets/Audio/BGM 下的文件名）：开场黑幕开始播，到 PRINT IT! 结算画面前停")]
        [SerializeField] private string bgmId = "bgm_show_me_love";

        [Header("开场（第 1–2 段）")]
        [SerializeField] private float blackHold = 0.9f;
        [SerializeField] private float fadeInDuration = 1.4f;
        [SerializeField] private string zoomGateWord = "ZOOM";
        [SerializeField] private float zoomPushDuration = 0.6f;

        [Header("机位（RailCameraController 的 waypoint 序号）")]
        [SerializeField] private int waypointZoomCloseUp = 1;
        [SerializeField] private int waypointCombat = 2;
        [SerializeField] private int waypointEnding = 3;
        [SerializeField] private float combatPushDuration = 6f;
        [SerializeField] private float endingPushDuration = 1.2f;

        [Header("收尾（第 8–9 段）")]
        [SerializeField] private float silenceAfterCombat = 1.4f;
        [SerializeField] private float cutToBlackDuration = 0.5f;
        [SerializeField] private float resultsHold = 0.6f;

        [Header("Skip（演出层跳过开场）")]
        [SerializeField] private Key skipKey = Key.Enter;

        [Header("台词（不可打字）")]
        [SerializeField]
        private DemoLine[] directorOpening =
        {
            new DemoLine { id = "pp_open_dir_01_production_value", speaker = DemoSpeaker.Director, text = "Oh. Now that's production value.", hold = 1.2f }
        };

        [SerializeField]
        private DemoLine[] retort =
        {
            new DemoLine { id = "pp_open_dir_02_get_on_the_pole", speaker = DemoSpeaker.Director, text = "Get on the pole.", hold = 0.8f },
            new DemoLine { id = "pp_open_agt_03_no", speaker = DemoSpeaker.Agent, text = "No.", hold = 0.6f },
            new DemoLine { id = "pp_open_dir_04_ten", speaker = DemoSpeaker.Director, text = "Ten.", hold = 0.9f },
            new DemoLine { speaker = DemoSpeaker.Director, text = "", hold = 0.7f },
            new DemoLine { id = "pp_open_dir_05_nine", speaker = DemoSpeaker.Director, text = "Nine—", hold = 0.6f },
            new DemoLine { id = "pp_open_agt_06_fuck_you", speaker = DemoSpeaker.Agent, text = "Fuck you.", hold = 1.0f }
        };

        [SerializeField]
        private DemoLine[] stageClaim =
        {
            new DemoLine { id = "pp_ent_agt_07_my_stage", speaker = DemoSpeaker.Agent, text = "That is my stage, bitch!", hold = 1.2f }
        };

        [SerializeField]
        private DemoLine[] wrapUp =
        {
            new DemoLine { id = "pp_end_agt_08_there", speaker = DemoSpeaker.Agent, text = "There.", hold = 0.8f },
            new DemoLine { id = "pp_end_agt_09_happy_now", speaker = DemoSpeaker.Agent, text = "Happy now?", hold = 1.0f },
            new DemoLine { id = "pp_end_dir_10_got_the_shot", speaker = DemoSpeaker.Director, text = "...We got the shot.", hold = 1.2f },
            new DemoLine { id = "pp_end_agt_11_hate_this_job", speaker = DemoSpeaker.Agent, text = "I hate this job.", hold = 1.2f },
            new DemoLine { id = "pp_end_dir_12_great", speaker = DemoSpeaker.Director, text = "Great.", hold = 0.6f },
            new DemoLine { id = "pp_end_dir_13_print_it", speaker = DemoSpeaker.Director, text = "Print it.", hold = 1.4f }
        };

        private bool _openingActive = true;
        private bool _skipRequested;
        private bool _resultsShown;
        private bool _dead;
        private bool _pushing;

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnDeath += HandlePlayerDeath;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDeath;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 结算屏：一键重开（Lab B 要求失败/结束 ≤2s 回到游戏中）
            if (_resultsShown && keyboard.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        private IEnumerator Start()
        {
            BeginRun();

            yield return BlackoutFadeIn();
            yield return OpeningBeats();
            FinishOpening();
            yield return CombatBeats();
            yield return WrapUpBeats();
        }

        // ---------------------------------------------------------------- 第 1 段：黑屏 → 淡入

        private void BeginRun()
        {
            if (stats != null) stats.BeginRun();

            // 主 BGM：黑幕一出现就起，一直到结算屏之前（含对话闪避）
            if (audio != null && !string.IsNullOrEmpty(bgmId))
                audio.PlayBgm(bgmId);

            // 开场站在导演轨（第 4 段才要求玩家自己切到特工轨），演出期间锁输入
            if (directorStage != null) directorStage.SetTricksEnabled(false);

            if (trackInput != null)
            {
                trackInput.ForceTrack(CharacterTrack.Director);
                trackInput.SetLetterInput(false);
                trackInput.SetSwitchInput(false);
            }

            if (hud != null)
            {
                hud.Blackout(1f);
                hud.RefreshTrack(trackInput != null ? trackInput.CurrentTrack : CharacterTrack.Director);
            }
        }

        private IEnumerator BlackoutFadeIn()
        {
            yield return new WaitForSeconds(blackHold);

            if (!SkipRequested() && hud != null)
                yield return hud.Fade(1f, 0f, fadeInDuration);
        }

        // ---------------------------------------------------------------- 第 2–4 段：ZOOM 门 → 互喷 → 切轨门

        private IEnumerator OpeningBeats()
        {
            if (dialogue != null)
            {
                yield return dialogue.Play(directorOpening, SkipRequested);
                if (SkipRequested()) yield break;

                yield return ZoomGate();
                if (SkipRequested()) yield break;

                yield return PushCamera(waypointZoomCloseUp, zoomPushDuration);
                if (SkipRequested()) yield break;

                yield return dialogue.Play(retort, SkipRequested);
                if (SkipRequested()) yield break;

                yield return SwitchGate();
            }
        }

        /// <summary>第 2 段：中央提示 TYPE: ZOOM，逐字母打完 → 快速推近钢管。</summary>
        private IEnumerator ZoomGate()
        {
            if (typing == null) yield break;

            if (hud != null) hud.ShowPromptText("TYPE:", "one letter per shot — finish the word");
            if (trackInput != null) trackInput.SetLetterInput(true);

            // 导演轨 → 大写显示（脚本开场写的就是 ZOOM）
            var gate = typing.SpawnGate(zoomGateWord, CharacterTrack.Director);

            while (!gate.IsCompleted)
            {
                if (SkipRequested())
                {
                    typing.ClearGate();
                    break;
                }

                if (hud != null) hud.SetPromptWord(gate);
                yield return null;
            }

            if (trackInput != null) trackInput.SetLetterInput(false);
            if (hud != null) hud.HidePrompt();
        }

        /// <summary>第 4 段：切轨教学门（Ctrl 单击 toggle），切到特工轨才放行。</summary>
        private IEnumerator SwitchGate()
        {
            if (hud != null)
                hud.ShowPromptText("TAP CAPS", "SWITCH TO AGENT — press CapsLock once");

            if (trackInput != null) trackInput.SetSwitchInput(true);

            while (trackInput != null && trackInput.CurrentTrack != CharacterTrack.Agent)
            {
                if (SkipRequested()) break;
                yield return null;
            }

            if (hud != null) hud.HidePrompt();
        }

        private void FinishOpening()
        {
            _openingActive = false;

            if (trackInput != null)
            {
                // skip 时替玩家完成切轨，保证战斗一定在特工轨上进行
                if (_skipRequested) trackInput.ForceTrack(CharacterTrack.Agent);

                // 切轨权限按阶段管理（不是一次性写死）：开场教学门之后全程可用，
                // 局内可以随时来回切（后面“频繁切角色”的设计靠这个开关，不靠一次性开关）。
                trackInput.SetSwitchInput(true);
            }

            if (typing != null) typing.ClearGate();
            if (hud != null) hud.HidePrompt();
        }

        // ---------------------------------------------------------------- 第 5–7 段：入场 → 波次 → 主战斗

        private IEnumerator CombatBeats()
        {
            if (encounter == null) yield break;

            // 镜头缓慢朝钢管推进（与剧情并行）
            StartCoroutine(PushCamera(waypointCombat, combatPushDuration));

            yield return encounter.PlayEntrance();

            if (dialogue != null) yield return dialogue.Play(stageClaim, null);

            if (hud != null) hud.ShowBanner("COMBAT START");

            if (trackInput != null) trackInput.SetLetterInput(true);

            encounter.BeginCombat();
            yield return encounter.WaitUntilCleared();
        }

        // ---------------------------------------------------------------- 第 8–9 段：收尾 → 结算屏

        private IEnumerator WrapUpBeats()
        {
            // 收尾是非交互节拍：在此之前切轨一直可用
            if (trackInput != null)
            {
                trackInput.SetLetterInput(false);
                trackInput.SetSwitchInput(false);
            }

            yield return PushCamera(waypointEnding, endingPushDuration);
            yield return new WaitForSeconds(silenceAfterCombat);

            if (dialogue != null && !_dead)
                yield return dialogue.Play(wrapUp, null);

            // 结算屏不要 BGM：CUT TO BLACK 时收掉
            if (audio != null) audio.StopBgm();

            if (hud != null) yield return hud.Fade(0f, 1f, cutToBlackDuration);
            yield return new WaitForSeconds(resultsHold);

            if (stats != null) stats.EndRun(RunEndCause.Completed);
            if (hud != null && stats != null) hud.ShowResults(stats);

            _resultsShown = true;
        }

        // ---------------------------------------------------------------- 工具

        /// <summary>推进到指定机位并等待到达（演出层控制速度）；同一时刻只允许一个推镜协程。</summary>
        private IEnumerator PushCamera(int waypointIndex, float duration)
        {
            if (rail == null) yield break;

            while (_pushing) yield return null;
            _pushing = true;

            rail.SetDwellDuration(0f);
            rail.SetMoveDuration(duration);

            int guard = 0;
            while (rail.CurrentIndex < waypointIndex && !rail.IsFinished && guard++ < 16)
                rail.Advance();

            while (rail.IsMoving)
                yield return null;

            _pushing = false;
        }

        /// <summary>第 5 段之前的任意键（默认 Enter）跳过开场演出，并写 skip_intro 遥测事件。</summary>
        private bool SkipRequested()
        {
            if (_skipRequested || !_openingActive) return _skipRequested;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[skipKey].wasPressedThisFrame)
            {
                _skipRequested = true;
                if (LabBTelemetry.I != null) LabBTelemetry.I.NoteSkipIntro();
                Debug.Log("[PolePositionFlow] opening skipped");
            }

            return _skipRequested;
        }

        private void HandlePlayerDeath()
        {
            // 失败态演出（BAD TAKE / THE LEAD IS DEAD. / RETAKE）下一轮实现；
            // 本轮沿用 GameOverController 占位，这里只停掉输入避免死后继续开枪。
            _dead = true;

            if (trackInput != null)
            {
                trackInput.SetLetterInput(false);
                trackInput.SetSwitchInput(false);
            }
        }
    }
}
