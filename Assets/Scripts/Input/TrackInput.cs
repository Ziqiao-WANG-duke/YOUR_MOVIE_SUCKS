using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YourMovieSucks.Input
{
    /// <summary>
    /// 双轨（特工/导演）输入核心。0.1 验证版本：
    /// 直接读取键码 + Ctrl 修饰状态判断归属，不依赖字符大小写，天然规避 Caps Lock / IME / Shift。
    /// </summary>
    public enum CharacterTrack
    {
        Agent,     // 特工轨：小写 / 红
        Director   // 导演轨：大写 / 蓝
    }

    public class TrackInput : MonoBehaviour
    {
        [Header("Input Mode")]
        [SerializeField] private bool toggleMode = true; // true=按一下切换, false=按住生效
        [Tooltip("额外把左右 Ctrl / Shift 也当切轨键（默认关）：Demo 只认 CapsLock，大小写语义对应两条轨道")]
        [SerializeField] private bool extendedSwitchKeys = false;

        [Header("Debug")]
        [Tooltip("关掉后不画右下调试轨信息（Demo 关卡用）")]
        [SerializeField] private bool showDebugOverlay = true;

        public CharacterTrack CurrentTrack { get; private set; } = CharacterTrack.Agent;

        /// <summary>字母输入开关（演出/对白/黑屏期间由演出层关闭）。</summary>
        public bool LettersEnabled { get; private set; } = true;

        /// <summary>切轨开关（切轨教学门前由演出层关闭）。</summary>
        public bool SwitchEnabled { get; private set; } = true;

        /// <summary>上一次变轨是否由演出层强制设定（遥测需排除，避免污染“玩家自发切换”口径）。</summary>
        public bool LastChangeWasForced { get; private set; }

        public System.Action<CharacterTrack> OnTrackChanged;
        public System.Action<CharacterTrack, Key> OnLetterTyped;

        /// <summary>演出层：开关字母输入。</summary>
        public void SetLetterInput(bool enabled) => LettersEnabled = enabled;

        /// <summary>演出层：开关切轨（Ctrl）。</summary>
        public void SetSwitchInput(bool enabled) => SwitchEnabled = enabled;

        /// <summary>演出层：直接设定当前轨道（开场默认落在导演轨，切轨教学门才要求玩家自己切）。</summary>
        public void ForceTrack(CharacterTrack track) => SetTrack(track, true);

        /// <summary>自检/自动化用：绕过输入设备，但走与真实按键完全相同的逻辑路径注入一个字母。</summary>
        public void InjectLetter(Key key)
        {
            if (!LettersEnabled) return;
            EmitLetter(CurrentTrack, key);
        }

        /// <summary>自检/自动化用：注入一次 Ctrl toggle（等价于按一下 Ctrl）。</summary>
        public void InjectSwitch()
        {
            if (!SwitchEnabled) return;
            ToggleTrack(false);
        }

        private static readonly Key[] LetterKeys =
        {
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
            Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
            Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z
        };

        private readonly List<string> _history = new List<string>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (SwitchEnabled)
            {
                if (toggleMode)
                {
                    // 切轨：CapsLock（大写键）单击边沿触发 toggle——
                    // 大写键对应大写词条（导演轨）、无大写对应小写词条（特工轨），语义对得上。
                    // 注意：这是边沿 toggle，轨道与系统 CapsLock 灯状态解耦。
                    bool switchPressed = keyboard.capsLockKey.wasPressedThisFrame;

                    if (extendedSwitchKeys)
                    {
                        switchPressed |= keyboard.leftCtrlKey.wasPressedThisFrame ||
                                         keyboard.rightCtrlKey.wasPressedThisFrame ||
                                         keyboard.leftShiftKey.wasPressedThisFrame ||
                                         keyboard.rightShiftKey.wasPressedThisFrame;
                    }

                    if (switchPressed) ToggleTrack(false);
                }
                else
                {
                    bool held = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                    SetTrack(held ? CharacterTrack.Director : CharacterTrack.Agent, false);
                }
            }

            if (!LettersEnabled)
                return;

            // 字母键：键码级读取，与大小写无关
            foreach (var key in LetterKeys)
            {
                if (keyboard[key].wasPressedThisFrame)
                    EmitLetter(CurrentTrack, key);
            }
        }

        private void ToggleTrack(bool forced)
            => SetTrack(CurrentTrack == CharacterTrack.Agent
                ? CharacterTrack.Director
                : CharacterTrack.Agent, forced);

        private void EmitLetter(CharacterTrack track, Key key)
        {
            OnLetterTyped?.Invoke(track, key);
            Record(track, key);
        }

        private void SetTrack(CharacterTrack track, bool forced)
        {
            if (track == CurrentTrack)
                return;

            CurrentTrack = track;
            LastChangeWasForced = forced;
            OnTrackChanged?.Invoke(track);
        }

        private void Record(CharacterTrack track, Key key)
        {
            string prefix = track == CharacterTrack.Agent ? "[AGENT]  " : "[DIRECTOR]";
            _history.Add($"{prefix} {key}");
            if (_history.Count > 14)
                _history.RemoveAt(0);
        }

        private void OnGUI()
        {
            // 调试面板只在编辑器/开发版里画（正式包不显示）
            if (!showDebugOverlay || !(Application.isEditor || Debug.isDebugBuild)) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.UpperRight
            };

            string trackLabel = CurrentTrack == CharacterTrack.Agent
                ? "AGENT (lowercase / red)"
                : "DIRECTOR (uppercase / blue)";

            const float margin = 20f;
            const float width = 720f;
            float x = Screen.width - width - margin;

            // 底部锚定：历史记录贴底，状态信息依次向上排布。
            float totalHeight = 116f + _history.Count * 22f;
            float y = Screen.height - margin - totalHeight;

            string gates = $"letters:{(LettersEnabled ? "ON" : "OFF")}  switch:{(SwitchEnabled ? "ON" : "OFF")}";
            string switchKeys = extendedSwitchKeys ? "CapsLock (+ Ctrl / Shift)" : "CapsLock";

            GUI.Label(new Rect(x, y, width, 26), $"Current Track: {trackLabel}   [{gates}]", style);
            GUI.Label(new Rect(x, y + 30, width, 26),
                $"Mode: {(toggleMode ? "TOGGLE (press once)" : "HOLD")}   |   Switch keys: {switchKeys}", style);
            GUI.Label(new Rect(x, y + 60, width, 26),
                "(lowercase words = AGENT, UPPERCASE words = DIRECTOR)", style);

            for (int i = 0; i < _history.Count; i++)
            {
                GUI.Label(new Rect(x, y + 116 + i * 22, width, 22), _history[i], style);
            }
        }
    }
}
