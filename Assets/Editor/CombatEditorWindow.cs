using System.Linq;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Combat;
using YourMovieSucks.Demo;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 战斗编辑器窗口：只是 <see cref="CombatEncounterAssetEditor"/> 的大窗口外壳。
    /// 编辑逻辑全部在资产 Inspector 里（点开 Assets 里的战斗数据资产即可使用），
    /// 这里提供「选择资产 + 放到大窗口里编辑 + 接到场景」的补充入口。
    /// 菜单：YourMovieSucks/Combat/Combat Editor
    /// </summary>
    public sealed class CombatEditorWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/Data/Combat";
        private const string DefaultPath = DefaultFolder + "/CombatEncounter.asset";

        private CombatEncounterAsset asset;
        private UnityEditor.Editor embedded;
        private Vector2 scroll;

        [MenuItem("YourMovieSucks/Combat/Combat Editor")]
        public static void Open()
        {
            var window = GetWindow<CombatEditorWindow>("Combat Editor");
            window.minSize = new Vector2(780f, 560f);
            window.TryAutoLoad();
            window.Show();
        }

        /// <summary>从资产 Inspector 打开并直接编辑指定资产（也供外部调用）。</summary>
        public static void OpenWindow(CombatEncounterAsset target)
        {
            var window = GetWindow<CombatEditorWindow>("Combat Editor");
            window.minSize = new Vector2(780f, 560f);
            window.asset = target;
            window.Rebuild();
            window.Show();
            window.Repaint();
        }

        /// <summary>把指定资产接到当前场景的 ScriptedEncounter（资产 Inspector 的按钮用）。</summary>
        public static void ApplyAssetToScene(CombatEncounterAsset target)
        {
            if (target == null)
            {
                Debug.LogWarning("[CombatEditor] 没有选择战斗数据资产。");
                return;
            }

            var encounters = Resources.FindObjectsOfTypeAll<ScriptedEncounter>()
                .Where(e => e && !EditorUtility.IsPersistent(e) && e.gameObject.scene.IsValid())
                .ToArray();
            if (encounters.Length == 0)
            {
                Debug.LogWarning("[CombatEditor] 当前场景里没有 ScriptedEncounter，未接线。");
                return;
            }

            foreach (var encounter in encounters)
            {
                Undo.RecordObject(encounter, "Assign combat encounter");
                encounter.EncounterAsset = target;
                EditorUtility.SetDirty(encounter);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(encounter.gameObject.scene);
            }
            Debug.Log($"[CombatEditor] 已把 {target.name} 接到 {encounters.Length} 个 ScriptedEncounter（记得保存场景）。");
        }

        private void OnEnable() => TryAutoLoad();

        private void OnDisable()
        {
            DestroyEmbedded();
        }

        private void TryAutoLoad()
        {
            if (asset && embedded != null && embedded.target == asset) return;
            if (!asset) asset = AssetDatabase.LoadAssetAtPath<CombatEncounterAsset>(DefaultPath);
            if (!asset)
                asset = AssetDatabase.FindAssets("t:CombatEncounterAsset")
                    .Select(g => AssetDatabase.LoadAssetAtPath<CombatEncounterAsset>(AssetDatabase.GUIDToAssetPath(g)))
                    .FirstOrDefault(a => a);
            Rebuild();
        }

        private void DestroyEmbedded()
        {
            if (embedded == null) return;
            DestroyImmediate(embedded);
            embedded = null;
        }

        private void Rebuild()
        {
            DestroyEmbedded();
            if (asset) embedded = UnityEditor.Editor.CreateEditor(asset);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var picked = (CombatEncounterAsset)EditorGUILayout.ObjectField(
                    asset, typeof(CombatEncounterAsset), false, GUILayout.Width(260f));
                if (picked != asset) { asset = picked; Rebuild(); }

                if (GUILayout.Button("选中资产", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    EditorGUIUtility.PingObject(asset);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(asset == null))
                {
                    if (GUILayout.Button("创建/定位资产", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                    {
                        asset = LoadOrCreateDefault();
                        Rebuild();
                    }
                    if (GUILayout.Button("应用到当前场景", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                        ApplyAssetToScene(asset);
                }
            }

            EditorGUILayout.Space(2f);

            if (asset == null || embedded == null)
            {
                EditorGUILayout.HelpBox("没有战斗数据资产。可以直接在 Assets 里双击 CombatEncounter.asset 编辑，或用右上角按钮创建。",
                                        MessageType.Info);
                return;
            }

            // 直接复用资产 Inspector 的界面：两处永远同一套逻辑
            scroll = EditorGUILayout.BeginScrollView(scroll);
            embedded.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private static CombatEncounterAsset LoadOrCreateDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CombatEncounterAsset>(DefaultPath);
            if (existing) return existing;

            if (!AssetDatabase.IsValidFolder(DefaultFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateFolder("Assets/Data", "Combat");
            }

            var created = ScriptableObject.CreateInstance<CombatEncounterAsset>();
            AssetDatabase.CreateAsset(created, DefaultPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CombatEditor] 已创建 {DefaultPath}");
            return created;
        }
    }
}
