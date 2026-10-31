# 验证报告：PS1-inspired Retro Rendering

本次结果不是“生成代码即验收”：完成了 Unity 编译、真实 Play Mode 截图对比、Frame Debugger、Profiler 采样，以及原项目整关自检。没有执行独立 Player 构建。

状态含义：**PASS**＝已测试通过；**FAIL**＝已测试失败；**NOT TESTED**＝本轮未能验证。不能把下方未测项视为通过。

## 1. 实施前检查

| 检查项 | 实际观察 |
|---|---|
| Unity | 6000.3.8f1，revision 1c7db571dde0 |
| 管线 / 包 | 当前为 URP，`com.unity.render-pipelines.universal` 17.3.0；不是依据任务描述假定。 |
| 活动配置 | PC Quality；Graphics 默认和 PC Quality 使用 `Assets/Settings/PC_RPAsset.asset`。另有 Mobile_RPAsset / Mobile_Renderer，非当前目标。 |
| Renderer | `Assets/Settings/PC_Renderer.asset`，Forward+；RenderGraph 启用（Compatibility Mode=false）；SSAO active，强度0.4、半径0.3。 |
| 渲染参数 | RenderScale=1，MSAA=1，HDR=true，深度/不透明纹理启用，SRP Batcher=true，4级阴影级联、50m 阴影距离。Linear 色彩空间，D3D12。 |
| 游戏相机 | `Assets/Scenes/PolePosition.unity/CameraRig/Main Camera`，单全视口透视相机，FOV60，near0.05/far200，原本无 RT、无 camera stack；自研 RailCameraController 控制轨道，未安装 Cinemachine。 |
| 后处理 | 未发现活动场景 Volume；相机没有显式启用后处理的 AdditionalCameraData。管线默认 SampleSceneProfile 配置了 Neutral Tonemapping、Bloom0.25、Vignette0.2，MotionBlur inactive。SSAO 是独立 Renderer Feature。没有修改这些资产或灯光。 |
| 环境材质 | M_Floor、M_Wall、M_Stage、M_Velvet、M_BrassPole、M_NeonPink、M_NeonCyan 均 URP Lit；无底色贴图，已有不同基色及霓虹发光配置。 |
| 敌人 | StripperZombie.prefab → Zombie.fbx 的 SkinnedMeshRenderer / Humanoid Animator。M_Zombie 为不透明 URP Lit，512×512 BaseColor PNG，Bilinear、压缩、sRGB，UV scale1/offset0。无正常使用的法线/发光贴图和 alpha clipping；Avatar valid/human，RootMotion=false。 |
| UI / 透明 | WordLabel 动态生成 World Space UGUI Text（SpecialElite），富文本进度色、Shadow、字母箭头、UIAlwaysOnTop Shader；血浆为世界空间透明 RawImage；DemoHud/Viewfinder/GameOver 为 ScreenSpaceOverlay，另有 IMGUI 血条/反馈/调参 UI。弱点标记为动态 URP Unlit 小球，不是文字网格。未发现需要转换的项目粒子材质。 |
| 显示 | 本次 Game View 实测1970×943；显示器2560×1600约240Hz；Player 默认1024×768、FullScreenWindow；vSync=0。未改 Player 分辨率设置。 |
| 初始工作区 | RetroRendering 目录已有未提交原型；Locus 插件也有大量既有修改。先备份原型到 `Library/Locus/tmp/retro-before/`，沿用而非清空。插件修改不触碰。 |

## 2. 功能验证表
| 功能 / 用例 | 状态 | 证据与边界 |
|---|---|---|
| C# 编译与域重载 | PASS | Unity 完成正式编译/域重载，新增/修改源码无语义警告。 |
| 三个 Shader 编译 | PASS | Environment / Character / ScreenComposite `isSupported=true`，ShaderUtil 无错误；实际绘制后 Console 无 Shader/C# error。 |
| A：真低分辨率 | PASS | 相机实际 RT=320×240，单采样，无 mipmap；Native 与输出同为1970×943；426×240、640×480逐一检查。真实帧斜边明显像素化。 |
| A：比例 / Point / Bilinear | PASS | 320×240在当前窗口按3倍输出960×720并居中黑边；X/Y放大比例相同且整数。426×240输出1278×720。双线性与Point真实帧有明显差异，没有拉伸。 |
| A：极小窗口缩小 | NOT TESTED | 代码使用整数倒数缩小；小于所选内部尺寸的窗口未视觉验收。 |
| B：RGB量化 | PASS | 4级测试截图世界区域各通道只落在0/85/170/255格点（绿色、蓝色该区域未用到255）；原生UI未量化。默认32级截图保留角色轮廓。 |
| B：有序抖色 | PASS | 4级、强度0与1的真实截图产生像素差；默认强度0。 |
| C：顶点吸附 | PASS | 0↔1产生真实几何画面差异；相机/动画冻结后连续两张吸附截图逐像素一致，无随机逐帧抖动；sharedMesh引用不变。 |
| C：近裁面极端运动 | NOT TESTED | 实现有按相机near淡出；贴面、高速运动、极端薄三角形未做压力测试。 |
| D：仿射纹理 | PASS | 透视棋盘墙0↔1产生真实画面差异，纹理仍可识别；屏幕标题像素完全不变。 |
| E：无贴图环境 | PASS | 无纹理Floor/Crimson Wall/Concrete显示各自基色、明暗和阴影，无粉色或消失；Forward+点光在地面产生可见青色光照。 |
| E2：gameplay 场景接入（新增） | PASS | `Tools > Retro Rendering > Apply to PolePosition scene` 已执行并保存：14 个 Venue Renderer 在运行时使用 `RetroRendering/Environment`，`Retro Rendering` 对象携带控制器与角色自动绑定。截图见 `Documentation/Screenshots/GameplayWired.png`。 |
| E3：环境亮度对照（新增） | PASS | 同机位冻结对比：修正漫反射 1/π 归一化并加入包裹/窄高光后，场景结构、舞台、卡座、霓虹渐变均可见；整体亮度低于原 Lit 但可读。不声称与原 Lit 逐像素等亮。 |
| F2：运行时角色材质绑定（新增） | PASS | 战斗中 `StripperZombie/Cube` 使用 `RetroRendering/Character`，`renderer.sharedMaterial == renderer.material`（TypingTarget 缓存引用未被替换），威胁染色仍写入同一实例；贴图与动画保留。 |
| E：分面/顶点色/灯光分段/发光调参 | NOT TESTED | 分面默认参与已验证画面，但未逐项隔离比较；顶点色网格和发光贴图样本未验收。 |
| F：现有贴图敌人 | PASS | 原材质/可选变体并排显示；贴图对象引用、UV scale/offset保持；两个Humanoid Animator有效且播放，原FBX/贴图/Prefab不改写。 |
| F：低分辨率 / 量化 / 吸附 / 仿射独立设置 | PASS | 屏幕效果覆盖原/变体敌人；对象效果仅作用于显式绑定的变体。各模块0/1独立切换，未修改文字Shader。 |
| F：透明/裁切转换状态 | PASS | 合成URP Lit测试材质的AlphaClip、Cutoff=.37、queue3000、ZWrite0转换正确；源材质不改写。 |
| F：透明/法线/发光实际画面 | NOT TESTED | 当前敌人没有这类材质样本。仅转换状态检查不是透明排序/法线/发光视觉通过。 |
| G：世界词条与高亮字母 | PASS | 实际PolePosition战斗截图：SpecialElite词条、绿色已输入字母、黄色箭头清晰；动态Canvas在UI层，由原生分辨率相机绘制。双怪同屏截图和原项目重叠断言均通过。 |
| G：屏幕HUD / 提示 / 结算 | PASS | 真实截图包括COMBAT START、血条、轨道提示、PRINT IT!和统计；UI保持原生分辨率。 |
| G：ScreenSpaceCamera、多相机UI、遮挡型世界UI | NOT TESTED | 当前项目主UI是Overlay和WorldSpace；多相机及要求3D深度遮挡的UI不在本轮验收范围。 |
| H：关闭 / 恢复 | PASS | 原相机RT/mask/rect/aspect、材质引用、Canvas camera和对象layer恢复；测试场景关闭效果截图与最初关闭基线**逐像素相同**；重复开关后自有RT和相机释放，原活动管线恢复。 |
| H：性能采样 | PASS | 同一测试场景600个预热后观测/配置；数字见下，不主张整体性能提升。gameplay 场景接入后的帧耗未重新采样（环境/角色改用了新 Shader，Draw/SetPass 会变），不沿用旧数字。 |
| 实际整关：敌人、输入、轨道、动画、结算 | PASS | 开启默认效果，合成InputSystem设备事件走原输入路径；100%准确率、12词、12击杀、双怪同屏、切轨、13/13配音、玩家存活、成功结算。场景接入后重跑一次为 31 PASS / 0 FAIL（输入退回 TrackInput 注入通道，已在报告内注明）。 |
| 原整关自检：BGM闪避音量断言 | FAIL | 开效果30 PASS / 1 FAIL；关闭效果另跑31 PASS / 0 FAIL。两次最低音量同为0.077、duck factor同为0.35；参考基准却分别为0.110/0.128，足以改变阈值判定。未修改音频/自检源码，也不将失败改标PASS。 |
| 独立构建、其他API/平台、XR、Cinemachine | NOT TESTED | 本轮只验证当前Windows D3D12 Editor；不宣称跨平台或Player构建通过。 |

### 自动检查范围

- 真实帧回归协程：最终 **28 PASS / 0 FAIL**，`Library/Locus/tmp/retro-validation/automated.txt`。
- 资产检查：Shader、材质、预设、贴图/UV、管线副本及无重复SSAO的Renderer、透明转换状态；`Library/Locus/tmp/retro-validation/assets.txt`。
- Unity Test Framework发现0个测试。本轮额外执行了上面的专用验证器和项目现有 `PolePositionAutoDrive`，不把“没有测试”算作通过。
- 观察战斗时一个外部Locus帧观察器达到3600工具帧上限；游戏内自检协程继续运行并最终输出完整报告。UI证据来自已保存的真实帧，而非该观察器的完成状态。

### 关于BGM失败

`PolePositionAutoDrive` 的音量参考采样受开场对白恢复时序影响；项目历史记录也列有这一风险。此次两个配置的实际最低音量和duck factor一致，但基准不同。证据**支持基准采样不稳定**，不能仅凭一次关闭效果通过就证明是渲染回归，也不能声称音频回归全绿。建议另行修正测试：在 `!DialogueDucking && DuckFactor > .99` 时取基准，然后重跑。本任务未改无关Gameplay脚本。

## 3. 性能记录

环境：RTX 5080 Laptop GPU，D3D12，Unity Editor；同一RetroTest、固定相机、暂停测试动画/运动，关闭调参IMGUI，vSync=0、targetFrameRate=-1。每组跳过120帧，收集600个观测。默认预设426×240；基线直接以1970×943绘制。选择不同内容宽高比会改变水平视野/可见几何，属于本效果实际成本，不是严格GPU微基准。

| 指标 | 原始基线 | Arcade Horror |
|---|---:|---:|
| 内部世界分辨率 | 1970×943 | 426×240 |
| 输出分辨率 | 1970×943 | 1970×943 |
| 主线程平均帧耗时 | 2.459 ms | 2.962 ms |
| 主线程P95 | 3.134 ms | 3.688 ms |
| GPU平均帧耗时 | 0.335 ms | 0.197 ms |
| GPU有效观测 / 600 | 476 | 456 |
| 平均Draw Calls | 42.97 | 43.97 |
| 平均SetPass | 15.00 | 27.96 |
| RenderTexture计数（整个Editor计数器） | 24 | 25 |
| RenderTexture字节（整个Editor计数器） | 210,657,742 | 152,507,332 |

专用426×240 RT实际使用 `R8G8B8A8_SRGB + D32_SFloat_S8_UInt`，平台把请求的24位深度升级为D32S8。格式估算1,226,880字节；`Profiler.GetRuntimeMemorySizeLong`实测1,227,912字节（约1.17MiB）。不包括全部URP暂态资源、驱动分配对齐或其他Editor纹理。

**结论：本机本场景GPU耗时降低，但CPU增加约0.503ms；不能据此声称整体性能提升。** Render Thread recorder不可用，GPU观测有缺失，不能用缺失值当0；这些不是独立Player帧率。

### 额外渲染工作

Frame Debugger确认：
- 原相机只渲染一次低分辨率3D世界；保留原阴影、DepthNormals、SSAO、CopyColor和最终输出路径。
- 增加一个只渲染合成背景和UI的Base Camera。放大、RGB量化和Bayer抖色合在**一个合成draw/pass**，没有三个重复全屏效果。
- WorldSpace UI绘制从原相机移到原生分辨率相机；Overlay/IMGUI最后绘制。
- 初版合成相机额外执行4个SSAO绘制，已改用独立Renderer副本消除。最终合成相机没有SSAO或阴影绘制。
- 最终Frame Debugger事件数19→32，增量不等于13个全屏Pass：其中包含环境/角色Shader导致的SRP Batch拆分。Profiler实际Draw Calls约43→44，SetPass约15→28。

原始采样：
- `Library/Locus/RunStates/profiler-retro_final_baseline-20261025-205557-843-a82681ac311d4eedbb85d9e491575638-summary.json`
- `Library/Locus/RunStates/profiler-retro_final_arcade-20261025-205557-853-30f6b5e1ae414ed48d0866f969bc697f-summary.json`
- 同名CSV保存600个观测；`Library/Locus/tmp/retro-validation/frame-final-baseline.json` 与 `frame-final-arcade.json` 保存事件列表。

## 4. 截图及原始回归报告

所有截图来自 `ScreenCapture.CaptureScreenshot` 的真实帧，**没有调用离帧 Camera.Render**。项目现有AutoDrive的旧离帧截图路径仅在本次运行时关闭，源码不变。

`Library/Locus/tmp/retro-validation/`：
- `native.png`、`320-point.png`、`320-bilinear.png`。
- `quantized4.png`、`dither4.png`。
- `snap1.png`、`snap-repeat.png`、`affine1.png`。
- `arcade-default.png`、`baseline-disabled.png`、`restored.png`。
- `gameplay-typed.png`、`gameplay-multiple.png`、`gameplay-late.png`。
- `gameplay-characters.png`（接入后的敌人+词条）、`amblift.png`（环境亮度调优后的场地）。
- `gameplay-retro.txt`（30/1）与 `gameplay-baseline.txt`（31/0）。
- `gameplay-retro-wired.txt`（场景接入后重跑：31/0）。

Library中的原始证据属于本机可清理产物；本文保留结论，关键截图另存于 `Assets/RetroRendering/Documentation/Screenshots/`。

## 5. 文件清单与范围

全部正式交付在 `Assets/RetroRendering/`，Unity同时生成对应`.meta`。

**沿用并修改的已有原型源码**：
- `Assets/RetroRendering/Scripts/RetroRenderingController.cs`
- `Assets/RetroRendering/Scripts/RetroMaterialBinding.cs`
- `Assets/RetroRendering/Scripts/RetroUIProtection.cs`
- `Assets/RetroRendering/Shaders/RetroObjectCommon.hlsl`
- `Assets/RetroRendering/Shaders/RetroScreenComposite.shader`

**新增源码**：
- `Assets/RetroRendering/Shaders/RetroEnvironment.shader`
- `Assets/RetroRendering/Shaders/RetroCharacter.shader`
- `Assets/RetroRendering/Scripts/RetroTestMotion.cs`
- `Assets/RetroRendering/Scripts/RetroValidationProbe.cs`
- `Assets/RetroRendering/Scripts/RetroCharacterAutoBinding.cs`（运行时把敌人材质原地换成角色 Shader，保留 TypingTarget 染色引用）
- `Assets/RetroRendering/Editor/RetroSetup.cs`
- `Assets/RetroRendering/Editor/RetroValidation.cs`
- `Assets/RetroRendering/Editor/RetroGameplaySetup.cs`（Apply/Remove gameplay 场景接线）

**保留未改的原型源码**：
- `Assets/RetroRendering/Scripts/RetroPreset.cs`
- `Assets/RetroRendering/Editor/RetroRenderingControllerEditor.cs`

**新增资产与文档**：
- `Materials/`：BruisedViolet、CheckerOriginal、CheckerRetro、DriedCrimson、DustyConcrete、OxidizedTeal、TestBaseline、ZombieRetro（.mat）。
- `Materials/PolePosition/`：M_Floor、M_Wall、M_Stage、M_BrassPole、M_Velvet、M_NeonPink、M_NeonCyan 的 `_Retro` 环境变体（基色/发光从原材质复制）。
- `Presets/`：CleanRetro、ArcadeHorror、AggressivePS1（.asset）。
- `Settings/PresentationRenderer.asset`、`Settings/Retro_PC_RPAsset.asset`。
- `Textures/Checker.asset`（测试棋盘，不替换原敌人贴图）。
- `Scenes/RetroTest.unity`。
- `Documentation/README.md`、`Documentation/Verification.md`、`Documentation/Screenshots/`。

**被修改的现有场景**：
- `Assets/Scenes/PolePosition.unity`：新增根对象 `Retro Rendering`（控制器 + 角色自动绑定），Venue 下 14 个 MeshRenderer 各新增一个 `RetroMaterialBinding`。无其他字段、相机、灯光或材质引用被改写；用 `Remove from PolePosition scene` 可完整撤销。

Git复核：原游戏脚本、敌人Prefab/FBX/贴图、原材质、原管线和ProjectSettings没有本任务产生的持久diff；唯一已跟踪文件的改动是上述场景接线。自动运行关卡新产生的14份telemetry已归档到 `Library/Locus/tmp/retro-validation/telemetry/`；原有未提交telemetry和Locus插件修改保留。

## 6. 回滚与未覆盖内容

设置步骤、手动A–H复测、材质转换限制及回滚见 `Assets/RetroRendering/Documentation/README.md`。关闭控制器或退出Play即恢复原渲染。不要对已有未提交原型或Locus包执行破坏性Git重置。

优先剩余验证：真实透明/法线/发光角色样本；小窗口与极近距离；独立Windows Player构建；低配GPU；其他图形API。若要扩展到烘焙lightmap/APV、TAA、XR或camera stack，应作为后续兼容任务，而非默认承诺已支持。
