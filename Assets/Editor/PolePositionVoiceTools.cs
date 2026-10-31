using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YourMovieSucks.Demo;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 配音接入工具（菜单 YourMovieSucks/Voice/...）：
    ///  1 · 检查导入结果（m4a 不被 Unity 支持，必须 wav/ogg）
    ///  2 · 配置导入设置（人声单声道 / DecompressOnLoad / Vorbis 0.7）
    ///  3 · 建立 VoLibrary 映射资产，并把音源与台词 id 接进 PolePosition 场景
    ///
    /// 约定：音频文件名 = 台词 id（例如 pp_open_dir_01_production_value.wav）。
    /// </summary>
    public static class PolePositionVoiceTools
    {
        public const string VoFolder = "Assets/Audio/VO/PolePosition";
        private const string SfxFolder = "Assets/Audio/SFX";
        private const string BgmFolder = "Assets/Audio/BGM";
        private const string LibraryPath = "Assets/Audio/VO/VoLibrary_PolePosition.asset";
        private const string SettingsPath = "Assets/Audio/VO/VoLineSettings_PolePosition.asset";
        private const string TypingSfxFolder = "Assets/Audio/SFX/Typing";
        private const string GunshotSfxFolder = "Assets/Audio/SFX/Gunshot";
        private const string HurtSfxFolder = "Assets/Audio/SFX/Hurt";

        /// <summary>切轨键音效（不从打字候选池里选）。</summary>
        private const string SwitchSfxId = "typewriter8";
        private const string ScenePath = "Assets/Scenes/PolePosition.unity";

        /// <summary>扫描这些目录建库：文件名 = id（VO / SFX / BGM 共用一张表，便于 audio.PlaySfx("sfx_roar") 这样调用）。</summary>
        private static readonly string[] AudioFolders = { VoFolder, SfxFolder, BgmFolder };

        /// <summary>对白文本 → 台词 id（用于把 id 写进场景里的台词数组）。</summary>
        private static readonly (string text, string id)[] LineIds =
        {
            ("Oh. Now that's production value.", "pp_open_dir_01_production_value"),
            ("Get on the pole.", "pp_open_dir_02_get_on_the_pole"),
            ("No.", "pp_open_agt_03_no"),
            ("Ten.", "pp_open_dir_04_ten"),
            ("Nine—", "pp_open_dir_05_nine"),
            ("Fuck you.", "pp_open_agt_06_fuck_you"),
            ("That is my stage, bitch!", "pp_ent_agt_07_my_stage"),
            ("There.", "pp_end_agt_08_there"),
            ("Happy now?", "pp_end_agt_09_happy_now"),
            ("...We got the shot.", "pp_end_dir_10_got_the_shot"),
            ("I hate this job.", "pp_end_agt_11_hate_this_job"),
            ("Great.", "pp_end_dir_12_great"),
            ("Print it.", "pp_end_dir_13_print_it")
        };

        /// <summary>诊断：把 asset 引用写入场景时，到底在哪一步丢的。</summary>
        [MenuItem("YourMovieSucks/Voice/9 · 诊断接线")]
        public static void DiagnoseWiring()
        {
            var library = AssetDatabase.LoadAssetAtPath<VoLibrary>(LibraryPath);
            Debug.Log($"[Diag] library asset={(library != null)} type={(library != null ? library.GetType().FullName : "-")} id={(library != null ? library.GetInstanceID() : 0)}");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 场景切换后会卸载未引用资源：必须在 OpenScene 之后重新取
            library = AssetDatabase.LoadAssetAtPath<VoLibrary>(LibraryPath);
            Debug.Log($"[Diag] library after OpenScene={(library != null)}");

            var director = UnityEngine.Object.FindFirstObjectByType<DemoAudioDirector>();
            Debug.Log($"[Diag] scene={scene.path} director={(director != null)} go={(director != null ? director.gameObject.name : "-")}");

            if (director == null || library == null) return;

            var so = new SerializedObject(director);
            var prop = so.FindProperty("voLibrary");
            Debug.Log($"[Diag] before={prop.objectReferenceValue}");

            prop.objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Diag] after apply (same SO)={prop.objectReferenceValue}");

            var fresh = new SerializedObject(director);
            Debug.Log($"[Diag] fresh SO={fresh.FindProperty("voLibrary").objectReferenceValue}");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Diag] SaveScene={EditorSceneManager.SaveScene(scene)}");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var reopened = UnityEngine.Object.FindFirstObjectByType<DemoAudioDirector>();
            Debug.Log($"[Diag] after reopen={(reopened != null ? new SerializedObject(reopened).FindProperty("voLibrary").objectReferenceValue?.ToString() : "no director")}");
        }

        /// <summary>一键：检查导入 → 配导入设置 → 建 VoLibrary 并接线（场景）。</summary>
        [MenuItem("YourMovieSucks/Voice/0 · 一键：检查 → 配置 → 建库接线")]
        public static void SetupAll()
        {
            // 刚拷进来的音频要先完成导入，否则建库时会找不到 clip
            AssetDatabase.Refresh();

            ReportImport();
            ConfigureImports();
            SetupLibraryAndScene();
        }

        [MenuItem("YourMovieSucks/Voice/1 · 检查导入结果")]
        public static void ReportImport()
        {
            string absolute = Path.GetFullPath(VoFolder);
            if (!Directory.Exists(absolute))
            {
                Debug.LogError($"[VoiceTools] folder missing: {absolute}");
                return;
            }

            int ok = 0;
            int missing = 0;

            foreach (var file in Directory.GetFiles(absolute))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".meta")) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoFolder}/{name}");
                if (clip == null)
                {
                    missing++;
                    Debug.LogWarning($"[VoiceTools] NOT IMPORTED: {name}");
                }
                else
                {
                    ok++;
                    Debug.Log($"[VoiceTools] ok: {name}  {clip.length:0.00}s  ch={clip.channels}  hz={clip.frequency}");
                }
            }

            Debug.Log($"[VoiceTools] summary: imported={ok} notImported={missing}");
            if (missing > 0)
                Debug.LogError("[VoiceTools] 有音频未导入 —— Unity 只支持 wav/aiff/mp3/ogg（m4a 需先转码）");
        }

        [MenuItem("YourMovieSucks/Voice/2 · 配置导入设置（VO）")]
        public static void ConfigureImports()
        {
            int count = 0;

            foreach (var folder in AudioFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                    bool isMusic = path.Replace('\\', '/').Contains("/BGM/");

                    // 音乐：保持立体声 + Streaming（4 分钟的歌不能整段解压进内存）
                    // 语音/音效：单声道 + DecompressOnLoad
                    importer.forceToMono = !isMusic;
                    importer.loadInBackground = false;

                    var settings = importer.defaultSampleSettings;
                    settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = isMusic ? 0.8f : 0.7f;
                    settings.preloadAudioData = !isMusic;
                    importer.defaultSampleSettings = settings;

                    importer.SaveAndReimport();
                    count++;
                }
            }

            Debug.Log($"[VoiceTools] configured {count} clip(s): mono / DecompressOnLoad / Vorbis 0.7");
        }

        [MenuItem("YourMovieSucks/Voice/3 · 建立 VoLibrary 并接线（场景）")]
        public static void SetupLibraryAndScene()
        {
            var library = BuildLibrary();
            if (library == null) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 关键：OpenScene 会卸载当前没被引用的资源，之前取到的 library 会变成“已销毁”→ 赋进场景就是 null。
            // 所以打开场景后再重新取一次（实测就是这里的坑）。
            library = AssetDatabase.LoadAssetAtPath<VoLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"[VoiceTools] VoLibrary 重新加载失败: {LibraryPath}");
                return;
            }

            // 演出音频总控：挂在 Systems 上
            var audio = UnityEngine.Object.FindFirstObjectByType<DemoAudioDirector>();
            if (audio == null)
            {
                var systems = GameObject.Find("Systems");
                var host = systems != null ? systems : new GameObject("Systems");
                audio = host.AddComponent<DemoAudioDirector>();
                Debug.Log("[VoiceTools] added DemoAudioDirector on Systems");
            }

            var audioSo = new SerializedObject(audio);
            SetObject(audioSo, "voLibrary", library);

            // 逐条调节参数（手调资产）：不存在就建一个，让窗口直接可调
            var lineSettings = AssetDatabase.LoadAssetAtPath<VoLineSettings>(SettingsPath);
            if (lineSettings == null)
            {
                lineSettings = ScriptableObject.CreateInstance<VoLineSettings>();
                AssetDatabase.CreateAsset(lineSettings, SettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[VoiceTools] created {SettingsPath}");
            }

            SetObject(audioSo, "lineSettings", lineSettings);
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            // 对白层拿到音频总控
            var dialogue = UnityEngine.Object.FindFirstObjectByType<DemoDialogue>();
            if (dialogue != null)
            {
                var dialogueSo = new SerializedObject(dialogue);
                SetObject(dialogueSo, "audio", audio);
                dialogueSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[VoiceTools] DemoDialogue not found in scene");
            }

            // 演出层（BGM 起停）；bgmId 用代码默认值兜底，这里只接音频总控
            var flowForAudio = UnityEngine.Object.FindFirstObjectByType<PolePositionFlow>();
            if (flowForAudio != null)
            {
                var flowAudioSo = new SerializedObject(flowForAudio);
                SetObject(flowAudioSo, "audio", audio);
                flowAudioSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 打字音效：候选池扫 Assets/Audio/SFX/Typing（排除切轨音效）
            WireTypingSfx(audio);
            WireHurtSfx(audio);

            // 把 id 写进场景里的四组台词数组（按文本匹配）
            int idsSet = 0;
            int idsMissing = 0;
            var flow = UnityEngine.Object.FindFirstObjectByType<PolePositionFlow>();
            if (flow != null)
            {
                var flowSo = new SerializedObject(flow);
                foreach (var field in new[] { "directorOpening", "retort", "stageClaim", "wrapUp" })
                {
                    var array = flowSo.FindProperty(field);
                    if (array == null) continue;

                    for (int i = 0; i < array.arraySize; i++)
                    {
                        var element = array.GetArrayElementAtIndex(i);
                        var textProp = element.FindPropertyRelative("text");
                        var idProp = element.FindPropertyRelative("id");

                        string id = IdForText(textProp.stringValue);
                        if (string.IsNullOrEmpty(id))
                        {
                            if (!string.IsNullOrEmpty(textProp.stringValue)) idsMissing++;
                            idProp.stringValue = string.Empty;
                        }
                        else
                        {
                            idProp.stringValue = id;
                            idsSet++;
                        }
                    }
                }

                flowSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[VoiceTools] VoLibrary entries={library.Count}, line ids set={idsSet}, unmatched lines={idsMissing}, scene saved: {scene.path}");
            if (idsMissing > 0)
                Debug.LogWarning("[VoiceTools] 有台词没匹配到 id —— 检查台词文本是否与 LineIds 表一致");
        }

        private static VoLibrary BuildLibrary()
        {
            // 先确保新音频已被导入
            AssetDatabase.Refresh();

            string absolute = Path.GetFullPath(VoFolder);
            if (!Directory.Exists(absolute))
            {
                Debug.LogError($"[VoiceTools] folder missing: {absolute}");
                return null;
            }

            EnsureFolder("Assets/Audio");
            EnsureFolder("Assets/Audio/VO");

            var library = AssetDatabase.LoadAssetAtPath<VoLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VoLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                Debug.Log($"[VoiceTools] created {LibraryPath}");
            }

            var entries = new List<VoLibrary.Entry>();
            int notImported = 0;

            foreach (var folder in AudioFolders)
            {
                string folderAbsolute = Path.GetFullPath(folder);
                if (!Directory.Exists(folderAbsolute)) continue;

                foreach (var file in Directory.GetFiles(folderAbsolute, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(".meta")) continue;

                    // 注意：要把子目录一起拼上进资产路径（子文件夹里的音频曾因只拼文件名而被跳过）
                    string assetPath = $"{folder}/" + file.Substring(folderAbsolute.Length + 1).Replace('\\', '/');
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (clip == null)
                    {
                        notImported++;
                        continue;
                    }

                    entries.Add(new VoLibrary.Entry
                    {
                        id = Path.GetFileNameWithoutExtension(name),
                        clip = clip
                    });
                }
            }

            entries.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
            library.SetEntries(entries);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VoiceTools] VoLibrary: {entries.Count} clips (not imported: {notImported})");
            return library;
        }

        private static string IdForText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string normalized = text.Trim();
            foreach (var (candidate, id) in LineIds)
            {
                if (string.Equals(candidate.Trim(), normalized, StringComparison.Ordinal)) return id;
            }

            return string.Empty;
        }

        private static void SetObject(SerializedObject so, string field, UnityEngine.Object value)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[VoiceTools] field '{field}' not found on {so.targetObject.GetType().Name}");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            var property = so.FindProperty(field);
            if (property == null) return;
            property.stringValue = value;
        }

        private static void SetStringArray(SerializedObject so, string field, List<string> values)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[VoiceTools] array field '{field}' not found on {so.targetObject.GetType().Name}");
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        /// <summary>打字音效：建/接线 TypingSfxPlayer，候选池扫 Assets/Audio/SFX/Typing（排除切轨音效）。</summary>
        private static void WireTypingSfx(DemoAudioDirector audio)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<TypingSfxPlayer>();
            if (player == null)
            {
                var systems = GameObject.Find("Systems");
                var host = systems != null ? systems : new GameObject("Systems");
                player = host.AddComponent<TypingSfxPlayer>();
                Debug.Log("[VoiceTools] added TypingSfxPlayer on Systems");
            }

            // 打字池：Typing 目录下除切轨音效以外的全部；枪声层：Gunshot 目录下的全部
            var ids = ScanSfxIds(TypingSfxFolder, SwitchSfxId);
            var gunshots = ScanSfxIds(GunshotSfxFolder, null);

            var so = new SerializedObject(player);
            SetObject(so, "audio", audio);
            SetObject(so, "trackInput", UnityEngine.Object.FindFirstObjectByType<YourMovieSucks.Input.TrackInput>());
            SetString(so, "trackSwitchSfxId", SwitchSfxId);
            SetStringArray(so, "typingSfxIds", ids);
            SetStringArray(so, "gunshotSfxIds", gunshots);
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[VoiceTools] typing SFX pool: {ids.Count} clip(s) [switch = {SwitchSfxId}], gunshot pool: {gunshots.Count} clip(s)");
        }

        /// <summary>玩家受伤音效：建/接线 PlayerHurtSfx，候选池扫 Assets/Audio/SFX/Hurt。</summary>
        private static void WireHurtSfx(DemoAudioDirector audio)
        {
            var hurt = UnityEngine.Object.FindFirstObjectByType<PlayerHurtSfx>();
            if (hurt == null)
            {
                var systems = GameObject.Find("Systems");
                var host = systems != null ? systems : new GameObject("Systems");
                hurt = host.AddComponent<PlayerHurtSfx>();
                Debug.Log("[VoiceTools] added PlayerHurtSfx on Systems");
            }

            var ids = ScanSfxIds(HurtSfxFolder, null);
            var so = new SerializedObject(hurt);
            SetObject(so, "audio", audio);
            SetObject(so, "playerHealth", UnityEngine.Object.FindFirstObjectByType<YourMovieSucks.Combat.PlayerHealth>());
            SetStringArray(so, "hurtSfxIds", ids);
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(ids.Count > 0
                ? $"[VoiceTools] hurt SFX pool: {ids.Count} clip(s)"
                : "[VoiceTools] hurt SFX pool empty — PlayerHurtSfx 会使用程序生成的占位受伤音（把素材放进 Assets/Audio/SFX/Hurt 后重跑本工具即可换成真素材）");
        }

        /// <summary>扫目录取音频 id（文件名）；excludeId 用于把切轨音效排除出候选池。</summary>
        private static List<string> ScanSfxIds(string folder, string excludeId)
        {
            var ids = new List<string>();
            string absolute = Path.GetFullPath(folder);
            if (!Directory.Exists(absolute)) return ids;

            foreach (var file in Directory.GetFiles(absolute, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".meta")) continue;

                string id = Path.GetFileNameWithoutExtension(name);
                if (!string.IsNullOrEmpty(excludeId) && string.Equals(id, excludeId, StringComparison.OrdinalIgnoreCase)) continue;

                string assetPath = $"{folder}/" + file.Substring(absolute.Length + 1).Replace('\\', '/');
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath) == null) continue;

                ids.Add(id);
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
