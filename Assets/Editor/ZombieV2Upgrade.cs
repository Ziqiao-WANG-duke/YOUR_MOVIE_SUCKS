using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// Swaps the zombie to "Scary Zombie Pack V2" without touching the previous assets.
    /// Keeps the same Mixamo bone names (weak points and humanoid retargeting stay valid)
    /// and rebuilds the same Animator states/parameters the gameplay code drives:
    /// Speed (float) / Rush (bool) / Attack (trigger) / Dead (trigger).
    /// Menu: YourMovieSucks/Monsters/3 · 升级到 Zombie V2
    /// </summary>
    public static class ZombieV2Upgrade
    {
        private const string Root = "Assets/Art/Monsters/ZombieV2";
        private const string ModelPath = Root + "/SM_ZOMBIE.fbx";
        private const string AnimFolder = Root + "/Animations";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialPath = Root + "/M_ZombieV2.mat";
        private const string ControllerPath = Root + "/ZombieV2.controller";
        private const string PrefabPath = "Assets/Prefabs/StripperZombie.prefab";
        private const string TestScenePath = "Assets/RetroRendering/Scenes/RetroTest.unity";
        private const float ModelScale = 1.25f;
        /// <summary>Target standing height in metres; the previous pack was ~2.2 m at 1.25× scale.</summary>
        private const float TargetHeight = 2.2f;

        private sealed class ClipSpec
        {
            public readonly string File, ClipName;
            public readonly bool Loop;
            public ClipSpec(string file, string clip, bool loop) { File = file; ClipName = clip; Loop = loop; }
        }

        private static readonly ClipSpec[] Clips =
        {
            new ClipSpec("zombie idle.fbx", "Zombie_Idle", true),
            new ClipSpec("zombie walk.fbx", "Zombie_Walk", true),
            new ClipSpec("zombie run.fbx", "Zombie_Run", true),
            new ClipSpec("zombie crawl.fbx", "Zombie_Crawl", true),
            new ClipSpec("running crawl.fbx", "Zombie_RunningCrawl", true),
            new ClipSpec("zombie attack.fbx", "Zombie_Attack", false),
            new ClipSpec("zombie biting.fbx", "Zombie_Biting", false),
            new ClipSpec("zombie biting (2).fbx", "Zombie_BitingAlt", false),
            new ClipSpec("zombie neck bite.fbx", "Zombie_NeckBite", false),
            new ClipSpec("zombie scream.fbx", "Zombie_Scream", false),
            new ClipSpec("zombie death.fbx", "Zombie_Death", false),
            new ClipSpec("zombie dying.fbx", "Zombie_Dying", false),
        };

        [MenuItem("YourMovieSucks/Monsters/3 · 升级到 Zombie V2（模型 + 动画 + 预制件）")]
        public static void UpgradeFromMenu()
        {
            if (!Upgrade()) return;
            EditorUtility.DisplayDialog("Zombie V2",
                $"已切换到 Scary Zombie Pack V2。\n\n模型：{ModelPath}\n控制器：{ControllerPath}\n预制件：{PrefabPath}\n\n旧的 Zombie/ 资产保留，可随时回退。", "OK");
        }

        public static bool Upgrade()
        {
            if (!File.Exists(ModelPath))
            {
                Debug.LogError($"[ZombieV2] 缺少模型：{ModelPath}");
                return false;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureModelImporter();
            ExtractEmbeddedTextures();
            ConfigureAnimationImporters();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/PSX_Char_Female_Base_M_Char_BaseColor.png");
            if (!texture) texture = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }).Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (texture)
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                if (ti)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    ti.alphaIsTransparency = false;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = Mathf.Max(512, texture.width);
                    ti.SaveAndReimport();
                }
            }
            else Debug.LogWarning("[ZombieV2] 未找到贴图；材质将没有基础色图");

            var material = BuildMaterial(texture);
            var controller = BuildController();
            var prefab = UpdatePrefab(material, controller);
            UpdateTestScene(controller);
            AssetDatabase.SaveAssets();
            Report(texture, material, controller, prefab);
            return prefab != null;
        }

        // ------------------------------------------------------------ importers

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!importer) { Debug.LogError($"[ZombieV2] 不是 ModelImporter：{ModelPath}"); return; }
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
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.SaveAndReimport();
        }

        private static void ExtractEmbeddedTextures()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!importer) return;
            Directory.CreateDirectory(TextureFolder);
            // FBX 内嵌 1 张贴图；找不到就跳过（材质退回白图）。
            if (!importer.ExtractTextures(TextureFolder))
                Debug.LogWarning("[ZombieV2] 模型内没有可抽取的贴图，材质将没有基础色图");
            AssetDatabase.Refresh();
        }

        private static void ConfigureAnimationImporters()
        {
            foreach (var spec in Clips)
            {
                string path = $"{AnimFolder}/{spec.File}";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) { Debug.LogWarning($"[ZombieV2] 动画缺失：{path}"); continue; }
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false;
                importer.importLights = false;

                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) { importer.SaveAndReimport(); continue; }
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = spec.ClipName;
                    clips[i].loopTime = spec.Loop;
                    clips[i].loopPose = false;
                    // Same convention as the previous pack: XZ becomes root motion (code drives
                    // the walk), Y and rotation stay in the pose so lunges and falling keep height.
                    clips[i].lockRootPositionXZ = false;
                    clips[i].keepOriginalPositionXZ = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].heightFromFeet = false;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------ assets

        private static Material BuildMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = "M_ZombieV2" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else
            {
                while (controller.parameters.Length > 0) controller.RemoveParameter(controller.parameters[0]);
                var machine = controller.layers[0].stateMachine;
                foreach (var t in machine.anyStateTransitions.ToArray()) machine.RemoveAnyStateTransition(t);
                foreach (var t in machine.entryTransitions.ToArray()) machine.RemoveEntryTransition(t);
                foreach (var s in machine.states.ToArray())
                {
                    foreach (var t in s.state.transitions.ToArray()) s.state.RemoveTransition(t);
                    machine.RemoveState(s.state);
                }
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Rush", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;

            var idle = Add(sm, "Idle", Clip("Zombie_Idle"), new Vector2(260, 0));
            var walk = Add(sm, "Walk", Clip("Zombie_Walk"), new Vector2(260, 90));
            var run = Add(sm, "Run", Clip("Zombie_Run"), new Vector2(260, 180));
            var attack = Add(sm, "Attack", Clip("Zombie_Attack"), new Vector2(520, 0));
            var death = Add(sm, "Death", Clip("Zombie_Death"), new Vector2(520, 120));
            // 入场演出用：冲入舞台后站定咆哮（StripperZombieEntrance 直接 Animator.Play("Scream")）
            var scream = Add(sm, "Scream", Clip("Zombie_Scream"), new Vector2(260, 270));
            sm.defaultState = idle;

            Link(idle, walk, AnimatorConditionMode.Greater, 0.5f, "Speed", 0.15f);
            Link(walk, idle, AnimatorConditionMode.Less, 0.5f, "Speed", 0.2f);
            Link(walk, run, AnimatorConditionMode.If, 0f, "Rush", 0.15f);
            Link(run, walk, AnimatorConditionMode.IfNot, 0f, "Rush", 0.2f);
            Link(idle, run, AnimatorConditionMode.If, 0f, "Rush", 0.1f);

            var anyAttack = sm.AddAnyStateTransition(attack);
            anyAttack.hasExitTime = false; anyAttack.duration = 0.05f; anyAttack.canTransitionToSelf = true;
            anyAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            var anyDeath = sm.AddAnyStateTransition(death);
            anyDeath.hasExitTime = false; anyDeath.duration = 0.05f; anyDeath.canTransitionToSelf = false;
            anyDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

            var backToIdle = attack.AddTransition(idle);
            backToIdle.hasExitTime = true; backToIdle.exitTime = 0.9f; backToIdle.duration = 0.15f;

            // 咆哮演完回站姿，后面的常规逻辑才能接上
            var screamToIdle = scream.AddTransition(idle);
            screamToIdle.hasExitTime = true; screamToIdle.exitTime = 0.92f; screamToIdle.duration = 0.2f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState Add(AnimatorStateMachine sm, string name, AnimationClip clip, Vector2 position)
        {
            var state = sm.AddState(name, position);
            state.motion = clip;
            if (!clip) Debug.LogWarning($"[ZombieV2] 状态 {name} 没有动画片段");
            return state;
        }

        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string parameter, float duration)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.AddCondition(mode, threshold, parameter);
            return transition;
        }

        private static AnimationClip Clip(string clipName)
        {
            var spec = Clips.FirstOrDefault(c => c.ClipName == clipName);
            if (spec == null) return null;
            return AssetDatabase.LoadAllAssetsAtPath($"{AnimFolder}/{spec.File}")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
        }

        // ------------------------------------------------------------ prefab

        private static GameObject UpdatePrefab(Material material, AnimatorController controller)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (!prefab || !modelAsset || !avatar)
            {
                Debug.LogError($"[ZombieV2] 缺少预制件/模型/Avatar（prefab={prefab}, model={modelAsset}, avatar={avatar}）");
                return null;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var oldModel = root.transform.Find("ZombieModel");
                if (oldModel) Object.DestroyImmediate(oldModel.gameObject);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "ZombieModel";
                model.transform.SetParent(root.transform, false);
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * ModelScale;
                var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinned)
                {
                    // The V2 mesh is much shorter than the previous pack, so fit by target height
                    // instead of reusing the old 1.25× constant.
                    float measured = skinned.bounds.size.y;
                    float fit = measured > 0.01f ? ModelScale * (TargetHeight / measured) : ModelScale;
                    model.transform.localScale = Vector3.one * fit;
                    float feetOffset = skinned.bounds.min.y - model.transform.position.y;
                    model.transform.localPosition = new Vector3(0f, -feetOffset, 0f);
                    Debug.Log($"[ZombieV2] height fit: measured={measured:F2}m at {ModelScale}×, scale={fit:F3} → target {TargetHeight}m");
                    if (material)
                    {
                        var slots = skinned.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) slots[i] = material;
                        skinned.sharedMaterials = slots;
                    }
                }
                else Debug.LogWarning("[ZombieV2] 新模型里没有 SkinnedMeshRenderer");

                var animator = model.GetComponent<Animator>();
                if (!animator) animator = model.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var view = root.GetComponent<Combat.TypingTargetAnimator>();
                if (view)
                {
                    var so = new SerializedObject(view);
                    var prop = so.FindProperty("animator");
                    if (prop != null) prop.objectReferenceValue = animator;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // ------------------------------------------------------------ test scene

        private static void UpdateTestScene(AnimatorController controller)
        {
            if (!File.Exists(TestScenePath)) return;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(TestScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            if (!scene.IsValid()) return;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (!prefab) return;
                var retroMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/RetroRendering/Materials/ZombieRetro.mat");

                var targets = new List<Transform>();
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == "Zombie Original" || root.name == "Zombie Retro Copy") targets.Add(root.transform);

                var created = new List<(string name, Vector3 pos, Quaternion rot, bool retro)>();
                foreach (var t in targets)
                {
                    created.Add((t.name, t.position, t.rotation, t.name.Contains("Retro")));
                    Object.DestroyImmediate(t.gameObject);
                }
                foreach (var spec in created)
                {
                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    copy.name = spec.name;
                    copy.transform.SetPositionAndRotation(spec.pos, spec.rot);
                    // Test copies only: drop gameplay behaviours, keep the visual rig.
                    var behaviours = copy.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int i = behaviours.Length - 1; i >= 0; i--)
                        if (!(behaviours[i] is YourMovieSucks.Retro.RetroMaterialBinding))
                            Object.DestroyImmediate(behaviours[i]);
                    if (!spec.retro || !retroMaterial) continue;
                    foreach (var renderer in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var binding = renderer.gameObject.AddComponent<YourMovieSucks.Retro.RetroMaterialBinding>();
                        binding.target = renderer;
                        binding.retroVariants = Enumerable.Repeat(retroMaterial, renderer.sharedMaterials.Length).ToArray();
                    }
                }

                // Re-point the controller's binding list at the rebuilt copy.
                var controllerScript = Object.FindObjectsByType<YourMovieSucks.Retro.RetroRenderingController>(FindObjectsSortMode.None)
                    .FirstOrDefault(c => c.gameObject.scene == scene);
                if (controllerScript)
                {
                    var bindings = scene.GetRootGameObjects()
                        .SelectMany(g => g.GetComponentsInChildren<YourMovieSucks.Retro.RetroMaterialBinding>(true))
                        .ToList();
                    var so = new SerializedObject(controllerScript);
                    var list = so.FindProperty("materialBindings");
                    list.arraySize = bindings.Count;
                    for (int i = 0; i < bindings.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = bindings[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[ZombieV2] 测试场景更新失败：{e.Message}"); }
            finally { UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        // ------------------------------------------------------------ report

        private static void Report(Texture2D texture, Material material, AnimatorController controller, GameObject prefab)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var skinned = model ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            string bounds = skinned ? $"mesh bounds {skinned.sharedMesh.bounds.size:F2}" : "no skinned mesh";
            string clipInfo = string.Join(", ", Clips.Select(c =>
            {
                var clip = Clip(c.ClipName);
                return $"{c.ClipName}={(clip ? clip.length.ToString("F2") + "s" : "MISSING")}";
            }));
            var bones = skinned ? string.Join(",", skinned.bones.Take(6).Select(b => b ? b.name : "null")) : "-";
            Debug.Log($"[ZombieV2] texture={(texture ? texture.width + "x" + texture.height + " " + texture.name : "none")}\n" +
                      $"[ZombieV2] avatar={(avatar ? avatar.name + " valid=" + avatar.isValid + " human=" + avatar.isHuman : "none")} {bounds}\n" +
                      $"[ZombieV2] material={material.name} controller states={controller.layers[0].stateMachine.states.Length} params={controller.parameters.Length}\n" +
                      $"[ZombieV2] prefab={prefab.name}\n[ZombieV2] clips: {clipInfo}\n[ZombieV2] first bones: {bones}");
        }
    }
}
