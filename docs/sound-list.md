# SOUND LIST（SFX Spotting）—《YOUR MOVIE SUCKS!》Lab C

> 音效管线 Stage 1「spotting」交付物：把游戏里每一个需要声音的瞬间列出来，标明实现状态与 5 个阶段（spotting → sourcing → editing & design → implementation → mixing）各自到哪一步。
> 事实来源：`BUILD_LOG.md` 会话 12/13/14、`Assets/Scripts/Demo/TypingSfxPlayer.cs`、`PlayerHurtSfx.cs`、`Assets/Audio/`。

## 1. 触发点清单（Spotting）

| # | 时刻 | 需要的声音 | 状态 |
|---|---|---|---|
| 1 | 敲击任意字母（打对） | 打字机「扳机 + 回弹」短音 | ✅ 已实现 |
| 2 | 敲错字母 | 同上但音高/音量略低 + `WRONG!` 文字 | ✅（复用打字池，音高区分） |
| 3 | 切轨（CapsLock） | 独立的机械「换挡」音 | ✅ 已实现（`typewriter8` 专用于切轨，不入打字池） |
| 4 | 击杀怪物 | 打字枪声层（随机手枪/霰弹） | ✅ 已实现（每击键叠一层枪声） |
| 5 | 玩家受伤 | 受伤闷响 + 屏幕血效同步 | ✅ 已实现（`PlayerHurtSfx`，接 `PlayerHealth.OnDamaged`） |
| 6 | 弱点被破坏（导演轨） | 弱点识别的「镜头对焦 / 咔嚓」提示 | ⬜ 待做（当前只有视觉反馈） |
| 7 | 门词完成（TYPE: ZOOM） | 机械快门 + 走带声 | ⬜ 待做 |
| 8 | 低血量（≤3 格） | 心跳/耳鸣持续层 | ⬜ 待做（现有 4 条低血 VO 未接入） |
| 9 | 结算屏（PRINT IT!） | 打字机换行 + 收尾锣声 | ⬜ 待做 |
| 10 | UI 交互（标题开始/静音/开关） | 干涩的机械点击 | ⬜ 待做 |

**结论**：作业要求「6+ 个音效走完全部五阶段」。当前**已入游戏**的有 4 个音效类别（打字、切轨、枪声、受伤），文件级 11 个；要凑满 6 类并让清单完整，建议补 #6/#7/#9（这三个都是「关键瞬间」，也直接提升 Audio polish 评分）。

## 2. 音频资产清单（Sourcing）

| id | 文件 | 时长/格式 | 来源 | 许可 | 用途 |
|---|---|---|---|---|---|
| typewriter1–7 | `Assets/Audio/SFX/Typing/typewriter1..7.wav` | 0.6s（原 1.0s 裁切 + 20ms 淡出）/ 44.1k mono | 【待补：素材包/作者链接】 | 【待补】 | 打字随机池（7 选 1） |
| typewriter8 | `Assets/Audio/SFX/Typing/typewriter8.wav` | 同上 | 同上 | 【待补】 | 切轨专用 |
| cg1 / pistol | `Assets/Audio/SFX/Gunshot/cg1.wav`、`pistol.wav` | 同上 | 【待补】 | 【待补】 | 每击键叠加的枪声层（2 选 1） |
| 受伤音 | 由 `PlayerHurtSfx` 引用（见其 Inspector） | — | 【待补】 | 【待补】 | 玩家被击中 |
| BGM | `Assets/Audio/BGM/bgm_show_me_love.mp3` | 4:00 / 192k 立体声 | 用户提供的 `BGM_Show Me Love.mp4` 转码 | 【待补】 | 主循环 |
| VO ×23 | `Assets/Audio/VO/PolePosition/*.wav` | 44.1k/16bit/mono | 用户录制或生成（见 `docs/voice-script.md`） | 【待补】 | 剧情 + 反应台词 |

> 归档：旧的 7 个 `eklee-*` 打字音已移出工程（`Library/Locus/tmp/sfx-old-eklee/`），原件备份 `Library/Locus/tmp/sfx-raw*`。

## 3. 处理与设计（Editing & design）

| 处理 | 说明 |
|---|---|
| 裁切 | 原始打字音 1.0s，其中约 0.6s 是尾音 → 剪到 0.6s + 20ms 淡出，避免快打叠成糊音 |
| 池化 | 打字 7 个随机 + 切轨 1 个独立；枪声 2 个独立池（**不能**放同一目录，否则会被当作打字音抽进池子） |
| 门控 | 同源 30ms 最小间隔：一次击键 = 打字音 + 随机枪声，两者共用门控 |
| 音量 | `sfxVolume 0.8`（打字素材峰值 −3…−8 dBFS，故比旧素材抬高）、`gunshotVolume` 单独可调 |
| 转码 | Unity 6.3 不导入 m4a/AAC（实测 23/23 NOT IMPORTED）→ 用 dotnet + NAudio（Windows Media Foundation）批量转 44.1k/16bit 单声道 |

## 4. 实现（Implementation）

- `Assets/Scripts/Demo/TypingSfxPlayer.cs`：打字池 + 切轨音 + 枪声层 + 30ms 门控；id 池由 `Assets/Editor/*VoiceTools*` 扫描目录生成，重建不会漏。
- `Assets/Scripts/Demo/PlayerHurtSfx.cs`：只在「真正掉血」（`PlayerHealth.OnDamaged`）触发，和屏幕血效同源。
- `Assets/Scripts/Demo/DemoAudioDirector.cs`：VO / SFX / BGM 三条音源，逐条参数（速度、延迟、音量、起始偏移）+ BGM ducking。
- 导入设置：SFX/VO = mono + DecompressOnLoad；BGM = Streaming + 保立体声。

## 5. 混音（Mixing）

| 项 | 参数 |
|---|---|
| BGM | `bgmVolume 0.22`（default），对话 duck ×0.35，2.5/s 平滑 |
| SFX | `sfxVolume 0.8` |
| 枪声 | `gunshotVolume`（独立，默认见 `LabBTuning`） |
| VO | `voVolume 1.0`，逐条可覆盖 |
| 总控 | 标题画面 `[M]` 静音键 → `AudioListener.volume`，PlayerPrefs 记住 |

**后续建议**：加一个简单 bus 结构（BGM/SFX/VO 三个 AudioMixer Group），把现在的「代码乘音量」换成 Mixer 推子；再做一次实机耳测（-6 dBFS 峰值上限、BGM 不掩蔽词条语音提示）。
