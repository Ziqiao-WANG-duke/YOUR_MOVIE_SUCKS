# refs/ — 参考素材板

Lab C 要求 `style/refs/` 存放**带来源**的参考素材。每加一张图/一段音，就在下面的 `REFS.md` 里加一行。

建议最少放这些（缺哪张就补哪张）：

| 分类 | 需要什么 | 用途 |
|---|---|---|
| 风格 | 70s grindhouse 海报 / 16mm 颗粒截图 | 定色彩、颗粒、对比 |
| 场景 | 霓虹钢管舞俱乐部内景参考 | 环境 art 的灯光与配色依据 |
| 敌人 | 丧尸/变异舞女 turnaround（正/侧/背） | Stage 1 概念 + Stage 2 建模依据 |
| 字体 | Special Elite 字样样本 | UI 排版依据 |
| UI | 胶片条/取景框/打字机面板参考 | HUD 视觉依据 |
| 音频 | 参考曲（放克/迪斯科）与参考音效 | 音乐/音效 brief 依据 |

---

## REFS.md 模板（每张图一行）

```
- style/refs/<文件名> — 来源：<链接或作者> — 许可：<CC0 / CC-BY / 付费 / 自拍> — 用途：<哪一个阶段>
```

> 规则：**没有来源的图不要放进来**。作业对「do you own this?」的要求就是这一栏。

## 现存可复用的工程内参考（不需要再下载）

- `Assets/RetroRendering/` —— 合成材质与调色参数（实际渲染规范）
- `Assets/Art/Shaders/` —— 环境 / 角色 / UI 着色器
- `Assets/Prefabs/StripperZombie.prefab`、`Assets/Prefabs/Zombie_Waiter.prefab` —— 两个敌人的最终外观
- `Assets/UI/Health/` —— 胶片血条素材（满/空）
- `Assets/Resources/Fonts/SpecialElite.ttf` —— UI 字体
