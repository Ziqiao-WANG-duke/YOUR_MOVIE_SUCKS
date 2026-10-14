using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 记录「每只怪每一次攻击瞬间的坐标」：按出生点（SpawnLeft / SpawnRight / SpawnBack / EntranceSpawn）
    /// 归类，每次攻击输出 根节点世界坐标 / 模型（子物体）世界坐标 / 模型本地坐标 / 缩放 / 相机坐标 / 距离。
    ///
    /// 用法（批处理或编辑器内）：
    ///   ① 先调 <see cref="Arm"/> 武装（写入 SessionState）
    ///   ② 进入 Play Mode，让怪走到攻击环并攻击
    ///   ③ 读 Library/Locus/tmp/zombie-attack-positions.txt
    /// 静态回调会被进入 Play Mode 的域重载清空，所以靠 SessionState 在重载后重接。
    /// </summary>
    [InitializeOnLoad]
    public static class ZombieAttackProbe
    {
        private const string Flag = "YourMovieSucks.ZombieAttackProbe";
        private const string FilePath = "Library/Locus/tmp/zombie-attack-positions.txt";

        private class Record
        {
            public string Spawn;
            public Vector3 SpawnPosition;
            public int Hits;
            public Vector3 PreviousRoot;
            public bool HasPrevious;
        }

        private static readonly Dictionary<TypingTarget, Record> Records = new();

        static ZombieAttackProbe()
        {
            if (SessionState.GetBool(Flag, false))
                EditorApplication.update += Tick;
        }

        public static void Arm()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? ".");
            System.IO.File.WriteAllText(FilePath, "== 每次攻击瞬间的坐标 ==\n");
            SessionState.SetBool(Flag, true);

            // 立即接上（Arm 常常是在已经进入 Play Mode 之后调的，那时 InitializeOnLoad 的静态构造已经跑过）
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[ZombieAttackProbe] armed");
        }

        public static void Disarm()
        {
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                // 退出 Play Mode：先摘掉回调（标记留着，下次进 Play 由静态构造重接）
                EditorApplication.update -= Tick;
                return;
            }

            foreach (var target in Object.FindObjectsByType<TypingTarget>(FindObjectsSortMode.None))
            {
                if (target == null || Records.ContainsKey(target)) continue;

                var record = new Record
                {
                    Spawn = NearestSpawnName(target.transform.position),
                    SpawnPosition = target.transform.position,
                };
                Records[target] = record;

                Log(target, record, "出生");

                var captured = target;
                var capturedRecord = record;
                target.OnAttackHit += () => Log(captured, capturedRecord, "攻击");
            }
        }

        private static string NearestSpawnName(Vector3 position)
        {
            var candidates = new (string name, Vector3 pos)[]
            {
                ("SpawnLeft", FindPosition("SpawnLeft")),
                ("SpawnRight", FindPosition("SpawnRight")),
                ("SpawnBack", FindPosition("SpawnBack")),
                ("EntranceSpawn", FindPosition("EntranceSpawn")),
            };

            string best = "未知";
            float bestDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                if (candidate.pos == Vector3.zero) continue;
                float distance = Vector3.Distance(position, candidate.pos);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate.name;
            }

            return bestDistance <= 3f ? best : $"{best}(远处 {bestDistance:0.0}m)";
        }

        private static Vector3 FindPosition(string name)
        {
            var found = GameObject.Find(name);
            return found != null ? found.transform.position : Vector3.zero;
        }

        private static void Log(TypingTarget target, Record record, string what)
        {
            var camera = Camera.main;
            var animator = target.GetComponentInChildren<Animator>();
            var model = animator != null ? animator.transform : null;

            Vector3 root = target.transform.position;
            Vector3 modelWorld = model != null ? model.position : root;
            float toCamera = camera != null ? Vector3.Distance(root, camera.transform.position) : 0f;

            // “可见模型”的度量：骨骼网格包围盒（跟随根骨骼/姿势）+ 骨盆骨骼世界位置
            // （仅看 Transform 坐标会得出“完全没动”，但渲染出来的模型会随 Walk/Idle/Attack 姿势前后偏）
            var skinned = target.GetComponentInChildren<SkinnedMeshRenderer>();
            string visualText = "";
            if (skinned != null)
            {
                Bounds worldBounds = skinned.bounds;
                Vector3 hips = skinned.rootBone != null ? skinned.rootBone.position : worldBounds.center;
                var flat = worldBounds.center - camera.transform.position;
                flat.y = 0f;
                visualText = $" 可见包围盒中心=({worldBounds.center.x:0.000},{worldBounds.center.y:0.000},{worldBounds.center.z:0.000}) " +
                             $"包围盒底/顶y={worldBounds.min.y:0.000}/{worldBounds.max.y:0.000} 骨盆世界=({hips.x:0.000},{hips.y:0.000},{hips.z:0.000}) " +
                             $"可见中心离相机(水平)={(camera != null ? flat.magnitude : 0f):0.000}";
            }

            string stateText = "";
            if (animator != null)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                string[] names = { "Idle", "Walk", "Run", "Attack", "Death" };
                string stateName = "?";
                foreach (string candidate in names)
                    if (info.IsName(candidate)) { stateName = candidate; break; }
                stateText = $" 动画状态={stateName} t={info.normalizedTime:0.00}";
            }

            string delta = record.HasPrevious
                ? $" 与上次攻击位移={Vector3.Distance(root, record.PreviousRoot):0.000}m"
                : string.Empty;

            if (what == "攻击")
            {
                record.Hits++;
                record.PreviousRoot = root;
                record.HasPrevious = true;
            }

            string line =
                $"[{record.Spawn}] {what} #{record.Hits} t={Time.realtimeSinceStartup:0.00} " +
                $"根=({root.x:0.000},{root.y:0.000},{root.z:0.000}) " +
                $"模型Transform世界=({modelWorld.x:0.000},{modelWorld.y:0.000},{modelWorld.z:0.000}) " +
                $"模型本地=({(model != null ? model.localPosition.x : 0f):0.000},{(model != null ? model.localPosition.y : 0f):0.000},{(model != null ? model.localPosition.z : 0f):0.000}) " +
                $"模型scale={(model != null ? model.localScale.x : 0f):0.00} 根scale={target.transform.localScale.x:0.00} " +
                $"相机=({(camera != null ? camera.transform.position.x : 0f):0.000},{(camera != null ? camera.transform.position.y : 0f):0.000},{(camera != null ? camera.transform.position.z : 0f):0.000}) " +
                $"离相机={toCamera:0.000}{stateText}{visualText}{delta}";

            try
            {
                System.IO.File.AppendAllText(FilePath, line + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ZombieAttackProbe] 写文件失败：" + e.Message);
            }
        }
    }
}
