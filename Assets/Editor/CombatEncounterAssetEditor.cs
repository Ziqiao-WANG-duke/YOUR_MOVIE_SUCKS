using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// CombatEncounter 资产的自定义 Inspector：点开 Assets 里的战斗数据就能直接编辑（不用去菜单找窗口）。
    /// 界面分三块：总览 / 怪物配置（一种怪 = 一个 Prefab，短词与长词是它的两种变体）/ 波次编辑。
    /// 改动只标脏，点「保存资产」才写盘。
    /// </summary>
    [CustomEditor(typeof(CombatEncounterAsset))]
    public sealed class CombatEncounterAssetEditor : UnityEditor.Editor
    {
        private const string DefaultPrefabPath = "Assets/Prefabs/StripperZombie.prefab";

        private SerializedObject _so;
        private readonly List<bool> _typeFold = new List<bool>();
        private readonly List<bool> _waveFold = new List<bool>();
        private bool _showOverview = true;
        private bool _showTypes = true;
        private bool _showWaves = true;
        private bool _showValidation = true;

        private void OnEnable()
        {
            _so = new SerializedObject(target);
            MigrateLegacyIfNeeded();
            RefreshFolds();
        }

        /// <summary>旧版资产（按词长档位拆成多条）打开时自动迁移，数值原样搬移。</summary>
        private void MigrateLegacyIfNeeded()
        {
            var asset = (CombatEncounterAsset)target;
            if (!asset.HasLegacyData) return;
            if (!CombatEncounterMigration.Migrate(asset)) return;
            _so.Update();
            Debug.Log($"[Combat] {asset.name} 是旧版结构，已自动迁移为「按 Prefab 的怪物类型 + 长短词数量」（数量与速度均未改变，记得保存）。");
        }

        private void RefreshFolds()
        {
            _typeFold.Clear();
            _waveFold.Clear();
            var types = _so.FindProperty("types");
            var waves = _so.FindProperty("waves");
            for (int i = 0; i < types.arraySize; i++) _typeFold.Add(i == 0);
            for (int i = 0; i < waves.arraySize; i++) _waveFold.Add(i < 2);
        }

        public override void OnInspectorGUI()
        {
            var asset = (CombatEncounterAsset)target;
            _so.Update();
            SyncFoldLists();
            EnsureSlotArrays();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("战斗编排（数值直接驱动战斗）：\n" +
                                        "1) 怪物配置：一种怪 = 一个 Prefab，短词/长词是它的两种变体（各自的词库、速度、伤害）。\n" +
                                        "2) 波次编辑：每一波给每种怪填「短词 / 长词」各刷几只，总数自动累加。",
                                        MessageType.Info);
                if (GUILayout.Button("打开完整窗口", GUILayout.Width(110f), GUILayout.Height(52f)))
                    CombatEditorWindow.OpenWindow(asset);
            }

            _showOverview = Section("总览", _showOverview, DrawOverview);
            _showTypes = Section($"怪物配置（{asset.TypeCount} 种）", _showTypes, DrawTypes);
            _showWaves = Section($"波次编辑（{asset.WaveCount} 波）", _showWaves, DrawWaves);

            _so.ApplyModifiedProperties();   // 先落值，校验才看得到最新数据

            _showValidation = Section("校验", _showValidation, () => DrawValidation(asset));

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存资产", GUILayout.Height(24f)))
                {
                    AssetDatabase.SaveAssetIfDirty(asset);
                    Debug.Log($"[Combat] 已保存 {AssetDatabase.GetAssetPath(asset)}");
                }
                if (GUILayout.Button("应用到当前场景", GUILayout.Height(24f)))
                    CombatEditorWindow.ApplyAssetToScene(asset);
                if (GUILayout.Button("校验并打印", GUILayout.Height(24f), GUILayout.Width(110f)))
                    Debug.Log(string.Join("\n", asset.Validate().Select(i => "[Combat] " + i)));
            }
            EditorGUILayout.LabelField("改动只标脏：确认无误后点「保存资产」再落盘。", EditorStyles.miniLabel);

            if (_so.ApplyModifiedProperties()) EditorUtility.SetDirty(asset);
        }

        private static bool Section(string title, bool open, System.Action body)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
                if (open)
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        body();
                    }
                }
            }
            return open;
        }

        // ------------------------------------------------------------------ 总览

        private void DrawOverview()
        {
            var types = _so.FindProperty("types");
            var waves = _so.FindProperty("waves");

            int enabledTypes = 0;
            for (int t = 0; t < types.arraySize; t++)
                if (types.GetArrayElementAtIndex(t).FindPropertyRelative("enabled").boolValue) enabledTypes++;

            int totalShort = 0, totalLong = 0;
            var perTypeShort = new int[types.arraySize];
            var perTypeLong = new int[types.arraySize];
            var typeEnabled = new bool[types.arraySize];
            for (int t = 0; t < types.arraySize; t++)
                typeEnabled[t] = types.GetArrayElementAtIndex(t).FindPropertyRelative("enabled").boolValue;

            for (int w = 0; w < waves.arraySize; w++)
                for (int t = 0; t < types.arraySize; t++)
                {
                    if (!typeEnabled[t]) continue;   // 未启用的类型不计入统计（与运行时一致）
                    perTypeShort[t] += SlotValue(waves, w, t, "shortCount");
                    perTypeLong[t] += SlotValue(waves, w, t, "longCount");
                }
            for (int t = 0; t < types.arraySize; t++)
            {
                totalShort += perTypeShort[t];
                totalLong += perTypeLong[t];
            }

            EditorGUILayout.LabelField($"怪物类型 {types.arraySize} 种（启用 {enabledTypes}）　波次 {waves.arraySize} 波　" +
                                       $"总怪数 {totalShort + totalLong}（短词 {totalShort} / 长词 {totalLong}）",
                                       EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                for (int t = 0; t < types.arraySize; t++)
                {
                    var el = types.GetArrayElementAtIndex(t);
                    var nameProp = el.FindPropertyRelative("displayName");
                    var prefabProp = el.FindPropertyRelative("prefab");
                    bool enabledType = el.FindPropertyRelative("enabled").boolValue;
                    string name = string.IsNullOrWhiteSpace(nameProp.stringValue) ? $"类型 {t + 1}" : nameProp.stringValue;
                    string prefabName = prefabProp.objectReferenceValue != null ? prefabProp.objectReferenceValue.name : "（无预制件）";
                    string flag = enabledType ? string.Empty : "　[未启用]";
                    int weakPool = el.FindPropertyRelative("postWeakWords").arraySize;
                    string weakHint = weakPool > 0 ? $"弱点后 {weakPool} 个词" : "弱点后用默认词库";
                    EditorGUILayout.LabelField($"· {name}（{prefabName}）{flag}：短词 {perTypeShort[t]} 只 / 长词 {perTypeLong[t]} 只　" +
                                               $"速度 {el.FindPropertyRelative("shortSpeed").floatValue:0.00} / {el.FindPropertyRelative("longSpeed").floatValue:0.00} m/s　" +
                                               $"伤害 {el.FindPropertyRelative("shortDamage").intValue} / {el.FindPropertyRelative("longDamage").intValue} 格　{weakHint}");
                }

                EditorGUILayout.Space(2f);
                for (int w = 0; w < waves.arraySize; w++)
                {
                    var wave = waves.GetArrayElementAtIndex(w);
                    int s = 0, l = 0;
                    for (int t = 0; t < types.arraySize; t++)
                    {
                        if (!typeEnabled[t]) continue;
                        s += SlotValue(waves, w, t, "shortCount");
                        l += SlotValue(waves, w, t, "longCount");
                    }
                    string note = wave.FindPropertyRelative("note").stringValue;
                    EditorGUILayout.LabelField($"· 第 {w + 1} 波：共 {s + l} 只（短 {s} / 长 {l}）　" +
                                               $"同屏 {wave.FindPropertyRelative("maxConcurrent").intValue}　" +
                                               $"间隔 {wave.FindPropertyRelative("spawnInterval").floatValue:0.00} s" +
                                               (string.IsNullOrWhiteSpace(note) ? string.Empty : $"　备注：{note}"),
                                               EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(_so.FindProperty("shuffleWords"), new GUIContent("每轮打乱词库顺序"));
            EditorGUILayout.PropertyField(_so.FindProperty("rushMultiplier"), new GUIContent("打错字母后的冲刺倍率"));

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("弱点被破坏后：默认词库", EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                DrawWordPool(_so.FindProperty("defaultPostWeakWords"));
                EditorGUILayout.LabelField("某种怪自己的「弱点后词库」为空时用这一套；两处都空 → 这类怪在导演轨不会挂弱点词。",
                                           EditorStyles.miniLabel);
            }
        }

        // -------------------------------------------------------------- 怪物配置

        private void DrawTypes()
        {
            var types = _so.FindProperty("types");
            int removeIndex = -1;

            for (int t = 0; t < types.arraySize; t++)
            {
                var el = types.GetArrayElementAtIndex(t);
                var nameProp = el.FindPropertyRelative("displayName");
                var prefabProp = el.FindPropertyRelative("prefab");
                var enabledProp = el.FindPropertyRelative("enabled");

                string label = string.IsNullOrWhiteSpace(nameProp.stringValue) ? $"类型 {t + 1}" : nameProp.stringValue;
                if (prefabProp.objectReferenceValue == null) label += "（未指定预制件）";
                if (!enabledProp.boolValue) label += "　· 未启用";

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _typeFold[t] = EditorGUILayout.Foldout(_typeFold[t], label, true);
                        GUILayout.FlexibleSpace();
                        enabledProp.boolValue = EditorGUILayout.ToggleLeft("参与本场战斗", enabledProp.boolValue, GUILayout.Width(104f));
                        if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(46f))) removeIndex = t;
                    }

                    if (removeIndex >= 0) continue;   // 本帧先不画内容，循环后统一删

                    if (!_typeFold[t]) continue;
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(nameProp, new GUIContent("名称"));
                        EditorGUILayout.PropertyField(prefabProp, new GUIContent("怪物预制件"));

                        DrawVariant(el, "短单词变体", "shortWords", "shortSpeed", "shortDamage");
                        EditorGUILayout.Space(2f);
                        DrawVariant(el, "长单词变体", "longWords", "longSpeed", "longDamage");
                        EditorGUILayout.Space(2f);

                        EditorGUILayout.LabelField("导演破坏弱点后的新词库", EditorStyles.miniBoldLabel);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawWordPool(el.FindPropertyRelative("postWeakWords"));
                            EditorGUILayout.LabelField("导演轨打掉这只怪的弱点后，战斗词只从这个词库换（挑第一个首字母不冲突的）；留空 = 用总览里的默认词库。",
                                                       EditorStyles.miniLabel);
                        }
                    }
                }
            }

            if (removeIndex >= 0)
            {
                types.DeleteArrayElementAtIndex(removeIndex);
                RemoveTypeColumn(removeIndex);
                if (removeIndex < _typeFold.Count) _typeFold.RemoveAt(removeIndex);
            }

            if (GUILayout.Button("+ 添加怪物类型"))
            {
                types.arraySize++;
                var added = types.GetArrayElementAtIndex(types.arraySize - 1);
                added.FindPropertyRelative("displayName").stringValue = "Zombie";
                added.FindPropertyRelative("enabled").boolValue = true;
                added.FindPropertyRelative("shortWords").arraySize = 0;
                added.FindPropertyRelative("longWords").arraySize = 0;
                var prefabProp = added.FindPropertyRelative("prefab");
                if (prefabProp.objectReferenceValue == null)
                    prefabProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);
                _typeFold.Add(true);
                SyncFoldLists();
                EnsureSlotArrays();
            }
        }

        private void DrawVariant(SerializedProperty typeEl, string header, string wordsField, string speedField, string damageField)
        {
            EditorGUILayout.LabelField(header, EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                DrawWordPool(typeEl.FindPropertyRelative(wordsField));

                using (new EditorGUILayout.HorizontalScope())
                {
                    var speed = typeEl.FindPropertyRelative(speedField);
                    speed.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("移动速度 (m/s)", speed.floatValue));
                    var damage = typeEl.FindPropertyRelative(damageField);
                    damage.intValue = Mathf.Max(1, EditorGUILayout.IntField("攻击伤害（格）", damage.intValue, GUILayout.Width(140f)));
                }
            }
        }

        private static void DrawWordPool(SerializedProperty words)
        {
            string pool = string.Join("\n", Enumerable.Range(0, words.arraySize)
                .Select(i => words.GetArrayElementAtIndex(i).stringValue));

            EditorGUILayout.LabelField($"词库（{words.arraySize} 个，一行一个）", EditorStyles.miniBoldLabel);
            string edited = EditorGUILayout.TextArea(pool, EditorStyles.textArea, GUILayout.MinHeight(56f));
            if (edited == pool) return;

            var list = edited.Split('\n').Select(w => w.Trim()).Where(w => w.Length > 0).ToArray();
            words.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) words.GetArrayElementAtIndex(i).stringValue = list[i];
        }

        // -------------------------------------------------------------- 波次编辑

        private void DrawWaves()
        {
            var types = _so.FindProperty("types");
            var waves = _so.FindProperty("waves");
            int removeIndex = -1;

            for (int w = 0; w < waves.arraySize; w++)
            {
                var wave = waves.GetArrayElementAtIndex(w);
                var slots = wave.FindPropertyRelative("slots");

                int shortTotal = 0, longTotal = 0;
                int disabledCounts = 0;
                for (int t = 0; t < slots.arraySize && t < types.arraySize; t++)
                {
                    var slot = slots.GetArrayElementAtIndex(t);
                    int s = Mathf.Max(0, slot.FindPropertyRelative("shortCount").intValue);
                    int l = Mathf.Max(0, slot.FindPropertyRelative("longCount").intValue);
                    bool enabledType = types.GetArrayElementAtIndex(t).FindPropertyRelative("enabled").boolValue;
                    if (!enabledType) { disabledCounts += s + l; continue; }
                    shortTotal += s;
                    longTotal += l;
                }
                string disabledHint = disabledCounts > 0 ? $"，未启用类型占 {disabledCounts} 只不刷" : string.Empty;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _waveFold[w] = EditorGUILayout.Foldout(_waveFold[w],
                            $"第 {w + 1} 波　共 {shortTotal + longTotal} 只（短 {shortTotal} / 长 {longTotal}）{disabledHint}", true);
                        GUILayout.FlexibleSpace();
                        var note = wave.FindPropertyRelative("note");
                        note.stringValue = EditorGUILayout.TextField(note.stringValue, GUILayout.Width(150f));
                        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22f))) removeIndex = w;
                    }

                    if (removeIndex >= 0) continue;
                    if (!_waveFold[w]) continue;

                    using (new EditorGUI.IndentLevelScope())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var conc = wave.FindPropertyRelative("maxConcurrent");
                            conc.intValue = Mathf.Max(1, EditorGUILayout.IntField("同屏上限", conc.intValue, GUILayout.Width(160f)));
                            var interval = wave.FindPropertyRelative("spawnInterval");
                            interval.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("刷怪间隔 (s)", interval.floatValue, GUILayout.Width(200f)));
                        }

                        if (types.arraySize == 0)
                        {
                            EditorGUILayout.HelpBox("先在「怪物配置」里添加怪物类型，这里才有可填的列。", MessageType.Info);
                            continue;
                        }

                        for (int t = 0; t < slots.arraySize && t < types.arraySize; t++)
                        {
                            var typeEl = types.GetArrayElementAtIndex(t);
                            var slot = slots.GetArrayElementAtIndex(t);
                            bool enabledType = typeEl.FindPropertyRelative("enabled").boolValue;
                            string name = typeEl.FindPropertyRelative("displayName").stringValue;
                            if (string.IsNullOrWhiteSpace(name)) name = $"类型 {t + 1}";
                            if (!enabledType) name += "（未启用）";

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label(name, GUILayout.Width(160f));
                                var shortProp = slot.FindPropertyRelative("shortCount");
                                shortProp.intValue = Mathf.Max(0, EditorGUILayout.IntField(shortProp.intValue, GUILayout.Width(48f)));
                                GUILayout.Label("短词", EditorStyles.miniLabel, GUILayout.Width(34f));
                                var longProp = slot.FindPropertyRelative("longCount");
                                longProp.intValue = Mathf.Max(0, EditorGUILayout.IntField(longProp.intValue, GUILayout.Width(48f)));
                                GUILayout.Label("长词", EditorStyles.miniLabel, GUILayout.Width(34f));
                                GUILayout.FlexibleSpace();
                                GUILayout.Label($"速度 {typeEl.FindPropertyRelative("shortSpeed").floatValue:0.00}/{typeEl.FindPropertyRelative("longSpeed").floatValue:0.00} m/s",
                                                EditorStyles.miniLabel, GUILayout.Width(150f));
                            }
                        }
                    }
                }
            }

            if (removeIndex >= 0)
            {
                waves.DeleteArrayElementAtIndex(removeIndex);
                if (removeIndex < _waveFold.Count) _waveFold.RemoveAt(removeIndex);
            }

            if (GUILayout.Button("+ 添加一波"))
            {
                waves.arraySize++;
                var added = waves.GetArrayElementAtIndex(waves.arraySize - 1);
                added.FindPropertyRelative("note").stringValue = string.Empty;
                added.FindPropertyRelative("maxConcurrent").intValue = 2;
                added.FindPropertyRelative("spawnInterval").floatValue = 1.1f;
                var slots = added.FindPropertyRelative("slots");
                slots.arraySize = types.arraySize;
                for (int t = 0; t < slots.arraySize; t++)
                {
                    var slot = slots.GetArrayElementAtIndex(t);
                    slot.FindPropertyRelative("shortCount").intValue = 0;
                    slot.FindPropertyRelative("longCount").intValue = 0;
                }
                _waveFold.Add(true);
                SyncFoldLists();
            }
        }

        // ---------------------------------------------------------------- 辅助

        private void DrawValidation(CombatEncounterAsset asset)
        {
            var issues = asset.Validate();
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("校验通过：没有发现问题。", MessageType.Info);
                return;
            }
            foreach (var issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }

        private static int SlotValue(SerializedProperty waves, int wave, int type, string field)
        {
            if (wave < 0 || wave >= waves.arraySize) return 0;
            var slots = waves.GetArrayElementAtIndex(wave).FindPropertyRelative("slots");
            if (type < 0 || type >= slots.arraySize) return 0;
            return Mathf.Max(0, slots.GetArrayElementAtIndex(type).FindPropertyRelative(field).intValue);
        }

        private void SyncFoldLists()
        {
            var types = _so.FindProperty("types");
            var waves = _so.FindProperty("waves");
            while (_typeFold.Count < types.arraySize) _typeFold.Add(true);
            while (_typeFold.Count > types.arraySize) _typeFold.RemoveAt(_typeFold.Count - 1);
            while (_waveFold.Count < waves.arraySize) _waveFold.Add(true);
            while (_waveFold.Count > waves.arraySize) _waveFold.RemoveAt(_waveFold.Count - 1);
        }

        /// <summary>每波的 slots 列数始终与类型数一致（新类型补一列 0）。</summary>
        private void EnsureSlotArrays()
        {
            var types = _so.FindProperty("types");
            var waves = _so.FindProperty("waves");
            for (int w = 0; w < waves.arraySize; w++)
            {
                var slots = waves.GetArrayElementAtIndex(w).FindPropertyRelative("slots");
                if (slots.arraySize == types.arraySize) continue;
                slots.arraySize = types.arraySize;
                for (int t = 0; t < slots.arraySize; t++)
                {
                    var slot = slots.GetArrayElementAtIndex(t);
                    slot.FindPropertyRelative("shortCount").intValue = Mathf.Max(0, slot.FindPropertyRelative("shortCount").intValue);
                    slot.FindPropertyRelative("longCount").intValue = Mathf.Max(0, slot.FindPropertyRelative("longCount").intValue);
                }
            }
        }

        private void RemoveTypeColumn(int typeIndex)
        {
            var waves = _so.FindProperty("waves");
            for (int w = 0; w < waves.arraySize; w++)
            {
                var slots = waves.GetArrayElementAtIndex(w).FindPropertyRelative("slots");
                if (typeIndex < slots.arraySize) slots.DeleteArrayElementAtIndex(typeIndex);
            }
        }
    }
}
