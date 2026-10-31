# ASSET LOG —《YOUR MOVIE SUCKS!》Lab C

> 作业规则：**每一个进游戏的资产都要有一行**，写清来源、许可、是否 AI 生成、是否占位版。
> 「do you own this?」的答案就在这张表里。标了 【待补】 的格子必须由作者补齐才能提交（这是我不能替你编的部分）。

**列含义**
- **来源**：链接/作者/工具名 + 生成日期（AI 的话写工具与 plan tier）。
- **许可**：CC0 / CC-BY（写署名）/ 付费（写订单）/ AI 生成（写工具条款允许商用）/ 自制。
- **占位**：`是` = 临时占位（灰盒、旧素材），`否` = 最终版。

---

## 1. 角色（Hero assets）

| 资产 | 文件 | 类型 | 来源 | 许可 | AI | 占位 | 备注 |
|---|---|---|---|---|---|---|---|
| 主要威胁：比基尼丧尸 网格 | `Assets/Art/Monsters/ZombieV2/SM_ZOMBIE.fbx` | Mesh | Gemini concept → Tripo 3D; Mixamo rig | Tripo/Gemini terms & tier unverified | 是 | 否 | 33 骨骼 mixamorig 人形骨架；512×512 基础色贴图（无 normal/ORM） |
| 同上 贴图 | `Assets/Art/Monsters/ZombieV2/Textures/` | Texture | 同上 | 同上 | 同上 | 否 | 单材质槽 |
| 同上 动画 ×12 | `Assets/Art/Monsters/ZombieV2/Animations/` | Animation | Adobe Mixamo (Scary Zombie pack per creator) | Check Adobe Mixamo redistribution terms | 否（自动绑定/现有动画） | 否 | 重命名为 Zombie_Idle/Walk/…，XZ 位移摘成根运动，按动作性质设循环 |
| 同上 状态机 | `Assets/Art/Monsters/ZombieV2/ZombieV2.controller` | AnimatorController | 自制 | — | 否 | 否 | Idle/Run/Attack/Death + 任意态→攻击 |
| 同上 材质 | `Assets/Art/Monsters/ZombieV2/M_ZombieV2.mat` | Material | 自制（基于包内贴图调色） | — | 否 | 否 | 运行时被 Retro 角色 shader 替换，但威胁染色写同一材质实例 |
| 第二敌人：酒保/侍者丧尸 网格+贴图 | `Assets/Art/Monsters/Zombie_Waiter/Zombie_Waiter.fbx` + `Textures/` | Mesh/Texture | Gemini concept → Tripo 3D; optional Substance Painter touch-up | Tripo/Gemini terms & tier unverified | 是 | 否 | 复用同一骨架管线；伤害 2 格 |
| 侍者 材质 | `Assets/Art/Monsters/Zombie_Waiter/M_Zombie_Waiter.mat` | Material | 自制 | — | 否 | 否 | — |
| 敌人预制件 | `Assets/Prefabs/StripperZombie.prefab`、`Assets/Prefabs/Zombie_Waiter.prefab` | Prefab | 自制（由 `Assets/Editor/ZombieAssetBuilder.cs` 生成） | — | 否 | 否 | 缩放 1.25（体高 2.20 m）、脚底在原点 |
| 弱点数据 | `Assets/Data/Monsters/Weakness_Zombie.asset` | ScriptableObject | 自制 | — | 否 | 否 | 5 条：HEAD / LEG / NECK / SHOULDER / MOUTH + 骨骼名 |
| Player | —（第一人称摄影机，无可见模型） | — | — | — | — | — | 取舍说明见 `PIPELINE_REPORT.md` §5 |

## 2. 环境（Environment）

| 资产 | 文件 | 类型 | 来源 | 许可 | AI | 占位 | 备注 |
|---|---|---|---|---|---|---|---|
| 场馆构件（地板/墙/天花/舞台/钢管/卡座/吧台） | 由 `Assets/Editor/PolePositionSceneBuilder.cs` 用 Unity Primitive 生成 | Mesh（引擎同款） | 自制 | — | 否 | 否 | 尺寸与位置写死在生成器里，重建不会丢 |
| 舞台模型 | `Assets/Art/PolePosition/Stage/` + `M_StageModel.mat` | Mesh/Material | Gemini stage concept → Tripo 3D | Tripo terms & tier unverified | 是 | 否 | — |
| 场馆材质 | `Assets/Art/PolePosition/M_*.mat`（Floor/Wall/Stage/Velvet/BrassPole/NeonPink/NeonCyan） | Material | 自制（含用户调色） | — | 否 | 否 | 注意：调参实验后需核对基色未被误放大 |
| Retro 环境变体 | `Assets/RetroRendering/Materials/PolePosition/*_Retro.mat` | Material | 自制（由 `RetroGameplaySetup` 生成） | — | 否 | 否 | 14 个 MeshRenderer 挂 `RetroMaterialBinding` |
| 灯光 | 场景内 `Lighting/KeyLight/StageLight/PoleSpot/Neon*` | Light | 自制 | — | 否 | 否 | Forward+ 附加光；聚光+点光 |

## 3. UI

| 资产 | 文件 | 类型 | 来源 | 许可 | AI | 占位 | 备注 |
|---|---|---|---|---|---|---|---|
| 字体 | `Assets/Resources/Fonts/SpecialElite.ttf` | Font | Google Fonts（Special Elite, Astigmatic） | SIL OFL 1.1（需保留版权声明，见字体文件同目录） | 否 | 否 | 打字机气质；取不到时回退内置 Arial |
| 血条贴图（满/空） | `Assets/UI/Health/UI_HealthStripFull.png`、`UI_HealthStripEmpty.png` | Texture | 用户提供（源目录 `D:\UnityProject\UI\HealthAlone`） | 【待补】 | 【待补】 | 否 | 9 格胶片；Bayer4 抖动版 |
| 血条遮罩材质 | `Assets/UI/Health/Resources/M_HealthCellMask.mat` | Material | 自制 | — | 否 | 否 | 仅用于把 `UIHealthCellMask` shader 打进包（运行时 `Shader.Find`） |
| 屏幕血效贴图 | `Assets/UI/Blood/T_Blood0-2.png` | Texture | 【待补】 | 【待补】 | 【待补】 | 否 | 爪痕 + 两种飞溅 |
| UI 着色器 | `Assets/Art/Shaders/UIAlwaysOnTop.shader`、`UIHealthCellMask.shader` | Shader | 自制 | — | 否 | 否 | 词条永远可见 / 逐格遮罩 |
| 血条/标题/HUD | `Assets/Scripts/UI/*.cs` 运行时建 UGUI | Code | 自制（AI 编码助手参与） | — | 部分 | 否 | 全部参数在 Inspector/资产里 |

## 4. VFX

| 资产 | 文件 | 类型 | 来源 | 来源许可 | AI | 占位 | 备注 |
|---|---|---|---|---|---|---|---|
| PS1 Retro 合成 | `Assets/RetroRendering/`（shader + 控制器） | Shader/Code | 自制 | — | 部分 | 否 | 低分辨率 + 抖动 + 顶点吸附 |
| 血效（爪痕/飞溅） | `Assets/Scripts/Combat/ScreenBlood.cs` | Code + Texture | 自制（贴图见 §3） | — | 否 | 否 | 可被 VFX 开关关闭 |
| 受击红闪 + WRONG! | `Assets/Scripts/UI/FeedbackUI.cs` | Code | 自制 | — | 否 | 否 | 可关闭 |
| 导演轨胶片滤镜 | `Assets/Scripts/UI/DirectorFilmFilter.cs` | Code | 自制 | — | 否 | 否 | 黑白 + 颗粒 + 划痕，可关闭 |
| 词条箭头 | `Assets/Scripts/UI/TextCaretArrow.cs` | Code（程序化 sprite） | 自制 | — | 否 | 否 | 指认当前待输入字母 |

## 5. 音频

见 `docs/sound-list.md` §2 与 `docs/voice-script.md`（逐条来源/许可/调参）。汇总：

| 类别 | 数量 | 来源 | 许可 | 占位 |
|---|---|---|---|---|
| 打字音效 | 8 | 【待补】 | 【待补】 | 否 |
| 枪声 | 2 | 【待补】 | 【待补】 | 否 |
| 受伤音 | 1 | 【待补】 | 【待补】 | 否 |
| 音乐 | 1 | 商业歌手完整歌曲《Show Me Love》，转码为 MP3 | **商业录音：授权未核实，不得视为可自由再分发** | 否 |
| 配音 | 23 | 朋友真人配音（13 已接入，10 未触发；按 voice-script 旧记录） | 演员允许使用/公开分发的范围待确认 | 否 |

## 制作来源核实补充（2026-10-31）
- Gemini 输出：两名敌人的角色三视图、钢管舞台视觉参考。原始比基尼丧尸提示词：`生成这个比基尼丧尸角色的三视图，三视图比例1920*1080，复古ps1风格`。
- Tripo 3D：根据上述 Gemini 参考图生成两个敌人和舞台模型，作者确认。Tripo 月订阅约 ¥80，具体许可证及订阅层级尚待核实。
- Mixamo：自动绑定并采用现成僵尸动画。不能直接把 Mixamo 和生成式 AI 等同。
- 材质：AI 输出贴图多数保留，一部分由作者使用 Substance Painter 手动修正；场景贴图部分在 Photoshop 调整。
- Shader：作者技术美术调研 + ChatGPT 说明稿 → Astra 编写；费用约 USD 20。代码实现不能被简单视为“完全自制、没有 AI 参与”。
- 音频：免费网页下载的 SFX 来源无法追溯，许可证不明；当前 BGM 是商业发行歌曲，无可确认的游戏分发授权；朋友配音许可待确认。**教育用途与公开仓库均不自动取得版权许可。**
- 证据与限制见 `docs/LABC_VERIFIED_PROCESS.md` 和 `docs/BEFORE_AFTER_VIDEO.md`。本表保留旧工程的资源路径与技术标注，未经重新运行 Unity 的条目不视为实机已验证。

## 6. 待办（补齐这张表才算完成）

1. 全部【待补】单元格：来源链接 / 作者 / 生成工具 + plan + 日期 / 许可。
2. 给 CC-BY 资产补署名（会在 Lab D 的 credits 里出现）。
3. 确认字体 OFL 声明文件随工程保留。
4. 检查是否有资产已从游戏中移除但仍留在 `Assets/`（移除或标注「未使用」）。
