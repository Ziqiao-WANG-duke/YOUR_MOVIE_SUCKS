# STAGE LOG —《YOUR MOVIE SUCKS!》Lab C

**Track: 3D.** Hero assets：主要威胁 = StripperZombie（骨骼网格 + 12 段动画）；Player = 第一人称摄影机（无可见模型，取舍见 `PIPELINE_REPORT.md` §5）。
**记录方式**：每个阶段一条，字段沿用作业模板。事实来自 `BUILD_LOG.md`、工程内资产与本轮 Lab C 的工作记录。

**约定**：
- `【待补】`＝只有作者知道的事实（AI 工具名 / plan tier / 日期 / 实际耗时），必须补齐才能提交。
- 没有用 AI 的阶段写 `无（自制 / 免费资产 / 手工）`——这同样是合规条目，不要留空。
- 评级与「工作室会怎么用」是我的建议，作者可改。

---

# A. 3D 美术管线

## 3D · Stage 0 · Art direction — 全项目
- **Tool and plan**：Locus（AI 编码/工具助手，付费订阅）+ Unity 6000.3.8f1；2026-10 【待补：具体日期】
- **What I asked for**：把 Lab B 原型的灰盒推到「70s grindhouse + PS1/16mm 模拟质感」；高饱和霓虹 + 番茄酱血浆 + 廉价冲印感，同时保证打字词条在游戏尺寸下可读。
- **What came out**：`style/STYLE.md`（色彩、颗粒/抖动/暗角参数、UI 与词条规则）+ `Assets/RetroRendering/` 的合成材质参数集（低分辨率、Bayer 抖动、顶点吸附、仿射 UV）。
- **Kept or redid**：全部保留。中途否掉过「写实 PBR 高光」方向——在聚光下会洗白画面，改为哑光 + 强对比。
- **Time**：【待补】分钟（无 AI 估计：【待补】）
- **Rating (1–5)**：**5** — 风格一次定死，后面所有资产有明确判据，返工最少。
- **Would a studio use AI here?** 会用 AI 快速铺风格方向与参数草案，但最终定调仍要美术总监拍板。

## 3D · Stage 1 · Concept art + turnaround — 敌人（缺口）
- **Tool and plan**：无独立概念图 / 无可交付 turnaround。【待补：若用 AI 生成，写工具 + plan + 日期 + prompt】
- **What I asked for**：要一张「变异舞女丧尸」的正/侧/背 turnaround，用来定比例、服装剪影与配色。
- **What came out**：**没有产出**。实际做法是用 Unity 里的 look-dev（Retro 测试场景 + 灯光）直接对比素材包里的两个候选模型，选完就进入建模/贴图。
- **Kept or redid**：判定为「跳过该阶段」，理由：原型已有可用网格，先用引擎内对比替代概念图。**这是本管线最大的缺口，评分会扣在这里。**
- **Time**：0 分钟（无 AI 估计：90 分钟出一张可用 turnaround）
- **Rating (1–5)**：**2** — 省了时间，但失去了「先把角色定死再执行」的机会。
- **Would a studio use AI here?** 会——概念阶段是 AI 最划算的地方，一次生成四张候选比手绘快一个数量级。

## 3D · Stage 2 · Modeling — StripperZombie / Zombie_Waiter
- **Tool and plan**：素材包 + Unity 导入；【待补：素材来源链接与许可】
- **What I asked for**：一个 2.2 m 高、剪影清楚、能挂 Humanoid 骨架的人形敌人；另需一个同骨架的第二敌人做变体。
- **What came out**：`SM_ZOMBIE.fbx`（33 骨骼 mixamorig 人形骨架，436 顶点、单材质槽、有 UV/法线无切线）+ `Zombie_Waiter.fbx`（复用同一骨架管线）。
- **Kept or redid**：**保留**网格，**手工改**三件事：① 预制件缩放 1.25（体高 1.76 → 2.20 m）；② 脚底对齐（`-bounds.min.y` 重算）；③ 出生朝向（模型正面 = +Z）。
- **Time**：【待补】分钟（无 AI 估计：自建人形模型约 8–12 小时）
- **Rating (1–5)**：**3** — 省时间，但素材包决定了剪影与比例，没做过自己的概念。
- **Would a studio use AI here?** 做变体/道具可以；hero 角色文本生成 3D 目前拓扑与 UV 不稳定，不建议。

## 3D · Stage 3 · Texturing — 敌人 + 场景
- **Tool and plan**：包内 512×512 基础色贴图 + 手工调色（Unity 材质）；环境材质自制；【待补：是否用 AI 生成/放大贴图】
- **What I asked for**：敌人要「脏、低饱和的皮肤 + 血迹」，环境要「湿地板反光 + 霓虹染色」，而且过 Retro 合成后仍然读得出来。
- **What came out**：`M_ZombieV2.mat` / `M_Zombie_Waiter.mat`（单槽基础色 + 调色）；`Assets/Art/PolePosition/M_*.mat` 七套环境材质；`Assets/RetroRendering/Materials/PolePosition/*_Retro.mat` 14 个 Retro 变体。
- **Kept or redid**：**重做过一次**：地板/舞台基色在调参实验中被放大 3–5 倍（画面发白），已 `git checkout` 回退并把「调参后核对材质」写进检查清单。
- **Time**：【待补】分钟（无 AI 估计：4–6 小时）
- **Rating (1–5)**：**3** — 环境贴图效果够用；敌人贴图是包内资产，细节经不起近看。
- **Would a studio use AI here?** 会用（材质/贴图变体、去噪、放大），但需要美术做最后一道「游戏尺寸眼睛验收」。

## 3D · Stage 4 · Rigging — 敌人
- **Tool and plan**：Unity Humanoid 导入 + 手工修导入设置。
- **What I asked for**：一套能驱动 12 段动画、并且「原地迈步」而不是整只怪向前漂」的人形绑定。
- **What came out**：Humanoid avatar（isValid/isHuman 通过）；导入把 12 段动画的 XZ 位移摘成**根运动**、Animator 关 `Apply Root Motion`，位移交给 `TypingTarget` 的车道/速度代码。
- **Kept or redid**：修过两个真实故障：① Animator 挂到预制件根节点导致骨骼路径多一节 → avatar 解不出、永远 T-pose；改为挂在 FBX 模型根。② 动画在片内前移 1.26–2.35 m，靠根运动设置解决。
- **Time**：【待补】分钟（无 AI 估计：2–3 小时）
- **Rating (1–5)**：**4** — 绑定一次成型，踩的坑都有明确判据（`avatar.isHuman`）。
- **Would a studio use AI here?** 自动绑定（Mixamo 一类）已经在用；AI 生成骨架反而更靠后。

## 3D · Stage 5 · Animation — 敌人
- **Tool and plan**：包内 12 段动画 + 手工重命名/循环设置 + AnimatorController 手工连线。
- **What I asked for**：待机 / 奔跑 / 攻击 / 死亡四类动作；受击与攻击必须有「打到」的瞬间，且不能靠缩放做打击感。
- **What came out**：`ZombieV2.controller`；12 段片段重命名（原本 take 名全是 `mixamo.com`）；攻击触发由计时器改为 `OnAttackHit` 事件；`AnyState→Attack` 允许自过渡。
- **Kept or redid**：重做过打击反馈：原来靠整体缩放 1.3×，怪进攻击态后回缩到 1.0× 会被看成「突然变小/下移」——改为奔跑动画 + 染色表达，缩放参数序列化默认 1。
- **Time**：【待补】分钟（无 AI 估计：3–5 小时）
- **Rating (1–5)**：**3** — 动作可用但不精致（无过渡层、无 hit-react）。
- **Would a studio use AI here?** 会用于补中间帧/生成变体，但根运动与循环点必须动画师校验。

# B. 共享阶段

## Shared · Environment art — 场馆
- **Tool and plan**：`PolePositionSceneBuilder` 程序化搭台（Unity Primitive）+ 自制材质 + 手放灯光。
- **What I asked for**：一个看得出是「地下脱衣舞俱乐部」的小舞台：舞台 + 黄铜钢管 + 卡座 + 吧台 + 霓虹灯管，且四个机位都能出画面。
- **What came out**：`Assets/Scenes/PolePosition.unity`（Floor / WallBack/Left/Right / Ceiling / Stage / BrassPole / Booth / BarCounter / Neon 系列 / Lighting 系列），一键重建幂等。
- **Kept or redid**：迭代过两轮：① 场景用 Retro 变体材质重接；② 刷怪点高度 0.9 → 0.5（丧尸原点在脚底）。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 程序化重建让「改一下再来一遍」几乎零成本。
- **Would a studio use AI here?** 会用 AI 出概念与环境纹理；白盒搭建仍建议手工（要匹配玩法机位）。

## Shared · UI art — HUD / 标题 / 结算
- **Tool and plan**：UGUI 运行时构建 + Special Elite 字体 + 自制 shader；血条贴图由作者提供。
- **What I asked for**：HUD 必须「像 70s 电影胶片」：轨道徽章、词条、血条、取景框、结算屏、标题画面；且一切文字在白/绿/黄/红四色体系内。
- **What came out**：轨道色条 + 徽章、词条（未打白 / 已打绿 / 箭头指认）、9 格胶片血条、16MM 取景框、`PRINT IT!` 结算屏、Lab C 新增标题画面 + 静音/VFX 选项。
- **Kept or redid**：重做多次：字体渲成黑色（漏 `_TextureSampleAdd`）→ 换成自定义 UI shader；血条素材整体换新（4 张旧素材归档）；血格 8 → 9；删掉分数数字；轨道色按作者要求红蓝互换。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 风格统一、参数可调；缺口是「菜单」只有一个（标题即菜单）。
- **Would a studio use AI here?** 会用（图标、按钮状态、排版草案），最终排版与可读性人工把关。

## Shared · VFX — 血效 / 滤镜 / 反馈
- **Tool and plan**：自制 shader + 运行时 UGUI 叠加；参数写进序列化字段便于调。
- **What I asked for**：三个「关键时刻」必须有明确视觉反馈（受伤、击杀、失败），而且**能被玩家一键关掉**。
- **What came out**：屏幕血效（三道爪痕 + 随机飞溅）、受击红闪 + `WRONG!`、导演轨黑白胶片滤镜、PS1 低分辨率合成、词条黄色箭头；Lab C 增加 `VfxSwitch` + `[V]` 开关（关掉后血效/红闪/滤镜全部停止）。
- **Kept or redid**：修过两次「截图不可信」：URP RenderGraph 下离帧 `camera.Render()` 拿到的是不完整帧，改为 `ScreenCapture.CaptureScreenshot` 后正常。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 三个关键瞬间都有反馈且可开关；缺「拾取/奖励」类效果（本作没有该玩法）。
- **Would a studio use AI here?** 生成血溅/颗粒序列帧可以；调用时机与强度必须手感调。

## Shared · Lighting — 场馆灯光
- **Tool and plan**：URP 17 Forward+，手放 Light + 材质自发光。
- **What I asked for**：两档区域光——舞台聚光（主角/怪）+ 霓虹点光（环境染色），且不能让高光滑度表面被洗白。
- **What came out**：`KeyLight / StageLight / PoleSpot / NeonPink_* / NeonCyan_* / NeonTube*` 等；Retro 合成后仍保留霓虹对比。
- **Kept or redid**：踩过「smoothness ≥ 0.5 的墙在强聚光前被镜面洗白」的坑 → 降 smoothness 改哑光。
- **Time**：【待补】
- **Rating (1–5)**：**3** — 氛围到位，但缺少反射探针，金属/血面反射是假的。
- **Would a studio use AI here?** 会用 AI 出灯光参考图；实机布光仍手工。

## Shared · Integration & review — 全部接起来并验收
- **Tool and plan**：Locus（AI 助手）驱动 Unity 批处理；自检驱动器 `PolePositionAutoDrive` + `PolePositionSelfTestRunner`；`BuildWindows` 一键构建。
- **What I asked for**：每次改完都能自动验：核心循环能玩通、词条可读、音频三条管线都在响、打包能跑。
- **What came out**：批处理自检（断言 + 截图 + PASS/FAIL 汇总），`-batchmode` 自检报告 `Library/Locus/tmp/poleposition-selftest.txt`；Windows 构建 + 冒烟（引擎自截图 `-shot`、`-autostart`）。
- **Kept or redid**：自检本身被重做多次（假绿/假红：并发断言要有「确实发生过」佐证；基准值要等过渡结束再采）。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 自动化让回归成本极低；缺口是浏览器端（本作只做 Windows，见报告 §5）。
- **Would a studio use AI here?** 会，且已经在用（AI 编码助手 + 自动化验收是当前最成熟的 AI 用法）。

# C. 音效管线（SFX）

## SFX · Stage 1 · Sound list（spotting）— 全游戏
- **Tool and plan**：人工梳理 + 文档化（`docs/sound-list.md`）
- **What I asked for**：把每个需要声音的瞬间列全，并按「已实现 / 待做」标注。
- **What came out**：10 个触发点，其中 4 类已入游戏（打字、切轨、枪声、受伤）。漏掉的 6 个（弱点破坏、门词完成、低血、结算、UI 交互）都写进清单。
- **Kept or redid**：保留清单，作为下一轮补做依据。
- **Time**：【待补】
- **Rating (1–5)**：**3** — 清单完整，但「补齐」这一步还没做。
- **Would a studio use AI here?** 不会——spotting 是导演/音频总监的判断工作。

## SFX · Stage 2 · Sourcing — 打字/枪声/受伤
- **Tool and plan**：作者提供的免费音效素材包；【待补：来源与许可】
- **What I asked for**：打字要「真机械打字机」而不是键盘咔哒；枪声要有「廉价 B 级片」味道。
- **What came out**：`typewriter1–8.wav`（打字 7 选 1 + 切轨专用 1）、`cg1.wav` / `pistol.wav`（枪声 2 选 1）、受伤音 1 个。
- **Kept or redid**：**重选过一轮**：旧的 7 个 `eklee-*` 键盘音「太像办公室」，全部移出工程归档。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 打字机质感对味；枪声偏薄。
- **Would a studio use AI here?** 会用（AI 音效生成做初稿），但授权与「不像素材库」仍需人耳判断。

## SFX · Stage 3 · Editing & design — 裁切/池化/门控
- **Tool and plan**：手工裁切 + Unity 导入设置 + dotnet/NAudio（转码）
- **What I asked for**：快打不能糊；一次击键要有「打字 + 枪」两层但不吵。
- **What came out**：每个打字音 1.0s → 0.6s + 20ms 淡出；打字池与枪声池分目录隔离（否则枪声会被抽进打字池）；同源 30ms 最小间隔门控。
- **Kept or redid**：修过一个真实 bug：枪声放在 Typing 目录里会被随机池抽中 —— 拆到 `Gunshot/` 独立目录。
- **Time**：【待补】
- **Rating (1–5)**：**4**
- **Would a studio use AI here?** 部分会（自动切音、响度归一），设计判断人工。

## SFX · Stage 4 · Implementation — 接入
- **Tool and plan**：`TypingSfxPlayer`、`PlayerHurtSfx`、`DemoAudioDirector`；id 池由编辑器工具扫描目录生成
- **What I asked for**：一行代码不用手写 clip 引用；重建素材库不漏；只在真掉血时响受伤音。
- **What came out**：`typing SFX pool = typewriter1..7 (size=7)`、`track-switch SFX = typewriter8`、`gunshot pool wired (size=2)`；自检断言「击键确实响了」。
- **Kept or redid**：保留了 id 方案（`VoLibrary` 同样的思路），换素材只需换文件。
- **Time**：【待补】
- **Rating (1–5)**：**4**
- **Would a studio use AI here?** 会（AI 写接入代码与自动化断言）。

## SFX · Stage 5 · Mixing — 混音
- **Tool and plan**：代码音量（`LabBTuning`：`sfxVolume / gunshotVolume / voVolume / bgmVolume`）+ F1 面板实时调
- **What I asked for**：打字不能被 BGM 掩蔽；枪声不能盖过台词；对话时 BGM 必让位。
- **What came out**：`sfxVolume 0.8`、`bgmVolume 0.22`、对话 duck ×0.35（2.5/s 平滑）；峰值压在 −3 dBFS 以内。
- **Kept or redid**：修过基准值采样错误（在 duck 斜坡中间取了 baseline → 断言假红）。
- **Time**：【待补】
- **Rating (1–5)**：**3** — 听感可用，但没上 AudioMixer bus，无法做响度归一。
- **Would a studio use AI here?** 不会（混音是耳朵活），AI 只用于响度检测报告。

# D. 音乐管线（Music）

## Music · Stage 1 · Music direction — 主循环
- **Tool and plan**：写 brief（`style/STYLE.md` §5.1）
- **What I asked for**：70s 放克/迪斯科 + 廉价钢管舞俱乐部现场感，鼓组要脏，贝斯走根音，不许恐怖片配乐。
- **What came out**：一页 brief（情绪、用途、时长、动态、混音目标）。
- **Kept or redid**：保留。
- **Time**：【待补】
- **Rating (1–5)**：**4**
- **Would a studio use AI here?** 不会——音乐方向是总监与作曲的对话。

## Music · Stage 2 · Composition & production — 主循环曲
- **Tool and plan**：【待补：作曲/生成工具 + plan + 日期】（素材为作者提供的 `BGM_Show Me Love.mp4`）
- **What I asked for**：一条能循环 4 分钟不腻、并且「一听就是这台词风格」的曲子。
- **What came out**：`Assets/Audio/BGM/bgm_show_me_love.mp3`（4:00，192k 立体声，由 mp4 音轨转码）。
- **Kept or redid**：保留；**已知差距**：作业要求 30–90s 循环，现曲 4:00（见报告 §4）。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 气质准确；长度不符合交付要求。
- **Would a studio use AI here?** 会用（生成初稿/变奏），最终编曲与循环点人工处理。

## Music · Stage 3 · Mixing & mastering — 与 SFX/VO 共存
- **Tool and plan**：代码音量 + ducking
- **What I asked for**：打字时音乐不抢戏；台词时音乐让位；进入结算屏前停。
- **What came out**：全局 0.22，对话 duck ×0.35（2.5/s 平滑），`CUT TO BLACK` 停播。
- **Kept or redid**：保留。
- **Time**：【待补】
- **Rating (1–5)**：**3** — 没有真正的母带处理，只做了相对音量。
- **Would a studio use AI here?** 不会。

## Music · Stage 4 · Implementation — 播放与状态
- **Tool and plan**：`DemoAudioDirector.PlayBgm/StopBgm` + Streaming 导入
- **What I asked for**：开场黑幕起播，结算前停，重开一局要能重来。
- **What came out**：Streaming + loop；`dialogue ducking ON/OFF` 日志可观测。
- **Kept or redid**：保留。
- **Time**：【待补】
- **Rating (1–5)**：**4**
- **Would a studio use AI here?** 会（接入代码/中间件脚本）。

# E. 配音管线（Voice）

## Voice · Stage 1 · Writing — 23 句
- **Tool and plan**：脚本写作（原稿 `Library/Locus/tmp/demo_script_v2.txt`），逐句定 id
- **What I asked for**：两个角色的互喷要短、狠、可被打断；反应台词要能插进战斗中不打断打字节奏。
- **What came out**：13 句剧情对白 + 10 句反应台词（受击 4 / 低血 4 / 死亡 2），全部见 `docs/voice-script.md`。
- **Kept or redid**：修过一轮：开场倒计时段加了「被打断」的一句（`pp_open_agt_06_fuck_you`）。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 角色声线清楚；反应台词还没接进游戏（见 Voice Stage 6）。
- **Would a studio use AI here?** 会用 AI 出候选台词，最终由编剧定稿。

## Voice · Stage 2 · Casting / voice design — 两个角色
- **Tool and plan**：【待补：真人录制 or TTS 工具 + 音色设置】
- **What I asked for**：AGENT 低、糙、短句；DIRECTOR 中音、语速快、油滑。
- **What came out**：两条可区分声线，命名规范 `pp_<段>_<角色>_<序号>_<slug>`。
- **Kept or redid**：保留。
- **Time**：【待补】
- **Rating (1–5)**：**4**
- **Would a studio use AI here?** 会用（AI 试音/多版本对比），正式配音签约演员。

## Voice · Stage 3 · Scratch voice — 草配
- **Tool and plan**：【待补：是否做过草配（时间轴占位）】
- **What I asked for**：先确认每句长度与节奏能塞进演出节拍。
- **What came out**：【待补】
- **Kept or redid**：【待补】
- **Time**：【待补】
- **Rating (1–5)**：【待补】
- **Would a studio use AI here?** 会——草配是 TTS 最舒服的用途。

## Voice · Stage 4 · Final recording or generation — 终版
- **Tool and plan**：【待补：工具/演员 + plan + 日期 + 条款页存档】
- **What I asked for**：23 句全部一次录完（同一天、同一套话筒位置），避免音色漂移。
- **What came out**：23 个 `Assets/Audio/VO/PolePosition/*.wav`。
- **Kept or redid**：【待补：是否有重录/弃用版本】
- **Time**：【待补】
- **Rating (1–5)**：【待补】
- **Would a studio use AI here?** 决定性问题是条款与授权（尤其声音克隆）——必须存档当时的 terms 页面。

## Voice · Stage 5 · Editing & processing — 转码与逐条调参
- **Tool and plan**：dotnet + NAudio（Windows Media Foundation）批量转码 + Unity 逐条参数
- **What I asked for**：全部变成 Unity 能导的格式，并且每条都能微调速度/延迟/起始偏移/音量。
- **What came out**：m4a 在 Unity 6.3 **无法导入**（实测 23/23 NOT IMPORTED）→ 批量转 44.1k/16bit/mono WAV（23/23 成功）；`VoLineSettings_PolePosition.asset` 里 7 条有手工参数（砍前奏 0.6–1.0s、延迟 0.2–0.3s）。
- **Kept or redid**：保留；坑已写进 `docs/voice-script.md` §3。
- **Time**：【待补】
- **Rating (1–5)**：**4** — 工具链顺手；没做降噪/电平归一。
- **Would a studio use AI here?** 会（批量降噪、自动对齐、响度归一）。

## Voice · Stage 6 · Implementation + captions — 接入与字幕
- **Tool and plan**：`DemoAudioDirector`（VoLibrary id→clip）+ `DemoDialogue`（以音频长度推进）+ `DemoHud.ShowSubtitle`
- **What I asked for**：字幕与音频严格同步；缺音频时退回按 hold 计时；每句必须上屏。
- **What came out**：13 句剧情对白全部接入且带字幕（字幕在音频前 55% 时长内铺完）。
- **Kept or redid**：**未完成部分**：10 句反应台词（受击/低血/死亡）已录制但**没有触发点**，因此游戏里听不到；接入方案写在 `docs/voice-script.md` §4，约 0.5 天。
- **Time**：【待补】
- **Rating (1–5)**：**3** — 剧情线完整、字幕规范清楚；反应台词缺口直接影响「关键时刻都有声音」。
- **Would a studio use AI here?** 会（接入代码 + 字幕自动分行）。

---

# F. 汇总表（写进 PIPELINE_REPORT 的那张）

| 管线 | 阶段 | 评级 | 耗时（分钟） | 补记 |
|---|---|---|---|---|
| 3D | 0 Art direction | 5 | 【待补】 | |
| 3D | 1 Concept + turnaround | 2 | 【待补】 | 缺口：无概念图 |
| 3D | 2 Modeling | 3 | 【待补】 | 外部素材 + 手工缩放/对齐 |
| 3D | 3 Texturing | 3 | 【待补】 | 踩过材质被放大的坑 |
| 3D | 4 Rigging | 4 | 【待补】 | Humanoid + 根运动 |
| 3D | 5 Animation | 3 | 【待补】 | 打击感重做 |
| 共享 | Environment | 4 | 【待补】 | |
| 共享 | UI | 4 | 【待补】 | 多次重做 |
| 共享 | VFX | 4 | 【待补】 | 可开关 |
| 共享 | Lighting | 3 | 【待补】 | 缺反射探针 |
| 共享 | Integration & review | 4 | 【待补】 | 自动化自检 |
| SFX | 1 Spotting | 3 | 【待补】 | 6 项待做 |
| SFX | 2 Sourcing | 4 | 【待补】 | 换过一轮素材 |
| SFX | 3 Editing & design | 4 | 【待补】 | 分池 + 门控 |
| SFX | 4 Implementation | 4 | 【待补】 | |
| SFX | 5 Mixing | 3 | 【待补】 | 无 bus |
| Music | 1 Direction | 4 | 【待补】 | |
| Music | 2 Composition | 4 | 【待补】 | 时长超要求 |
| Music | 3 Mixing & mastering | 3 | 【待补】 | |
| Music | 4 Implementation | 4 | 【待补】 | |
| Voice | 1 Writing | 4 | 【待补】 | 23 句 |
| Voice | 2 Casting | 4 | 【待补】 | |
| Voice | 3 Scratch | 【待补】 | 【待补】 | |
| Voice | 4 Final | 【待补】 | 【待补】 | |
| Voice | 5 Editing | 4 | 【待补】 | m4a 不可导入 |
| Voice | 6 Implementation + captions | 3 | 【待补】 | 10 句未接入 |

# G. 需要你补的清单（提交前必须清零）

**最快方式：只填下面这张表，然后告诉我，我把值写回上面每一条。**（内容是「只有你知道」的事实，我不能替你编。）

| # | 阶段 | 工具 + plan tier + 日期 | 实际耗时（分钟） | 无 AI 估计（分钟） |
|---|---|---|---|---|
| 1 | 3D·0 Art direction | | | |
| 2 | 3D·1 Concept + turnaround | | | |
| 3 | 3D·2 Modeling | | | |
| 4 | 3D·3 Texturing | | | |
| 5 | 3D·4 Rigging | | | |
| 6 | 3D·5 Animation | | | |
| 7 | Shared·Environment | | | |
| 8 | Shared·UI | | | |
| 9 | Shared·VFX | | | |
| 10 | Shared·Lighting | | | |
| 11 | Shared·Integration & review | | | |
| 12 | SFX·1 Spotting | | | |
| 13 | SFX·2 Sourcing | | | |
| 14 | SFX·3 Editing & design | | | |
| 15 | SFX·4 Implementation | | | |
| 16 | SFX·5 Mixing | | | |
| 17 | Music·1 Direction | | | |
| 18 | Music·2 Composition & production | | | |
| 19 | Music·3 Mixing & mastering | | | |
| 20 | Music·4 Implementation | | | |
| 21 | Voice·1 Writing | | | |
| 22 | Voice·2 Casting / voice design | | | |
| 23 | Voice·3 Scratch voice | | | |
| 24 | Voice·4 Final recording / generation | | | |
| 25 | Voice·5 Editing & processing | | | |
| 26 | Voice·6 Implementation + captions | | | |

另外四件只需要一句话回复的事：

1. **Stage 1 概念图**：补一张 turnaround，还是确认「以 look-dev 代替」照实写？（补图直接提分）
2. **Voice Stage 3/4**：草配做过吗？终版是真人还是 TTS？工具/演员名 + 条款页截图存哪。
3. **Music Stage 2**：曲子是自己做的 / AI 生成 / 授权曲目？写清来源与许可。
4. **钱**：Lab C 期间为工具花过的钱（每个工具一行：工具 / plan / 金额 / 是否有条款截图）。

## 提交前自检（打勾即可）

- [ ] `STAGE_LOG.md` 里没有任何 `【待补】`（含汇总表）
- [ ] `ASSET_LOG.md` 里每一行都有来源 + 许可
- [ ] `style/STYLE.md` §7 的 `style/refs/` 至少 5 张带来源的参考图
- [ ] `PIPELINE_REPORT.md` §4 的时间/金额已填
- [ ] 已知差距两选一：补做 or 在报告 §5.3 写清理由（音乐时长 + 缺失音效）
