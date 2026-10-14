using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YourMovieSucks.Input;
using YourMovieSucks.Level;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Combat
{
    /// <summary>
    /// 核心循环流程状态机：
    /// 到达机位 → 刷怪 → 全清 → 2s 后自动推进（与导演轨解耦）。
    /// 击杀会爆浆（自动渐隐），并在场上有余敌时开启抓拍窗口（Money Shot）。
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        [SerializeField] private TypingController typing;
        [SerializeField] private RailCameraController rail;
        [SerializeField] private ScreenBlood blood;
        [SerializeField] private DirectorStage directorStage;

        [Header("Waves")]
        [SerializeField] private int enemiesPerWave = 2;
        [SerializeField] private float waveClearAdvanceDelay = 2f;

        private static readonly string[] EnemyWords =
            { "COP", "FAT", "BANG", "RAT", "DOG", "CAT" };

        private readonly List<TypingTarget> _aliveEnemies = new();
        private int _waveIndex;

        private void Awake()
        {
            if (typing == null) typing = GetComponent<TypingController>();
            if (typing == null) typing = Object.FindFirstObjectByType<TypingController>();
            if (rail == null) rail = Object.FindFirstObjectByType<RailCameraController>();
            if (blood == null) blood = GetComponent<ScreenBlood>();
            if (directorStage == null) directorStage = GetComponent<DirectorStage>();

            if (rail != null) rail.OnWaypointReached += HandleWaypointReached;
        }

        private void OnDestroy()
        {
            if (rail != null) rail.OnWaypointReached -= HandleWaypointReached;
        }

        private void Start()
        {
            CreateLighting();
        }

        private void CreateLighting()
        {
            var lightGo = new GameObject("StageLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 6f;
            light.color = new Color(1f, 0.4f, 0.85f); // 霓虹粉紫
            lightGo.transform.position = new Vector3(0f, 3f, 4f);

            lightGo.AddComponent<StageLighting>(); // 默认初始熄灭（暗区）

            typing.SpawnWord("NEON", CharacterTrack.Director, 0f, lightGo);
        }

        private void HandleWaypointReached(int index)
        {
            SpawnWave(index);
        }

        private void SpawnWave(int waveIndex)
        {
            _waveIndex = waveIndex;

            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : new Vector3(0f, 1.5f, 0f);
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;

            for (int i = 0; i < enemiesPerWave; i++)
            {
                // 一前一后：近处 5，远处 7.5；左右稍微错开
                float dist = 5f + i * 2.5f;
                float x = (i - (enemiesPerWave - 1) * 0.5f) * 1.2f;
                Vector3 pos = camPos + forward * dist + right * x;
                pos.y = 1.5f;

                string word = EnemyWords[(waveIndex * enemiesPerWave + i) % EnemyWords.Length];
                SpawnEnemy(pos, word);
            }
        }

        private void SpawnEnemy(Vector3 position, string word)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Enemy_{word}_{_waveIndex}";
            cube.transform.position = position;

            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.red;

            var target = cube.AddComponent<TypingTarget>();
            target.OnDeath += () =>
            {
                _aliveEnemies.Remove(target);

                // 每次击杀爆浆（自动渐隐），与推进解耦
                if (blood != null) blood.Splatter();

                if (_aliveEnemies.Count == 0)
                {
                    // 全清：导演可在 2s 空隙内做别的操作（如 WIPE），推进不依赖导演
                    typing.SpawnWord("WIPE", CharacterTrack.Director, waveClearAdvanceDelay, null);
                    StartCoroutine(AdvanceAfterDelay());
                }
                else
                {
                    // 开启抓拍窗口（Money Shot）
                    if (directorStage != null) directorStage.OpenSnapshotWindow();

                    // 剩余怪物中一只加速靠近
                    foreach (var e in _aliveEnemies)
                    {
                        if (e != null && !e.IsDead)
                        {
                            e.Rush();
                            break;
                        }
                    }
                }
            };

            _aliveEnemies.Add(target);
            typing.SpawnWord(word, CharacterTrack.Agent, 0f, cube);
        }

        private IEnumerator AdvanceAfterDelay()
        {
            yield return new WaitForSeconds(waveClearAdvanceDelay);
            if (rail != null) rail.Advance();
        }
    }
}
