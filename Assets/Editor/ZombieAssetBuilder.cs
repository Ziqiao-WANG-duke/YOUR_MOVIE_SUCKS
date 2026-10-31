using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 丧尸素材（Scary Zombie Pack）→ Unity 可用的敌人资产，一条命令跑完：
    /// ① 导入设置（Humanoid 骨架 / 原地动画 / 片段改名与循环 / 只算切线）
    /// ② 材质 M_Zombie（URP/Lit + 512 基础色贴图）
    /// ③ Animator Controller（Idle / Walk / Run / Attack / Death）
    /// ④ 预制件 Assets/Prefabs/StripperZombie.prefab（Animator + TypingTarget + TypingTargetAnimator）
    /// ⑤ 把预制件接进 POLE POSITION 场景的 ScriptedEncounter（只写这两个字段，不动场景其它内容）
    ///
    /// 全程幂等：重复执行不会产生第二份资产，也不会改 GUID（AnimatorController 是原地清空重建）。
    /// 菜单：YourMovieSucks/Monsters/…
    /// 批处理：-executeMethod YourMovieSucks.EditorTools.ZombieAssetBuilder.Build
    /// </summary>
    public static class ZombieAssetBuilder
    {
        public const string RootFolder = "Assets/Art/Monsters/Zombie";
        public const string ModelPath = RootFolder + "/Zombie.fbx";
        public const string AnimFolder = RootFolder + "/Animations";
        public const string TexturePath = RootFolder + "/Textures/PSX_Char_Female_Base_M_Char_BaseColor.png";
        public const string MaterialPath = RootFolder + "/M_Zombie.mat";
        public const string ControllerPath = RootFolder + "/Zombie.controller";
        public const string PrefabFolder = "Assets/Prefabs";
        public const string PrefabPath = PrefabFolder + "/StripperZombie.prefab";
        public const string WeaknessSetPath = "Assets/Data/Monsters/Weakness_Zombie.asset";

        /// <summary>模型 FBX 的高度（原始单位 cm，Z 轴向上）→ Unity 里应为 1.7 m 左右。</summary>
        private const float ExpectedHeight = 1.70f;

        /// <summary>尸体在死亡动画后的停留时间（秒）；0 = 立即消失（灰盒方块行为）。</summary>
        private const float DeathHoldSeconds = 1.2f;

        /// <summary>
        /// 模型在预制件里的缩放。1.76 m 的原始体高太瘦小时：1.25 → 约 2.2 m，
        /// 配合近身攻击距离（1.7 m）后，怪在攻击时基本占满画面、只看得见上半身。
        /// </summary>
        private const float ModelScale = 1.25f;

        /// <summary>怪物开始攻击玩家的距离（米，相对相机）= 只看得见上半身的那种“贴脸”。</summary>
        private const float EnemyAttackRange = 1.7f;

        /// <summary>离相机的前向距离小于此值后开始把车道收窄（远处保持宽车道，词条才不叠）。</summary>
        private const float LaneConvergeStart = 6f;

        /// <summary>收到攻击距离时的车道横向偏移（绝对值）。</summary>
        private const float CloseLaneOffset = 0.85f;

        private sealed class ClipSpec
        {
            public string File;
            public string ClipName;
            public bool Loop;

            public ClipSpec(string file, string clipName, bool loop)
            {
                File = file;
                ClipName = clipName;
                Loop = loop;
            }
        }

        // 12 段动画：包内文件名 → Unity 片段名。循环的只有「站着/走/跑/爬」这类无始无终的动作。
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

        [MenuItem("YourMovieSucks/Monsters/0 · 一键：导入丧尸 + 建资产 + 接线场景")]
        public static void BuildFromMenu()
        {
            if (!Build()) return;
            WirePolePositionScene();
            EditorUtility.DisplayDialog("丧尸敌人",
                $"已生成：\n{PrefabPath}\n\n已接进 POLE POSITION 场景的 ScriptedEncounter。", "OK");
        }

        [MenuItem("YourMovieSucks/Monsters/2 · 核对丧尸资产（只看不改）")]
        public static void VerifyFromMenu()
        {
            Verify();
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("丧尸敌人", "核对结果见 Console（[ZombieAssetBuilder] 开头）", "OK");
        }

        /// <summary>批处理入口：导入 + 建资产 + 接线场景 + 核对，一条命令跑完。</summary>
        public static bool BuildAndWire()
        {
            if (!Build()) return false;
            WirePolePositionScene();
            Verify();
            return true;
        }

        public static bool Build()
        {
            AssetDatabase.Refresh(); // 素材可能是刚从包外复制进来的，先让 Unity 认下来

            if (!File.Exists(ModelPath))
            {
                Debug.LogError($"[ZombieAssetBuilder] 模型不存在：{ModelPath}（先把 Scary Zombie Pack 的 fbx 复制进来）");
                return false;
            }

            ConfigureModelImporter();
            ConfigureAnimationImporters();
            ConfigureTextureImporter();
            AssetDatabase.Refresh();

            var material = CreateMaterial();
            var controller = BuildAnimatorController();
            var prefab = BuildPrefab(material, controller);
            Report(material, controller, prefab);

            AssetDatabase.SaveAssets();
            Debug.Log("[ZombieAssetBuilder] 完成：导入设置 + 材质 + Animator + 预制件");
            return true;
        }

        [MenuItem("YourMovieSucks/Monsters/1 · 只把丧尸接进 POLE POSITION 场景")]
        public static void WireSceneFromMenu()
        {
            WirePolePositionScene();
            EditorUtility.DisplayDialog("丧尸敌人", "已把预制件接进 ScriptedEncounter，并修正刷怪点高度。", "OK");
        }

        // ---------------------------------------------------------------- 导入设置

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[ZombieAssetBuilder] 不是 ModelImporter：{ModelPath}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // 模型 FBX 里那两条 take 都只有 1 帧（Take 001 / mixamo.com），导入只会多出一个空片段
            importer.importAnimation = false;

            importer.materialImportMode = ModelImporterMaterialImportMode.None; // 材质由本工具建，不依赖 fbx 旁边的 .fbm
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;     // 源文件没有切线
            importer.meshCompression = ModelImporterMeshCompression.Off;       // 低模，别压
            importer.optimizeGameObjects = false;                              // 保留骨骼层级（以后要挂断肢/布娃娃）
            importer.isReadable = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;                                      // fbx 单位 cm → 1.70 m
            importer.SaveAndReimport();
        }

        private static void ConfigureAnimationImporters()
        {
            foreach (var spec in Clips)
            {
                string path = $"{AnimFolder}/{spec.File}";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[ZombieAssetBuilder] 动画文件缺失或未导入：{path}");
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importCameras = false;
                importer.importLights = false;

                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    Debug.LogWarning($"[ZombieAssetBuilder] 没有动画片段：{path}");
                    importer.SaveAndReimport();
                    continue;
                }

                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = spec.ClipName;
                    clips[i].loopTime = spec.Loop;
                    clips[i].loopPose = false;

                    // 混妈（Mixamo）这几段都是「带位移」的：Hips 在片段里往前走 1.2–2.3 m。
                    // 位移由 TypingTarget 的代码负责（车道+速度都可调），所以这里把 XZ 从姿势里摘出来当根运动、
                    // 而 Animator 不开 Apply Root Motion → 视觉上原地迈步，不会自己飘走。
                    // Y（起落）与旋转留在姿势里，倒地/前扑的上下运动才不会丢。
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

        // ---------------------------------------------------------------- 材质 / 动画状态机 / 预制件

        /// <summary>
        /// 弱点词池（同屏最多 5 只怪 → 5 条、首字母互不相同 → 吸附锁定永不歧义）。
        /// 部位/词/骨骼/识别后的简单词全部在这张表里，加内容只改这里。
        /// </summary>
        private static readonly (string label, string word, string bone)[] WeaknessEntries =
        {
            ("HEAD",     "HEAD",     "mixamorig:Head"),
            ("LEG",      "LEG",      "mixamorig:LeftLeg"),
            ("NECK",     "NECK",     "mixamorig:Neck"),
            ("SHOULDER", "SHOULDER", "mixamorig:LeftShoulder"),
            ("MOUTH",    "MOUTH",    "mixamorig:Head"),
        };

        /// <summary>幂等创建/刷新弱点配置资产。</summary>
        private static Combat.MonsterWeaknessSet CreateWeaknessSet()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Monsters");

            var set = AssetDatabase.LoadAssetAtPath<Combat.MonsterWeaknessSet>(WeaknessSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<Combat.MonsterWeaknessSet>();
                AssetDatabase.CreateAsset(set, WeaknessSetPath);
            }

            var so = new SerializedObject(set);
            var entries = so.FindProperty("entries");
            entries.arraySize = WeaknessEntries.Length;

            for (int i = 0; i < WeaknessEntries.Length; i++)
            {
                var element = entries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("partLabel").stringValue = WeaknessEntries[i].label;
                element.FindPropertyRelative("weakWord").stringValue = WeaknessEntries[i].word;
                element.FindPropertyRelative("boneName").stringValue = WeaknessEntries[i].bone;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            return set;
        }

        /// <summary>基础色贴图（512 身体图集）：sRGB、无 mip 外的事项、Clamp 避免跨区采样、不压到更小。</summary>
        private static void ConfigureTextureImporter()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ZombieAssetBuilder] 贴图未导入：{TexturePath}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static Material CreateMaterial()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[ZombieAssetBuilder] 贴图缺失：{TexturePath}（材质会没有基础色贴图）");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = "M_Zombie" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            // 幂等：已存在也刷成当前参数
            material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f); // PSX 质感：哑光，避免霓虹灯把皮肤打成镜面
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildAnimatorController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            else
            {
                ClearController(controller); // 原地清空，保住 GUID（预制件与场景引用不会断）
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Rush", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idle = AddState(sm, "Idle", Clip("Zombie_Idle"), new Vector3(260, 0));
            var walk = AddState(sm, "Walk", Clip("Zombie_Walk"), new Vector3(260, 90));
            var run = AddState(sm, "Run", Clip("Zombie_Run"), new Vector3(260, 180));
            var attack = AddState(sm, "Attack", Clip("Zombie_Attack"), new Vector3(520, 0));
            var death = AddState(sm, "Death", Clip("Zombie_Death"), new Vector3(520, 120));
            sm.defaultState = idle;

            Link(idle, walk, AnimatorConditionMode.Greater, 0.5f, "Speed", 0.15f);
            Link(walk, idle, AnimatorConditionMode.Less, 0.5f, "Speed", 0.20f);
            Link(walk, run, AnimatorConditionMode.If, 0f, "Rush", 0.15f);
            Link(run, walk, AnimatorConditionMode.IfNot, 0f, "Rush", 0.20f);
            Link(idle, run, AnimatorConditionMode.If, 0f, "Rush", 0.10f);

            var anyAttack = sm.AddAnyStateTransition(attack);
            anyAttack.hasExitTime = false;
            anyAttack.duration = 0.05f;
            anyAttack.canTransitionToSelf = true; // 每轮攻击都要重放动作（片段 2.6 s > 伤害间隔 1.5 s）
            anyAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            var anyDeath = sm.AddAnyStateTransition(death);
            anyDeath.hasExitTime = false;
            anyDeath.duration = 0.05f;
            anyDeath.canTransitionToSelf = false;
            anyDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

            // 攻击演完回站姿（死亡是终点，不设回来的路）
            var backToIdle = attack.AddTransition(idle);
            backToIdle.hasExitTime = true;
            backToIdle.exitTime = 0.9f;
            backToIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ClearController(AnimatorController controller)
        {
            while (controller.parameters.Length > 0)
                controller.RemoveParameter(controller.parameters[0]);

            var sm = controller.layers[0].stateMachine;
            foreach (var transition in sm.anyStateTransitions.ToArray())
                sm.RemoveAnyStateTransition(transition);
            foreach (var transition in sm.entryTransitions.ToArray())
                sm.RemoveEntryTransition(transition);
            foreach (var child in sm.states.ToArray())
            {
                foreach (var transition in child.state.transitions.ToArray())
                    child.state.RemoveTransition(transition);
                sm.RemoveState(child.state);
            }
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector2 position)
        {
            var state = sm.AddState(name, position);
            state.motion = clip;
            if (clip == null)
                Debug.LogWarning($"[ZombieAssetBuilder] 状态 {name} 没有动画片段");
            return state;
        }

        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to,
            AnimatorConditionMode mode, float threshold, string parameter, float duration)
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
            if (spec == null)
            {
                Debug.LogWarning($"[ZombieAssetBuilder] 片段表里没有 {clipName}");
                return null;
            }

            string path = $"{AnimFolder}/{spec.File}";
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == clipName);
        }

        private static GameObject BuildPrefab(Material material, AnimatorController controller)
        {
            EnsureFolder(PrefabFolder);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[ZombieAssetBuilder] 取不到模型资产：{ModelPath}");
                return null;
            }

            var root = new GameObject("StripperZombie");

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "ZombieModel";
            model.transform.SetParent(root.transform, false);

            var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                model.transform.localScale = Vector3.one * ModelScale;

                // 预制件原点 = 脚底（老方块是「中心」在锚点上，所以场景里的锚点高度要相应下移）
                float feetOffset = skinned.bounds.min.y - model.transform.position.y;
                model.transform.localPosition = new Vector3(0f, -feetOffset, 0f);

                if (material != null)
                {
                    var materials = skinned.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    skinned.sharedMaterials = materials;
                }
            }
            else
            {
                Debug.LogWarning("[ZombieAssetBuilder] 模型里没有 SkinnedMeshRenderer");
            }

            // Animator 必须待在「FBX 模型根」上：人形片段靠 avatar 重定向，
            // 而 avatar 的骨骼路径是相对 FBX 模型根算的。把 Animator 挂到外层根节点
            // （StripperZombie/ZombieModel/…）会让骨骼路径多一节、片段解不出来，
            // 表现就是「Animator 状态在走，但模型一直保持 T-pose」。
            var animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                if (avatar != null) animator.avatar = avatar;
                else Debug.LogWarning("[ZombieAssetBuilder] 模型 FBX 里没有 Avatar");
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // 位移归 TypingTarget 管
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;

            var set = CreateWeaknessSet();

            root.AddComponent<Combat.TypingTarget>();
            var target = root.GetComponent<Combat.TypingTarget>();
            var targetSo = new SerializedObject(target);
            SetProperty(targetSo, "attackRange", p => p.floatValue = EnemyAttackRange);
            SetProperty(targetSo, "laneConvergeStart", p => p.floatValue = LaneConvergeStart);
            SetProperty(targetSo, "closeLaneOffset", p => p.floatValue = CloseLaneOffset);
            // 骨骼模型不做缩放反馈：打错字母的 1.3× 会在“由 Rushing 转 Attacking”时缩回 1.0，
            // 表现就是第一次攻击后模型突然变小/下移（实测可见包围盒顶 3.203 → 2.579 m，而根节点没动）。
            // 奔跑动画 + 橘色染色已经足够表达“被激怒了”，攻击动作由每轮的 Attack 片段负责。
            SetProperty(targetSo, "rushScaleMultiplier", p => p.floatValue = 1f);
            SetProperty(targetSo, "attackLungeScaleMultiplier", p => p.floatValue = 1f);
            targetSo.ApplyModifiedPropertiesWithoutUndo();

            var view = root.AddComponent<Combat.TypingTargetAnimator>();
            var viewSo = new SerializedObject(view);
            SetProperty(viewSo, "animator", p => p.objectReferenceValue = animator);
            SetProperty(viewSo, "deathHold", p => p.floatValue = DeathHoldSeconds);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // 导演轨弱点识别：每只怪一份弱点状态（哪条配置 + 是否已识别 + 弱点骨骼）
            var weakness = root.AddComponent<Combat.MonsterWeakness>();
            var weaknessSo = new SerializedObject(weakness);
            SetProperty(weaknessSo, "set", p => p.objectReferenceValue = set);
            weaknessSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- 场景接线
        /// <summary>
        /// 只写 ScriptedEncounter 的 zombiePrefab 与 5 个刷怪锚点的高度：
        /// 老方块的原点在「身体中心」，锚点高度 0.9 表示方块中心（脚底在 0，舞台面在 0.5）；
        /// 丧尸预制件原点在「脚底」，所以锚点高度改成 0.5 = 舞台面，人才是站在台上而不是浮空/陷进去。
        /// 场景其它内容一律不动。
        /// </summary>
        public static void WirePolePositionScene()
        {
            const string scenePath = "Assets/Scenes/PolePosition.unity";
            const float enemyFeetY = 0.5f; // = 舞台台面高度

            // 菜单路径下先处理当前场景的未保存改动（批处理里没有人需要确认）
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[ZombieAssetBuilder] 场景接线已取消");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[ZombieAssetBuilder] 预制件不存在：{PrefabPath}（先跑一次 Build）");
                return;
            }

            var encounter = Object.FindFirstObjectByType<Demo.ScriptedEncounter>();
            if (encounter == null)
            {
                Debug.LogError("[ZombieAssetBuilder] 场景里没有 ScriptedEncounter");
                return;
            }

            var so = new SerializedObject(encounter);
            SetProperty(so, "zombiePrefab", p => p.objectReferenceValue = prefab);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 调参面板（F1）的面板值存在场景里，改代码默认值不影响已存在场景 —— 这里同步改掉场景里的值
            var tuning = Object.FindFirstObjectByType<LabB.LabBTuning>();            if (tuning != null)
            {
                var tuningSo = new SerializedObject(tuning);
                SetProperty(tuningSo, "attackRange", p => p.floatValue = EnemyAttackRange);
                tuningSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[ZombieAssetBuilder] F1 面板 attackRange（场景值）→ {EnemyAttackRange}");
            }

            int anchors = 0;
            foreach (string name in new[] { "PoleHugAnchor", "EntranceSpawn", "SpawnLeft", "SpawnRight", "SpawnBack" })
            {
                var found = FindInScene(name);
                if (found == null) continue;

                var position = found.transform.position;
                if (!Mathf.Approximately(position.y, enemyFeetY))
                {
                    found.transform.position = new Vector3(position.x, enemyFeetY, position.z);
                    EditorUtility.SetDirty(found);
                    anchors++;
                }
            }

            // 导演轨弱点识别接线：Systems 上挂编排视图，词条管理器接 TrackInput（按轨显隐）。
            // 不接镜头：弱点识别刻意不做任何镜头动作（作者 2026-10-20）
            var systems = FindInScene("Systems");

            if (systems != null)
            {
                var view = systems.GetComponent<Combat.DirectorWeaknessView>();
                if (view == null) view = systems.AddComponent<Combat.DirectorWeaknessView>();
                var viewSo = new SerializedObject(view);
                SetProperty(viewSo, "typing", p => p.objectReferenceValue = Object.FindFirstObjectByType<Typing.TypingController>());
                SetProperty(viewSo, "trackInput", p => p.objectReferenceValue = Object.FindFirstObjectByType<Input.TrackInput>());
                viewSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var labelManager = Object.FindFirstObjectByType<YourMovieSucks.UI.WordLabelManager>();
            if (labelManager != null)
            {
                var labelSo = new SerializedObject(labelManager);
                SetProperty(labelSo, "trackInput", p => p.objectReferenceValue = Object.FindFirstObjectByType<Input.TrackInput>());
                labelSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZombieAssetBuilder] 场景已接线：ScriptedEncounter.zombiePrefab = {PrefabPath}；锚点高度修正 {anchors} 个 → y={enemyFeetY}");
        }

        private static GameObject FindInScene(string name)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindRecursive(root.transform, name);
                if (found != null) return found.gameObject;
            }

            Debug.LogWarning($"[ZombieAssetBuilder] 场景里找不到 {name}");
            return null;
        }

        private static Transform FindRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ---------------------------------------------------------------- 自检报告（数值核对，不靠肉眼）

        /// <summary>只看不改：核对 avatar / 片段 / 材质 / Animator / 预制件，以及当前场景里的接线。</summary>
        public static void Verify()
        {
            Report(
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath),
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        private static void Report(Material material, AnimatorController controller, GameObject prefab)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("[ZombieAssetBuilder] ===== 丧尸资产报告 =====");

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            log.AppendLine($"avatar: {(avatar != null ? avatar.name : "<无>")} | valid={avatar?.isValid} human={avatar?.isHuman}");

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                var skinned = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinned != null)
                {
                    var bounds = skinned.bounds;
                    log.AppendLine($"高度={bounds.size.y:0.000} m（期望 {ExpectedHeight:0.00}）  脚底 y={bounds.min.y:0.000}  顶点={skinned.sharedMesh.vertexCount} 材质槽={skinned.sharedMaterials.Length}");
                }

                log.AppendLine($"朝向：{DescribeFacing(instance)}");
                log.AppendLine($"骨骼数={instance.GetComponentsInChildren<Transform>(true).Length}");
                Object.DestroyImmediate(instance);
            }

            foreach (var spec in Clips)
            {
                var clip = Clip(spec.ClipName);
                if (clip == null)
                {
                    log.AppendLine($"片段 {spec.ClipName}: <缺失>");
                    continue;
                }

                log.AppendLine($"片段 {spec.ClipName,-20} {clip.length,6:0.000}s loop={spec.Loop} humanMotion={clip.humanMotion}");
            }

            if (material != null)
                log.AppendLine($"材质 {AssetDatabase.GetAssetPath(material)} baseMap={(material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap").name : "<空>")}");

            if (controller != null)
            {
                var sm = controller.layers[0].stateMachine;
                log.AppendLine($"Animator {AssetDatabase.GetAssetPath(controller)} 参数={string.Join(",", controller.parameters.Select(p => p.name))}");

                foreach (var child in sm.states)
                {
                    var motion = child.state.motion as AnimationClip;
                    log.AppendLine($"   状态 {child.state.name,-8} → {(motion != null ? $"{motion.name} ({motion.length:0.00}s)" : "<没有片段>")}");

                    foreach (var transition in child.state.transitions)
                    {
                        string conditions = string.Join(" && ", transition.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold:0.##}"));
                        log.AppendLine($"      → {transition.destinationState.name}  [{conditions}] duration={transition.duration:0.00}");
                    }
                }

                foreach (var transition in sm.anyStateTransitions)
                {
                    string conditions = string.Join(" && ", transition.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold:0.##}"));
                    log.AppendLine($"   AnyState → {transition.destinationState.name}  [{conditions}]");
                }
            }

            if (prefab != null)
            {
                var view = prefab.GetComponent<Combat.TypingTargetAnimator>();
                var target = prefab.GetComponent<Combat.TypingTarget>();
                log.AppendLine($"预制件 {PrefabPath} TypingTarget={target != null} View={view != null}");

                var model = prefab.transform.Find("ZombieModel");
                if (model != null)
                {
                    var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
                    float feet = smr != null ? smr.bounds.min.y : 0f;
                    log.AppendLine($"   模型 scale={model.localScale.x:0.00}（体高约 {(smr != null ? smr.bounds.size.y : 0f):0.00} m）本地y={model.localPosition.y:0.000} 脚底世界y={feet:0.000}");
                }

                if (target != null)
                {
                    var targetSo = new SerializedObject(target);
                    log.AppendLine($"   近身参数 attackRange={targetSo.FindProperty("attackRange")?.floatValue:0.00} " +
                                   $"laneConvergeStart={targetSo.FindProperty("laneConvergeStart")?.floatValue:0.00} " +
                                   $"closeLaneOffset={targetSo.FindProperty("closeLaneOffset")?.floatValue:0.00} " +
                                   $"deathLinger={targetSo.FindProperty("deathLinger")?.floatValue:0.00}");
                }

                // 动画能不能真的驱动网格，全看 Animator 上的 avatar（人形片段必须靠它重定向）
                foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
                {
                    var animatorAvatar = animator.avatar;
                    log.AppendLine($"   Animator @{(animator.transform == prefab.transform ? "预制件根" : animator.transform.name)} " +
                                   $"controller={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<无>")} " +
                                   $"avatar={(animatorAvatar != null ? $"{animatorAvatar.name} valid={animatorAvatar.isValid} human={animatorAvatar.isHuman}" : "<没有 avatar → 人形片段不会生效>")} rootMotion={animator.applyRootMotion}");
                }
            }

            log.Append(DescribeSceneWiring());
            Debug.Log(log.ToString().TrimEnd());
        }

        /// <summary>当前打开的场景里 ScriptedEncounter 的丧尸接线状态（不在 POLE POSITION 场景就说不适用）。</summary>
        private static string DescribeSceneWiring()
        {
            var encounter = Object.FindFirstObjectByType<Demo.ScriptedEncounter>();
            if (encounter == null) return "场景接线：当前场景没有 ScriptedEncounter（跳过）";

            var so = new SerializedObject(encounter);
            var property = so.FindProperty("zombiePrefab");
            var wired = property != null ? property.objectReferenceValue : null;

            var line = new System.Text.StringBuilder();
            line.AppendLine($"场景接线：ScriptedEncounter.zombiePrefab = {(wired != null ? AssetDatabase.GetAssetPath(wired) : "<空 → 会退回灰盒方块>")}");

            foreach (string name in new[] { "SpawnLeft", "SpawnRight", "SpawnBack", "EntranceSpawn", "PoleHugAnchor" })
            {
                var found = FindInScene(name);
                if (found != null) line.AppendLine($"   {name}.y = {found.transform.position.y:0.00}");
            }

            return line.ToString().TrimEnd();
        }

        /// <summary>用「脚踝→脚趾」的方向判断模型正面朝哪个轴（做朝向接线时的依据）。</summary>
        private static string DescribeFacing(GameObject instance)
        {
            var bones = instance.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.Contains("Foot") || t.name.Contains("Toe"))
                .ToList();

            var foot = bones.FirstOrDefault(t => t.name.EndsWith("LeftFoot"));
            var toe = bones.FirstOrDefault(t => t.name.EndsWith("LeftToeBase"));
            if (foot == null || toe == null) return "<找不到脚踝/脚趾骨骼>";

            Vector3 forward = toe.position - foot.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return "<脚踝脚趾重合>";

            forward.Normalize();
            return $"脚踝→脚趾 = ({forward.x:0.00}, {forward.z:0.00})，即 {(Mathf.Abs(forward.z) > Mathf.Abs(forward.x) ? (forward.z > 0 ? "正面朝 +Z" : "正面朝 -Z") : (forward.x > 0 ? "正面朝 +X" : "正面朝 -X"))}";
        }

        // ---------------------------------------------------------------- 小工具

        private static void SetProperty(SerializedObject so, string field, System.Action<SerializedProperty> apply)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[ZombieAssetBuilder] 字段不存在：{so.targetObject.GetType().Name}.{field}");
                return;
            }

            apply(property);
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
