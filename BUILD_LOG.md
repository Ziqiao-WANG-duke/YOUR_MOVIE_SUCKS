# BUILD_LOG.md — YOUR MOVIE SUCKS! (Lab B)

> 规则：每次工作会话结束补一条——日期、使用的工具、最关键的那条 prompt、什么坏了、大约耗时。
> 本文件同时作为本学期后续 Lab 的工作日志（Lab D 反思用）。

## Oct 6 — 会话 1：读需求 + 搭骨架

- **工具**：Locus（Unity 工程内 AI 助手，工作目录 `D:\UnityProject\YOUR_MOVIE_SUCKS`）
- **Prompt that mattered**：读 `D:\DUKE\GenAI\Lab_B_Playable_Prototype (Final).pdf`，告诉我这份文件的需求是什么、为了满足需求打算做什么
- **做了什么**：
  - 提取并归档 Lab B 需求（`Library/Locus/tmp/lab_b.txt`），核对工程现状：未装 WebGL 模块、无 git 仓库、无 Node.js
  - 决定交付形态：Windows 独立版 zip + 网盘链接（作者决定，偏离作业默认的浏览器链接要求）
  - `git init` + Unity `.gitignore`；创建 Lab B 顶层交付物 `AGENTS.md`、`GDD.md`（由 Rep 3 GDD 转换）、`PROTOTYPE.md`（模板，含待作者填写的 Prototype Card）、`BUILD_LOG.md`；建立 `telemetry/`
- **What broke**：无（尚未编译或构建）。已知障碍：编辑器未安装 WebGL 模块；仓库缺少 git 身份（`user.email` 未配置），首次提交前需补
- **时间**：约 0.5 小时

## Oct 7 — 会话 2：读 Demo 脚本 + 盘点差距

- **工具**：Locus（Unity 工程内 AI 助手）
- **Prompt that mattered**：阅读我写的新脚本，判断实现这些内容要做/修改什么内容（不要先执行）
- **做了什么**：读懂 6 页 Demo 脚本（中文需用 PyMuPDF 提取，`pdftotext` 丢字）；只读盘点全部 19 个脚本与场景层级；产出节拍→能力映射表与四类缺口（新增系统 / 需改造文件 / 场景资产 / Lab B 交付物）
- **What broke**：无。发现隐患：项目内**零音频基础设施**、无 accuracy/WPM/MISSTYPES 任何埋点、`WordLabel` 只按 `MatchText` 渲染会吃掉空格、`TrackInput` 的 Ctrl/CapsLock/Shift 五键都会切轨（与教学提示不一致）
- **时间**：约 1 小时

## Oct 7 — 会话 3：脚本 v2 决策 + 同步 GDD

- **工具**：Locus
- **Prompt that mattered**：1，lab b Prototype Card 目标设定为按下 ctrl 键切换角色这个事情是否符合认知。2，同步进 gdd。请继续
- **做了什么**：diff 脚本 v1→v2（新增受击台词、低血量台词、玩家死亡/Game Over 段）；确认 6 条实现决策（显示留空格/输入跳空格、只用 toggle、ZOOM 多语义先做过场推镜、提供 skip、失败态 BAD TAKE / THE LEAD IS DEAD. / RETAKE、主战斗总数递减到 0）；把节拍表与决策写入设计源第 14/15 节并重新生成 `GDD.md`；填好 Prototype Card 的 Question（其余为待作者确认的草案）
- **What broke**：无。待解决：脚本仍写 `HOLD CTRL` 与 toggle 决策不一致（需改提示文案）；`user.email` 未配置，尚无法提交
- **时间**：约 1 小时

## Oct 7 — 会话 4：B1 基础件（统计 / 调参面板 / 遥测）

- **工具**：Locus
- **Prompt that mattered**：请继续（在同步 GDD 之后继续实现）
- **做了什么**：
  - 新增 `Assets/Scripts/Demo/RunStats.cs`（单局统计：词数/字母数/失误/击杀/准确率/WPM；结算屏与遥测共用同一套埋点）
  - 新增 `Assets/Scripts/LabB/LabBTuning.cs`（**18 个**运行时参数，F1 面板，含 Variant 1/2 切换与两个 juice 开关）
  - 新增 `Assets/Scripts/LabB/LabBTelemetry.cs`（`run_start` / `run_end{duration,score,cause,failWord,failX,failY,words,letters,mistypes,kills,accuracy,wpm}` / `retry{latency}` / `skip_intro` / `track_switch{from,to,effective}` / `variant_switch`；T 键导出 JSON 到游戏目录旁的 `telemetry/` 与持久化目录；角落一行摘要：局数/中位时长/重开率）
  - `TypingTarget` 接入面板数值（approachSpeed / rushSpeed / attackRange / attackCountdown / attackDamage 五个参数立即生效）
  - 三个组件挂到 `SampleScene` 的 TrackInputDebugger 并保存场景
- **验证**：编译通过；Play Mode 冒烟测试——面板渲染正常、遥测导出成功（`telemetry_SampleScene_20261007_140906.json` 结构正确，已归档到 `Library/Locus/tmp/`）、Console 无 warn/error
- **What broke**：首次编译失败 `CS0104`（`System.Object` 与 `UnityEngine.Object` 歧义）→ 用 `UnityEngine.Object` 全限定名修复；另发现两个体验问题：既有调试 OnGUI（词表/日志/轨道状态）叠满屏幕、调参面板默认展开会遮挡画面，两者都需在 Demo 测试前加开关
- **时间**：约 1.5 小时

## Oct 7 — 会话 5：新关卡「POLE POSITION」可玩闭环（Demo 脚本第 1–9 段）

- **工具**：Locus（Unity 工程内 AI 助手）
- **Prompt that mattered**：重新阅读 `DEMO脚本.pdf`，开设一个新关卡来实现脚本内容；先告诉我打算怎么做、做什么 → 范围选择「先做可玩闭环」（节拍 1–7 + 收尾 + PRINT IT! 结算屏）
- **做了什么**：
  - 新场景 `Assets/Scenes/PolePosition.unity`（`SampleScene` 保持不动），由 Editor 生成器 `Assets/Editor/PolePositionSceneBuilder.cs` 一键重建：灰盒俱乐部（舞台/黄铜钢管/霓虹灯管/卡座/吧台）+ 4 个机位 + 系统与演出组件接线 + 加入 Build Settings 首位；另有 `Validate` 校验入口（枚举全部序列化引用报空）
  - 演出层新脚本：`PolePositionFlow`（12 节拍状态机）、`DemoHud`（黑屏淡入/中央提示逐字母/COMBAT START 横幅/对白字幕/轨道徽章/PRINT IT! 结算屏）、`DemoDialogue`（对白+停顿，支持 skip）、`ScriptedEncounter`（4 固定词波次 + 递减主战斗）、`StripperZombieEntrance`（冲入抱杆演出）、`ViewfinderOverlay`（导演取景框，承接 Variant「取景框开/关」）
  - 改底层：词条**显示保留空格**（`TypingWord.BuildDisplayRichText`，只有字母参与进度）、`TypingController` 输入门（TYPE: ZOOM）、`TrackInput` 切轨收窄为左右 Ctrl + 字母/切轨开关 + 自检注入通道、`TypingTarget.Frozen`、`DirectorStage.SetTricksEnabled`、`RailCameraController` 可变推镜时长
  - 自检：`PolePositionAutoDrive`（自动打完整关并逐项校验）+ `Assets/Editor/PolePositionSelfTestRunner.cs`（批处理跑 + 3D 截图）
- **验证**：批处理自检 **10 pass / 0 fail**（ZOOM 门→推镜机位 1→Ctrl 切轨门→wave1 出现→主战斗清零→词序 mine/my pole/get off/wait your turn→PRINT IT! 结算屏→kills 12 = 4+8→存活）；结算数字 accuracy 100% / words 12 / wpm 32 / kills 12 / mistypes 0；4 张渲染截图人工核对（俱乐部、钢管、抱杆+词条、血浆糊屏）
- **What broke（已修）**：① 批处理无物理键盘，合成设备事件不被 `wasPressedThisFrame` 采纳 → 自检自动降级到注入通道（真实按键路径仍需人工确认）② `EnterPlaymode` 前注册的 `EditorApplication.update` 收尾回调被域重载清空，导致自检跑完却挂住 15 分钟 → 改为驱动器自己退出 ③ 地板发亮：高光滑度面被强附加光的镜面反射洗白（与 albedo 无关）→ 灰盒改哑光、聚光灯 14→8 ④ 血浆层整屏 0.5 alpha 盖 1.2s，而 Demo 无擦镜节拍 → 本关实例降到 0.32 / 0.75s ⑤ 两次把 Unity 挂在长生命周期异步任务上，取消任务连带杀了编辑器进程 → 改批处理短进程
- **时间**：约 3 小时

## Oct 7 — 会话 6：词条遮挡与同首字母词条冲突修复

- **工具**：Locus（编辑器内实机验证，未重建场景以免覆盖手工调整）
- **Prompt that mattered**：① 怪物身上的单词会被中心的钢管挡住，请让单词永远显示在最上层；② `my pole` 与 `my turn` 同时刷新时，玩家想打 `my turn` 却会因为首字母吸附锁到 `my pole`，希望同首字母的单词不会同时出现
- **做了什么**：
  - 新增 `Assets/Art/Shaders/UIAlwaysOnTop.shader`（同 UI/Default 但 `ZTest Always` + `Queue=Overlay`）与材质 `Assets/Art/Shaders/M_WordLabelOnTop.mat`；接到 `WordLabelManager.alwaysOnTopMaterial` → `WordLabel` 的 Text（场景已保存）；词条再加黑色描边，压在亮黄铜/氖灯上也能看清
  - `TypingController.HasActiveWordStartingWith` + `ScriptedEncounter.TryTakeCompatibleWord`：主战斗刷怪时跳过与场上活动词条**首字母冲突**的词；全部冲突则等场上词条打完再刷（不消耗主战斗总数）
  - 自检新增断言：全程不得出现两个同首字母活动词条；并根据几何/屏幕位置自动截“词条位于钢管之后”的图
- **验证**：编辑器内实机自检 **11 pass / 0 fail**（这次走的是**真实 InputSystem 设备事件路径**，不是注入通道）；受控 A/B 截图（同一帧、同一位置，屏幕横向重叠 dX=0px）：默认材质下词条几乎被钢管吞掉 → 换新材质后完整可见
- **What broke（已修）**：`unity_hot_reload` 报成功但 `WordLabel.Init` 新增的描边没生效（运行时对象上没有 Outline）→ 用 `unity_recompile` + 域重载复核才生效；批处理下合成设备事件仍不被采纳（仍由注入通道兜底）
- **时间**：约 1 小时

## Oct 7/8 — 会话 7：字色改白 + CapsLock 切轨 + 大写 ZOOM

- **工具**：Locus（编辑器内实机验证）
- **Prompt that mattered**：① 怪物身上的字体是黑色、与背景区别不大，请换成白色；② 我原来的设计是按 CapsLock 切换角色而不是 Ctrl（小写词=特工、大写词=导演，要大写键对应大写词）；③ 开场的 zoom 改成大写 ZOOM
- **做了什么**：
  - 词条配色改为「未打=白 / 当前待打=黄 / 已完成=绿」（红/蓝在暗背景下不清晰，作者定）；4 向粗黑描边改成 1px 细投影
  - **修真因**：`Assets/Art/Shaders/UIAlwaysOnTop.shader` 补上 `float4 _TextureSampleAdd` 并在采样后相加——字体图集是 Alpha8（RGB=0、A=覆盖度），内置 UI shader 靠它把 RGB 补成白，漏了就会把字形渲成黑色
  - 切轨键改为 **CapsLock**（`TrackInput`；Ctrl / Shift 降为可选开关，默认关），提示文案改 `TAP CAPS`；自检驱动器也改用 CapsLock 注入
  - 开场 `TYPE: ZOOM` 门改走导演轨 → 显示**大写 ZOOM**（`TypingController.SpawnGate` 增加 track 参数）
  - 取景框“16MM · SAFE FRAME”标签从左上移到右上（原本与 HP 文字重叠）
- **验证**：编辑器内实机自检 **11 pass / 0 fail**（走真实 InputSystem 设备事件路径，含 `switch key (CapsLock) toggled to AGENT track`）；像素测量：词条区域近白像素 **391** vs 默认材质 **392**（字色已与内置 UI 材质一致，不再是黑字）；A/B 同帧截图：默认材质下词条被钢管吞掉 → 新材质下画在钢管之上；Game 视口截图核对布局与 `TYPE: ZOOM` 大写
- **What broke（已修）**：① 先误判“字黑”是描边太粗导致（实际是 shader 缺 `_TextureSampleAdd`，形状/Alpha 都对所以很像“颜色没设对”）② 用 `unity_execute` 写紧扁的 await 轮询去等游戏内事件不可靠：35s 真实时间只推进 ~4s 游戏时间，看起来像演出卡死 → 改为让游戏内驱动器自己跑，外部只用 bash sleep + 短调用读状态
- **时间**：约 1.5 小时

## Oct 8 — 会话 8：修复“局内无法切回导演轨”

- **工具**：Locus（编辑器内实机验证）
- **Prompt that mattered**：局内似乎没有持续检测按下 CapsLock 的机制，切到特工后怎么按都不会切回导演；这事不要写死，之后会有频繁局内切换角色的设计
- **根因**：`PolePositionFlow.FinishOpening()` 在教学门之后调了 `trackInput.SetSwitchInput(false)`，之后再也没人重新打开 → 切轨输入整局被永久关闭（不是按键检测的问题：开场那一次切换能成功，正说明检测是好的）
- **做了什么**：切轨权限改为**按阶段声明**：开场教学门前锁定 → 教学门之后全程可用 → 只在非交互节拍（收尾对白 / 死亡 / 结算）关闭；设计源与 `GDD.md` §4.2 补上这条策略，明写“为后续局内频繁切角色留机制”
- **验证**：新增 3 条自检断言并实机跑通 **14 pass / 0 fail**（真实 InputSystem 设备事件路径）：`switch input stays enabled after the opening gate`、`mid-combat CapsLock switched to DIRECTOR`、`mid-combat CapsLock switched back to AGENT`；切轨不产生多余字母（accuracy 100% / mistypes 0）
- **What broke**：无新问题；本次是上一轮为一个教学门不被提前跳过而强关切轨引入的回归，已由自检断言防住
- **时间**：约 0.5 小时

## Oct 8 — 会话 9：修复“多只怪词条重叠”（车道 + 同点不叠怪）

- **工具**：Locus（批处理自检验证；GUI 编辑器在验证中途被关，改用批处理）
- **Prompt that mattered**：有些波次怪物会从同一个刷新点刷好几只，单词重叠难读；想法 ① 同一点上只死了才刷下一个 ② 后方怪物单词颜色更深/透明度更低
- **做了什么**：
  - 采纳想法 ① 并补上更深一层的原因：`TypingTarget` 原本让每只怪都朝**相机位置本身**移动，不同刷怪点的怪会横向收敛到同一点（几秒后必然叠在一起）。现改为只在水平面朝“车道点”（`laneOffset`，沿相机右方向）接近：不同怪守不同车道，不再收敛；顺带修掉“怪会飘到相机高度”
  - `ScriptedEncounter`：每个刷怪点同一时刻只养一只（死亡才释放），波次与主战斗都从“空点”刷；车道与刷新点一一对应（-2.6 / +2.6 / 0）
  - 未做想法 ②（按距离降透明度）：车道+同点占用已消除重叠成因，且降透明度与“字要清晰”相冲；留作备选
- **验证**：批处理自检 **16 pass / 0 fail**；新增两条断言：`no two word labels ever overlapped on screen`（按相机投影算词条包围盒）与 `test actually exercised two-enemies-at-once (max alive=2)`；截图 `pp_06_two_alive_a.png` 里抱杆怪的 `mine` 与右车道的 `back off` 分离可读
- **What broke**：发现上一条“词条不重叠”断言是**空跑**的——自检驱动器每秒一只枪毙，同屏从未达到 2 只（新加的 max alive 证据断言把它暴露出来）→ 给驱动器加人类节奏（主战斗每词后 2.6s 停顿）才真正压到并发场景
- **时间**：约 1.5 小时

## Oct 10 — 会话 10：接入配音（23 条 VO）

- **工具**：Locus + dotnet/NAudio（转码）+ 批处理自检
- **Prompt that mattered**：我找了一个配音，已按一句一个 m4a 存好在 `D:\UnityProject\VoiceLines`，告诉我需要怎么配合（例如怎么命名）→ 你先把这些台词加进去
- **做了什么**：
  - **格式关**：实测 Unity 6.3 **不导入 m4a**（23/23 NOT IMPORTED）。本机无任何转码器，于是在 `Library/Locus/tmp/vo-convert` 建了一个 dotnet + NAudio 小工具（走 Windows Media Foundation）批量转 **44.1kHz/16bit/单声道 WAV**，23/23 成功
  - **素材处理**（原作者文件保留在 `D:\UnityProject\VoiceLines`，原始转码件备份在 `Library/Locus/tmp/vo-raw`）：每条剪棂首尾静音（原本普遍有 0.9–1.5s 首静音，“No.” 实际只有 0.85s 说话）+ 响度统一到 −20dBFS RMS / 峰值≤−3dBFS（原峰值散布 −26.6…−3.4dBFS，增益 +0.4…+23.6dB）
  - **接入层**：新增 `VoLibrary`（id→AudioClip 映射资产）+ `DemoAudioDirector`（VO/SFX/BGM 三条音源 + 音量）；`DemoLine` 加 `id`；对白推进改为**以音频长度为准**（字幕在音频前 55% 内铺完），无音频时退回原子幕计时；`LabBTuning` 加 `voVolume/bgmVolume/sfxVolume`
  - **工具链**：`Assets/Editor/PolePositionVoiceTools.cs`（菜单 YourMovieSucks/Voice/0–3、9）：检查导入 → 配导入设置（mono/DecompressOnLoad/Vorbis0.7）→ 建 VoLibrary + 接线（写 id、指音源、存场景），并接入场景生成器
- **验证**：批处理自检 **18 pass / 0 fail**：`VO library: 23 clip(s)`、`all 13 scripted dialogue VO clips are wired`、`VO played during run: 13`（`every scripted dialogue line actually played its VO (13/13)`）；整关时长 35s → 52.5s，正是音频驱动的对白节奏
- **What broke**：`voLibrary` 写进场景后总是变 null。根因：`AssetDatabase.LoadAssetAtPath` 拿到的资产在随后 `EditorSceneManager.OpenScene` 时被当作**未引用资源卸载**，赋值实际赋了一个已销毁对象 → 序列化为 null（无异常、静默）。修法：**打开场景之后重新取一次资产引用**（同类坑已记入 memory）
- **待做**：bark（受击/低血量）与死亡台词的**节拍机制**还没做（音频已在 VoLibrary 里备好）；SFX/BGM 素材到位后接 `sfx_door_kick` / `sfx_roar` / `sfx_last_shot` / `bgm_combat` / `amb_neon`
- **时间**：约 2 小时

## Oct 10 — 会话 11：逐条音频参数 + 调节窗口（速度/延迟/音量/偏移）

- **工具**：Locus（用作者已打开的编辑器做编译/接线/自检；批处理因工程锁被挡）
- **Prompt that mattered**：我需要在 Unity 里做调整，例如加快音频播放速度、让音频 delay 几秒再播，你能做一个编辑器或别的办法吗
- **做了什么**：
  - 新增 `VoLineSettings`（手调资产）：逐条 `speed / delaySeconds / volume / startOffsetSeconds`；与 `VoLibrary` 分开，重建库不会覆盖手调值
  - `DemoAudioDirector`：按逐条参数应用（延迟播放协程、`pitch` 变速、`startOffset` 跳开头、逐条音量×全局音量）；新增 `GetEffectiveDuration()`（延迟 + (长度−偏移)/速度 + 尾巴）供对白与窗口共用；**Play 中正在播的那条会即时跟随速度/音量改动**
  - `DemoDialogue`：字幕等完逐条延迟再开始逐字铺开（延迟期间收起上一句）
  - 新增编辑器窗口 `YourMovieSucks/Voice Tuner (POLE POSITION)`：列表化滑杆（速度/延迟/音量/偏移）+ 实时“生效时长”列 + 搜索/全部重置/停止试听 + ▶（编辑器原速试听，内部预览接口）+ ▶Play（Play 模式下按真实参数播放）+ 一键“重建库/接线”
  - 工具体与场景生成器都会创建并接线 `VoLineSettings_PolePosition.asset`
- **验证**：实机自检 **19 pass / 0 fail**，新增断言 `per-line speed/delay feed the pacing formula (expected 2.88s, got 2.88s)`（临时设 speed=2/delay=0.5 验证后还原）；`VO played during run: 13`；窗口截图核对（23 条全部列出，列与按钮完整）
- **What broke / 已知限制**：① 批处理被“工程已被另一个 Unity 实例打开”挡住 → 改用作者已打开的编辑器完成编译/接线/自检 ② Unity 原生无“变速不变调”：速度用 `pitch` 实现，**加快会同时变调**（UI 提示与文档已写明；要变速不变调需离线时间伸缩）
- **时间**：约 1 小时

## Oct 10 — 会话 12：BGM（Show Me Love）+ 打字/切轨音效

- **工具**：Locus + dotnet/NAudio（mp4→mp3）+ 已在打开的编辑器内实机自检
- **Prompt that mattered**：① `SFX\BGM\BGM_Show Me Love.mp4` 作为主 BGM，从开场黑幕起到 PRINT IT! 结算屏前；对话时压低、结束恢复 ② `SFX\TypingSFX` 里的打字音效：按 CapsLock 切轨用 `eklee-KeyPressOldEffect01`，打字 a–z 从其他音效里随机
- **做了什么**：
  - BGM：mp4 音频轨 → **MP3 192k 立体声**（`Assets/Audio/BGM/bgm_show_me_love.mp3`，4:00）；导入设为 Streaming + 保立体声（VO/音效仍是 mono+DecompressOnLoad）
  - 打字音效：拷入 `Assets/Audio/SFX/Typing/`；因每个都是 1.0s（约 0.6s 是尾音，快打会叠成糊音）→ 剪到 0.6s + 20ms 淡出（原件备份在 `Library/Locus/tmp/sfx-raw`）
  - 新增 `TypingSfxPlayer`：按 a–z（任意轨道、含错键）从 6 条候选池随机播（最近 2 条不重复 + 30ms 最小间隔）；切轨键（CapsLock）单独播专用音效，**演出层强制设轨不出声**
  - `DemoAudioDirector`：BGM 闪避（对话时 ×0.35，2.5/s 平滑过渡）；`PolePositionFlow` 开场黑幕起 BGM、CUT TO BLACK 时停
  - 工具体：递归扫子目录（修了一个“只拼文件名、丢掉子目录”导致音频被当未导入的 bug）；建库前先 `AssetDatabase.Refresh()`；自动把打字候选池接进 `TypingSfxPlayer`
  - 混音：作者 BGM 母带很热（实测 RMS **−8.3dB**，比配音 −20dBFS 响 12dB）→ 场景里 `bgmVolume` 0.6→**0.22**、`sfxVolume` 0.8→**0.5**（voVolume 1）
- **验证**：实机自检 **27 pass / 0 fail**，音频相关：`BGM playing from the opening black screen`、`BGM stopped before the results screen`、`duck factor actually dropped during dialogue (min=0.35)`、`BGM ducked during the closing dialogue (min=0.077 vs baseline=0.182)`、`typing SFX actually played on keystrokes (26 > 20)`、`track-switch SFX played on CapsLock (3 >= 2)`；库共 31 条（23 VO + 1 BGM + 7 打字）
- **What broke（已修）**：① 子文件夹里的音频取不到 clip（资产路径丢子目录）② 音频刚拷入未导入就建库 ③ 自检的“基准音量”采到了闪避恢复的过渡中间值 → 等过渡跑完再采样（**功能本身没问题**，是测试写错）④ 场景里 `LabBTuning` 仍是旧序列化默认值 0.6/0.8（**已存场景的序列化值会覆盖新代码默认值**，本次已手动改成 0.22/0.5 并保存）
- **时间**：约 2 小时

## Oct 10 — 会话 13：打字音效换成 typewriter1–8

- **工具**：Locus（已在打开的编辑器内实机自检）
- **Prompt that mattered**：打字音效不好，找了新的替换：打字从 typewriter1–7 随机，CapsLock 用 typewriter8
- **做了什么**：
  - 旧的 7 个 `eklee-*` 移出工程（归档到 `Library/Locus/tmp/sfx-old-eklee/`）；`typewriter1..8.wav` 拷入 `Assets/Audio/SFX/Typing/`
  - `TypingSfxPlayer.trackSwitchSfxId` → **typewriter8**；工具体 `SwitchSfxId` → typewriter8（候选池自动变成 typewriter1..7，切轨音不入池）
  - 混音：`sfxVolume` 0.5 → **0.8**（新素材峰值 −3~−8dBFS，比旧的 −0.2dB 轻很多）；重建库（31 → **32** 条）并重接场景
  - 新素材**不需处理**：0.28–0.69s、无长死尾、峰值散布仅 5dB（旧素材是 1.0s + 0.6s 尾音）
- **验证**：实机自检 **29 pass / 0 fail**：`typing SFX pool = typewriter1..7 (size=7)`、`track-switch SFX = typewriter8`、`typing SFX actually played on keystrokes (28 > 20)`、`track-switch SFX played on CapsLock (3 >= 2)`、`typing SFX never picks the switch sound (last=typewriter3)`；BGM/闪避/配音断言均保持通过
- **What broke**：无新问题（唯一的坑已记入 memory：**换素材必须把旧文件从工程里删掉**，否则重建库时旧条目仍在，会把旧声音重新拉进随机池）
- **时间**：约 0.5 小时

## Oct 10 — 会话 14：每次击键叠一声随机枪声

- **工具**：Locus（已在打开的编辑器内实机自检）
- **Prompt that mattered**：新加入两个枪声 `cg1.wav` / `pistol.wav`，能不能在播放每个打字音效的同时，也随机从这两个里播一个
- **做了什么**：
  - 两个枪声放入 **独立目录** `Assets/Audio/SFX/Gunshot/`（不能进 Typing 目录，否则会被当成打字音进入随机池）；原件备份在 `Library/Locus/tmp/sfx-raw-gun/`
  - 素材处理：两者**峰值归一到 −6dBFS**（原始峰值 0.0 / −0.3dBFS、RMS −12/−13dB，比打字音高 13–16dB）；`pistol` 尾巴剪到 **0.65s** + 25ms 淡出（原 0.99s，快打会糊）
  - `TypingSfxPlayer` 新增枪声层：`gunshotSfxIds` 池 + `gunshotVolumeScale`，**一次击键 = 打字音 + 随机枪声**（同源 30ms 最小间隔门控，枪声不占打字池）
  - `LabBTuning` 新增 `gunshotVolume`（F1 面板可调）；工具体新增 `ScanSfxIds()`，同时扫描 Typing 与 Gunshot 两个目录（重建库 32 → **34** 条）
- **验证**：实机自检 **31 pass / 0 fail**：`gunshot pool wired (size=2)`、`typing SFX played: 27 / gunshots: 27 (last=pistol)`、`gunshot layered on every keystroke (27, last=pistol)`；BGM/闪避/配音断言均保持通过
- **What broke**：无
- **时间**：约 0.5 小时
## Oct 11 — 会话 15：丧尸模型替换方块敌人（Scary Zombie Pack）

- **工具**：Locus（编辑器未开，全部走短命批处理 Unity.exe -batchmode）
- **Prompt that mattered**：我目前在 D:/UnityProject/Scary Zombie Pack 放了一个怪物模型和一整套配套动画，请检查这些模型和动画是否符合规范，如果符合就先导入工程，然后把原本的方块敌人替换成这个丧尸模型
- **做了什么**：
  - **素材体检**（纯 Python 解二进制 FBX，未进 Unity）：13 个 fbx 都是 v7700 二进制；Zombie.fbx = 33 骨骼 mixamorig 人形骨架 + 1 个蒙皮网格（436 顶点 / 单材质槽 / 有 UV 与法线、无切线）；12 段动画 fbx **只含骨架**（无网格无材质，体积小）；贴图 512×512 RGB 基础色（无 normal/ORM）；单位 cm → 高约 1.76 m；脚踝→脚趾指向 +Z（正面朝 +Z）
  - **两处必须处理的不合规**：① 12 段动画都**带位移**（Hips 在片段里前进 1.26–2.35 m，不是原地）→ 导入时把 XZ 从姿势里摘成根运动、Animator 关 Apply Root Motion，视觉上原地迈步，位移仍归 TypingTarget 的车道/速度；② 12 个片段的 take 名全叫 mixamo.com → 导入时重命名为 Zombie_Idle/Walk/… 并按动作性质设循环
  - 素材入工程：Assets/Art/Monsters/Zombie/（模型 + Animations/ 12 段 + Textures/）
  - 新增 Assets/Editor/ZombieAssetBuilder.cs（菜单 YourMovieSucks/Monsters/0·1·2，全程幂等）+ Assets/Editor/ZombieEnemyVerify.cs（无人值守跑分 + 怪物状态探针）
  - 新增运行期 Assets/Scripts/Combat/TypingTargetAnimator.cs（按怪物状态驱动站立/走/跑/攻击/死亡）
  - ScriptedEncounter 改为刷 Assets/Prefabs/StripperZombie.prefab（出生朝向玩家；未接线自动退回方块）；TypingTarget 支持子物体 Renderer + 可配置击杀延迟（灰盒行为不变）；WordLabel 标签高度改按目标包围盒算（方块仍是旧的 1.7）
  - 场景：ScriptedEncounter.zombiePrefab 接线 + 5 个刷怪锚点 y 0.9 → **0.5**（丧尸预制件原点在脚底 = 舞台台面；方块的原点在身体中心）；场景生成器同步改，重建不会回退
- **验证**：素材报告（avatar valid/human、12 片段时长与循环、材质贴图、Animator 5 状态→片段映射、预制件引用）+ 无人值守跑分 **31 pass / 0 fail**（results: accuracy=100% words=12 kills=12 duration=56.3s）+ 探针：动画确实驱动网格（hips本地Z 在 ±0.16 m 内 → 原地；hips 世界 Y 1.31–1.52 → 脚踩台面）+ 自检截图 6 张目视核对（抱杆/行走姿态、贴图、词条高度）
- **What broke**：第一版预制件把 Animator 挂在**外层根节点**上 → 人形片段靠 avatar 重定向，骨骼路径多一节 → 表现是「Animator 状态在走、模型永远是 T-pose」（截图看出来才发现）。已记入 memory。另：批处理无 vsync 会跑到约 1134fps，打字音效 0.03s 真实时间节流被打穿 → 音效计数 12 会误报 2 条 FAIL（探针限帧 60 后 99 → 全通过）
- **时间**：约 1.5 小时

## Oct 11 — 会话 16：怪物放大 + 贴脸攻击距离

- **工具**：Locus（编辑器已连上，全部实机改 + 实机验证）
- **Prompt that mattered**：把怪物的模型放的更大（现在看起来很小没有冲击力）；让怪物在更近的距离攻击玩家，近到几乎只能看到上半身
- **做了什么**：
  - 模型放大：预制件 `ZombieModel` 统一缩放 **1.25**（体高 1.76 → **2.20 m**，脚底仍在预制件原点），由 `ZombieAssetBuilder.ModelScale` 生成，重建不会再丢
  - 攻击距离：`TypingTarget.attackRange` 4 → **1.7 m**（预制件序列化值；同时改 `LabBTuning.attackRange` 默认值与**场景里**的 F1 面板值 —— 面板值存在场景，改代码默认值对已存在场景无效）
  - 近身车道收敛：新增 `laneConvergeStart=6` / `closeLaneOffset=0.85`。远处仍守 ±2.6 宽车道（词条不叠），进入 6 m 后按前向距离线性收到 ±0.85。**不收窄的话横向 2.6 m 在 1.7 m 距离已经是 61°，怪会停在画面外打你**
  - **修掉两个既有问题（都是把攻击距离改小才暴露出来的）**：
    - `RequestAttack()` 被拒（一次只准一只攻击）之后怪不会停下，会一路 `MoveTowards` 走到车道点 = 走进/穿过相机。旧 4 m 参数下它站在画面外所以看不出来
    - 改成「到攻击距离就站住」后仍有横向残差：怪追的是一个**会跟着收窄的目标**，最后一段是斜着追上去的，停下时实测停在横向 1.84 m ≈ **53°**（贴到画面边上）。最终改成显式的**攻击环站位点**（离相机 attackRange、横向 lane），到站即停，无残差
  - 实机复核：怪最终停在 (-0.93, 0.50, 6.73)（横向 0.93 ≈ 31°，画面内）、`state=Attacking`、`动画=[Attack]`；整局 11 次采样到 Attack 状态（改之前这一整局 0 次 —— 旧参数下自动驾驶杀得太快，攻击动画根本轮不到）
- **验证**：
  - Play 模式定点取景（相机固定 WP2，怪放攻击环上）：脚 **viewport y=-0.52**（出画）、头顶 **y=0.66**（画面上 2/3）、两只横向 x=0.24 / 0.76 都在画面内，词条全可见且不重叠 → 就是「只看得见上半身」那条线
  - 编辑器内跑完整关自检 **31 pass / 0 fail**（`no two word labels ever overlapped on screen` 在收窄车道后仍通过；`results: accuracy=100% words=12 kills=12 duration=59.2s`）
  - 数值核对：预制件模型包围盒 `(-0.77, 0.00, -0.30) → (0.76, 2.20, 0.19)`，高 2.20 m、脚底 y=0.00
- **What broke**：无新问题。但发现自检里 `BGM ducked during the closing dialogue` 这条断言**本来就是擦边**：日志显示 `ducking ON @ 18.99s` 紧接着取基准（0.128），基准是在闪避斜坡中间采到的（正常应 0.22），阈值 `minBgm < baseline*0.6` 只差 0.3%。两次同环境跑：一次 FAIL（0.077 vs 0.0768）、一次 PASS（0.077 vs 0.0774）。与本次改动无关（发生在开场对白期，怪还没出现）。建议改成「等闪避回到 1 再取基准」
- **时间**：约 1 小时

## Oct 12 — 会话 17：词条不再被血条挡住 + 近身字号减半

- **工具**：Locus（编辑器内实机改 + 实机量屏幕坐标验证）
- **Prompt that mattered**：怪贴脸攻击时，从画面左边刷出的怪头顶单词会被左上血条挡住；而且靠近时字体跟着放大，最近处大到难以辨认——请把最大的字体大小调小至目前的一半
- **做了什么**（全在 `WordLabel`，词条是世界空间画布，靠近时表观大小按透视自然放大，所以要做两件事）：
  - **近身限尺**：新增 `closeReferenceDistance=2.0` / `closeScaleFactor=0.5`。表观尺寸封顶：距离 ≥2.0 m 完全不变（远处照样好读），比它近就按比例缩小，最近处 = **目前的 0.5 倍**（实测贴脸时 localScale 0.0030 vs 基准 0.0050）
  - **屏幕安全区**：新增 `safeMin/safeMax=(0.10,0.10)/(0.90,0.82)`，把词条（按**文字实际尺寸**夹，不是 400×100 画布）夹回安全框。左上血条块是 IMGUI 固定像素（30,18 520×52 / 30,80 420×40），视口占位随窗口大小变：1544×837 时下沿 0.857、1024×768 时 0.844 → 安全区上边取 0.82
  - **顺带修掉词条高度不稳**：原来用 `SkinnedMeshRenderer.bounds` 算“头顶高度”，但它是「局部盒 × 根骨骼」，会随姿势漂（同一只怪实测 2.12–2.70 m），且 `localToWorldMatrix` 对骨骼网格无效（顶点由骨骼矩阵变换）。改成用绑定姿势的 `localBounds.size.y` × 根骨骼缩放，再与实时包围盒取最大值
- **验证**（Play 模式把相机固定 WP2、左右各放一只贴脸 + 挂词条）：
  - 血条块下沿视口 y=0.857；词条文字框 y=[0.74,0.82] → **不再被血条压住**，且贴在怪物可见最高点（0.739）之上
  - 字号：贴脸时 scale 比例 **0.60**（4 m 外仍是 1.00，不受影响）
  - 完整关自检 **31 pass / 0 fail**（`no two word labels ever overlapped on screen` 保持通过；`results: accuracy=100% words=12 kills=12 duration=59.2s`）
- **What broke**：无。调试期间在内存里改过 AutoDrive 开关，已从磁盘重载场景复原（`autoStartOnPlay=false`、面板 `attackRange=1.7`）
- **时间**：约 1 小时

## Oct 12 — 会话 18：修「贴脸后突然回退 + 卡在姿势 + 站着掉血」

- **工具**：Locus（编辑器内实机复现 + 量化验证）
- **Prompt that mattered**：怪物到最近距离攻击第一次后，模型会突然位置回退，然后卡死在当前姿势；虽然没有攻击动作，但会造成伤害，请修正
- **复现与定位**（把探针扩成记录“模型子物体/Animator/HP/镜头”，跑真实流程 + 可控取景台）：
  - **突然回退**：攻击环 = `相机 + 前方 1.7 m`，而战斗机位是慢推的（WP1 z=4.0 → WP2 z=5.2）。镜头往前推 1.2 m，环也跟着前移 1.2 m → 怪只能后退 1.2 m。它此时在**攻击态**（Speed=0 → 播站立），所以看起来就是「卡在姿势里往后滑」
  - **没有攻击动作却造成伤害**：伤害是 `attackCountdown`（1.5 s）计时器每轮结算，而攻击动画只在 `StartAttack`（第一次进攻击态）触发一次 → 第一下之后就一直站着掉血
- **修了什么**：
  - `TypingTarget`：到站**上锁**（`_arrived`）——站住不动，镜头推进不再让它倒退；只有镜头贴到 `keepClearDistance=0.9 m` 内才让一点（避免相机穿进模型），离超过 `attackRange×3` 才解除锁重新接近
  - `TypingTargetAnimator`：新增订阅 `OnAttackHit` → 每轮伤害都 `SetTrigger("Attack")`（伤害落在挥击中段，手感更对）；动画速度改为跟随**实际位移**（`> Time.deltaTime * 0.05`），侧滑时不会再摆站立姿势
  - `Zombie.controller`：`AnyState → Attack` 改为 `canTransitionToSelf = true`（片段 2.6 s > 伤害间隔 1.5 s，不允许自转移的话第二轮触发会被吞掉）
- **验证**：
  - 可控复现：把怪放在攻击环（相机 z=4.0）→ 手动把相机推到 WP2（z=5.2，即战斗慢推的 1.2 m）→ 两只怪 z **保持 5.68 不动**（修前会退到 6.9），攻击方动画在 t=0.08→0.45 之间反复重启（正好每 1.5 s 一轮）
  - 真实流程（accelerate 到攻击环）：位置恒为 (-0.99,0.50,6.73)/(1.00,0.50,6.74)，动画 `Attack` 在 0.08→0.58 后回到 0.16（重启），并伴随 `根scale=1.50` 的挥击缩放
  - 完整关自检：**30 pass / 1 fail**（唯一 FAIL 是既有的 `BGM ducked during the closing dialogue` 擦边断言，三次跑 baseline=0.110/0.128/0.129，与本改动无关；`no two word labels ever overlapped`、`player survived`、`accuracy=100% words=12 kills=12` 均通过）
- **What broke**：调试期间在内存里改过 `approachSpeed/attackDamage`，已从磁盘重载场景复原（`approachSpeed=0.5 attackDamage=35 autoStart=false`）
- **时间**：约 1.5 小时

## Oct 12 — 会话 19：修「怪物位置回退」的真正来源：CameraShake 抢相机位置

- **工具**：Locus（编辑器内复现 + 隔离测试）
- **Prompt that mattered**：动画卡顿修好了，但怪物仍然有位置回退；怪物到达的最靠近玩家的位置跟它发起攻击的位置不一样，请修正
- **排查过程**（把探针扩成记录“相机 z / 距离 / 模型子物体 / Animator / HP”）：
  - 先排除了「到站后追攻击环」：加上 `_arrived` 上锁后，无论怎么推镜头，怪的位置都恒定不变（实测恒为 5.68 / 6.73）
  - 又排除了我上一轮加的 `keepClearDistance` 退让：它用的是**三维**距离，而相机比怪的脚高 1.6 m → 三维距离恒 ≥1.6 m，永远小于不了 0.9 → 纯死代码（已删）
  - 真正来源：`CameraShake` 与轨道镜头**抢相机位置所有权**。旧实现是「晃开始时捕获一次基准 → 期间每帧写 基准+随机偏移 → 结束写回基准」。连续击键/连续受击时基准只在“安静 >0.25 s”的空档刷新，一旦基准过期：晃动期间轨道镜头的推进被覆盖（相机被钉在旧位置），晃动结束那一帧又把相机**拽回旧位置** → 整个画面（包括已经贴脸的怪）看起来就是「突然位置回退」，而怪自己其实一动没动
- **修了什么**：
  - `CameraShake`：改为只施加**临时偏移** —— `LateUpdate` 里先撤销上一帧的偏移、再加新的，**永不写回基准**；`OnDisable` 也会把偏移撤掉。相机位置的所有权明确归 `RailCameraController`
  - `TypingTarget`：删掉那条永远不触发的 `keepClearDistance` 退让分支（到站即站住，不做任何让位）
- **验证**：
  - 隔离测试：晃动期间手动把相机推进 1 m → 晃动结束后相机停在 5.000（“推进后”是 4.990，差 0.010 m）＝晃动没有篡改相机；旧实现会跳回 4.000（差 −1.000）
  - 完整关自检（驱动器连续打字 + 每次击杀/受击触发晃动）：相机 z 在战斗段**严格单调** 4.01 → 5.20（无任何倒退跳变），收尾段按设计退回 WP3（4.2）
  - 自检 **30 pass / 1 fail**：唯一 FAIL 仍是既有的擦边断言 `BGM ducked during the closing dialogue`（baseline 采在闪避斜坡中间 0.110 vs 阈值 0.066），与本改动无关；标签不重叠、玩家存活、`accuracy=100% words=12 kills=12` 均通过
- **What broke**：无（场景已从磁盘重载复原）
- **时间**：约 1.5 小时

## Oct 12 — 会话 20：报告攻击坐标 + 推镜改到开战前完成

- **工具**：Locus（新增攻击坐标记录器，编辑器内实测）
- **Prompt that mattered**：怪物第一次攻击的位置与之后的攻击位置仍不一致（我说的是模型所在的位置）；先汇报三个出生点的怪物所有第一次/第二次攻击的坐标
- **新增工具**：`Assets/Editor/ZombieAttackProbe.cs` —— 按出生点归类，记录每次攻击瞬间的「根坐标 / 模型子物体世界坐标 / 模型本地坐标 / 缩放 / 相机坐标 / 离相机 / 动画状态与相位 / 骨骼网格包围盒中心 / 骨盆骨骼世界位置」。结论写 `Library/Locus/tmp/zombie-attack-positions.txt`
- **实测数据**（相机固定在 WP2 (0,2.1,5.2)、关掉推镜与打字；每只怪记录 10 次攻击，间隔 1.50 s）：
  - SpawnLeft(-2.6)：根 **(-1.033,0.500,6.761)**，模型世界 (-1.040,0.567,6.772)，离相机 2.462
  - SpawnRight(+2.6)：根 **(1.033,0.500,6.761)**，模型世界 (1.040,0.567,6.772)，离相机 2.462
  - SpawnBack(0)：根 **(0.000,0.500,7.103)**，模型世界 (0.000,0.568,7.116)，离相机 2.486
  - EntranceSpawn(0)：根 **(0.156,0.500,7.030)**，模型世界 (0.165,0.567,7.040)，离相机 2.436
  - **同一只怪 10 次攻击之间的位移全部 = 0.000 m**，可见包围盒中心波动 ≤0.002 m，动画都在 `Attack t=0.57`；四个出生点到相机的径向距离 1.837–1.903（差 ≤6 cm）
- **结论**：模型自己一动不动；能看出“第一次和之后不一致”的唯一变量是**相机**——战斗机位是慢推的（WP1 4.0 → WP2 5.2，共 1.2 m），实测同一只静止的怪离相机从 2.60 m 变到 1.88 m（表观尺寸差 38%）
- **改动（作者选定：推镜提前到战斗前完成）**：`PolePositionFlow.CombatBeats()` 里把 `PushCamera(waypointCombat, combatPushDuration)` 的协程句柄接住，在入场 + 对白之后、`BeginCombat()` 之前 `yield return push` —— 运镜仍与入场并行（观感不变），但**开战前镜头必须到位**
- **验证**：实机跑真实流程，读记录器：入场点是 t=18.5、相机 (0,2.3,4.0)=WP1（按设计还在推）；所有战斗波次的怪出生时相机已在 (0,2.1,5.2)=WP2；SpawnLeft 的真实攻击坐标 **(-1.033,0.500,6.761)**、相机 (0,2.1,5.2) 恒定、攻击间位移 0.000 m —— 与关掉镜头的定点测量完全一致
- **What broke**：无（场景已从磁盘重载复原：attackRange=1.7 / approachSpeed=0.5 / attackDamage=35 / autoStart=false / combatPushDuration=6）
- **时间**：约 1.5 小时

## Oct 12 — 会话 21：回退推镜改动 + 找到“模型位置变化”的真凶（缩放反馈）

- **工具**：Locus（A/B 对照测量）
- **Prompt that mattered**：问题仍然存在，而且第一个怪出场过了很长时间才看到单词，先把这个调回去
- **回退**：`PolePositionFlow.CombatBeats()` 恢复成原来的「推镜与入场并行、不等它」→ 自检时长从 59.2 s 回到 **53.5 s**，单词延迟消失（之前是我把开战延后到推镜结束，而入场怪的词条要等开战才出现）
- **A/B 对照找到真凶**：同一只怪第一次 vs 第二次攻击（根节点坐标完全相同、位移 0.000 m）：

  | | 第一次攻击 | 第二次攻击 | 差 |
  |---|---|---|---|
  | 根 scale | **1.30** | **1.00** | −0.30 |
  | 可见包围盒顶 y | **3.203 m** | **2.579 m** | **−0.62 m** |
  | 骨盆骨骼 y | 1.931 | 1.603 | −0.33 m |

  机制：打错字母 `Rush()` 把整只怪放大到 **1.3×**；它第一次攻击时状态切成 `Attacking`，紧接着 `RestoreScale()` 按「当前不是 Rushing」把它**缩回 1.0×** → 模型在一次攻击之内突然缩小/下移 0.62 m（根节点一动不动，所以只看 Transform 永远查不出来）。另外每次攻击的 `AttackLunge()` 还有一帧 1.5× 脉冲
- **改了什么**：`TypingTarget` 把两个缩放反馈提成序列化参数（`rushScaleMultiplier=1.3` / `attackLungeScaleMultiplier=1.5`，保持灰盒方块行为不变）；丧尸预制件上设为 **1**（奔跑动画 + 橘色染色 + 加速已经足够表达“被激怒”，攻击动作由每轮 Attack 片段负责）。同时把回缩的**根因**也修了：新增持久的 `_rushScaleFactor`（被打错后一直保持），`RestoreScale()` 不再按“当前是不是 Rushing 状态”重算——这样将来任何 >1 的倍数都不会再回缩
- **验证**（同样把怪 Rush 一下）：修后 4 次攻击 根 scale 全 **1.00**、可见包围盒顶全 **2.579 m**、骨盆 1.600/1.603、攻击间位移 0.000 m → 0.62 m 突变消失；另把某只怪的倍数临时设回 1.3 复测：4 次攻击**全部** 1.30 / 包围盒顶 3.203（旧实现是第一次 1.30、第二次就变 1.00）；完整自检 **31 pass / 0 fail**（`accuracy=100% words=12 kills=12 duration=53.5s`）
- **What broke**：无（场景已从磁盘重载复原）
- **时间**：约 1 小时

## Oct 12 — 会话 22：按 Lab B 递交要求补齐（面板 / 构建包 / 文档）

- **工具**：Locus（编辑器未开，全部走短命批处理）
- **Prompt that mattered**：读 Lab_B_Playable_Prototype (Final).pdf，告诉我为达成递交内容还需要做什么；live link 不做，模型是我自己做的，修改完先直接打包
- **做了什么**：
  - **补构建脚本** `Assets/Editor/LabBBuild.cs`（`YourMovieSucks.LabB.EditorTools.LabBBuild.BuildWindows`，只打 PolePosition）—— 之前 `AGENTS.md` 里写的这个类**根本不存在**，构建命令跑不起来
  - **调参面板补齐**（评分表 15 分「Tunability」：Lab B 期间调过的每个数字都要在面板上，且 juice 必须有开关）：
    - 新增可实时调：`enemyDeathHold` / `rushScaleMultiplier` / `attackLungeScaleMultiplier` / `laneConvergeStart` / `closeLaneOffset` / `labelCloseReferenceDistance` / `labelCloseScaleFactor` / `labelSafeTop` / `splatterAlpha`
    - 把**死字段接上**：`screenShakeOn` + `shakeStrength` 现在真的会关/缩放 `CameraShake`；新增 `screenBloodOn` 控制糊血
    - 删掉从未实现的 `hitStopOn` / `hitStopSeconds`（Lab B 规定 juice ≤1–2 个，本作实跑的是晃动 + 糊血两个，都带开关）
    - `TypingTarget.ApplyTuning()` / `CameraShake` / `ScreenBlood` / `WordLabel` 都改为读面板（面板关闭时回退各自的序列化默认值）
  - **打包**：`-executeMethod ...LabBBuild.BuildWindows` → `Build/Windows/YourMovieSucks_LabB.exe`（102.6 MB，61.5 s，0 error）→ 删掉 `*_BurstDebugInformation_DoNotShip` → 压成 `Build/YourMovieSucks_LabB_Windows.zip`（43.7 MB），里面含给测试者的 `PLAYTEST-README.txt`（怎么跑 / 按 T 导出遥测 / 键位）
  - **文档**：`AGENTS.md` 同步（构建命令与代码对齐、产物与 zip、明确「本 Lab 不做浏览器版」的偏差、丧尸模型与动画为作者自制、F1 面板 ~30 参数）
- **验证**：
  - 批处理自检 **29 pass / 2 fail** —— 两条 FAIL 都是已知的无头帧率问题（无 vsync 跑到 ~1100fps，打字音效 0.03s 最小间隔被击穿，计数从 99 掉到 13；同一份代码限帧后是全过），`accuracy=100% words=12 kills=12 duration=51.2s` 正常
  - 构建出的 exe 冒烟：`-batchmode -nographics -logFile ...` 跑 20 s 未退出、日志无异常、出现 `[DemoAudioDirector] dialogue ducking ON`（场景/演出/音频都起来了）
  - 我改的面板代码没有再动玩法数值（面板默认值与预制件一致：`attackRange=1.7`、两个缩放倍数=1、车道收窄 6/0.85）
- **What broke**：无
- **时间**：约 1.5 小时

## Oct 13 — 会话 23：去掉「当前字母标黄」，改成字母上方的向下箭头

- **工具**：Locus（批处理自检 + 定点截图验证）
- **Prompt that mattered**：打字时第一个首字母直接标成黄色，误导玩家以为已经按下、从第二个字母开始输；删掉这个机制，改成向下的三角形箭头，玩家输入到哪个字母箭头就悬在哪个字母上方
- **做了什么**：
  - `WordLabel` / `DemoHud`：未打字母**不再单独染色**（一律白色，打完变绿）——怪物头顶词条与中央输入门（TYPE: ZOOM / TAP CAPS）都改了
  - 新增 `Assets/Scripts/UI/TextCaretArrow.cs`：运行时生成向下三角 sprite（不依赖字体字形）+ 把箭头摆到指定字符正上方；词条与输入门共用
  - `TypingWord.CurrentDisplayIndex`：当前待输入字母在「显示串（含空格）」里的下标（与逐字母变色的计数口径一致）
  - 箭头用与词条相同的 `UIAlwaysOnTop` 材质，不会被钢管等几何挡住；词条那边的箭头挂在词条画布下，自动跟着近身限尺/安全区一起缩放与挪位
- **踩到并修掉的坑**：`Text.cachedTextGenerator.characters` 在这里拿不到有效逐字符坐标（实测每个字符的 cursorPos 都一样、Count 还等于带标签的原始串长 142，advance 退回了兜底值）→ 改为**量宽度**：用 `TextGenerator.GetPreferredWidth` 分别量「当前字母左边的前缀」和「前缀+当前字母」，两者之差即字母宽度；居中对齐的文本框要自己补 `(rect - preferred)/2` 偏移
- **验证**：
  - 数值：词条 "mine" 箭头 x 随进度 **171 → 195 → 211.5 → 235.5**（逐个字母）；输入门 "TYPE: ZOOM" **709 → 752.5 → 800.5 → 851.5**（每个字母的宽度都按真实渲染量，Z=39、O=48）
  - 截图：自检里 "mine" 的箭头正压在 `m` 上方、所有字母白色（无黄字）；另跑打包版并在输入门处截窗：`TYPE: ZOOM` 全白 + 黄色 ▼ 正悬在 `Z` 上方
  - 完整自检仍 29 pass / 2 fail（两条都是已知无头帧率问题：无 vsync 跑 ~1100fps 打穿了打字音效 0.03s 最小间隔）
- **重建**：`Build/YourMovieSucks_LabB_Windows.zip`（43.7 MB）已重打，可以直接发测试者
- **What broke**：无（临时调试开关 `TextCaretArrow.DebugCaret` 默认已关）
- **时间**：约 1.5 小时

## Oct 13 — 会话 24：失误惩罚补齐 + 记住场景删掉的那根灯带

- **工具**：Locus（编辑器内实时验证；构建改走编辑器排队）
- **Prompt that mattered**：我删掉了场景里挡 ZOOM 单词的灯带（记住这个变更别改回去）；另外怪出现后，只要不按怪的首字母，按多少错键都不被惩罚——改成玩家只要输错 a–z 就触发惩罚
- **灯带（已记住，不会改回去）**：
  - 场景里删掉的是 `NeonTubeStage`（舞台上方 `(0,5.6,12.2)` 的粉色灯带，正好压在开场中央 `TYPE: ZOOM` 提示后面）；现存灯带 = Left / Right / BackA / BackB ✓
  - **同步改了场景生成器**：`PolePositionSceneBuilder` 里创建它的那行已移除并加注释「作者删除，不要再加回来」——否则下次重建场景它会复活
- **失误惩罚**：
  - 根因：`TypingController.HandleLetter` 在「按的字母不是任何词条首字母」时只 `ResetCombo()` 就 return，**不发 `OnLetterMismatch`** → 惩罚（怪加速、WRONG 闪字、失误计数）全都没触发；惩罚只在**已经锁定**词条后再打错才会发生
  - 改法：纯误击也发 `OnLetterMismatch(null, key)`；`AgentShooter.HandleLetterMismatch` 改为统一处理——① 锁定中的特工轨词条 → 罚这只（与设计一致）② 其它（纯误击 / 导演轨 / 输入门）→ 罚「离相机最近、带着词条、且不在攻击态」的那只（攻击态的 `Rush()` 本来就无效）
  - 副作用（正面）：纯误击现在也会计入遥测 `mistypes`（以前不计），结算屏 MISSTYPES 与准确率都对得上
- **验证**（编辑器内实时，用 `TrackInput.InjectLetter` 注入按键）：
  - 负对照：强制特工轨、新怪带词条 `back off`(首字母 B) → 按 **B**：`Mistypes 4→4`（正确字母不算失误）
  - 正对照：按 **S**（与所有首字母无关）→ `Mistypes 4→5`，且**新怪立刻变成 `Rushing`**
  - 另测得：场上当时唯一在走路、带词条的怪被选中（已在攻击态的那只不受影响）
  - 注：`Rush()` 对已在攻击态的怪无效（既有设计），此时仍有 WRONG 闪字 + 连击中断 + 失误计数
- **What broke**：编辑器内直接调 `BuildPipeline` 会与编译器流水线冲突（`CompleteActiveBuildWhilePumping` 抛 `InvalidCastException`、报"scripts had compiler errors"）→ 改为把构建排到编辑器下一帧（`EditorApplication.delayCall`）执行，等价于点菜单，构建成功
- **重新打包**：`Build/YourMovieSucks_LabB_Windows.zip`（43.7 MB，含本次修复），可发测试者
- **时间**：约 1 小时

## Oct 13 — 会话 25：文档同步 + git 历史 + 交付打包（按 Lab B 要求）

- **工具**：Locus（编辑器开着；构建走编辑器排队）
- **Prompt that mattered**：可以（同步设计文档）。这个版本请更新所有有关文档，按 Lab B 要求把除 PROTOTYPE.md / telemetry/ / gameplay 视频以外的都准备好，随后打包最新版本给我
- **文档同步**（`GDD.md` 与设计源 `Locus/knowledge/design/your-movie-sucks-gdd.md` 两边同时改，保持一致）：
  - §5.1.4 打错字母：改成「**任何 a–z 错键都算失误**」并写明两种罚法（锁定词条→罚对应怪；纯误击→罚最近那只带词条的怪）+ 每次都 WRONG 闪字/连击清零/计入 `mistypes`
  - §5.1.5 攻击倒计时：由「3s」更正为**实现值 1.5s**（面板 `attackCountdown`），并标注原设计写 3s
  - §14.2 逐字母反馈：由「当前字母高亮」改为「未打字母一律白色 + **上方黄色三角箭头**指出当前待输入位置」（世界空间词条与中央输入门同逻辑）
  - §8 怪物动画：由「Mixamo 自动绑定」改为「**作者自制**模型与 12 段动画，Humanoid 导入、位移摘成根运动后由代码驱动」
  - `AGENTS.md` 新增 §7「Lab B 交付清单（对照作业要求）」：8 项要求 → 状态 → 位置/说明（含"不做浏览器版/live link"的偏差、未实现 hit-stop 的理由、遥测归档与视频仍待作者）
- **git 历史**（此前仓库 0 提交）：`.gitignore` 补掉 Locus 工具产物 `Directory.Build.props`，然后按主题分 **11 个提交**：忽略规则 → 四个交付 md + Lab A 输入 → 包清单与工程设置 → 核心循环 → Demo 演出层 + 调参/遥测 → UI（含字母箭头）→ 美术音频资产 → 场景与预制件 → 编辑器工具（含打包脚本）→ 遥测归档 → 其余 meta/inputactions。工作区干净，`.git` 25 MB
  - ⚠️ 提交作者是仓库里原有的占位身份 `Your Name <Your Name>`，**需要作者改成真名/邮箱**（或告诉我，我重置重提）
- **交付打包**（两个包，都在 `Build/`）：
  - `YourMovieSucks_LabB_Windows.zip`（43.7 MB）＝**可玩版本**，给测试者/上传网盘。构建于 22:24（含失误惩罚修复），并做冒烟：`-batchmode -nographics` 跑 18 s 不崩、日志 0 异常、开场对白已起
  - `YourMovieSucks_LabB_Folder.zip`（38.2 MB）＝**游戏文件夹 + 隐藏 `.git`**（Lab B 提交项「仓库链接 或 含 .git 的 zip」的后半条）。含 2070 个条目：四个交付 md、Assets/Packages/ProjectSettings、telemetry/ 全部 JSON、`.git` 完整历史（1131 个对象）；已排除 Library/Temp/Logs/Build/obj/UserSettings/Locus
- **What broke**：① 编辑器内直接 `BuildPipeline` 与编译流水线冲突（见上一会话）→ 排队到下一帧；② 写 zip 的 PowerShell 脚本里带中文注释会被 PS 5.1 按 ANSI 读成语法错误 → 脚本改纯 ASCII；③ 第一版 zip 因目录枚举中途抛 IOException 而静默漏掉 Packages/ProjectSettings/telemetry → 改为顶层逐项遍历 + 事后校验条目
- **时间**：约 1.5 小时

## Oct 18 — 会话 26：导演轨「弱点识别」系统（第一版）

- **工具**：Locus（编辑器未连 → 全程短命批处理；含一条自动验收脚本）
- **Prompt that mattered**：导演轨弱点识别系统的完整需求（切导演轨→红光+弱点词→输入弱点词→特写→换简单词→切回击杀，不动现有玩法）；确认「5 个弱点词轮换」方案后开工
- **设计决定（作者确认）**：同屏最多 5 只怪 → 弱点池 5 条、首字母互不相同（HEAD/LEG/NECK/SHOULDER/MOUTH），每只怪出生分到未被占用的一条 → 现有「按首字母吸附」永不歧义。识别后的简化词：HIT/LIE/CUT/SHUT/EAT（与演示战斗词首字母不冲突）
- **新增**：
  - `MonsterWeaknessSet`（ScriptableObject：部位 / 弱点词 / 骨骼名 / 简化词）+ `Assets/Data/Monsters/Weakness_Zombie.asset`
  - `MonsterWeakness`（挂在预制件，每只怪一份状态）：按 `boneName` 递归解析骨骼、运行时生成红色发光小球 + 弱点词锚点、脉冲、识别时放大闪光、`Recognized` + `OnRecognized`
  - `DirectorWeaknessView`（Systems 上）：切轨时显示/回收弱点词、识别后调特写并换词、简化词同首字母冲突时排队等空位
  - `WeakPointCloseUpCamera`（相机上）：短暂接管相机推到弱点（实测位移 6.55 m、离弱点 1.08 m）、回位并恢复原 pose
  - `WeaknessFlowVerify`（编辑器脚本 + 菜单 `YourMovieSucks/Verify DIRECTOR WEAKNESS Flow`）：自动跑完作业验收的 8 步
- **对现有代码的小改动（行为不变）**：`TypingWord` 加 `IsWeakness`/`LabelOffsetY`；`TypingController` 加 `OnWordRemoved` + `SpawnWord` 可选参数；`WordLabelManager` 按轨显隐标签；`WordLabel` 支持固定标签偏移；`RunStats` 弱点词不计入战斗统计、单独计数；`ScriptedEncounter` 生成词条时把原词写进弱点组件；自检「同首字母」断言改为**按轨**统计（跨轨弱点词与战斗词同首字母是无害的）
- **修复的两个真问题**：
  - 特写一开始**没真正推进**（相机只动 0.31 m = 抖动幅度）：`Play()` 用 `rail.IsMoving` 判断让位，而验收里把轨道镜头禁用在了"正在推轨"状态上 → 改成「启用中的轨道镜头正在推进才让位」。改后位移 6.55 m
  - 验收脚本自身 bug：`GameObject.Find` 找不到 inactive 对象，把"标签已隐藏"误判为失败 → 改为连 inactive 一起查
- **验证**：
  - 自动验收 **15 pass / 0 fail**（生成怪→战斗词可见→切导演轨战斗词隐藏→红光+HEAD 词出现→输入 HEAD→已识别+特写+换成 HIT→切回可见新词→输入 HIT 击杀→弱点计数 1、战斗字母数 3 不含弱点词），截图 3 张存 `Library/Locus/tmp/weakness-shots/`
  - 原有 F2 自检 **29 pass / 2 fail**（两条仍是已知无头帧率问题），`no two active words ever shared the same initial letter` 通过、`accuracy=100% words=12 kills=12`
- **What broke**：无（除上述两个已修问题）
- **时间**：约 3 小时

## Oct 20 — 会话 27：修复「识别弱点后怪物原地开打」

- **工具**：Locus（编辑器连接中；改动后热编译 + 编辑器内跑两套自检，不退出 Unity）
- **Prompt that mattered**：使用导演敌人输入完弱点单词后，怪物会停留在当前位置直接攻击，而不是走到离玩家的最近位置再开始攻击，请修复
- **根因**：怪物的「到站开打」是拿**怪物到相机的距离**判定的（站位点 = 相机前方 `attackRange` 处的攻击环），而弱点特写会把相机挪到怪物脸前 —— 特写期间间距瞬间缩到 1 m 级，于是一路加速中的怪（打错字母 1.6 m/s）在特写里撞进那个“假攻击环”，`_arrived` 置位并**latch 住**（只有离相机超过 `attackRange×3` 才解除）→ 镜头回位后它停在原地、离玩家还很远就开始攻击
  - 现场复现（不进编辑器界面的自动对照）：加速怪 + 只播特写不定格 → `t+0.72s state=Attacking arrived=True pos=(-2.81, 8.25) dCam=3.53`（比修复前的位置还往后退了 0.2 m）；同条件下定格 → 特写期间位置不动，结束走到攻击环 `dCam=2.47` 才开打
- **修复**：
  - `TypingTarget` 新增 `SpotlightHold`（与入场演出的 `Frozen` 分开，互不覆盖）：置位时**不走、不判断到站、不请求攻击**（`Update` 开头一并 return）
  - `WeakPointCloseUpCamera.Play(focus, onFinished)` 增加回调：**任何出口都必回调一次**（正在播/轨道在推/播完），调用方拿它解除定格，漏调就会把怪定死
  - `DirectorWeaknessView` 识别时置 `SpotlightHold = true` 并在回调里解除
  - 顺带修掉一个观感问题：特写期间怪不再朝镜头走（以前会一路走向相机/穿过镜头）
- **验证**：
  - `YourMovieSucks/Verify DIRECTOR WEAKNESS Flow` → **17 pass / 0 fail**（新增两条回归断言：特写期间未进入攻击态；特写结束后解除定格、继续走向玩家 7.18 m）
  - 原有 F2 自带自检（编辑器内跑，`exitWhenFinished=false`）→ **31 pass / 0 fail**（本次 0 失败；`no two active words…`、`accuracy=100% words=12 kills=12`、`player survived` 全通过）
  - A/B 对照数据见上（先在 live 里复现 bug，再验证修复）
- **文档**：`GDD.md` / 设计源 §16 补「特写期间怪物定格」与新的 17 项断言
- **What broke**：无
- **时间**：约 1 小时

## Oct 20 — 会话 28：词条字体（Special Elite）+ 攻击威胁配色

- **工具**：Locus（编辑器在跑自检后退出，全程 `dotnet build` 做编译校验，运行期验收待编辑器重开）
- **Prompt that mattered**：给怪物攻击加提示；单词字体换成刚装的 Special Elite；颜色白/黄/红对应「远处/正在靠近/准备攻击」，攻击中闪红，颜色变化要自然连续
- **字体**：`SpecialElite.ttf` 在用户字体目录（`%LOCALAPPDATA%\Microsoft\Windows\Fonts\`，非工程内）→ 复制进 `Assets/Resources/Fonts/SpecialElite.ttf`；`WordLabel` 用 `Resources.Load<Font>("Fonts/SpecialElite")` 取，取不到则**警告一次并回退内置 Arial**（不会整块词条消失）
- **威胁配色**（都在 `WordLabel`，参数序列化可调）：
  - 危险度 = 怪物根节点到相机的**三维距离**：`farDistance` 9 m 以外 → 白；`dangerDistance` 3 m 以内 → 红；黄色出现在危险度 `yellowPoint` 0.6
  - 白→黄→红两段线性插值 + **指数平滑**（`tintSmoothTime` 0.35 s，与帧率无关）→ 阶段切换是渐变不是跳色
  - 怪物进入攻击态（`TypingTarget.IsAttacking`）→ 危险度锁 1（红）+ 正弦闪烁（`flashHz` 3.2、`flashMinBrightness` 0.35）
  - 颜色量化为 32 级再比较，只有真的变了才重建富文本（否则每帧重建文字网格）
  - 弱点词条（导演轨、挂在骨骼锚点上）会 `GetComponentInParent<TypingTarget>()` 找到同一只怪，同样上色；输入门等无目标词条保持白色
- **未改的部分**：已完成字母仍为绿色（进度反馈保留），箭头仍为黄色 `#ffe066`
- **验证**：
  - `dotnet build Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj`：**0 error**（4–5 条都是原有 warning）
  - 新增自动验收 `WordTintVerify`（菜单 `YourMovieSucks/Verify ENEMY LABEL Font + Threat Tint`）：断言字体是 Special Elite、9.5/6/2.6 m 三段颜色（白/黄/红）、单帧最大跳变 < 0.2（连续性）、攻击中亮度起伏 > 0.2 且始终红；抓 4 张图到 `Library/Locus/tmp/tint-shots/`。**待编辑器重开后再跑**
- **What broke**：无（编辑器在本次会话中途被关闭，运行期验证顺延）
- **时间**：约 40 分钟
