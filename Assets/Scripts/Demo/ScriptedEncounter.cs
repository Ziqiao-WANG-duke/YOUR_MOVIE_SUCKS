using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YourMovieSucks.Combat;
using YourMovieSucks.Input;
using YourMovieSucks.LabB;
using YourMovieSucks.Typing;

namespace YourMovieSucks.Demo
{
    /// <summary>
    /// Demo 关卡战斗编排（脚本第 6–7 段）：
    /// ① 脚本波次 1–4：固定词序、每波 1 只、打完才刷下一只（mine → my pole → get off → wait your turn）；
    /// ② MAIN COMBAT：总数由调参面板驱动（默认 4 + 8 = 12，与脚本结算示例一致），
    ///    同屏数量逐渐递减（2 → 1），9 条「钢管争夺」词池轮转，清零后结束。
    /// 怪物身体 = 丧尸预制件（`Assets/Prefabs/StripperZombie.prefab`，骨骼动画 + 贴图）；
    /// 若没接 zombiePrefab（例如老场景或生成器未接线）则退回灰盒方块，保证关卡照样能跑。
    /// </summary>
    public class ScriptedEncounter : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TypingController typing;
        [SerializeField] private ScreenBlood blood;
        [SerializeField] private Transform poleAnchor;
        [SerializeField] private Transform entranceSpawn;
        [SerializeField] private Transform[] spawnPoints;

        [Header("怪物外观")]
        [Tooltip("丧尸预制件（Assets/Prefabs/StripperZombie.prefab）。留空则退回灰盒方块占位体")]
        [SerializeField] private GameObject zombiePrefab;

        [Header("脚本波次（固定词序，每波 1 只）")]
        [SerializeField] private string[] waveWords = { "mine", "my pole", "get off", "wait your turn" };

        [Header("主战斗")]
        [SerializeField] private string[] mainWords =
        {
            "MINE", "MY POLE", "GET OFF", "BACK OFF", "MY TURN",
            "NOT YOURS", "GET IN LINE", "WAIT YOUR TURN", "THIS IS MY STAGE"
        };

        [Tooltip("面板关闭（Enabled=false）时使用的主战斗怪物数")]
        [SerializeField] private int mainCombatEnemies = 8;

        [Tooltip("主战斗同屏上限")]
        [SerializeField] private int mainMaxConcurrent = 2;

        [Tooltip("主战斗收尾时的同屏上限（数量递减）")]
        [SerializeField] private int mainEndConcurrent = 1;

        [Tooltip("主战斗刷怪间隔")]
        [SerializeField] private float spawnInterval = 1.1f;

        [Header("入场演出")]
        [SerializeField] private float entranceDashDuration = 0.45f;
        [SerializeField] private float entranceHugHold = 2.5f;

        [Tooltip("刷怪点对应的横向车道（与 spawnPoints 同序，沿相机右方向的世界单位）。\n" +
                 "不同怪守不同车道，头顶词条才不会横向叠在一起")]
        [SerializeField] private float[] spawnLaneOffsets = { -2.6f, 2.6f, 0f };

        [Tooltip("入场演出（第 1 只，抱钢管）用的车道")]
        [SerializeField] private float entranceLane;

        private readonly List<TypingTarget> _alive = new();
        private readonly Queue<string> _mainQueue = new();

        /// <summary>每个刷怪点当前的占用者：该点上一只没死就不会再从同一点刷，避免同点叠怪、词条完全重叠。</summary>
        private readonly Dictionary<int, TypingTarget> _spawnOccupant = new();

        private TypingTarget _entranceZombie;
        private int _remainingPool;
        private bool _running;
        private bool _cleared;
        private string _lastMainWord = string.Empty;

        public bool IsCleared => _cleared;
        public int AliveCount => CountAlive();

        public event System.Action OnCleared;

        // ---------------------------------------------------------------- 演出层入口

        /// <summary>脚本第 5 段：刷出第一只并让它冲入舞台抱住钢管（此时还没有词条）。</summary>
        public IEnumerator PlayEntrance()
        {
            Vector3 pos = entranceSpawn != null
                ? entranceSpawn.position
                : (poleAnchor != null ? poleAnchor.position + new Vector3(4.5f, 0f, 1.2f) : new Vector3(4.5f, 1f, 8f));

            _entranceZombie = CreateEnemy(pos, true, entranceLane);

            yield return new WaitForSeconds(entranceDashDuration + 0.1f);
        }

        /// <summary>脚本第 6–7 段：给入场僵尸挂上第一波词条，然后自动推进到全清。</summary>
        public void BeginCombat()
        {
            if (_running) return;
            _running = true;
            StartCoroutine(CombatRoutine());
        }

        public IEnumerator WaitUntilCleared()
        {
            while (!_cleared) yield return null;
        }

        // ---------------------------------------------------------------- 波次流程

        private IEnumerator CombatRoutine()
        {
            // 波次 1：入场僵尸（已在 PlayEntrance 刷出，抱着钢管）
            yield return RunSingleWave(_entranceZombie, WaveWord(0));

            // 波次 2–4：从舞台侧翼陆续刷入（每个点一只，等空点）
            for (int i = 1; i < waveWords.Length; i++)
            {
                int pointIndex;
                while (!TryPickFreeSpawnPoint(out pointIndex)) yield return null;

                var zombie = CreateEnemy(SpawnPointAt(pointIndex), false, LaneAt(pointIndex), pointIndex);
                yield return RunSingleWave(zombie, WaveWord(i));
            }

            // MAIN COMBAT：总数递减，同屏上限逐渐收紧
            _remainingPool = ResolveMainTotal();
            RefillMainQueue();

            while (_remainingPool > 0 || CountAlive() > 0)
            {
                while (_remainingPool > 0 && CountAlive() < CurrentConcurrentLimit())
                {
                    // 同首字母的词条不许同时在场上：按首字母吸附会出现“想打 my turn 却锁到 my pole”
                    if (!TrySpawnMainEnemy())
                    {
                        yield return null;   // 等场上词条打完再刷，不消耗总数
                        continue;
                    }

                    _remainingPool--;
                    yield return new WaitForSeconds(spawnInterval);
                }

                yield return null;
            }

            _cleared = true;
            OnCleared?.Invoke();
        }

        private IEnumerator RunSingleWave(TypingTarget zombie, string word)
        {
            if (zombie == null || zombie.IsDead) yield break;

            typing.SpawnWord(word, CharacterTrack.Agent, 0f, zombie.gameObject);

            while (zombie != null && !zombie.IsDead)
                yield return null;
        }

        // ---------------------------------------------------------------- 刷怪

        /// <summary>
        /// 刷一只主战斗怪：只挑与场上活动词条**首字母不冲突**的词。
        /// 若队列里所有词都冲突则返回 false（本轮不刷，等场上词条打完再说），不消耗总数。
        /// 原因：锁定按首字母吸附，两个同首字母词同时在场上会出现
        /// “想打 my turn 却锁到 my pole”的歧义。
        /// </summary>
        private bool TrySpawnMainEnemy()
        {
            // 两个前提：① 有与场上词条首字母不冲突的词 ② 有一个空着的刷怪点（同一点不叠怪）
            if (!TryTakeCompatibleWord(out string word)) return false;
            if (!TryPickFreeSpawnPoint(out int pointIndex)) return false;

            var zombie = CreateEnemy(SpawnPointAt(pointIndex), false, LaneAt(pointIndex), pointIndex);
            typing.SpawnWord(word, CharacterTrack.Agent, 0f, zombie.gameObject);
            return true;
        }

        private bool TryTakeCompatibleWord(out string word)
        {
            word = null;
            if (_mainQueue.Count == 0) RefillMainQueue();

            int candidates = _mainQueue.Count;
            for (int i = 0; i < candidates; i++)
            {
                string candidate = _mainQueue.Dequeue();

                if (!IsInitialTaken(candidate))
                {
                    word = candidate;
                    _lastMainWord = word;
                    return true;
                }

                _mainQueue.Enqueue(candidate); // 首字母冲突：放回队尾，下次再试
            }

            return false;
        }

        private bool IsInitialTaken(string word)
        {
            if (typing == null || string.IsNullOrEmpty(word)) return false;
            return typing.HasActiveWordStartingWith(word[0]);
        }

        private TypingTarget CreateEnemy(Vector3 position, bool entrance, float laneOffset, int spawnIndex = -1)
        {
            var go = CreateEnemyBody(position, entrance);

            var target = go.GetComponent<TypingTarget>();
            if (target == null) target = go.AddComponent<TypingTarget>();

            if (entrance)
            {
                var entranceCmp = go.AddComponent<StripperZombieEntrance>();
                entranceCmp.Init(poleAnchor, entranceDashDuration, entranceHugHold);
            }

            target.SetLane(laneOffset);
            target.OnDeath += () => HandleEnemyDeath(target);
            _alive.Add(target);

            if (spawnIndex >= 0) _spawnOccupant[spawnIndex] = target;

            return target;
        }

        /// <summary>
        /// 怪物身体：默认刷丧尸预制件；没接线时退回灰盒方块。
        /// 出生即朝向玩家（模型正面 = +Z）：怪物是从舞台深处朝相机过来的，否则丧尸会倒着走。
        /// 车道与速度仍由 TypingTarget 控制，这里只管朝向。
        /// </summary>
        private GameObject CreateEnemyBody(Vector3 position, bool entrance)
        {
            if (zombiePrefab != null)
            {
                var body = Instantiate(zombiePrefab, position, FacePlayer(position));
                body.name = entrance ? "StripperZombie_Entrance" : "StripperZombie";
                return body;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = entrance ? "StripperZombie_Entrance" : "StripperZombie";
            cube.transform.position = position;
            cube.transform.localScale = new Vector3(0.8f, 1.8f, 0.55f);

            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(1f, 0.35f, 0.6f); // 霓虹粉变异舞女占位体（灰盒）

            return cube;
        }

        /// <summary>朝玩家（相机）方向的水平朝向；找不相机则保持原朝向。</summary>
        private static Quaternion FacePlayer(Vector3 position)
        {
            var camera = Camera.main;
            if (camera == null) return Quaternion.identity;

            Vector3 dir = camera.transform.position - position;
            dir.y = 0f;
            return dir.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(dir);
        }

        private void HandleEnemyDeath(TypingTarget target)
        {
            _alive.Remove(target);

            // 释放刷怪点：同一点上一只死了才会再刷下一只
            List<int> freed = null;
            foreach (var kvp in _spawnOccupant)
            {
                if (kvp.Value != target) continue;
                freed ??= new List<int>();
                freed.Add(kvp.Key);
            }

            if (freed != null)
                foreach (int key in freed) _spawnOccupant.Remove(key);

            // 击杀爆浆（自动渐隐）；Demo 脚本没有擦镜节拍，故不刷 WIPE 词条
            if (blood != null) blood.Splatter();
        }

        /// <summary>挑一个空着的刷怪点（该点上一只怪死了才算空）。挑不到返回 false，由调用方等下一帧再试。</summary>
        private bool TryPickFreeSpawnPoint(out int index)
        {
            index = -1;
            if (spawnPoints == null || spawnPoints.Length == 0) return false;

            int start = Random.Range(0, spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                int candidate = (start + i) % spawnPoints.Length;
                if (!IsSpawnPointFree(candidate)) continue;

                index = candidate;
                return true;
            }

            return false;
        }

        private bool IsSpawnPointFree(int index)
        {
            if (!_spawnOccupant.TryGetValue(index, out var occupant)) return true;
            return occupant == null || occupant.IsDead;
        }

        private Vector3 SpawnPointAt(int index)
        {
            if (spawnPoints == null || index < 0 || index >= spawnPoints.Length)
            {
                return poleAnchor != null
                    ? poleAnchor.position + new Vector3(Random.Range(-2.5f, 2.5f), -1f, 1.5f)
                    : new Vector3(Random.Range(-2.5f, 2.5f), 1f, 9f);
            }

            var point = spawnPoints[index];
            return point != null ? point.position : Vector3.forward * 9f;
        }

        private float LaneAt(int index)
        {
            if (spawnLaneOffsets == null || spawnLaneOffsets.Length == 0) return 0f;
            return spawnLaneOffsets[Mathf.Clamp(index, 0, spawnLaneOffsets.Length - 1)];
        }

        private int CountAlive()
        {
            int alive = 0;
            for (int i = 0; i < _alive.Count; i++)
            {
                var target = _alive[i];
                if (target != null && !target.IsDead) alive++;
            }
            return alive;
        }

        private int CurrentConcurrentLimit()
        {
            // 数量逐渐递减：最后 3 只开始只允许 1 只同屏
            return _remainingPool > 3 ? Mathf.Max(1, mainMaxConcurrent) : Mathf.Max(1, mainEndConcurrent);
        }

        private string WaveWord(int index)
        {
            if (waveWords == null || waveWords.Length == 0) return "mine";
            return waveWords[Mathf.Clamp(index, 0, waveWords.Length - 1)];
        }

        private int ResolveMainTotal()
        {
            var tuning = LabBTuning.I;
            int total = tuning != null && tuning.Enabled ? tuning.mainCombatTotalEnemies : mainCombatEnemies;
            return Mathf.Max(0, total);
        }

        private void RefillMainQueue()
        {
            _mainQueue.Clear();
            if (mainWords == null || mainWords.Length == 0) return;

            var pool = new List<string>(mainWords);
            while (pool.Count > 0)
            {
                int index = Random.Range(0, pool.Count);

                // 避免与上一个词紧邻重复
                if (pool[index] == _lastMainWord && pool.Count > 1)
                    index = (index + 1) % pool.Count;

                string word = pool[index];
                pool.RemoveAt(index);

                _mainQueue.Enqueue(word);
                _lastMainWord = word;
            }
        }
    }
}
