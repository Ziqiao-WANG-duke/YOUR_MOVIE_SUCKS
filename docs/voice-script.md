# VOICE SCRIPT —《YOUR MOVIE SUCKS!》Lab C

> 配音管线交付物（写词 → 选角/声音设计 → 草配 → final → 剪辑 → 接入+字幕）。
> 事实来源：`PolePositionFlow.cs`（台词表与 id）、`Assets/Audio/VO/PolePosition/`（23 个文件）、`VoLineSettings_PolePosition.asset`（逐条调参）、`Library/Locus/tmp/demo_script_v2.txt`（原稿）。
> 语言：英语（成人向，含脏话）。全角标点仅用于文档，实际字幕用半角。

## 1. 角色与声音设计（Casting / voice design）

| 角色 | 性格 | 声音设定 | 台词量 |
|---|---|---|---|
| **AGENT**（特工 / 打手） | 不耐烦、嫌脏活、短句 | 低音、气声多、语速偏慢；被打断时会拔高 | 8 句 |
| **DIRECTOR**（导演 / 摄影） | 自恋、行话多、亢奋 | 中音、语速快、句尾上扬；把事故当素材 | 9 句 |
| 尾字处理 | — | 每句结尾必须干净（不留呼吸尾巴），方便字幕与音频长度对齐 | — |

## 2. 剧情对白（已接入，13 句）

字幕：全部有（`DemoHud.ShowSubtitle`）。播放顺序即演出顺序；`hold` 是无音频时的兜底停留时长。

| # | id | 角色 | 台词 | 触发时机 |
|---|---|---|---|---|
| 1 | `pp_open_dir_01_production_value` | DIRECTOR | "Oh. Now that's production value." | 开场黑幕淡入后第一句 |
| 2 | `pp_open_dir_02_get_on_the_pole` | DIRECTOR | "Get on the pole." | 开场对喷（计数前） |
| 3 | `pp_open_agt_03_no` | AGENT | "No." | 同上 |
| 4 | `pp_open_dir_04_ten` | DIRECTOR | "Ten." | 倒计时开始 |
| 5 | `pp_open_dir_05_nine` | DIRECTOR | "Nine—" | 倒计时（教学门 `TYPE: ZOOM` 期间） |
| 6 | `pp_open_agt_06_fuck_you` | AGENT | "Fuck you." | 倒计时被他自己打断 |
| 7 | `pp_ent_agt_07_my_stage` | AGENT | "That is my stage, bitch!" | 入场丧尸冲上台、特工抢台 |
| 8 | `pp_end_agt_08_there` | AGENT | "There." | 战斗全清后 |
| 9 | `pp_end_agt_09_happy_now` | AGENT | "Happy now?" | 同上 |
| 10 | `pp_end_dir_10_got_the_shot` | DIRECTOR | "...We got the shot." | 同上 |
| 11 | `pp_end_agt_11_hate_this_job` | AGENT | "I hate this job." | 同上 |
| 12 | `pp_end_dir_12_great` | DIRECTOR | "Great." | 同上 |
| 13 | `pp_end_dir_13_print_it` | DIRECTOR | "Print it." | 进结算屏前最后一句 |

## 3. 逐条剪辑参数（Editing & processing）

`Assets/Audio/VO/VoLineSettings_PolePosition.asset`（可在 Inspector 直接调，Play 中即时生效）：

| id | speed（变速=变调） | delay | volume | startOffset | 备注 |
|---|---|---|---|---|---|
| `pp_open_dir_01_production_value` | 1.0 | 0 | 1.0 | **1.0s** | 砍掉开头的呼吸/废话 |
| `pp_open_dir_02_get_on_the_pole` | 1.0 | 0 | 1.0 | **0.6s** | 同上 |
| `pp_open_agt_03_no` | 1.0 | 0.3 | 1.0 | 0 | 稍微晚进，卡在对喷节奏上 |
| `pp_end_dir_10_got_the_shot` | 1.0 | 0.3 | 1.0 | 0 | — |
| `pp_end_agt_11_hate_this_job` | 1.0 | 0.2 | 1.0 | 0 | — |
| `pp_ent_agt_07_my_stage` | 1.0 | 0 | 1.0 | **1.0s** | 砍掉前奏 |
| `pp_end_dir_13_print_it` | 1.0 | 0 | 1.0 | **1.0s** | 砍掉前奏，卡结算屏前 |
| 其余 6 句 | 1.0 | 0 | 1.0 | 0 | 默认 |

技术处理：Unity 6.3 **不导入 m4a/AAC**（实测 23/23 NOT IMPORTED）→ dotnet + NAudio（Windows Media Foundation）批量转 **44.1kHz / 16bit / 单声道 WAV**；导入 mono + DecompressOnLoad。

## 4. 反应台词（已录制，10 句，**尚未接入**）

文件已在工程里，但代码里没有触发点。原始设计（原稿 §「受到攻击特殊台词 / 低血量 / 死亡」）如下：

| # | id | 角色 | 台词 | 设计触发 | 状态 |
|---|---|---|---|---|---|
| 14 | `pp_hit_agt_01_working_here` | AGENT | "I'm working here!" | 玩家受伤（随机） | ⬜ 未接入 |
| 15 | `pp_hit_agt_02_not_the_face` | AGENT | "Not the face!" | 玩家受伤（随机） | ⬜ 未接入 |
| 16 | `pp_hit_dir_01_ruining_the_shot` | DIRECTOR | "Careful! You're ruining the shot!" | 玩家受伤（随机） | ⬜ 未接入 |
| 17 | `pp_hit_dir_02_bleeding_toward_camera` | DIRECTOR | "Try bleeding toward the camera!" | 玩家受伤（随机） | ⬜ 未接入 |
| 18 | `pp_low_agt_01_little_help` | AGENT | "Little help?!" | 血量首次 ≤3 格（只一次） | ⬜ 未接入 |
| 19 | `pp_low_dir_01_keep_going` | DIRECTOR | "Keep going! This is great!" | 同上 | ⬜ 未接入 |
| 20 | `pp_low_agt_02_gonna_die` | AGENT | "I'm gonna die here!" | 同上（备选版） | ⬜ 未接入 |
| 21 | `pp_low_dir_02_make_it_cinematic` | DIRECTOR | "Then make it cinematic!" | 同上（备选版） | ⬜ 未接入 |
| 22 | `pp_death_dir_01_cut` | DIRECTOR | "...Cut." | 玩家死亡（第一句） | ⬜ 未接入 |
| 23 | `pp_death_dir_02_unusable` | DIRECTOR | "Well, that was unusable." | 玩家死亡（第二句，接 Game Over） | ⬜ 未接入 |

**接入建议（约 0.5 天工作量）**：
1. 受击：`PlayerHealth.OnDamaged` → 随机抽 #14–#17（带 8s 冷却，避免刷屏）。
2. 低血：`CurrentCells <= 3` 首次触发 → 播 #18/#19（两条错开 0.6s，做成对喷）。
3. 死亡：`PlayerHealth.OnDeath` → 先播 #22，停 0.8s，再播 #23，然后出 `BAD TAKE / THE LEAD IS DEAD.` 结算。
4. 三条都要走 `DemoAudioDirector.PlayVo(id)`，字幕用 `DemoHud.ShowSubtitle`，才能满足「voice lines fit and are captioned」。

> 这 10 句接入后，「每个关键时刻都有声音」这条评分项才算真正拿满（目前受伤只有音效没有台词、死亡只有结算屏）。

## 5. 字幕规范

- 一句一行，最长约 42 字符，超出换行而不是缩小字号。
- 字幕在音频前 55% 时长内铺完（`DemoDialogue` 规则），音频缺失时退回按 `hold` 计时。
- 字幕出现/消失都要有淡入淡出，不允许突然闪断（与屏幕血效同层级，不遮挡词条）。
