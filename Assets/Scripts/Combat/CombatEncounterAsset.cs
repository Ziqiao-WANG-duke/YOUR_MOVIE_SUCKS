using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourMovieSucks.Combat
{
    /// <summary>词长档位：同一只怪的两种变体（短词 / 长词）。</summary>
    public enum MonsterWordTier
    {
        ShortWord,
        LongWord
    }

    /// <summary>
    /// 一种怪物（= 一个怪物 Prefab）。长短词只是同一只怪的两种变体，
    /// 各自有词库、移动速度与攻击伤害。
    /// </summary>
    [Serializable]
    public sealed class CombatMonsterType
    {
        public string displayName = "Zombie";
        public GameObject prefab;
        [Tooltip("本场战斗是否使用这种怪（未选中的不会出现在波次表里）")]
        public bool enabled = true;

        [Header("短单词变体")]
        public string[] shortWords = Array.Empty<string>();
        [Min(0f)] public float shortSpeed = 0.5f;
        [Min(1)] public int shortDamage = 1;

        [Header("长单词变体")]
        public string[] longWords = Array.Empty<string>();
        [Min(0f)] public float longSpeed = 0.4f;
        [Min(1)] public int longDamage = 1;

        [Header("弱点被破坏后（导演轨）")]
        [Tooltip("导演在导演轨打掉这只怪的弱点后，战斗词换成这里的词（一行一个）。\n留空 = 用弱点资产（Weakness_Zombie 等）里的简化词。")]
        public string[] postWeakWords = Array.Empty<string>();

        public string[] WordsFor(MonsterWordTier tier)
            => tier == MonsterWordTier.LongWord ? longWords : shortWords;

        public float SpeedFor(MonsterWordTier tier)
            => Mathf.Max(0f, tier == MonsterWordTier.LongWord ? longSpeed : shortSpeed);

        public int DamageFor(MonsterWordTier tier)
            => Mathf.Max(1, tier == MonsterWordTier.LongWord ? longDamage : shortDamage);
    }

    /// <summary>一波里某一种怪的长词 / 短词数量。</summary>
    [Serializable]
    public sealed class CombatWaveSlot
    {
        [Min(0)] public int shortCount;
        [Min(0)] public int longCount;
        public int Total => Mathf.Max(0, shortCount) + Mathf.Max(0, longCount);
    }

    /// <summary>一种参与战斗的怪物：自己的预制件、词池、以及可选的速度覆盖。</summary>
    [Serializable]
    public sealed class CombatMonsterKind
    {
        public string displayName = "Zombie";
        public GameObject prefab;
        public MonsterWordTier tier = MonsterWordTier.ShortWord;

        [Tooltip("这只怪使用的词池（每个词会被分给一只怪）。")]
        public string[] words = Array.Empty<string>();

        [Tooltip("勾上则用下面的速度，而不是资产里该档位的统一速度。")]
        public bool overrideTierSpeed;
        public float approachSpeed = 0.5f;
        public float rushSpeed = 1.6f;
    }

    /// <summary>一波：每种已选怪物各刷几条短词/长词，以及这一波的节奏。</summary>
    [Serializable]
    public sealed class CombatWave
    {
        [Tooltip("备注（只用于编辑器显示）")]
        public string note = string.Empty;

        [Tooltip("与 types 一一对应：这一波每种怪物各刷几只短词/长词")]
        public CombatWaveSlot[] slots = Array.Empty<CombatWaveSlot>();

        [Min(0f)] public float spawnInterval = 1.1f;
        [Min(1)] public int maxConcurrent = 2;

        [Tooltip("旧版：每种怪的数量（仅用于迁移到 slots）")]
        [HideInInspector] public int[] counts = Array.Empty<int>();
    }

    /// <summary>
    /// 战斗编排数据：参与的怪物种类、每波各档数量、波次数量、长/短词怪物行动速度。
    /// 由 <see cref="YourMovieSucks.Demo.ScriptedEncounter"/> 在战场里执行；编辑器窗口见 Combat Editor。
    /// </summary>
    [CreateAssetMenu(menuName = "YourMovieSucks/Combat Encounter", fileName = "CombatEncounter")]
    public sealed class CombatEncounterAsset : ScriptableObject
    {
        [Header("怪物类型（= 实际使用的 Prefab；长短词是它的两种变体）")]
        public CombatMonsterType[] types = Array.Empty<CombatMonsterType>();

        [Header("波次")]
        public CombatWave[] waves = Array.Empty<CombatWave>();

        [Tooltip("勾上则每轮打乱词库顺序；关闭时严格按词库顺序取词")]
        public bool shuffleWords;

        [Header("弱点被破坏后：默认词库（某种怪没单独配时用它）")]
        [Tooltip("某个怪物类型的「弱点后词库」为空时用这一套；两处都空 → 这类怪在导演轨不会挂弱点词。")]
        public string[] defaultPostWeakWords = Array.Empty<string>();

        [Tooltip("被打错字母后的冲刺倍率（冲刺速度 = 移动速度 × 该值）")]
        [Min(1f)] public float rushMultiplier = 1.6f;

        // ---------------- 旧版数据（仅用于一次性迁移，迁移后置空） ----------------
        [SerializeField, HideInInspector] private CombatMonsterKind[] kinds;
        [SerializeField, HideInInspector] private float shortWordSpeed = 0.5f;
        [SerializeField, HideInInspector] private float longWordSpeed = 0.4f;

        public CombatMonsterKind[] LegacyKinds => kinds;
        public float LegacyShortSpeed => shortWordSpeed;
        public float LegacyLongSpeed => longWordSpeed;
        public bool HasLegacyData => kinds != null && kinds.Length > 0;

        public void ClearLegacyData()
        {
            kinds = Array.Empty<CombatMonsterKind>();
            foreach (var wave in waves ?? Array.Empty<CombatWave>())
                if (wave != null) wave.counts = Array.Empty<int>();
        }

        public int TypeCount => types?.Length ?? 0;
        public int WaveCount => waves?.Length ?? 0;

        // ---------------- 查询 ----------------

        public CombatMonsterType TypeAt(int index)
        {
            if (types == null || index < 0 || index >= types.Length) return null;
            return types[index];
        }

        /// <summary>本场战斗实际参与刷怪的怪物（enabled = true）。</summary>
        public IEnumerable<(int index, CombatMonsterType type)> EnabledTypes()
        {
            for (int i = 0; i < TypeCount; i++)
            {
                var type = types[i];
                if (type != null && type.enabled) yield return (i, type);
            }
        }

        public int EnabledTypeCount
        {
            get { int n = 0; foreach (var _ in EnabledTypes()) n++; return n; }
        }

        public int ShortCount(int wave, int type)
        {
            var slot = SlotAt(wave, type);
            return slot != null ? Mathf.Max(0, slot.shortCount) : 0;
        }

        public int LongCount(int wave, int type)
        {
            var slot = SlotAt(wave, type);
            return slot != null ? Mathf.Max(0, slot.longCount) : 0;
        }

        public CombatWaveSlot SlotAt(int wave, int type)
        {
            if (waves == null || wave < 0 || wave >= waves.Length) return null;
            var slots = waves[wave].slots;
            if (slots == null || type < 0 || type >= slots.Length) return null;
            return slots[type];
        }

        /// <summary>本波总数：只统计已选中的怪物（编辑器与运行时用同一套统计）。</summary>
        public int WaveTotal(int wave)
        {
            int total = 0;
            foreach (var (index, _) in EnabledTypes()) total += ShortCount(wave, index) + LongCount(wave, index);
            return total;
        }

        public int Total()
        {
            int total = 0;
            for (int w = 0; w < WaveCount; w++) total += WaveTotal(w);
            return total;
        }

        /// <summary>整场里某一种怪的总数（短+长）。</summary>
        public int TotalOfType(int type)
        {
            int total = 0;
            for (int w = 0; w < WaveCount; w++) total += ShortCount(w, type) + LongCount(w, type);
            return total;
        }

        public int TotalOfTier(MonsterWordTier tier)
        {
            int total = 0;
            for (int w = 0; w < WaveCount; w++)
                foreach (var (index, _) in EnabledTypes())
                    total += tier == MonsterWordTier.LongWord ? LongCount(w, index) : ShortCount(w, index);
            return total;
        }

        public float SpeedOf(int type, MonsterWordTier tier)
        {
            var t = TypeAt(type);
            return t != null ? t.SpeedFor(tier) : 0.5f;
        }

        public float RushOf(int type, MonsterWordTier tier)
            => SpeedOf(type, tier) * Mathf.Max(1f, rushMultiplier);

        public int DamageOf(int type, MonsterWordTier tier)
        {
            var t = TypeAt(type);
            return t != null ? t.DamageFor(tier) : 1;
        }

        public string[] WordsOf(int type, MonsterWordTier tier)
        {
            var t = TypeAt(type);
            return t != null ? t.WordsFor(tier) : Array.Empty<string>();
        }

        /// <summary>该怪物类型被破坏弱点后的新词库：类型自己的词库优先，空则用资产级默认词库。</summary>
        public string[] PostWeakWordsOf(int type)
        {
            var t = TypeAt(type);
            var own = t != null ? t.postWeakWords : null;
            if (HasAnyWord(own)) return own;
            return defaultPostWeakWords != null ? defaultPostWeakWords : Array.Empty<string>();
        }

        /// <summary>词库里是否至少有一个可用词（忽略空白项）。</summary>
        public static bool HasAnyWord(string[] words)
        {
            if (words == null) return false;
            foreach (var w in words)
                if (!string.IsNullOrWhiteSpace(w)) return true;
            return false;
        }

        /// <summary>旧接口（旧版编辑器/工具用）：按“种类”取值已不适用，保留以免旧代码编译失败。</summary>
        public int KindCount => TypeCount;
        [Obsolete("怪物不再分种类，请用 ShortCount/LongCount(wave, type)")]
        public int Count(int wave, int type) => ShortCount(wave, type) + LongCount(wave, type);

        /// <summary>编辑器与自检共用的校验；返回人类可读的问题列表（空 = 没发现问题）。</summary>
        public List<string> Validate()
        {
            var issues = new List<string>();
            if (TypeCount == 0) issues.Add("没有怪物类型：至少加一种。");
            if (EnabledTypeCount == 0) issues.Add("没有勾选任何怪物类型：本场不会刷出怪。");

            for (int t = 0; t < TypeCount; t++)
            {
                var type = types[t];
                if (type == null) { issues.Add($"类型 {t + 1} 是空条目。"); continue; }
                string name = string.IsNullOrWhiteSpace(type.displayName) ? $"类型 {t + 1}" : type.displayName;
                if (type.prefab == null) issues.Add($"{name}：没有指定预制件（会退回默认丧尸预制件）。");
                if (!type.enabled) continue;

                ValidatePool(issues, $"{name}（短词）", type.shortWords, CountForTier(t, MonsterWordTier.ShortWord));
                ValidatePool(issues, $"{name}（长词）", type.longWords, CountForTier(t, MonsterWordTier.LongWord));
                ValidatePostWeakPool(issues, name, PostWeakWordsOf(t), TotalOfType(t));
            }

            if (WaveCount == 0) issues.Add("没有波次：至少加一波。");
            for (int w = 0; w < WaveCount; w++)
            {
                var wave = waves[w];
                if (wave == null) { issues.Add($"第 {w + 1} 波是空条目。"); continue; }
                if (WaveTotal(w) == 0) issues.Add($"第 {w + 1} 波没有任何怪物。");
                if (wave.slots != null && wave.slots.Length != TypeCount)
                    issues.Add($"第 {w + 1} 波的数量列数与类型数不一致（{wave.slots.Length} vs {TypeCount}），已在编辑器里自动修正。");
            }
            return issues;
        }

        private int CountForTier(int type, MonsterWordTier tier)
        {
            int need = 0;
            for (int w = 0; w < WaveCount; w++)
                need += tier == MonsterWordTier.LongWord ? LongCount(w, type) : ShortCount(w, type);
            return need;
        }

        private static void ValidatePool(List<string> issues, string label, string[] words, int need)
        {
            int usable = 0;
            // 同首字母的候选词会触发“锁定吸附”歧义，同屏 >1 时应避免
            int duplicates = 0;
            var seen = new HashSet<char>();
            if (words != null)
                foreach (var w in words)
                {
                    if (string.IsNullOrWhiteSpace(w)) continue;
                    usable++;
                    if (!seen.Add(char.ToUpperInvariant(w.Trim()[0]))) duplicates++;
                }

            if (usable == 0)
            {
                if (need > 0) issues.Add($"{label}：词库是空的，但波次里需要 {need} 只。");
                return;
            }
            if (duplicates > 0)
                issues.Add($"{label}：词库里有 {duplicates} 个词与别的词同首字母（同屏多只时按首字母吸附会歧义）。");
            if (need > usable)
                issues.Add($"{label}：波次里需要 {need} 只但词库只有 {usable} 个词（会用重复词补足）。");
        }

        /// <summary>弱点后的新词库：唯一的换词来源，空则不会发生换词。</summary>
        private static void ValidatePostWeakPool(List<string> issues, string typeName, string[] words, int battleCount)
        {
            int usable = 0;
            var seen = new HashSet<char>();
            int duplicates = 0;
            if (words != null)
                foreach (var w in words)
                {
                    if (string.IsNullOrWhiteSpace(w)) continue;
                    usable++;
                    if (!seen.Add(char.ToUpperInvariant(w.Trim()[0]))) duplicates++;
                }

            if (usable == 0)
            {
                if (battleCount > 0)
                    issues.Add($"{typeName}（弱点后词库）：类型词库和总览里的默认词库都是空的，导演轨不会给这类怪挂弱点词。");
                return;
            }

            if (duplicates > 0)
                issues.Add($"{typeName}（弱点后词库）：有 {duplicates} 个词与别的词同首字母，同屏多只会挑不到词而晚些换。");
        }
    }
}
