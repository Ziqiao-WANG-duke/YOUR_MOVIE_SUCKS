# AGENTS.md — YOUR MOVIE SUCKS! (Lab B / Playable Prototype)

本文件夹既是 Duke GAMEDSGN《Gen AI for Games》**Lab B** 的游戏文件夹，也是《YOUR MOVIE SUCKS!》的长期实现工程。
任何 AI 助手开始工作前，先读本文件、`GDD.md`、`PROTOTYPE.md`，并按第 5 节的协作规则办事。

## 1. 技术栈与确切版本（不要擅自升级）

| 项 | 版本 | 说明 |
|---|---|---|
| 引擎 | Unity 6000.3.8f1 | 编辑器路径 `C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe` |
| 渲染管线 | URP 17.3.0 (`com.unity.render-pipelines.universal`) | 资产在 `Assets/Settings` |
| 输入 | `com.unity.inputsystem` 1.18.0 | 新输入系统，单键盘，读取键码 + Ctrl 修饰状态 |
| UI | `com.unity.ugui` 2.0.0 | 运行期调试/调参面板使用 IMGUI (`OnGUI`) |
| 测试 | `com.unity.test-framework` 1.6.0 | |
| 语言 | C#（`Assembly-CSharp`，脚本根 `Assets/`） | |
| 目标平台 | **Windows x64 Standalone**（Mono） | `windowsstandalonesupport` 已安装；**WebGL 模块未安装** |

环境事实（影响任何计划）：

- 本机**没有 Node.js / npm**，因此不存在 Web 原生栈。
- Unity 只安装了 `windowsstandalonesupport` 播放器模块；导出浏览器版本需先在 Unity Hub 安装 WebGL Build Support。
- 同一时刻只允许一个编辑器实例持有本工程；**批处理构建前必须先关闭已打开的编辑器**。
- 仓库已初始化 git；`Library/`、`Logs/`、`Build/`、`Locus/` 已忽略（见 `.gitignore`）。

## 2. 文件布局

```
AGENTS.md            <- Lab B 交付物，必须在顶层
GDD.md               <- Lab B 交付物（由 Rep 3 GDD 转换）
PROTOTYPE.md         <- Lab B 交付物：Prototype Card / Loop Log / Variant / Playtest / Verdict
BUILD_LOG.md         <- Lab B 交付物：每次工作会话一条
telemetry/           <- Lab B 遥测 JSON（playtest 回收文件放这里）
Assets/Scripts/      <- 游戏代码（Combat / Input / Level / Typing / UI）
Assets/Scripts/Demo/ <- Demo 演出层（序列状态机 / 对白 / 中央提示 / 黑屏 / 结算 / 音频 / 自检驱动器）
Assets/Scripts/LabB/ <- Lab B 交付物代码（调参面板 / 遥测导出）
Assets/Editor/       <- 编辑器工具（关卡场景生成器 / 丧尸资产工具 / 批处理自检入口 / 构建脚本）
Assets/Art/          <- 美术资产：`PolePosition/` 关卡灰盒材质 + `Shaders/` + **`Monsters/Zombie/`**
Assets/Data/Monsters/ <- 怪物数据配置（Weakness_Zombie.asset = 5 条弱点：部位/弱点词/骨骼/简化词）
Assets/Art/Monsters/Zombie/ <- 丧尸敌人：**模型与配套动画均为作者自制**（不是商店/他人素材），`Zombie.fbx`（人形骨架 + 网格）、`Animations/` 12 段、`Textures/` 基础色贴图、`M_Zombie.mat`、`Zombie.controller`
Assets/Prefabs/      <- 预制件：`StripperZombie.prefab`（= 关卡里的丧尸敌人：Animator + TypingTarget + TypingTargetAnimator）
Assets/Audio/        <- 音频：VO/ 配音（文件名 = 台词 id）+ SFX/ 音效 + BGM/ 音乐；VoLibrary_PolePosition.asset 是 id→clip 映射
Assets/Scenes/       <- SampleScene.unity（灰盒沙盒）+ PolePosition.unity（Demo 关卡「POLE POSITION」）
Build/               <- 构建产物，不入库
Locus/knowledge/     <- 设计源与执行计划（不入库）
《YOUR MOVIE SUCKS!》.pdf、Game core loop.png  <- Lab A / Rep 3 原始设计输入
```

## 3. 如何运行

1. 用 Unity 6000.3.8f1 打开本工程。
2. 打开 `Assets/Scenes/PolePosition.unity`（Demo 关卡「POLE POSITION」），按 Play。
3. 键位见第 4 节。开局演出会先要求 `TYPE: ZOOM`，再要求按一下 CapsLock 切到特工轨。

**重建关卡场景**（可复现，不用手摆 Hierarchy）：菜单 `YourMovieSucks/Build POLE POSITION Scene`；
校验接线：`YourMovieSucks/Validate POLE POSITION Scene`；自动打完整关自检：按 `F2`，或菜单
`YourMovieSucks/Run POLE POSITION Self-Test (Play Mode)`。

**导演轨弱点识别验收**：菜单 `YourMovieSucks/Verify DIRECTOR WEAKNESS Flow`（自动跑完 8 步流程，报告写 `Library/Locus/tmp/weakness-verify.txt`；批处理 `-executeMethod YourMovieSucks.EditorTools.WeaknessFlowVerify.Run`）。

**词条字体 + 威胁配色验收**：菜单 `YourMovieSucks/Verify ENEMY LABEL Font + Threat Tint`（检查 Special Elite 已导入、白→黄→红三段颜色、颜色连续无跳变、攻击中闪烁；报告写 `Library/Locus/tmp/word-tint-verify.txt`，截图存 `Library/Locus/tmp/tint-shots/`；批处理 `-executeMethod YourMovieSucks.EditorTools.WordTintVerify.Run`）。

**怪物（丧尸）资产**：菜单 `YourMovieSucks/Monsters/0 · 一键：导入丧尸 + 建资产 + 接线场景`
（导入设置（人形 / 原地动画 / 片段命名+循环）→ 材质 `M_Zombie` → Animator `Zombie.controller` → `Assets/Prefabs/StripperZombie.prefab` → 接进场景），
`…/2 · 核对丧尸资产` 只打印核对报告。批处理：`-executeMethod YourMovieSucks.EditorTools.ZombieAssetBuilder.BuildAndWire`；
无人值守跑分 + 怪物状态探针：`-executeMethod YourMovieSucks.EditorTools.ZombieEnemyVerify.RunWithProbe`（结论写 `Library/Locus/tmp/zombie-probe.txt`）。
敌人外观接在 `ScriptedEncounter.zombiePrefab` 上：**留空则退回灰盒方块**（老场景仍可跑）。
素材来自包外目录 `D:\UnityProject\Scary Zombie Pack`（模型 `Zombie.fbx` = 人形骨架 + 网格，12 段动画 fbx 只含骨架）。

**音频（配音）**：菜单 `YourMovieSucks/Voice/0 · 一键：检查 → 配置 → 建库接线`（新增音频后跑一次即可）。
约定：音频文件名 = 台词 id（如 `pp_open_dir_01_production_value.wav`），放 `Assets/Audio/VO/PolePosition/`；
Unity 不支持 m4a，需 wav/ogg。首尾静音已剪、响度统一到 −20dBFS RMS。

**逐条音频调节**：菜单 `YourMovieSucks/Voice Tuner (POLE POSITION)` —— 速度（pitch，会同时变调）/ 延迟 / 音量 / 起始偏移，
写到 `Assets/Audio/VO/VoLineSettings_PolePosition.asset`（手调资产，重建库不覆盖）；
**Play 模式下改动即时生效**；▶ = 编辑器原速试听，▶Play = 按真实参数在 Play 模式播放。全局音量在 F1 面板。

**BGM / 打字音效**：`Assets/Audio/BGM/bgm_show_me_love.mp3`（开场黑幕起 → PRINT IT! 结算屏前停，对话时自动压低）；
打字音效在 `Assets/Audio/SFX/Typing/`：a–z 从 typewriter1–7 随机（最近 2 条不重复），CapsLock 切轨固定用 `typewriter8`；
**每次击键同时叠一声随机枪声**（`Assets/Audio/SFX/Gunshot/`：`cg1` / `pistol`，已峰值归一到 −6dBFS），枪声层音量看 F1 面板 `gunshotVolume`。

## 4. 如何构建与打包（可交付版本）

    "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" -batchmode -quit ^
      -projectPath "D:\UnityProject\YOUR_MOVIE_SUCKS" ^
      -executeMethod YourMovieSucks.LabB.EditorTools.LabBBuild.BuildWindows ^
      -logFile "Logs/labb-build.log"

- 代码：`Assets/Editor/LabBBuild.cs`（只打 `Assets/Scenes/PolePosition.unity` 一个场景）。
- 产物：`Build/Windows/YourMovieSucks_LabB.exe` 与同目录 `YourMovieSucks_LabB_Data/`；打包前请在编辑器里点一次菜单 `YourMovieSucks/Build POLE POSITION Scene` 重建场景（场景是生成的）。
- 一键包（给测试者/上传）：`Build/YourMovieSucks_LabB_Windows.zip`，里面含 `PLAYTEST-README.txt`（说明怎么跑、怎么按 `T` 导出遥测）。
- 发布方式：Windows 独立版 zip + 作者自己上传网盘。**本 Lab 明确不制作浏览器版**（不安装 WebGL 模块），与 Lab B 默认的"必须浏览器可玩/live link"不一致，该偏差已由作者确认并记在 `PROTOTYPE.md` 顶部。
- 冒烟检查（构建后建议跑一次）：`YourMovieSucks_LabB.exe -batchmode -nographics -logFile <路径>`，看日志里有没有异常、有没有 `[DemoAudioDirector] dialogue ducking ON`（说明场景/演出起来了）。

## 5. 原型键位（单一动词 = 打字击发）

| 键 | 作用 |
|---|---|
| `A`–`Z` | 对当前锁定目标击发下一个字母（首字母附加锁定） |
| `CapsLock`（单击 toggle） | 切换特工轨 / 导演轨（大写键 ↔ 大写词条 = 导演轨）；**Demo 固定 toggle，不做按住模式**；Ctrl / Shift 默认不参与切轨（可选项） |
| （演出层）`Enter` | 跳过开场演出（skip），并记录 `skip_intro` 遥测事件（原写“任意键”会与 ZOOM 输入门冲突） |
| `F2` | 关卡自检：自动打完「POLE POSITION」并输出校验报告 |
| `1` / `2` | Variant 运行时切换：单轨 / 双轨（并记录 switch 事件） |
| `R` | 失败后一键重开（目标 ≤2 秒回到游戏中）；BAD TAKE 屏上同时提供可点击的 `RETAKE` 按钮 |
| `F1` | 显示 / 隐藏调参面板（IMGUI，~30 个实时参数；Lab B 期间调过的每个数字都在上面）|
| `T` | 导出本局遥测 JSON 并显示文件路径 |
| `Esc` | 退出（仅构建版本） |

## 6. 协作规则（AI 助手必须遵守）

1. **一次只改一个数**：每次改动都要在 `PROTOTYPE.md` 的 Loop Log 写一条——改了什么、为什么、哪个数字动了。
2. **调过的每个数字都要在调参面板上**（面板参数不少于 8 个），禁止把玩法数值硬编码在无法实时调整的位置。
3. **不要改遥测字段名**（`run_start` / `run_end{duration, score, cause, x, y}` / `retry` / `variant_switch`），它们决定后续分析口径。
4. **小步提交**：一次提交对应一个改动，提交信息写明改了什么参数/机制；禁止一次塞入大量无关改动。
5. **不越界**：菜单（重开以外）、剧情、存档、元进度、超出原型问题所需的关卡、1–2 个以上的 juice 效果，全部不做。
6. **不猜测**：任何"因为好玩/更好"的论断都要能落回某个数字；没有测量依据的调参不接受。
7. **每会话补 `BUILD_LOG.md`**：日期、使用的工具、最关键的那条 prompt、什么坏了、大约耗时。
8. 改动 Lab B 原型时不影响 `Assets/Scripts/` 下的长期灰盒代码，反之亦然。
9. **词条显示与输入分离**：显示保留原文空格（`my pole`），输入不打空格、不打标点，一个词打完自动跳到下一个词（匹配串为去空格大写）。
10. **轨道反馈必须明显**：切换后 HUD/取景框必须立刻变化，否则无法验证"Ctrl 切换是否符合认知"。

## 7. Lab B 交付清单（对照作业要求）

| # | 要求 | 状态 | 位置 / 说明 |
|---|---|---|---|
| 1 | Live link（能在浏览器里玩） | **作者决定不做** | 交付形态 = Windows 独立版 zip（作者自己上传网盘）。该偏差已记在 `PROTOTYPE.md` 顶部与本文件 §4，不安装 WebGL 模块 |
| 2 | Markdown 四件套 | ✅ | `AGENTS.md` / `GDD.md` / `PROTOTYPE.md` / `BUILD_LOG.md`。PROTOTYPE.md 的 Prototype Card、Loop Log、Variant、Playtest Log、Verdict 由作者填（助手的 Loop Log 记录方式见本文件 §6）|
| 3 | 调参面板 ≥ 8 个实时参数 | ✅ 约 30 个 | `F1`（IMGUI）。**Lab B 期间调过的每个数字都在上面**：移动与威胁、刷怪、词条（近身限尺 / 屏幕安全区）、怪的外观缩放与近身站位（车道收窄、到站站位）、音频、两个 juice 开关 |
| 4 | 遥测导出（每次 run / 时长 / 重开 / 怎么死的、死在哪） | ✅ 导出、⬜ 归档 | `T` 键写 JSON：`run_start` / `run_end{duration,score,cause,failWord,failX,failY,...}` / `retry{latency}` / `track_switch` / `variant_switch` / `skip_intro`；同时写 exe 旁 `telemetry/` 与 `%AppData%` 两处，屏幕会显示路径。**playtest 收上来的 JSON 由作者放进 `telemetry/`** |
| 5 | 一个动词 / 失败态 / ≤2 秒一键重开 / 成绩信号 | ✅ | 打字即击发（单一动词）；BAD TAKE 失败演出；`R` 一键重开（`LoadScene`）；结算屏 ACCURACY / WORDS / WPM / KILLS / MISSTYPES |
| 6 | Juice ≤ 1–2 个，且每个都有开关 | ✅ | 只保留**镜头晃动**与**糊血**两个，都能在面板上关掉 / 调强度；原设计的 hit-stop 按"≤2 个"这一条未实现（面板上已删掉无效字段）|
| 7 | 60–90 秒 gameplay 视频 | ⬜ 作者录 | 本机没有 ffmpeg；可装 Unity **Recorder** 包直录 Game 视图，或用 OBS / Windows 游戏栏 |
| 8 | 提交：GitHub 仓库链接（加老师为协作者）**或**含隐藏 `.git` 的游戏文件夹 zip | ✅ 已备好 zip | 本仓库**没有远程**，走 zip 这条：`Build/YourMovieSucks_LabB_Folder.zip`（含 `.git`，已排除 Library / Temp / Logs / Build）|

补充：给测试者的可玩包 = `Build/YourMovieSucks_LabB_Windows.zip`（内含 `PLAYTEST-README.txt`：怎么跑、结束按 `T` 导出遥测、键位表）。
