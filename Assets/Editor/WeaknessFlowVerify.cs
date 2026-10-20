using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using YourMovieSucks.Combat;
using YourMovieSucks.Input;
using YourMovieSucks.Level;
using YourMovieSucks.Typing;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 导演轨「弱点识别」验收脚本：自动跑完作业里的 8 步流程并写报告。
    ///
    ///   1 刷一只怪，特工轨显示原始战斗词
    ///   2 切导演轨 → 战斗词标签隐藏
    ///   3 弱点红色标记出现 + 弱点词条出现
    ///   4 输入弱点词 → 已识别、镜头推到弱点
    ///   5 切回特工轨 → 换成更简单的战斗词
    ///   6 输入新词 → 怪物被击杀
    ///   7 结算统计口径：弱点词不计入战斗单词/字母
    ///
    /// 用法：
    ///   批处理： -executeMethod YourMovieSucks.EditorTools.WeaknessFlowVerify.Run
    ///   菜单：   YourMovieSucks/Verify DIRECTOR WEAKNESS Flow
    /// 结论写到 Library/Locus/tmp/weakness-verify.txt（批处理用退出码 1 表示有失败）。
    /// 进入 Play Mode 会触发域重载、静态回调会被清空 —— 所以靠 SessionState 在重载后重接。
    /// </summary>
    [InitializeOnLoad]
    public static class WeaknessFlowVerify
    {
        private const string Flag = "YourMovieSucks.WeaknessVerify";
        private const string ReportPath = "Library/Locus/tmp/weakness-verify.txt";

        private static readonly List<string> Lines = new();
        private static int _step;
        private static float _wait;
        private static float _settle = 0.6f;

        private static TypingController _typing;
        private static TrackInput _track;
        private static TypingTarget _monster;
        private static MonsterWeakness _weakness;
        private static WeakPointCloseUpCamera _closeUp;
        private static TypingWord _agentWord;
        private static string _queue = string.Empty;
        private static int _queueIndex;
        private static Vector3 _cameraBefore;
        private static string _simplifiedMatch = string.Empty;
        private static int _pass;
        private static int _fail;

        static WeaknessFlowVerify()
        {
            if (SessionState.GetBool(Flag, false)) EditorApplication.update += Tick;
        }

        [MenuItem("YourMovieSucks/Verify DIRECTOR WEAKNESS Flow")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
            File.WriteAllText(ReportPath, "== DIRECTOR WEAKNESS flow verify ==\n");

            SessionState.SetBool(Flag, true);
            EditorSceneManager.OpenScene("Assets/Scenes/PolePosition.unity", OpenSceneMode.Single);

            _step = 0;
            _wait = 0f;
            Debug.Log("[WeaknessVerify] entering play mode");
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (Time.realtimeSinceStartup < _wait) return;

            switch (_step)
            {
                case 0: Setup(); break;
                case 1: SwitchToDirector(); break;
                case 2: CheckDirectorView(); break;
                case 3: TypeNextLetter(); break;
                case 4: CheckRecognition(); break;
                case 5: BackToAgent(); break;
                case 6: TypeNextLetter(); break;
                case 7: CheckKillAndFinish(); break;
            }
        }

        // ---------------------------------------------------------------- steps

        private static void Setup()
        {
            var flow = Object.FindFirstObjectByType<YourMovieSucks.Demo.PolePositionFlow>();
            if (flow != null) { flow.StopAllCoroutines(); flow.enabled = false; }

            var rail = Object.FindFirstObjectByType<RailCameraController>();
            if (rail != null) rail.enabled = false;

            _typing = Object.FindFirstObjectByType<TypingController>();
            _track = Object.FindFirstObjectByType<TrackInput>();
            _closeUp = Object.FindFirstObjectByType<WeakPointCloseUpCamera>();

            if (_typing == null || _track == null)
            {
                Fail("找不到 TypingController / TrackInput");
                Finish();
                return;
            }

            _track.SetLetterInput(true);
            _track.SetSwitchInput(true);
            _track.ForceTrack(CharacterTrack.Agent);

            var wp2 = GameObject.Find("WP2_Combat");
            var camera = Camera.main;
            if (wp2 != null && camera != null)
                camera.transform.SetPositionAndRotation(wp2.transform.position, wp2.transform.rotation);

            foreach (var old in Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<YourMovieSucks.UI.WordLabel>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StripperZombie.prefab");
            var spawn = GameObject.Find("SpawnLeft");
            Vector3 position = spawn != null ? spawn.transform.position : new Vector3(-4.2f, 0.5f, 12f);

            var go = Object.Instantiate(prefab, position, Quaternion.LookRotation(camera.transform.position - position));
            go.name = "VerifyZombie";
            _monster = go.GetComponent<TypingTarget>();
            _monster.SetLane(-2.6f);
            _weakness = go.GetComponent<MonsterWeakness>();

            _agentWord = _typing.SpawnWord("MINE", CharacterTrack.Agent, 0f, go);
            if (_weakness != null) _weakness.OriginalWord = "MINE";

            Check("怪物已生成，特工轨战斗词 = MINE", _agentWord != null);
            Check("怪物带弱点组件，弱点词 = " + (_weakness != null ? _weakness.WeakWord : "<无>"), _weakness != null && !string.IsNullOrEmpty(_weakness.WeakWord));
            Check("特工轨战斗词标签可见", LabelActive("WordLabel_MINE", true));

            _cameraBefore = camera.transform.position;
            _step = 1;
            Next();
        }

        private static void SwitchToDirector()
        {
            _track.ForceTrack(CharacterTrack.Director);
            _step = 2;
            Next();
        }

        private static void CheckDirectorView()
        {
            Check("切导演轨后，原战斗词标签被隐藏", LabelActive("WordLabel_MINE", false));

            TypingWord weaknessWord = null;
            foreach (var word in _typing.Words)
            {
                if (word == null || !word.IsWeakness) continue;
                if (word.Target == _weakness.LabelAnchor) { weaknessWord = word; break; }
            }

            Check("导演轨出现弱点词条 " + (_weakness != null ? _weakness.WeakWord : "?"), weaknessWord != null);

            var marker = _weakness.WeakPoint.Find("WeakPointMarker");
            Check("弱点红色标记已显示", marker != null && marker.gameObject.activeSelf);

            if (weaknessWord == null || _weakness == null)
            {
                Finish();
                return;
            }

            Capture("01_director_view_marker_and_weak_word");

            _queue = weaknessWord.MatchText;
            _queueIndex = 0;
            _step = 3;
            Next();
        }

        private static void TypeNextLetter()
        {
            if (_queueIndex >= _queue.Length)
            {
                _step = _step == 3 ? 4 : 7;
                Next(_step == 4 ? 0.3f : 0.8f);
                return;
            }

            char letter = _queue[_queueIndex++];
            _track.InjectLetter((Key)((int)Key.A + (letter - 'A')));
            Next(0.12f);
        }

        private static void CheckRecognition()
        {
            Check("弱点词输入完成 → 怪物标记为已识别", _weakness.Recognized);

            Capture("02_close_up_on_weak_point");

            float toWeakPoint = Vector3.Distance(Camera.main.transform.position, _weakness.WeakPoint.position);
            float moved = Vector3.Distance(Camera.main.transform.position, _cameraBefore);
            Check($"镜头推进到弱点（离弱点 {toWeakPoint:0.00} m，位移 {moved:0.00} m）", toWeakPoint < 1.6f);

            var combat = FindCombatWord(_monster.gameObject);
            _simplifiedMatch = combat != null ? combat.MatchText : "<无>";
            Check("战斗词已换成更简单的词（" + _simplifiedMatch + "）", combat != null && _simplifiedMatch != "MINE");

            Check("弱点标记已隐藏", _weakness.WeakPoint.Find("WeakPointMarker") == null || !_weakness.WeakPoint.Find("WeakPointMarker").gameObject.activeSelf);

            // 回归：特写把相机挪到怪物脸前，而“到站”看的是到相机的距离 —— 怪物不能因此原地开打
            Check("特写期间怪物没有误判到站（未进入攻击态）", !_monster.IsAttacking);

            _track.ForceTrack(CharacterTrack.Agent);
            _step = 5;
            Next(1.2f);   // 等特写播完（推 0.18 + 保持 0.5 + 回位 0.28），才能检查定格是否解除
        }

        private static void BackToAgent()
        {
            Check("切回特工轨后，新战斗词标签可见", LabelActive("WordLabel_" + _simplifiedMatch, true));
            Capture("03_agent_view_simplified_word");

            // 回归：镜头回位后怪物必须还在走向玩家，不能停在原地（离玩家很远）开打
            float toCamera = Vector3.Distance(_monster.transform.position, Camera.main.transform.position);
            Check($"特写结束后怪物解除定格、继续走向玩家（离相机 {toCamera:0.00} m，state={_monster.State}）",
                !_monster.SpotlightHold && !_monster.IsAttacking && toCamera > 2f);

            int weaknessWords = 0;
            foreach (var word in _typing.Words) if (word != null && word.IsWeakness) weaknessWords++;
            Check("导演轨弱点词条已回收（剩余 " + weaknessWords + "）", weaknessWords == 0);

            var combat = FindCombatWord(_monster.gameObject);
            if (combat == null)
            {
                Fail("找不到新的战斗词，无法继续");
                Finish();
                return;
            }

            _queue = combat.MatchText;
            _queueIndex = 0;
            _step = 6;
            Next();
        }

        private static void CheckKillAndFinish()
        {
            Check("输入新战斗词后怪物被击杀", _monster == null || _monster.IsDead);

            var stats = Object.FindFirstObjectByType<YourMovieSucks.Demo.RunStats>();
            if (stats != null)
            {
                Check($"弱点识别计数 = {stats.WeaknessRecognitions}", stats.WeaknessRecognitions >= 1);
                Check($"战斗字母数 = {stats.LettersTyped}（不含弱点词的 {(_weakness != null ? _weakness.WeakWord.Length : 0)} 个字母）",
                    stats.LettersTyped < 20);
            }

            Finish();
        }

        // ---------------------------------------------------------------- helpers

        private static TypingWord FindCombatWord(GameObject monster)
        {
            foreach (var word in _typing.Words)
            {
                if (word == null || word.IsWeakness || word.IsGate || word.Target == null) continue;
                if (word.Target == monster) return word;
            }

            return null;
        }

        /// <summary>注意：GameObject.Find 找不到 inactive 对象，而“已隐藏”的标签恰恰是 inactive —— 必须连 inactive 一起查。</summary>
        private static bool LabelActive(string name, bool expectedVisible)
        {
            foreach (var label in Object.FindObjectsByType<YourMovieSucks.UI.WordLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label == null || label.gameObject.name != name) continue;
                return label.gameObject.activeSelf == expectedVisible;
            }

            return false;
        }

        /// <summary>把主相机渲进 RenderTexture 存 PNG（世界空间的弱点标记与词条会被拍到；屏空间 HUD 不会）。</summary>
        private static void Capture(string name)
        {
            var camera = Camera.main;
            if (camera == null) return;

            const int width = 1280;
            const int height = 720;
            RenderTexture rt = null;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = previousTarget;

                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Locus/tmp/weakness-shots"));
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), texture.EncodeToPNG());
                Object.Destroy(texture);
                Debug.Log("[WeaknessVerify] capture " + name);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WeaknessVerify] capture failed: " + e.Message);
            }
            finally
            {
                camera.targetTexture = previousTarget;
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
            Lines.Add(line);
            if (!ok) Debug.LogWarning("[WeaknessVerify] " + line);
            else Debug.Log("[WeaknessVerify] " + line);
            File.AppendAllText(ReportPath, line + "\n");
        }

        private static void Fail(string label)
        {
            _fail++;
            Lines.Add("[FAIL] " + label);
            Debug.LogWarning("[WeaknessVerify] [FAIL] " + label);
            File.AppendAllText(ReportPath, "[FAIL] " + label + "\n");
        }

        private static void Finish()
        {
            string summary = $"== summary: {_pass} pass / {_fail} fail ==";
            Lines.Add(summary);
            File.AppendAllText(ReportPath, summary + "\n");
            Debug.Log("[WeaknessVerify] " + summary);

            EditorApplication.update -= Tick;
            SessionState.SetBool(Flag, false);

#if UNITY_EDITOR
            if (Application.isBatchMode) EditorApplication.Exit(_fail > 0 ? 1 : 0);
#endif
            _step = 99;
        }
    }
}
