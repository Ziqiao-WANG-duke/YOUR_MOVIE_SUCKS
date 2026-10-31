using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YourMovieSucks.Combat;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 把一只「已绑定好的人形模型」变成可上场的敌人预制件：
    /// ① 复制进 Assets/Art/Monsters/&lt;Name&gt;/ 并按 Humanoid + 关动画导入
    /// ② 抽出内嵌贴图、建独立材质（URP Lit）
    /// ③ 以现有 StripperZombie 预制件为模板（保留 TypingTarget / TypingTargetAnimator / MonsterWeakness 的全部参数），
    ///    只替换模型子物体、Avatar、共享的 AnimatorController，并按目标身高重算缩放与脚底
    /// ④ 报告身高、骨骼数、以及「弱点骨骼名是否命中」
    /// 菜单：YourMovieSucks/Monsters/4 · 从选中的 FBX 生成敌人预制件
    /// </summary>
    public static class MonsterPrefabBuilder
    {
        private const string TemplatePrefabPath = "Assets/Prefabs/StripperZombie.prefab";
        private const string SharedControllerPath = "Assets/Art/Monsters/ZombieV2/ZombieV2.controller";
        private const string WeaknessPath = "Assets/Data/Monsters/Weakness_Zombie.asset";
        private const float TargetHeight = 2.2f;

        /// <summary>弱点系统按名字找骨骼，缺了会退回根节点。</summary>
        private static readonly string[] WeaknessBones =
        {
            "mixamorig:Head", "mixamorig:Neck", "mixamorig:LeftLeg", "mixamorig:LeftShoulder"
        };

        [MenuItem("YourMovieSucks/Monsters/4 · 从选中的 FBX 生成敌人预制件")]
        public static void BuildFromSelection()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Monster Builder", "先在 Project 里选中一个 .fbx 模型。", "OK");
                return;
            }
            if (Build(path)) EditorUtility.DisplayDialog("Monster Builder", "已生成预制件，详见 Console。", "OK");
        }

        /// <summary>把工程外/工程内的 FBX 变成敌人预制件。sourcePath 可以是绝对路径。</summary>
        public static bool Build(string sourcePath, string displayName = null)
        {
            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"[MonsterBuilder] 找不到模型：{sourcePath}");
                return false;
            }

            string name = string.IsNullOrWhiteSpace(displayName)
                ? Path.GetFileNameWithoutExtension(sourcePath).Replace(" ", "_")
                : displayName.Replace(" ", "_");
            string folder = $"Assets/Art/Monsters/{name}";
            string modelPath = $"{folder}/{Path.GetFileName(sourcePath)}";
            string textureFolder = folder + "/Textures";
            string materialPath = $"{folder}/M_{name}.mat";
            string prefabPath = $"Assets/Prefabs/{name}.prefab";

            if (!File.Exists(modelPath))
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.CreateFolder("Assets/Art/Monsters", name);
                }
                File.Copy(sourcePath, modelPath, false);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // ① 导入设置：Humanoid、模型内不带动画（动画用共享的那一套）
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (!importer) { Debug.LogError($"[MonsterBuilder] 不是 ModelImporter：{modelPath}"); return false; }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeGameObjects = false;
            importer.isReadable = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.useFileScale = true;
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"[MonsterBuilder] {name} 的 Avatar 不是有效的人形骨架（isValid={avatar?.isValid}, isHuman={avatar?.isHuman}）。这套人形动画无法共用。");
                return false;
            }

            // ② 内嵌贴图 + 材质
            Directory.CreateDirectory(textureFolder);
            if (!importer.ExtractTextures(textureFolder))
                Debug.LogWarning($"[MonsterBuilder] {name} 没有可抽取的内嵌贴图，材质将没有基础色图。");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var texture = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault();
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + name };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);

            // ③ 以现有预制件为模板，只换模型
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(SharedControllerPath);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!template || !modelAsset)
            {
                Debug.LogError("[MonsterBuilder] 缺少模板预制件或模型资产。");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(TemplatePrefabPath);
            GameObject prefab;
            float finalScale;
            float height;
            int boneCount;
            try
            {
                var old = root.transform.Find("ZombieModel");
                if (old) Object.DestroyImmediate(old.gameObject);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "ZombieModel";
                model.transform.SetParent(root.transform, false);
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (!skinned) { Debug.LogError($"[MonsterBuilder] {name} 里没有 SkinnedMeshRenderer。"); Object.DestroyImmediate(model); return false; }

                height = skinned.bounds.size.y;
                finalScale = height > 0.01f ? TargetHeight / height : 1f;
                model.transform.localScale = Vector3.one * finalScale;
                model.transform.localPosition = new Vector3(0f, -(skinned.bounds.min.y - model.transform.position.y), 0f);

                var slots = skinned.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                skinned.sharedMaterials = slots;

                var animator = model.GetComponent<Animator>();
                if (!animator) animator = model.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var view = root.GetComponent<TypingTargetAnimator>();
                if (view)
                {
                    var so = new SerializedObject(view);
                    var prop = so.FindProperty("animator");
                    if (prop != null) prop.objectReferenceValue = animator;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                boneCount = skinned.bones?.Length ?? 0;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();

            // ④ 报告（含弱点骨骼校验）
            var prefabRenderer = prefab ? prefab.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            var bones = prefabRenderer ? prefabRenderer.bones.Select(b => b ? b.name : "null").ToHashSet() : new System.Collections.Generic.HashSet<string>();
            var missing = WeaknessBones.Where(b => !bones.Contains(b)).ToArray();
            string missingText = missing.Length == 0 ? "全部命中" : "缺：" + string.Join(", ", missing);

            Debug.Log($"[MonsterBuilder] {name}\n" +
                      $"  模型：{modelPath}\n  材质：{materialPath}（贴图 {(texture ? texture.width + "x" + texture.height : "无")}）\n" +
                      $"  预制件：{prefabPath}\n  Avatar：{avatar.name} valid={avatar.isValid} human={avatar.isHuman}\n" +
                      $"  骨骼：{boneCount} 根；弱点骨骼 {missingText}\n" +
                      $"  缩放：{finalScale:F3}×（绑定盒 {height:F2} m → 目标 {TargetHeight:0.0} m）");
            return prefab != null;
        }
    }
}
