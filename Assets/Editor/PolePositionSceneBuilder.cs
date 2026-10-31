using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YourMovieSucks.Combat;
using YourMovieSucks.Demo;
using YourMovieSucks.Input;
using YourMovieSucks.LabB;
using YourMovieSucks.Level;
using YourMovieSucks.Typing;
using YourMovieSucks.UI;

namespace YourMovieSucks.EditorTools
{
    /// <summary>
    /// 关卡「POLE POSITION」场景生成器：一键重建 `Assets/Scenes/PolePosition.unity`。
    /// 用生成器而不是手摆 Hierarchy：场景内容可复现、可 diff、引用接线不漏。
    ///
    /// 用法：
    ///   · 菜单 `YourMovieSucks/Build POLE POSITION Scene`
    ///   · 批处理 `-batchmode -quit -executeMethod YourMovieSucks.EditorTools.PolePositionSceneBuilder.Build`
    /// 校验（检查所有序列化引用是否有空）：`YourMovieSucks/Validate POLE POSITION Scene`。
    /// </summary>
    public static class PolePositionSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/PolePosition.unity";
        private const string ArtFolder = "Assets/Art/PolePosition";

        // 钢管是全场视觉锚点
        private static readonly Vector3 PoleCenter = new Vector3(0f, 2.7f, 10f);

        /// <summary>敌人锚点高度 = 舞台台面：敌人预制件（丧尸）的原点在脚底；灰盒方块的原点在身体中心，旧值 0.9。</summary>
        private const float EnemyFeetY = 0.5f;

        [MenuItem("YourMovieSucks/Build POLE POSITION Scene")]
        public static void BuildFromMenu()
        {
            if (!Build()) return;
            EditorUtility.DisplayDialog("POLE POSITION", $"场景已生成：\n{ScenePath}", "OK");
        }

        [MenuItem("YourMovieSucks/Validate POLE POSITION Scene")]
        public static void ValidateFromMenu()
        {
            Validate();

            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("POLE POSITION", "校验结果见 Console（[PolePositionSceneBuilder] 开头）", "OK");
        }

        public static bool Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[PolePositionSceneBuilder] build cancelled by user");
                return false;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            var camera = BuildCameraRig(out var rail, out var dollyTrack);
            BuildVenue(out var poleHugAnchor, out var entranceSpawn, out var spawnPoints, out var stageLight);
            var systems = BuildSystems(stageLight);
            BuildHud();

            WireCamera(camera, rail, dollyTrack);
            WireSystems(systems, camera);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AddToBuildSettings();

            Debug.Log($"[PolePositionSceneBuilder] scene written: {ScenePath}");
            return true;
        }

        // ---------------------------------------------------------------- 光照与环境

        private static void BuildEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.05f, 0.075f);
            RenderSettings.skybox = null;   // 室内场馆：不要天空盒，靠霓虹灯照明
            RenderSettings.fog = false;

            var root = new GameObject("Lighting").transform;

            var key = new GameObject("KeyLight");
            key.transform.SetParent(root, false);
            var keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 0.12f;
            keyLight.color = new Color(0.6f, 0.62f, 0.85f);
            key.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            CreatePointLight(root, "NeonPink_Stage", new Vector3(0f, 4.6f, 8.5f),
                new Color(1f, 0.25f, 0.75f), 9f, 12f);
            CreatePointLight(root, "NeonCyan_Bar", new Vector3(-8f, 4.2f, 4.5f),
                new Color(0.25f, 0.9f, 1f), 5f, 11f);
            CreatePointLight(root, "NeonPink_Booth", new Vector3(8f, 4.2f, 4.5f),
                new Color(1f, 0.3f, 0.55f), 5f, 11f);

            // 打在钢管上的聚光灯：镜头对准钢管时必须看得见
            var spotGo = new GameObject("PoleSpot");
            spotGo.transform.SetParent(root, false);
            spotGo.transform.position = new Vector3(0f, 5.4f, 10f);
            spotGo.transform.rotation = Quaternion.LookRotation(PoleCenter - spotGo.transform.position);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.intensity = 8f;
            spot.range = 16f;
            spot.spotAngle = 55f;
            spot.color = new Color(1f, 0.82f, 0.86f);
        }

        private static GameObject CreatePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None; // 灰盒阶段省性能
            return go;
        }

        // ---------------------------------------------------------------- 相机与机位

        private static Camera BuildCameraRig(out RailCameraController rail, out DollyTrack dollyTrack)
        {
            var root = new GameObject("CameraRig").transform;

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraShake));
            cameraGo.transform.SetParent(root, false);
            cameraGo.tag = "MainCamera";

            var camera = cameraGo.GetComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.008f, 0.022f, 1f);

            var trackGo = new GameObject("DollyTrack", typeof(DollyTrack));
            trackGo.transform.SetParent(root, false);
            dollyTrack = trackGo.GetComponent<DollyTrack>();

            var waypointRoot = new GameObject("Waypoints").transform;
            waypointRoot.SetParent(root, false);

            // WP0 入场 → WP1 ZOOM 快推特写 → WP2 战斗慢推 → WP3 收尾看向钢管
            var w0 = CreateWaypoint(waypointRoot, "WP0_Entry", new Vector3(0f, 1.7f, -1.5f), PoleCenter);
            var w1 = CreateWaypoint(waypointRoot, "WP1_ZoomCloseUp", new Vector3(0f, 2.3f, 4.0f), PoleCenter);
            var w2 = CreateWaypoint(waypointRoot, "WP2_Combat", new Vector3(0f, 2.1f, 5.2f), PoleCenter);
            var w3 = CreateWaypoint(waypointRoot, "WP3_Ending", new Vector3(0f, 2.8f, 4.2f), new Vector3(0f, 1.6f, 10f));

            dollyTrack.AddWaypoint(w0);
            dollyTrack.AddWaypoint(w1);
            dollyTrack.AddWaypoint(w2);
            dollyTrack.AddWaypoint(w3);

            rail = trackGo.AddComponent<RailCameraController>();
            return camera;
        }

        private static Transform CreateWaypoint(Transform parent, string name, Vector3 position, Vector3 lookAt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(lookAt - position);
            return go.transform;
        }

        // ---------------------------------------------------------------- 场地（灰盒）

        private static Transform BuildVenue(
            out Transform poleHugAnchor,
            out Transform entranceSpawn,
            out Transform[] spawnPoints,
            out StageLighting stageLighting)
        {
            var root = new GameObject("Venue").transform;

            // 地板发亮的真正原因：光滑度高的面在 URP 下会把聚光灯/点光的镜面反射铺满整片地面（与 albedo 无关），
            // 所以灰盒阶段先用哑光地板；“棋盘格反光地砖”等美术阶段加反射探针后再提高光滑度。
            var floorMat = CreateMaterial("M_Floor", new Color(0.06f, 0.05f, 0.07f), 0f, 0.2f);
            var wallMat = CreateMaterial("M_Wall", new Color(0.1f, 0.045f, 0.06f), 0.05f, 0.25f);
            var stageMat = CreateMaterial("M_Stage", new Color(0.09f, 0.055f, 0.07f), 0f, 0.35f);
            var brassMat = CreateMaterial("M_BrassPole", new Color(0.85f, 0.68f, 0.28f), 1f, 0.9f);
            var velvetMat = CreateMaterial("M_Velvet", new Color(0.45f, 0.05f, 0.12f), 0f, 0.25f);
            var neonPink = CreateMaterial("M_NeonPink", new Color(1f, 0.3f, 0.8f), 0f, 0.4f, new Color(3.2f, 0.5f, 2.2f));
            var neonCyan = CreateMaterial("M_NeonCyan", new Color(0.3f, 0.95f, 1f), 0f, 0.4f, new Color(0.4f, 2.6f, 3.2f));

            // 地板 / 墙 / 天花
            CreatePrimitive("Floor", root, PrimitiveType.Cube, new Vector3(0f, -0.1f, 10f), new Vector3(22f, 0.2f, 30f), floorMat);
            CreatePrimitive("WallBack", root, PrimitiveType.Cube, new Vector3(0f, 3f, 25.2f), new Vector3(22f, 6f, 0.4f), wallMat);
            CreatePrimitive("WallLeft", root, PrimitiveType.Cube, new Vector3(-11f, 3f, 10f), new Vector3(0.4f, 6f, 30f), wallMat);
            CreatePrimitive("WallRight", root, PrimitiveType.Cube, new Vector3(11f, 3f, 10f), new Vector3(0.4f, 6f, 30f), wallMat);
            CreatePrimitive("Ceiling", root, PrimitiveType.Cube, new Vector3(0f, 6.2f, 10f), new Vector3(22f, 0.3f, 30f), wallMat);

            // 舞台与钢管
            CreatePrimitive("Stage", root, PrimitiveType.Cube, new Vector3(0f, 0.25f, 10f), new Vector3(9f, 0.5f, 4.5f), stageMat);
            var pole = CreatePrimitive("BrassPole", root, PrimitiveType.Cylinder, PoleCenter, new Vector3(0.28f, 2.2f, 0.28f), brassMat);
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());

            // 卡座与吧台（丝绒红）
            CreatePrimitive("BoothLeft", root, PrimitiveType.Cube, new Vector3(-7.5f, 0.6f, 3.5f), new Vector3(3.4f, 1.2f, 1.2f), velvetMat);
            CreatePrimitive("BoothRight", root, PrimitiveType.Cube, new Vector3(7.5f, 0.6f, 3.5f), new Vector3(3.4f, 1.2f, 1.2f), velvetMat);
            CreatePrimitive("BarCounter", root, PrimitiveType.Cube, new Vector3(-8.5f, 0.6f, 13f), new Vector3(1.4f, 1.2f, 7f), stageMat);

            // 霓虹灯管（自发光，给“廉价脱衣舞俱乐部”的底色）
            CreatePrimitive("NeonTubeLeft", root, PrimitiveType.Cube, new Vector3(-10.7f, 4.5f, 10f), new Vector3(0.22f, 0.22f, 20f), neonPink);
            CreatePrimitive("NeonTubeRight", root, PrimitiveType.Cube, new Vector3(10.7f, 4.5f, 10f), new Vector3(0.22f, 0.22f, 20f), neonCyan);
            CreatePrimitive("NeonTubeBackA", root, PrimitiveType.Cube, new Vector3(-3.2f, 3.6f, 24.8f), new Vector3(0.3f, 2.6f, 0.2f), neonPink);
            CreatePrimitive("NeonTubeBackB", root, PrimitiveType.Cube, new Vector3(3.2f, 3.6f, 24.8f), new Vector3(0.3f, 2.6f, 0.2f), neonCyan);
            // NeonTubeStage（舞台上方 0,5.6,12.2 那根粉色灯带）已由作者删除：它正好挡在开场中央
            // “TYPE: ZOOM”提示后面。**不要再加回来**（重建场景时要保持和现有场景一致）。

            // 舞台灯光（导演轨 NEON 词条的作用对象；Demo 关卡默认点亮）
            var stageLightGo = new GameObject("StageLight");
            stageLightGo.transform.SetParent(root, false);
            stageLightGo.transform.position = new Vector3(0f, 4.4f, 11.5f);
            var stageLight = stageLightGo.AddComponent<Light>();
            stageLight.type = LightType.Point;
            stageLight.color = new Color(1f, 0.45f, 0.85f);
            stageLight.intensity = 7f;
            stageLight.range = 14f;
            stageLight.shadows = LightShadows.None;
            stageLighting = stageLightGo.AddComponent<StageLighting>();

            // 抱杆锚点 / 侧幕入场点 / 刷怪点（高度见 EnemyFeetY：站在舞台台面上，不浮空也不陷进去）
            poleHugAnchor = CreateAnchor(root, "PoleHugAnchor", new Vector3(0.42f, EnemyFeetY, 10.15f));
            entranceSpawn = CreateAnchor(root, "EntranceSpawn", new Vector3(5.4f, EnemyFeetY, 11.4f));
            spawnPoints = new[]
            {
                CreateAnchor(root, "SpawnLeft", new Vector3(-4.2f, EnemyFeetY, 12f)),
                CreateAnchor(root, "SpawnRight", new Vector3(4.2f, EnemyFeetY, 12f)),
                CreateAnchor(root, "SpawnBack", new Vector3(0f, EnemyFeetY, 13.6f))
            };

            return root;
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        // ---------------------------------------------------------------- 系统与演出组件

        private struct SystemRefs
        {
            public TypingController Typing;
            public TrackInput TrackInput;
            public RunStats Stats;
            public LabBTuning Tuning;
            public LabBTelemetry Telemetry;
            public MonsterAttackCoordinator Coordinator;
            public PlayerHealth PlayerHealth;
            public DirectorStage DirectorStage;
            public ScreenBlood Blood;
            public AgentShooter Shooter;
            public GameOverController GameOver;
            public ScriptedEncounter Encounter;
            public DemoDialogue Dialogue;
            public PolePositionFlow Flow;
            public PolePositionAutoDrive AutoDrive;
            public DemoAudioDirector AudioDirector;
            public TypingSfxPlayer TypingSfx;
            public StageLighting StageLighting;
        }

        private static SystemRefs BuildSystems(StageLighting stageLighting)
        {
            var go = new GameObject("Systems");

            var refs = new SystemRefs
            {
                Typing = go.AddComponent<TypingController>(),
                TrackInput = go.AddComponent<TrackInput>(),
                Stats = go.AddComponent<RunStats>(),
                Tuning = go.AddComponent<LabBTuning>(),
                Telemetry = go.AddComponent<LabBTelemetry>(),
                Coordinator = go.AddComponent<MonsterAttackCoordinator>(),
                PlayerHealth = go.AddComponent<PlayerHealth>(),
                DirectorStage = go.AddComponent<DirectorStage>(),
                Blood = go.AddComponent<ScreenBlood>(),
                Shooter = go.AddComponent<AgentShooter>(),
                GameOver = go.AddComponent<GameOverController>(),
                Encounter = go.AddComponent<ScriptedEncounter>(),
                Dialogue = go.AddComponent<DemoDialogue>(),
                Flow = go.AddComponent<PolePositionFlow>(),
                AutoDrive = go.AddComponent<PolePositionAutoDrive>(),
                AudioDirector = go.AddComponent<DemoAudioDirector>(),
                TypingSfx = go.AddComponent<TypingSfxPlayer>(),
                StageLighting = stageLighting
            };

            return refs;
        }

        private static void BuildHud()
        {
            var go = new GameObject("Hud");
            go.AddComponent<DemoHud>();
            go.AddComponent<FeedbackUI>();
            go.AddComponent<WordLabelManager>();
            go.AddComponent<ViewfinderOverlay>();
        }

        // ---------------------------------------------------------------- 引用接线

        private static void WireCamera(Camera camera, RailCameraController rail, DollyTrack dollyTrack)
        {
            var shake = camera.GetComponent<CameraShake>();
            var shakeSo = new SerializedObject(shake);
            Set(shakeSo, "targetCamera", p => p.objectReferenceValue = camera);
            shakeSo.ApplyModifiedPropertiesWithoutUndo();

            // 导演轨弱点识别**不做镜头动作**（作者 2026-10-20：战斗中不干扰玩家），所以相机上不再挂特写组件
            var railSo = new SerializedObject(rail);
            Set(railSo, "targetCamera", p => p.objectReferenceValue = camera);
            Set(railSo, "track", p => p.objectReferenceValue = dollyTrack);
            Set(railSo, "moveDuration", p => p.floatValue = 2f);
            Set(railSo, "dwellDuration", p => p.floatValue = 0f);
            Set(railSo, "autoAdvance", p => p.boolValue = false);
            railSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireSystems(SystemRefs refs, Camera camera)
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<DemoHud>();

            // TypingController：Demo 关卡不要开局测试词、不要调试面板
            var typingSo = new SerializedObject(refs.Typing);
            Set(typingSo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
            Set(typingSo, "spawnTestWordsOnStart", p => p.boolValue = false);
            Set(typingSo, "showDebugOverlay", p => p.boolValue = false);
            typingSo.ApplyModifiedPropertiesWithoutUndo();

            // TrackInput：只用左右 Ctrl 切轨、不画调试信息
            var trackSo = new SerializedObject(refs.TrackInput);
            Set(trackSo, "toggleMode", p => p.boolValue = true);
            Set(trackSo, "extendedSwitchKeys", p => p.boolValue = false);
            Set(trackSo, "showDebugOverlay", p => p.boolValue = false);
            trackSo.ApplyModifiedPropertiesWithoutUndo();

            // RunStats
            var statsSo = new SerializedObject(refs.Stats);
            Set(statsSo, "typing", p => p.objectReferenceValue = refs.Typing);
            statsSo.ApplyModifiedPropertiesWithoutUndo();

            // LabBTuning：面板默认收起（F1 打开）
            var tuningSo = new SerializedObject(refs.Tuning);
            Set(tuningSo, "Enabled", p => p.boolValue = true);
            Set(tuningSo, "PanelVisible", p => p.boolValue = false);
            tuningSo.ApplyModifiedPropertiesWithoutUndo();

            // LabBTelemetry
            var telemetrySo = new SerializedObject(refs.Telemetry);
            Set(telemetrySo, "typing", p => p.objectReferenceValue = refs.Typing);
            Set(telemetrySo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
            Set(telemetrySo, "stats", p => p.objectReferenceValue = refs.Stats);
            Set(telemetrySo, "playerHealth", p => p.objectReferenceValue = refs.PlayerHealth);
            Set(telemetrySo, "directorStage", p => p.objectReferenceValue = refs.DirectorStage);
            telemetrySo.ApplyModifiedPropertiesWithoutUndo();

            // PlayerHealth
            var healthSo = new SerializedObject(refs.PlayerHealth);
            Set(healthSo, "maxHp", p => p.floatValue = 100f);
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            // DirectorStage：Demo 开场禁用导演词条（ZOOM 取“过场推镜”语义）
            var directorSo = new SerializedObject(refs.DirectorStage);
            Set(directorSo, "typing", p => p.objectReferenceValue = refs.Typing);
            Set(directorSo, "lighting", p => p.objectReferenceValue = refs.StageLighting);
            Set(directorSo, "blood", p => p.objectReferenceValue = refs.Blood);
            Set(directorSo, "targetCamera", p => p.objectReferenceValue = camera);
            directorSo.ApplyModifiedPropertiesWithoutUndo();

            // ScreenBlood：Demo 脚本没有擦镜节拍，所以把糊屏调轻（低 alpha + 快渐隐），
            // 否则每次击杀都会红屏遮住全部信息 1.2 秒。（只改本关卡实例，不动脚本默认值）
            var bloodSo = new SerializedObject(refs.Blood);
            Set(bloodSo, "splatterAlpha", p => p.floatValue = 0.32f);
            Set(bloodSo, "fadeDuration", p => p.floatValue = 0.75f);
            bloodSo.ApplyModifiedPropertiesWithoutUndo();

            // StageLighting：默认点亮
            var lightSo = new SerializedObject(refs.StageLighting);
            Set(lightSo, "litAtStart", p => p.boolValue = true);
            lightSo.ApplyModifiedPropertiesWithoutUndo();

            // AgentShooter
            var shooterSo = new SerializedObject(refs.Shooter);
            Set(shooterSo, "typing", p => p.objectReferenceValue = refs.Typing);
            shooterSo.ApplyModifiedPropertiesWithoutUndo();

            // GameOverController
            var overSo = new SerializedObject(refs.GameOver);
            Set(overSo, "playerHealth", p => p.objectReferenceValue = refs.PlayerHealth);
            overSo.ApplyModifiedPropertiesWithoutUndo();

            // ScriptedEncounter：波次词序与主战斗词池用代码默认值，这里只接引用与锚点
            var encounterSo = new SerializedObject(refs.Encounter);
            Set(encounterSo, "typing", p => p.objectReferenceValue = refs.Typing);
            Set(encounterSo, "blood", p => p.objectReferenceValue = refs.Blood);
            Set(encounterSo, "poleAnchor", p => p.objectReferenceValue = FindInScene("PoleHugAnchor"));
            Set(encounterSo, "entranceSpawn", p => p.objectReferenceValue = FindInScene("EntranceSpawn"));
            SetObjectArray(encounterSo, "spawnPoints", new[]
            {
                (UnityEngine.Object)FindInScene("SpawnLeft"),
                FindInScene("SpawnRight"),
                FindInScene("SpawnBack")
            });

            // 怪物外观：丧尸预制件（YourMovieSucks/Monsters/0 · 一键生成）
            var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieAssetBuilder.PrefabPath);
            Set(encounterSo, "zombiePrefab", p => p.objectReferenceValue = zombiePrefab);
            if (zombiePrefab == null)
                Debug.LogWarning("[PolePositionSceneBuilder] 丧尸预制件未找到（跑一次 YourMovieSucks/Monsters/0）—— 刷怪会退回灰盒方块");

            encounterSo.ApplyModifiedPropertiesWithoutUndo();

            // DemoDialogue
            var dialogueSo = new SerializedObject(refs.Dialogue);
            Set(dialogueSo, "hud", p => p.objectReferenceValue = hud);
            Set(dialogueSo, "audio", p => p.objectReferenceValue = refs.AudioDirector);
            dialogueSo.ApplyModifiedPropertiesWithoutUndo();

            // DemoAudioDirector：接上 VoLibrary（若已生成）
            var audioSo = new SerializedObject(refs.AudioDirector);
            var voLibrary = AssetDatabase.LoadAssetAtPath<VoLibrary>("Assets/Audio/VO/VoLibrary_PolePosition.asset");
            var lineSettings = AssetDatabase.LoadAssetAtPath<VoLineSettings>("Assets/Audio/VO/VoLineSettings_PolePosition.asset");
            Set(audioSo, "voLibrary", p => p.objectReferenceValue = voLibrary);
            Set(audioSo, "lineSettings", p => p.objectReferenceValue = lineSettings);
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            // 打字音效（候选池由 YourMovieSucks/Voice/0 扫描填入）
            var typingSfxSo = new SerializedObject(refs.TypingSfx);
            Set(typingSfxSo, "audio", p => p.objectReferenceValue = refs.AudioDirector);
            Set(typingSfxSo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
            typingSfxSo.ApplyModifiedPropertiesWithoutUndo();
            if (voLibrary == null)
                Debug.LogWarning("[PolePositionSceneBuilder] VoLibrary 未找到（跑一次 YourMovieSucks/Voice/3 · 建立 VoLibrary 并接线）");

            // DemoHud
            if (hud != null)
            {
                var hudSo = new SerializedObject(hud);
                Set(hudSo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
                hudSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ViewfinderOverlay
            var viewfinder = UnityEngine.Object.FindFirstObjectByType<ViewfinderOverlay>();
            if (viewfinder != null)
            {
                var vfSo = new SerializedObject(viewfinder);
                Set(vfSo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
                vfSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // FeedbackUI / WordLabelManager
            var feedback = UnityEngine.Object.FindFirstObjectByType<FeedbackUI>();
            if (feedback != null)
            {
                var so = new SerializedObject(feedback);
                Set(so, "typing", p => p.objectReferenceValue = refs.Typing);
                Set(so, "playerHealth", p => p.objectReferenceValue = refs.PlayerHealth);
                Set(so, "cameraShake", p => p.objectReferenceValue = camera != null ? camera.GetComponent<CameraShake>() : null);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 导演轨弱点识别编排：挂在 Systems 上（与 TypingController / TrackInput 同物件）
            var systemsGo = refs.Encounter != null ? refs.Encounter.gameObject : null;
            if (systemsGo != null)
            {
                var weaknessView = systemsGo.GetComponent<YourMovieSucks.Combat.DirectorWeaknessView>();
                if (weaknessView == null) weaknessView = systemsGo.AddComponent<YourMovieSucks.Combat.DirectorWeaknessView>();
                var weaknessSo = new SerializedObject(weaknessView);
                Set(weaknessSo, "typing", p => p.objectReferenceValue = refs.Typing);
                Set(weaknessSo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
                weaknessSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var labelManager = UnityEngine.Object.FindFirstObjectByType<WordLabelManager>();
            if (labelManager != null)
            {
                var overlayMaterial = CreateOverlayMaterial();
                var so = new SerializedObject(labelManager);
                Set(so, "typing", p => p.objectReferenceValue = refs.Typing);
                Set(so, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
                Set(so, "alwaysOnTopMaterial", p => p.objectReferenceValue = overlayMaterial);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // PolePositionFlow：演出总控
            var flowSo = new SerializedObject(refs.Flow);            Set(flowSo, "typing", p => p.objectReferenceValue = refs.Typing);
            Set(flowSo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
            Set(flowSo, "rail", p => p.objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RailCameraController>());
            Set(flowSo, "encounter", p => p.objectReferenceValue = refs.Encounter);
            Set(flowSo, "hud", p => p.objectReferenceValue = hud);
            Set(flowSo, "dialogue", p => p.objectReferenceValue = refs.Dialogue);
            Set(flowSo, "stats", p => p.objectReferenceValue = refs.Stats);
            Set(flowSo, "directorStage", p => p.objectReferenceValue = refs.DirectorStage);
            Set(flowSo, "playerHealth", p => p.objectReferenceValue = refs.PlayerHealth);
            flowSo.ApplyModifiedPropertiesWithoutUndo();

            // PolePositionAutoDrive：关卡自检驱动器（F2 或外部调用触发）
            var driveSo = new SerializedObject(refs.AutoDrive);
            Set(driveSo, "typing", p => p.objectReferenceValue = refs.Typing);
            Set(driveSo, "trackInput", p => p.objectReferenceValue = refs.TrackInput);
            Set(driveSo, "rail", p => p.objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RailCameraController>());
            Set(driveSo, "encounter", p => p.objectReferenceValue = refs.Encounter);
            Set(driveSo, "hud", p => p.objectReferenceValue = hud);
            Set(driveSo, "stats", p => p.objectReferenceValue = refs.Stats);
            Set(driveSo, "playerHealth", p => p.objectReferenceValue = refs.PlayerHealth);
            Set(driveSo, "autoStartOnPlay", p => p.boolValue = false);
            driveSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UnityEngine.Object FindInScene(string name)
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindRecursive(go.transform, name);
                if (found != null) return found.gameObject;
            }

            Debug.LogWarning($"[PolePositionSceneBuilder] object not found: {name}");
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

        // ---------------------------------------------------------------- 资产与工具

        private static GameObject CreatePrimitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;

            if (material != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }

            return go;
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(ArtFolder);

            string path = $"{ArtFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            // 幂等：已存在的材质也会被更新到当前参数，方便改数值后重建场景
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 词条专用的“永远在最上层”材质（Assets/Art/Shaders/M_WordLabelOnTop.mat，ZTest Always + Overlay 队列），
        /// 让怪物头顶词条不被钢管等几何与血浆层遮住。与其它材质一样幂等更新。
        /// </summary>
        private static Material CreateOverlayMaterial()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Shaders");

            const string path = "Assets/Art/Shaders/M_WordLabelOnTop.mat";
            var shader = Shader.Find("YourMovieSucks/UIAlwaysOnTop");
            if (shader == null)
            {
                Debug.LogWarning("[PolePositionSceneBuilder] shader 'YourMovieSucks/UIAlwaysOnTop' not found — 词条会被几何遮住");
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "M_WordLabelOnTop" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.renderQueue = 4000; // Overlay：在场景几何（2000）与血浆层（3000）之后
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                string leaf = System.IO.Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                    AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- 序列化写入

        private static void Set(SerializedObject so, string field, Action<SerializedProperty> apply)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[PolePositionSceneBuilder] field '{field}' not found on {so.targetObject.GetType().Name}");
                return;
            }

            apply(property);
        }

        private static void SetObjectArray(SerializedObject so, string field, UnityEngine.Object[] values)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[PolePositionSceneBuilder] array field '{field}' not found on {so.targetObject.GetType().Name}");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---------------------------------------------------------------- 校验

        /// <summary>打开生成的场景并枚举所有序列化引用，报告空引用（接线漏检）。</summary>
        public static bool Validate()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[PolePositionSceneBuilder] scene missing: {ScenePath}");
                return false;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var issues = new List<string>();
            int components = 0;
            int references = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        issues.Add($"{root.name}: missing script");
                        continue;
                    }

                    components++;
                    var so = new SerializedObject(behaviour);
                    var iterator = so.GetIterator();

                    while (iterator.NextVisible(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (iterator.name == "m_Script") continue;

                        references++;
                        if (iterator.objectReferenceValue == null)
                            issues.Add($"{PathOf(behaviour.transform)} → {behaviour.GetType().Name}.{iterator.name} = null");
                    }
                }
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[PolePositionSceneBuilder] validation OK — {components} components / {references} references, 0 null");
                return true;
            }

            foreach (var issue in issues)
                Debug.LogWarning($"[PolePositionSceneBuilder] {issue}");

            Debug.LogWarning($"[PolePositionSceneBuilder] validation found {issues.Count} issue(s) out of {references} references");
            return false;
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
