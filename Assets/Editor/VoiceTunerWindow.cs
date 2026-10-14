using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Demo;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 配音调节窗口：把每条音频的「速度 / 延迟 / 音量 / 起始偏移」做成可滑动的列表，不必改代码。
    ///
    /// 用法：菜单 `YourMovieSucks/Voice Tuner (POLE POSITION)`
    ///  · 参数写到资产 Assets/Audio/VO/VoLineSettings_PolePosition.asset（手调资产，不会被重建覆盖）
    ///  · **Play 模式下改动即时生效**：下一条台词用新参数，正在播的那条会立刻改速度/音量
    ///  · ▶ 为编辑器内原速试听（用编辑器内部预览接口，不受参数影响的只有速度/延迟）
    ///
    /// 说明：速度用 AudioSource.pitch 实现，**变快同时变调**；要"变速不变调"需离线时间伸缩。
    /// </summary>
    public class VoiceTunerWindow : EditorWindow
    {
        private VoLibrary _library;
        private VoLineSettings _settings;
        private Vector2 _scroll;
        private string _filter = string.Empty;
        private bool _dirty;

        [MenuItem("YourMovieSucks/Voice Tuner (POLE POSITION)")]
        [MenuItem("Window/YourMovieSucks/Voice Tuner")]
        public static void Open()
        {
            var window = GetWindow<VoiceTunerWindow>("Voice Tuner");
            window.minSize = new Vector2(900f, 420f);
            window.Reload();
        }

        private void OnEnable() => Reload();

        private void OnDisable()
        {
            SaveIfDirty();
        }

        private void Reload()
        {
            var libraryGuid = AssetDatabase.FindAssets("t:VoLibrary");
            _library = libraryGuid.Length > 0
                ? AssetDatabase.LoadAssetAtPath<VoLibrary>(AssetDatabase.GUIDToAssetPath(libraryGuid[0]))
                : null;

            _settings = LoadOrCreateSettings();
        }

        internal static VoLineSettings LoadOrCreateSettings()
        {
            const string path = "Assets/Audio/VO/VoLineSettings_PolePosition.asset";

            var settings = AssetDatabase.LoadAssetAtPath<VoLineSettings>(path);
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder("Assets/Audio/VO")) return null;

            settings = ScriptableObject.CreateInstance<VoLineSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VoiceTuner] created {path}");
            return settings;
        }

        private void OnGUI()
        {
            DrawHeader();

            if (_library == null || _settings == null)
            {
                EditorGUILayout.HelpBox("找不到 VoLibrary / VoLineSettings。先跑一次 YourMovieSucks/Voice/0 · 一键：检查 → 配置 → 建库接线。", MessageType.Warning);
                return;
            }

            DrawToolbar();
            DrawRows();
            HandlePendingSave();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"库：{(_library != null ? _library.Count + " clips" : "未找到")}", GUILayout.Width(160f));
                EditorGUILayout.LabelField($"设置：{(_settings != null ? _settings.name : "未找到")}", GUILayout.Width(260f));

                if (GUILayout.Button("重建库/接线", GUILayout.Width(110f)))
                {
                    PolePositionVoiceTools.SetupLibraryAndScene();
                    Reload();
                }

                if (GUILayout.Button("选中设置资产", GUILayout.Width(110f)) && _settings != null)
                    Selection.activeObject = _settings;
            }

            EditorGUILayout.HelpBox(
                "速度用 pitch 实现：加快会同时变调。延迟 = 播这条之前等几秒。\n" +
                "Play 模式下改参数会即时生效（下一条台词用新值；正在播的那条立刻改速度/音量）。\n" +
                "▶ 是编辑器内原速试听；带速度/延迟的效果请在 Play 模式下用「▶Play」听。",
                MessageType.Info);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("搜索", GUILayout.Width(32f));
                _filter = EditorGUILayout.TextField(_filter, GUILayout.Width(240f));

                if (GUILayout.Button("全部重置", GUILayout.Width(90f)))
                {
                    if (EditorUtility.DisplayDialog("重置", "把所有逐条参数恢复默认？", "重置", "取消"))
                    {
                        Undo.RecordObject(_settings, "Reset VO line settings");
                        _settings.ResetAll();
                        MarkDirty();
                    }
                }

                if (GUILayout.Button("停止试听", GUILayout.Width(90f))) StopPreview();

                EditorGUILayout.LabelField(Application.isPlaying ? "状态：Play 中（改动即时生效）" : "状态：编辑模式（原速试听）", GUILayout.Width(240f));
            }
        }

        private void DrawRows()
        {
            DrawRowHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in _library.Entries)
            {
                if (entry == null || entry.clip == null) continue;
                if (!string.IsNullOrEmpty(_filter) &&
                    entry.id.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                DrawRow(entry.clip, entry.id);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawRowHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("id", EditorStyles.boldLabel, GUILayout.Width(230f));
                EditorGUILayout.LabelField("原长", EditorStyles.boldLabel, GUILayout.Width(44f));
                EditorGUILayout.LabelField("生效", EditorStyles.boldLabel, GUILayout.Width(52f));
                EditorGUILayout.LabelField("速度", EditorStyles.boldLabel, GUILayout.Width(132f));
                EditorGUILayout.LabelField("延迟(秒)", EditorStyles.boldLabel, GUILayout.Width(132f));
                EditorGUILayout.LabelField("音量", EditorStyles.boldLabel, GUILayout.Width(132f));
                EditorGUILayout.LabelField("偏移(秒)", EditorStyles.boldLabel, GUILayout.Width(132f));
                EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);
            }
        }

        private void DrawRow(AudioClip clip, string id)
        {
            var setting = _settings.Get(id);
            bool hasEntry = !ReferenceEquals(setting, null) && !string.IsNullOrEmpty(setting.id);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(id, GUILayout.Width(230f));
                EditorGUILayout.LabelField($"{clip.length:0.00}", GUILayout.Width(44f));

                float effective = setting.delaySeconds +
                                  Mathf.Max(clip.length - setting.startOffsetSeconds, 0f) / Mathf.Max(setting.speed, 0.01f);
                var previousColor = GUI.color;
                GUI.color = setting.speed > 1.2f ? new Color(1f, 0.8f, 0.4f) : Color.white;
                EditorGUILayout.LabelField($"{effective:0.00}", GUILayout.Width(52f));
                GUI.color = previousColor;

                EditorGUI.BeginChangeCheck();

                float speed = EditorGUILayout.Slider(setting.speed, 0.25f, 3f, GUILayout.Width(132f));
                float delay = EditorGUILayout.Slider(setting.delaySeconds, 0f, 5f, GUILayout.Width(132f));
                float volume = EditorGUILayout.Slider(setting.volume, 0f, 8f, GUILayout.Width(132f));
                float offset = EditorGUILayout.Slider(setting.startOffsetSeconds, 0f, Mathf.Max(clip.length, 0.1f), GUILayout.Width(132f));

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_settings, "Voice line settings");
                    var editable = _settings.EnsureEntry(id);   // 首次编辑时才真正落一条记录
                    editable.speed = speed;
                    editable.delaySeconds = delay;
                    editable.volume = volume;
                    editable.startOffsetSeconds = offset;
                    MarkDirty();
                    setting = editable;
                }

                if (GUILayout.Button("▶", GUILayout.Width(28f))) Preview(clip);
                if (GUILayout.Button("▶Play", GUILayout.Width(56f))) PlayInGame(id);
                using (new EditorGUI.DisabledScope(!hasEntry))
                {
                    if (GUILayout.Button("重置", GUILayout.Width(48f)))
                    {
                        Undo.RecordObject(_settings, "Reset VO line setting");
                        _settings.ResetEntry(id);
                        MarkDirty();
                    }
                }
            }
        }

        private void PlayInGame(string id)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[VoiceTuner] 「▶Play」需要在 Play 模式下使用（那里会应用速度/延迟）");
                return;
            }

            var director = FindFirstObjectByType<DemoAudioDirector>();
            if (director == null)
            {
                Debug.LogWarning("[VoiceTuner] 场景里没有 DemoAudioDirector");
                return;
            }

            director.PlayVo(id);
        }

        private void MarkDirty()
        {
            EditorUtility.SetDirty(_settings);
            _dirty = true;
            Repaint();
        }

        private void HandlePendingSave()
        {
            // 拖动过程中不落盘，松手/离开窗口时再保存
            if (!_dirty) return;
            if (Event.current == null || Event.current.type == EventType.MouseUp || Event.current.type == EventType.Used)
            {
                SaveIfDirty();
            }
        }

        private void SaveIfDirty()
        {
            if (!_dirty || _settings == null) return;

            _dirty = false;
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- 编辑器试听（内部预览接口）

        private static void Preview(AudioClip clip)
        {
            if (clip == null) return;

            var audioUtil = Type.GetType("UnityEditor.AudioUtil, UnityEditor");
            if (audioUtil == null)
            {
                Debug.LogWarning("[VoiceTuner] 编辑器试听接口不可用，请在 Play 模式试听");
                return;
            }

            foreach (var name in new[] { "PlayPreviewClip", "PlayClip" })
            {
                var method = audioUtil.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                if (method != null)
                {
                    method.Invoke(null, new object[] { clip, 0, false });
                    return;
                }

                method = audioUtil.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(AudioClip) }, null);
                if (method != null)
                {
                    method.Invoke(null, new object[] { clip });
                    return;
                }
            }

            Debug.LogWarning("[VoiceTuner] 编辑器试听接口签名不匹配，请在 Play 模式试听");
        }

        private static void StopPreview()
        {
            var audioUtil = Type.GetType("UnityEditor.AudioUtil, UnityEditor");
            if (audioUtil == null) return;

            foreach (var name in new[] { "StopAllPreviewClips", "StopAllClips" })
            {
                var method = audioUtil.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) continue;

                method.Invoke(null, null);
                return;
            }
        }
    }
}
