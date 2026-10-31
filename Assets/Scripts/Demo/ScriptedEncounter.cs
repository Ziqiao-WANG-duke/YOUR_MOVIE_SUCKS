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

        [Header("战斗编排（留空 = 用下面的硬编码波次）")]
        [Tooltip("由 Combat Editor 生成的战斗数据：怪物种类 / 每波长短词数量 / 波次数 / 长词与短词怪速度")]
        [SerializeField] private CombatEncounterAsset encounterAsset;

        /// <summary>战斗编排资产；为空时走硬编码波次（脚本波 4 + 主战斗）。</summary>
        public CombatEncounterAsset EncounterAsset
        {
            get => encounterAsset;
            set => encounterAsset = value;
        }

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
        [Tooltip("入场僵尸站定咆哮的定格时长（Scream 动画），结束后恢复常规接近/攻击")]
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
        private bool _shuffleWords;

        public bool IsCleared => _cleared;

        /// <summary>战斗已经开始（BeginCombat 之后、清场之前）。导演模式滤镜用它做门控。</summary>
        public bool IsRunning => _running && !_cleared;
        public int AliveCount => CountAlive();

        public event System.Action OnCleared;

        // ---------------------------------------------------------------- 演出层入口

        /// <summary>脚本第 5 段：刷出第一只并让它冲入舞台站定咆哮（此时还没有词条）。</summary>
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
            if (encounterAsset != null && encounterAsset.WaveCount > 0)
            {
                yield return RunAssetWaves(encounterAsset);
                yield break;
            }

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
            BindWeakness(zombie, word);

            while (zombie != null && !zombie.IsDead)
                yield return null;
        }

        /// <summary>
        /// 把本局的原始战斗词记到弱点组件上（导演轨弱点识别后要用它算“更简单的战斗词”，
        /// 以及换词时的同首字母兑底）。没有弱点组件的怪物（灰盒方块）自动忽略。
        /// </summary>
        /// <summary>
        /// 把本局的原始战斗词 + 弱点破坏后的新词库记到弱点组件上
        /// （导演轨弱点识别后要用它们算换上的新词）。没有弱点组件的怪物（灰盒方块）自动忽略。
        /// </summary>
        private static void BindWeakness(TypingTarget zombie, string word, string[] postWeakWords = null)
        {
            if (zombie == null) return;

            var weakness = zombie.GetComponent<MonsterWeakness>();
            if (weakness == null) return;

            weakness.OriginalWord = word;
            weakness.PostWeakWords = postWeakWords ?? System.Array.Empty<string>();
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
            BindWeakness(zombie, word);
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
            return CreateEnemy(position, entrance, laneOffset, spawnIndex, zombiePrefab);
        }

        private TypingTarget CreateEnemy(Vector3 position, bool entrance, float laneOffset, int spawnIndex, GameObject prefab)
        {
            var go = CreateEnemyBody(position, entrance, prefab);

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
        private GameObject CreateEnemyBody(Vector3 position, bool entrance, GameObject prefab)
        {
            if (prefab != null)
            {
                var body = Instantiate(prefab, position, FacePlayer(position));
                body.name = entrance ? prefab.name + "_Entrance" : prefab.name;
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

        // ---------------------------------------------------------------- 资产驱动波次

        /// <summary>本波要刷的一只怪：哪一种怪物 + 哪个词长变体。</summary>
        private struct AssetSpawn
        {
            public int type;
            public MonsterWordTier tier;
        }

        private static int SpawnKey(int type, MonsterWordTier tier) => type * 2 + (int)tier;

        /// <summary>
        /// 按战斗编排资产推进：每波按「每种怪的长/短词数量」刷怪，
        /// 速度与伤害取该怪物该变体的配置；单词不与其他活动词条首字母冲突。
        /// </summary>
        private IEnumerator RunAssetWaves(CombatEncounterAsset asset)
        {
            _shuffleWords = asset.shuffleWords;
            var queues = new Dictionary<int, Queue<string>>();
            var pools = new Dictionary<int, string[]>();

            foreach (var (index, type) in asset.EnabledTypes())
            {
                for (int t = 0; t < 2; t++)
                {
                    var tier = (MonsterWordTier)t;
                    var words = new List<string>();
                    var source = type.WordsFor(tier);
                    if (source != null)
                        foreach (var word in source)
                            if (!string.IsNullOrWhiteSpace(word)) words.Add(word.Trim());
                    int key = SpawnKey(index, tier);
                    pools[key] = words.ToArray();
                    queues[key] = new Queue<string>();
                }
            }

            bool entranceUsed = false;
            for (int w = 0; w < asset.WaveCount; w++)
            {
                var wave = asset.waves[w];
                if (wave == null) continue;

                var plan = BuildWavePlan(asset, w);
                int next = 0;
                int limit = Mathf.Max(1, wave.maxConcurrent);

                // 入场僵尸已经在场上（由 PlayEntrance 刷出，且计入 CountAlive）。
                // 它必须先领到词条，否则「场上已有 1 只」会把同屏上限吃满，
                // 于是既不会再刷怪、它也永远不会死——整波卡在那里。
                if (w == 0 && !entranceUsed && _entranceZombie != null && !_entranceZombie.IsDead && plan.Count > 0)
                {
                    var slot = plan[0];
                    if (TryTakeWord(slot, pools, queues, out string entranceWord))
                    {
                        ApplySpawnSettings(_entranceZombie, asset, slot);
                        typing.SpawnWord(entranceWord, CharacterTrack.Agent, 0f, _entranceZombie.gameObject);
                        BindWeakness(_entranceZombie, entranceWord, asset.PostWeakWordsOf(slot.type));
                        next++;
                        entranceUsed = true;
                    }
                }

                while (next < plan.Count || CountAlive() > 0)
                {
                    if (next < plan.Count && CountAlive() < limit)
                    {
                        var slot = plan[next];
                        if (!TryTakeWord(slot, pools, queues, out string word)) { yield return null; continue; }
                        if (!TryPickFreeSpawnPoint(out int point)) { yield return null; continue; }

                        var type = asset.TypeAt(slot.type);
                        var prefab = type != null && type.prefab != null ? type.prefab : zombiePrefab;
                        var zombie = CreateEnemy(SpawnPointAt(point), false, LaneAt(point), point, prefab);
                        ApplySpawnSettings(zombie, asset, slot);
                        typing.SpawnWord(word, CharacterTrack.Agent, 0f, zombie.gameObject);
                        BindWeakness(zombie, word, asset.PostWeakWordsOf(slot.type));
                        next++;
                        yield return new WaitForSeconds(Mathf.Max(0f, wave.spawnInterval));
                    }
                    else yield return null;   // 词首字母冲突 / 没空刷怪点 / 同屏已满：等场上打完再刷
                }
            }

            _cleared = true;
            OnCleared?.Invoke();
        }

        /// <summary>把这一波拆成「逐只」清单，并按类型轮转交织，避免同一种怪连续扎堆。</summary>
        private static List<AssetSpawn> BuildWavePlan(CombatEncounterAsset asset, int wave)
        {
            var perType = new List<List<AssetSpawn>>();
            foreach (var (index, _) in asset.EnabledTypes())
            {
                var list = new List<AssetSpawn>();
                for (int i = 0; i < asset.ShortCount(wave, index); i++)
                    list.Add(new AssetSpawn { type = index, tier = MonsterWordTier.ShortWord });
                for (int i = 0; i < asset.LongCount(wave, index); i++)
                    list.Add(new AssetSpawn { type = index, tier = MonsterWordTier.LongWord });
                if (list.Count > 0) perType.Add(list);
            }

            var plan = new List<AssetSpawn>();
            var cursor = new int[perType.Count];
            bool any;
            do
            {
                any = false;
                for (int i = 0; i < perType.Count; i++)
                {
                    if (cursor[i] >= perType[i].Count) continue;
                    plan.Add(perType[i][cursor[i]++]);
                    any = true;
                }
            } while (any);
            return plan;
        }

        /// <summary>把该怪物该变体的移动速度 / 冲刺速度 / 攻击伤害注入到场上这只怪。</summary>
        private static void ApplySpawnSettings(TypingTarget zombie, CombatEncounterAsset asset, AssetSpawn slot)
        {
            if (zombie == null) return;
            zombie.ApplyMoveSpeeds(asset.SpeedOf(slot.type, slot.tier), asset.RushOf(slot.type, slot.tier));
            zombie.ApplyAttackCells(asset.DamageOf(slot.type, slot.tier));
        }

        /// <summary>从该（怪物类型, 词长）词库里取一个与场上活动词条首字母不冲突的词；词库用完则重新洗牌。</summary>
        private bool TryTakeWord(AssetSpawn slot, Dictionary<int, string[]> pools, Dictionary<int, Queue<string>> queues, out string word)
        {
            word = null;
            int key = SpawnKey(slot.type, slot.tier);
            if (!pools.TryGetValue(key, out var pool) || pool.Length == 0) return false;
            if (!queues.TryGetValue(key, out var queue)) return false;

            for (int attempt = 0; attempt < pool.Length * 2; attempt++)
            {
                if (queue.Count == 0)
                {
                    var order = new List<string>(pool);
                    if (_shuffleWords)
                    {
                        for (int i = order.Count - 1; i > 0; i--)
                        {
                            int j = Random.Range(0, i + 1);
                            (order[i], order[j]) = (order[j], order[i]);
                        }
                    }
                    foreach (var candidate in order) queue.Enqueue(candidate);
                }

                string next = queue.Dequeue();
                if (string.IsNullOrEmpty(next) || IsInitialTaken(next)) continue;
                word = next;
                return true;
            }
            return false;
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
