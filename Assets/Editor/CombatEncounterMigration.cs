using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 把旧版战斗数据（kinds + 每波 counts，按“词长档位拆成多条”的模型）
    /// 一次性迁移成新版结构（每种怪 = 一个 Prefab，含短词/长词两种变体）。
    /// 迁移只做搬移，不改变任何数量与速度，所以总怪数、长/短词数量都保持原样。
    /// </summary>
    public static class CombatEncounterMigration
    {
        [MenuItem("YourMovieSucks/Combat/迁移旧版战斗数据（kinds → 怪物类型）")]
        public static void MigrateAllMenu()
        {
            int migrated = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CombatEncounterAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<CombatEncounterAsset>(path);
                if (asset == null || !asset.HasLegacyData) continue;
                if (!Migrate(asset)) continue;
                AssetDatabase.SaveAssetIfDirty(asset);
                migrated++;
                Debug.Log($"[Combat] 已迁移旧版战斗数据：{path}");
            }
            if (migrated == 0) Debug.Log("[Combat] 没有需要迁移的旧版战斗数据。");
        }

        /// <summary>就地迁移一份资产；返回是否发生了改动。调用方负责 SetDirty / 保存。</summary>
        public static bool Migrate(CombatEncounterAsset asset)
        {
            if (asset == null) return false;
            var kinds = asset.LegacyKinds;
            if (kinds == null || kinds.Length == 0) return false;

            Undo.RecordObject(asset, "Migrate combat encounter");

            // ---- 1. 按 Prefab 归并成“怪物类型”，长短词分别落到两个变体 ----
            var types = new List<CombatMonsterType>();
            var kindType = new int[kinds.Length];
            var kindTier = new MonsterWordTier[kinds.Length];
            var nameScore = new List<int>();

            for (int k = 0; k < kinds.Length; k++)
            {
                var kind = kinds[k];
                kindTier[k] = kind != null ? kind.tier : MonsterWordTier.ShortWord;

                int found = -1;
                if (kind != null && kind.prefab != null)
                    for (int t = 0; t < types.Count; t++)
                        if (types[t].prefab == kind.prefab) { found = t; break; }

                if (found < 0)
                {
                    int cells = PrefabAttackCells(kind != null ? kind.prefab : null);
                    var created = new CombatMonsterType
                    {
                        displayName = StripTierPrefix(kind != null ? kind.displayName : null),
                        prefab = kind != null ? kind.prefab : null,
                        enabled = true,
                        shortWords = Array.Empty<string>(),
                        longWords = Array.Empty<string>(),
                        shortSpeed = Mathf.Max(0f, asset.LegacyShortSpeed),
                        longSpeed = Mathf.Max(0f, asset.LegacyLongSpeed),
                        shortDamage = cells,
                        longDamage = cells,
                    };
                    types.Add(created);
                    nameScore.Add(-1);
                    found = types.Count - 1;
                }

                var type = types[found];
                var words = new List<string>();
                if (kind != null && kind.words != null)
                    foreach (var word in kind.words)
                        if (!string.IsNullOrWhiteSpace(word)) words.Add(word.Trim());

                float speed = kind != null && kind.overrideTierSpeed
                    ? Mathf.Max(0f, kind.approachSpeed)
                    : Mathf.Max(0f, kindTier[k] == MonsterWordTier.LongWord ? asset.LegacyLongSpeed : asset.LegacyShortSpeed);

                if (kindTier[k] == MonsterWordTier.LongWord)
                {
                    type.longWords = Merge(type.longWords, words);
                    type.longSpeed = speed;
                }
                else
                {
                    type.shortWords = Merge(type.shortWords, words);
                    type.shortSpeed = speed;
                }

                // 名字：优先取“不带短词/长词前缀、且词库最大”的那条，避免迁移后全叫「短词丧尸」「长词丧尸」
                if (kind != null && !string.IsNullOrWhiteSpace(kind.displayName) && !HasTierPrefix(kind.displayName)
                    && words.Count > nameScore[found])
                {
                    type.displayName = kind.displayName.Trim();
                    nameScore[found] = words.Count;
                }

                kindType[k] = found;
            }

            // ---- 2. 每波的数量：按「类型 + 长短词」重新累加 ----
            var waves = asset.waves ?? Array.Empty<CombatWave>();
            foreach (var wave in waves)
            {
                if (wave == null) continue;
                var slots = new CombatWaveSlot[types.Count];
                for (int t = 0; t < slots.Length; t++) slots[t] = new CombatWaveSlot();

                var counts = wave.counts ?? Array.Empty<int>();
                int n = Mathf.Min(counts.Length, kinds.Length);
                for (int k = 0; k < n; k++)
                {
                    int count = Mathf.Max(0, counts[k]);
                    if (count == 0) continue;
                    var slot = slots[kindType[k]];
                    if (kindTier[k] == MonsterWordTier.LongWord) slot.longCount += count;
                    else slot.shortCount += count;
                }
                wave.slots = slots;
            }

            asset.types = types.ToArray();
            asset.ClearLegacyData();
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static int PrefabAttackCells(GameObject prefab)
        {
            if (prefab == null) return 1;
            var target = prefab.GetComponent<TypingTarget>() ?? prefab.GetComponentInChildren<TypingTarget>(true);
            return target != null ? target.AttackCells : 1;
        }

        private static string[] Merge(string[] existing, List<string> added)
        {
            if (added.Count == 0) return existing ?? Array.Empty<string>();
            var list = new List<string>(existing ?? Array.Empty<string>());
            foreach (var word in added)
                if (!list.Contains(word)) list.Add(word);
            return list.ToArray();
        }

        private static bool HasTierPrefix(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            return trimmed.StartsWith("短词", StringComparison.Ordinal) || trimmed.StartsWith("长词", StringComparison.Ordinal);
        }

        private static string StripTierPrefix(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Zombie";
            var trimmed = name.Trim();
            if (HasTierPrefix(trimmed)) trimmed = trimmed.Substring(2).Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "Zombie" : trimmed;
        }

        /// <summary>供自检/工具使用：当前资产是否还是旧版结构。</summary>
        public static bool NeedsMigration(CombatEncounterAsset asset) => asset != null && asset.HasLegacyData;

        /// <summary>供自检使用：列出资产里所有需要迁移的资产路径。</summary>
        public static string[] FindLegacyAssets()
            => AssetDatabase.FindAssets("t:CombatEncounterAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => NeedsMigration(AssetDatabase.LoadAssetAtPath<CombatEncounterAsset>(p)))
                .ToArray();
    }
}
