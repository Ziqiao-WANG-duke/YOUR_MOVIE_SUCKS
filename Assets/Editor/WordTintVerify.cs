using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using YourMovieSucks.Combat;
using YourMovieSucks.Input;
using YourMovieSucks.Level;
using YourMovieSucks.Typing;
using YourMovieSucks.UI;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 怪物词条「字体 + 威胁配色」验收脚本：字体是否是 Special Elite、颜色是否随距离连续地从白→黄→红、
    /// 攻击态是否锁红并闪烁，同时抓 4 张图留证据。
    ///
    /// 用法：
    ///   菜单：   YourMovieSucks/Verify ENEMY LABEL Font + Threat Tint
    ///   批处理： -executeMethod YourMovieSucks.EditorTools.WordTintVerify.Run
    /// 报告写到 Library/Locus/tmp/word-tint-verify.txt（批处理用退出码 1 表示有失败）。
    ///
    /// 进入 Play Mode 会触发域重载、静态回调会被清空 —— 所以靠 SessionState 在重载后重接。
    /// </summary>
    [InitializeOnLoad]
    public static class WordTintVerify
    {
        private const string Flag = "YourMovieSucks.WordTintVerify";
        private const string ReportPath = "Library/Locus/tmp/word-tint-verify.txt";
        private const string FontResourcePath = "Fonts/SpecialElite";

        /// <summary>词条字号（WordLabel 默认 44）：三个测试距离都按“怪物根节点到相机的三维距离”给。</summary>
        private const float FarDistance = 9.5f;
        private const float MidDistance = 6f;
        private const float NearDistance = 2.6f;

        /// <summary>怪物根节点离地高度（StripperZombie 预制件的落点 y）。</summary>
        private const float MonsterY = 0.5f;

        private static int _step;
        private static float _wait;
        private static int _pass;
        private static int _fail;

        private static TypingController _typing;
        private static TrackInput _track;
        private static GameObject _monsterGo;
        private static TypingTarget _monster;
        private static WordLabel _label;
        private static Camera _camera;
        private static Vector3 _wp2Position;
        private static Quaternion _wp2Rotation;

        private static bool _sampling;
        private static Color _prevTint;
        private static bool _hasPrev;
        private static float _maxFrameJump;

        private static bool _flashSampling;
        private static float _flashMin = float.PositiveInfinity;
        private static float _flashMax = float.NegativeInfinity;
        private static float _flashGreenMax = float.NegativeInfinity;
        private static int _flashSamples;

        static WordTintVerify()
        {
            if (SessionState.GetBool(Flag, false)) EditorApplication.update += Tick;
        }

        [MenuItem("YourMovieSucks/Verify ENEMY LABEL Font + Threat Tint")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
            File.WriteAllText(ReportPath, "== ENEMY LABEL font + threat tint verify ==\n");

            SessionState.SetBool(Flag, true);
            EditorSceneManager.OpenScene("Assets/Scenes/PolePosition.unity", OpenSceneMode.Single);

            _step = 0;
            _wait = 0f;
            Debug.Log("[WordTintVerify] entering play mode");
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (_sampling || _flashSampling) SampleFrame();
            if (Time.realtimeSinceStartup < _wait) return;

            switch (_step)
            {
                case 0: Setup(); break;
                case 1: CheckFar(); break;
                case 2: CheckMid(); break;
                case 3: CheckNear(); break;
                case 4: CheckAttackingFlash(); break;
            }
        }

        // ---------------------------------------------------------------- steps

        private static void Setup()
        {
            var flow = Object.FindFirstObjectByType<YourMovieSucks.Demo.PolePositionFlow>();
            if (flow != null) { flow.StopAllCoroutines(); flow.enabled = false; }

            var drive = Object.FindFirstObjectByType<YourMovieSucks.Demo.PolePositionAutoDrive>();
            if (drive != null) { drive.StopAllCoroutines(); drive.enabled = false; }

            var rail = Object.FindFirstObjectByType<RailCameraController>();
            if (rail != null) { rail.StopAllCoroutines(); rail.enabled = false; }

            _typing = Object.FindFirstObjectByType<TypingController>();
            _track = Object.FindFirstObjectByType<TrackInput>();
            _camera = Camera.main;

            if (_typing == null || _track == null || _camera == null)
            {
                Fail("找不到 TypingController / TrackInput / Main Camera");
                Finish();
                return;
            }

            var wp2 = GameObject.Find("WP2_Combat");
            if (wp2 != null) _camera.transform.SetPositionAndRotation(wp2.transform.position, wp2.transform.rotation);
            _wp2Position = _camera.transform.position;
            _wp2Rotation = _camera.transform.rotation;

            _typing.ClearGate();
            foreach (var word in new List<TypingWord>(_typing.Words)) _typing.RemoveWord(word);
            foreach (var old in Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<WordLabel>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);

            _track.SetLetterInput(true);
            _track.SetSwitchInput(true);
            _track.ForceTrack(CharacterTrack.Agent);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StripperZombie.prefab");
            if (prefab == null)
            {
                Fail("找不到 Assets/Prefabs/StripperZombie.prefab");
                Finish();
                return;
            }

            Vector3 position = ResolveMonsterPosition(FarDistance);
            _monsterGo = Object.Instantiate(prefab, position, Quaternion.LookRotation(_camera.transform.position - position));
            _monsterGo.name = "TintVerifyZombie";
            _monster = _monsterGo.GetComponent<TypingTarget>();
            _monster.SetLane(0f);
            _monster.Frozen = true;   // 定住：三个距离点都是固定值，颜色才能断言

            _typing.SpawnWord("MINE", CharacterTrack.Agent, 0f, _monsterGo);

            _step = 1;
            Next(0.6f);
        }

        private static void CheckFar()
        {
            _label = FindLabel("WordLabel_MINE");
            if (_label == null)
            {
                Fail("没生成词条标签 WordLabel_MINE（WordLabelManager 没接线？）");
                Finish();
                return;
            }

            var text = _label.transform.Find("Text") != null ? _label.transform.Find("Text").GetComponent<Text>() : null;
            var expected = Resources.Load<Font>(FontResourcePath);

            Check($"Assets/Resources/{FontResourcePath}.ttf 已导入为 Font 资源", expected != null);
            Check($"词条字体 = {(text != null && text.font != null ? text.font.name : "<无>")}",
                text != null && expected != null && text.font == expected);

            Color tint = _label.ThreatTint;
            Check($"远处 {FarDistance:0.0} m → 白色（r={tint.r:0.00} g={tint.g:0.00} b={tint.b:0.00}）",
                tint.r > 0.9f && tint.g > 0.85f && tint.b > 0.78f);

            Capture("01_far_white");

            BeginSampling();
            MoveMonsterTo(MidDistance);
            _step = 2;
            Next(1.1f);
        }

        private static void CheckMid()
        {
            EndSampling();

            Color tint = _label.ThreatTint;
            Check($"中途 {MidDistance:0.0} m → 黄色（r={tint.r:0.00} g={tint.g:0.00} b={tint.b:0.00}）",
                tint.r > 0.9f && tint.g > 0.55f && tint.g < 0.95f && tint.b < 0.4f);
            Check($"颜色变化是连续的（单帧最大跳变 {_maxFrameJump:0.000} < 0.2）", _maxFrameJump < 0.2f);

            Capture("02_mid_yellow");

            BeginSampling();
            MoveMonsterTo(NearDistance);
            _step = 3;
            Next(1.1f);
        }

        private static void CheckNear()
        {
            EndSampling();

            Color tint = _label.ThreatTint;
            Check($"近身 {NearDistance:0.0} m → 红色（r={tint.r:0.00} g={tint.g:0.00} b={tint.b:0.00}）",
                tint.r > 0.7f && tint.g < 0.35f && tint.b < 0.35f);
            Check($"近身段的颜色变化也是连续的（单帧最大跳变 {_maxFrameJump:0.000} < 0.2）", _maxFrameJump < 0.2f);

            Capture("03_near_red");

            // 直接进入攻击态（Frozen 会冻结攻击倒计时，所以不会真的扣血）
            _monster.StartAttack();
            BeginFlashSampling();
            _step = 4;
            Next(0.9f);
        }

        private static void CheckAttackingFlash()
        {
            float spread = _flashMax - _flashMin;
            Check($"攻击中在红色底子上闪烁（亮度 {_flashMin:0.00}–{_flashMax:0.00}，起伏 {spread:0.00}，采样 {_flashSamples} 帧）",
                _flashSamples >= 10 && spread > 0.2f);
            Check($"攻击中始终是红色（采样 {_flashSamples} 帧，最亮时绿色通道 {_flashGreenMax:0.00} < 0.4）",
                _flashSamples > 0 && _flashGreenMax < 0.4f);

            Capture("04_attacking_flash");

            Finish();
        }

        // ---------------------------------------------------------------- 采样

        private static void BeginSampling()
        {
            _sampling = true;
            _hasPrev = false;
            _maxFrameJump = 0f;
        }

        private static void EndSampling()
        {
            _sampling = false;
        }

        /// <summary>逐帧记录颜色跳变：连续过渡的判据是“任何一帧的任一通道变化都很小”。</summary>
        private static void SampleFrame()
        {
            if (_label == null) return;

            Color tint = _label.ThreatTint;
            if (_hasPrev)
            {
                float jump = Mathf.Max(
                    Mathf.Abs(tint.r - _prevTint.r),
                    Mathf.Abs(tint.g - _prevTint.g),
                    Mathf.Abs(tint.b - _prevTint.b));
                if (jump > _maxFrameJump) _maxFrameJump = jump;
            }

            _prevTint = tint;
            _hasPrev = true;

            if (!_flashSampling) return;

            float brightness = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
            if (brightness < _flashMin) _flashMin = brightness;
            if (brightness > _flashMax) _flashMax = brightness;
            if (tint.g > _flashGreenMax) _flashGreenMax = tint.g;
            _flashSamples++;
        }

        private static void BeginFlashSampling()
        {
            _flashSampling = true;
            _flashMin = float.PositiveInfinity;
            _flashMax = float.NegativeInfinity;
            _flashGreenMax = float.NegativeInfinity;
            _flashSamples = 0;
        }

        private static void MoveMonsterTo(float distance)
        {
            if (_monsterGo == null) return;
            _monsterGo.transform.position = ResolveMonsterPosition(distance);
        }

        /// <summary>相机正前方、与相机三维距离 = distance 的地面点（怪物根节点 y 固定）。</summary>
        private static Vector3 ResolveMonsterPosition(float distance)
        {
            Vector3 forward = _camera.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;

            float vertical = _wp2Position.y - MonsterY;
            float horizontal = Mathf.Sqrt(Mathf.Max(distance * distance - vertical * vertical, 0.04f));

            Vector3 position = _wp2Position + forward * horizontal;
            position.y = MonsterY;
            return position;
        }

        // ---------------------------------------------------------------- helpers

        private static WordLabel FindLabel(string name)
        {
            foreach (var label in Object.FindObjectsByType<WordLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label != null && label.gameObject.name == name) return label;
            }

            return null;
        }

        /// <summary>把主相机渲进 RenderTexture 存 PNG（世界空间词条会被拍到；屏空间 HUD 不会）。</summary>
        private static void Capture(string name)
        {
            if (_camera == null) return;

            const int width = 1280;
            const int height = 720;
            RenderTexture rt = null;
            RenderTexture previousTarget = _camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                _camera.targetTexture = rt;
                _camera.Render();
                _camera.targetTexture = previousTarget;

                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Locus/tmp/tint-shots"));
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), texture.EncodeToPNG());
                Object.Destroy(texture);
                Debug.Log("[WordTintVerify] capture " + name);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WordTintVerify] capture failed: " + e.Message);
            }
            finally
            {
                _camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (rt != null) rt.Release();
            }
        }

        private static void Next(float wait = 0.35f)
        {
            _wait = Time.realtimeSinceStartup + wait;
        }

        private static void Check(string label, bool ok)
        {
            if (ok) _pass++; else _fail++;
            string line = $"[{(ok ? "PASS" : "FAIL")}] {label}";
            if (!ok) Debug.LogWarning("[WordTintVerify] " + line);
            else Debug.Log("[WordTintVerify] " + line);
            File.AppendAllText(ReportPath, line + "\n");
        }

        private static void Fail(string label)
        {
            _fail++;
            Debug.LogWarning("[WordTintVerify] [FAIL] " + label);
            File.AppendAllText(ReportPath, "[FAIL] " + label + "\n");
        }

        private static void Finish()
        {
            string summary = $"== summary: {_pass} pass / {_fail} fail ==";
            File.AppendAllText(ReportPath, summary + "\n");
            Debug.Log("[WordTintVerify] " + summary);

            EditorApplication.update -= Tick;
            SessionState.SetBool(Flag, false);

#if UNITY_EDITOR
            if (Application.isBatchMode) EditorApplication.Exit(_fail > 0 ? 1 : 0);
#endif
            _step = 99;
        }
    }
}
