# PS1-inspired Retro Rendering

面向 **Unity 6000.3.8f1 / URP 17.3.0 / RenderGraph / Forward+**。默认 Arcade Horror；不含 CRT、VHS、色差、噪声或镜头抖动。

## 快速试用

### gameplay 场景（已接入）

`Assets/Scenes/PolePosition.unity` 已保存接入，直接进入 Play Mode 即可看到效果：

- 根对象 `Retro Rendering`：`RetroRenderingController`（默认 Arcade Horror、426×240、开启量化）与 `RetroCharacterAutoBinding`。
- `Venue` 下 14 个 MeshRenderer 各带 `RetroMaterialBinding`，指向 `Materials/PolePosition/*_Retro.mat` 环境变体。
- 取消控制器 `retroEnabled` 即可在同一场景对比原始画面；退出 Play 后原材质、原管线不变。
- 重新接入或完整移除：**Tools > Retro Rendering > Apply to PolePosition scene** / **Remove from PolePosition scene**。两个命令都幂等，且只改场景接线与 `Materials/PolePosition/` 下自己生成的材料。
- 敌人不使用预制件内材质替换：`RetroCharacterAutoBinding` 在运行时把每只怪**已实例化的材质**原地换成角色 Shader，因此 `TypingTarget` 缓存的 `material.color`（威胁染色）仍写同一个材质对象。

### 独立测试场景
1. 打开 `Assets/RetroRendering/Scenes/RetroTest.unity`，进入 Play Mode。
2. 选择 `Retro Rendering` 对象，在 Inspector 修改 `RetroRenderingController`。
3. 左下角按钮可切换 Clean / Arcade / PS1、分辨率或总开关。Inspector 可单独调整量化、抖色、顶点吸附、仿射 UV、环境法线风格。
4. `RetroTestMotion.motion` 控制测试相机缓慢横移和棋盘墙旋转；**只存在于测试场景，不会给战斗相机加运动**。
5. 左边僵尸保留原材质，右边使用可逆角色变体；两者使用原 FBX、UV、贴图、Avatar 和 AnimatorController。

### 实际关卡，无需保存场景
1. 打开 `Assets/Scenes/PolePosition.unity`，进入 Play Mode。
2. 执行 **Tools > Retro Rendering > Preview gameplay (Play Mode only)**。
3. 自动选中运行时控制器，可修改所有参数。原始僵尸和环境材质不替换，只启用世界低分辨率、量化和 UI 保护。
4. 停止 Play Mode，预览对象消失。此菜单不会保存或修改游戏场景。

### 持久接入（显式选择）
在目标场景新建对象并添加 `RetroRenderingController`：
- `sceneCamera`：已有的游戏 Base Camera（保留 MainCamera 标签、轨道控制器和 AudioListener）。
- `compositeShader`：`Assets/RetroRendering/Shaders/RetroScreenComposite.shader`；构建时应显式引用，避免 Shader.Find 对应变体被剥离。
- `expectedPipeline`：`Assets/Settings/PC_RPAsset.asset`。
- `pipelineOverride`：`Assets/RetroRendering/Settings/Retro_PC_RPAsset.asset`。
- `presentationRendererIndex`：1。
- `preset`：选择预设，点击 **Copy values from preset asset**。预设只复制一次，不会逐帧覆盖后续调整。

副本以现有 PC 管线为源：Renderer 0 仍引用原 `PC_Renderer`，Renderer 1 是无 SSAO/无额外 Feature 的 UI 合成 Renderer。控制器只在运行期间临时切换当前 Quality 的管线引用，关闭时恢复。原管线、Renderer、Volume、灯光和 GraphicsSettings 文件不改写。如果实际源管线与 `expectedPipeline` 不符，拒绝启用，避免拿旧配置覆盖新质量设置。

未设置副本也可运行，但合成相机会继承默认 Renderer 的额外 Feature，可能重复执行无用效果，不建议作为最终配置。修改 PC 管线配置后，需要重新检查副本差异；生成工具不覆盖已有资产。

## 模块与参数

| 模块 | 参数 / 行为 |
|---|---|
| 真低分辨率 | 原游戏相机写入单采样、无 mipmap RT；320×240 / 426×240 / 640×480 / Native。不是事后对全分辨率图像马赛克化。 |
| 放大 | Point 使用整数倍、居中黑边，避免像素宽度交替；窗口小于目标时采用整数倒数缩小（极小窗口未视觉验收）；Bilinear 保持比例适配。原生屏幕与所选比例不同时，重新按所选相机比例投影，不拉伸。整数放大可能产生较宽黑边，这是刻意行为。 |
| 颜色 | `quantization` 独立开关；`colorLevels` 为每个 RGB 通道的级数；在显示颜色空间中量化。`dithering=0` 关闭 4×4 Bayer 抖色，无随机帧噪声。默认 32 级、0 抖色。 |
| 顶点吸附 | 对 clip.xy / clip.w 映射的像素网格取整，再乘回 w；不改变 z/w、网格资源或骨骼。`_VertexSnap` 可逐材质关闭，`_SnapStrength` 连续调节，`_SnapNearFade` 从实际近裁面淡出。 |
| 仿射 UV | 用 uv×w / w 的插值组合恢复屏幕线性 UV，再与原透视 UV 混合；`_Affine` 可逐材质关闭。保留源 UV 和 `_BaseMap` 的平铺/偏移。三角形对角线处的扭曲是该效果的一部分。 |
| 环境 | 无贴图默认白采样×基色；支持可选贴图、顶点色、环境底色/强度、主光与 Forward+ 附加光、阴影深浅、漫反射分段、漫反射包裹、窄高光、平滑/面法线、发光。漫反射按 URP PBR 的 1/π 归一化，同一基色与灯光下亮度可与原 Lit 对照。不是原 Lit 的等价替代，需自愿选用。 |
| 角色 | URP PBR 光照，底色贴图、法线贴图、发光、金属度/光滑度标量、裁切与普通 alpha 混合；标准 SkinnedMeshRenderer 动画保持原样。 |
| UI | 世界空间 UGUI 及其文字/箭头子对象临时转到 UI Layer 5，最后由原生分辨率相机绘制。原有 ScreenSpaceOverlay 与 IMGUI 不进入低分辨率 RT；相机拥有权、层和材质均恢复。 |

### 可逆材质选择

不要批量替换原材质。优先给**明确选中的 Renderer**添加 `RetroMaterialBinding`：
1. `target` 指向具体 Renderer。
2. `retroVariants` 数量与原材质槽数量一致；空槽代表保持原材质。
3. 将该 Binding 加入控制器的 `materialBindings`。
4. 运行时创建独立材质实例，关闭系统或禁用 Binding 时恢复原引用。
5. `controlSnapping` / `controlAffine` 控制是否接收控制器强度；材质自身 `_VertexSnap` / `_Affine` 仍可禁止该功能。
6. `controlSnapGrid=false` 时保留材质自己的 `_SnapResolution`，可独立选择吸附网格，不必跟随 RT 尺寸。
7. `controlShading` 仅建议环境开启，角色默认不随环境切成面法线。

四个环境色板：`DriedCrimson`、`OxidizedTeal`、`DustyConcrete`、`BruisedViolet`，位于 `Assets/RetroRendering/Materials/`。gameplay 场景用的 7 个变体（对应原 `M_Floor`/`M_Wall`/`M_Stage`/`M_BrassPole`/`M_Velvet`/`M_NeonPink`/`M_NeonCyan`）在 `Assets/RetroRendering/Materials/PolePosition/`，基色与发光从原材质复制；暗环境靠 `_AmbientColor`（0.55/0.47/0.68）与 `_DiffuseWrap` 抬亮，霓虹用低环境强度保持自发光。材质可以直接编辑，但如果把它直接赋给现有 Renderer，总开关无法知道先前的原材质；要自动恢复请使用 Binding。

角色变体：选中原 **URP Lit Material**，执行 **Assets > Retro Rendering > Create character variant (no assignment)**。只生成新资产，不自动赋给任何对象。复制纹理引用而不是改贴图导入设置。

转换器对不支持的材质明确报错并保留原样：自定义 Shader、Specular workflow、金属/遮蔽/视差/细节图、底色 Alpha 光滑度、加法或预乘混合。正常不透明/裁切/普通 alpha 混合有对应状态。源材质未启用的法线/发光效果不会突然启用。

## 预设

| 预设 | 内部分辨率 | RGB 级数 | 抖色 | 吸附 | 仿射 | 环境法线 |
|---|---:|---:|---:|---:|---:|---|
| Clean Retro | 426×240 | 32 | 0 | 0 | 0 | 平滑 |
| Arcade Horror（默认） | 426×240 | 32 | 0 | 0.15 | 0.15 | 面法线 |
| Aggressive PS1 | 320×240 | 16 | 0.65 | 0.8 | 0.75 | 面法线 |

预设资产位于 `Assets/RetroRendering/Presets/`。总开关优先于所有参数；Native 只关闭降分辨率，不等于关闭其他模块。低分辨率与颜色处理对原材质同样有效，吸附/仿射只作用于主动使用 Retro Shader 的对象。

## 自动和手动验证

- **Tools > Retro Rendering > Validate assets and shaders**：Shader 编译、材质、贴图引用、UV 变换、预设、裁切/混合转换状态检查。
- 在 RetroTest 的 Play Mode 执行 **Run real-frame tests (RetroTest Play Mode)**：冻结测试运动/动画，切换各模块，截图与像素差对比，再恢复参数。输出 `Library/Locus/tmp/retro-validation/automated.txt` 与 PNG。
- 如测试场景不存在，可执行 **Create test assets and scene**；已有文件会保留，不会覆盖用户调参。
- 项目 Unity Test Framework 当前发现 0 个测试；这里提供的是菜单资产验证器和真实帧回归协程。

手动复测顺序：
1. Native ↔ 320×240，检查斜边像素、黑边、比例；Point ↔ Bilinear。
2. 关闭其他模块，把 RGB 调为 4，再改回 32；抖色 0 ↔ 1，确认只有世界画面受影响。
3. 测试相机缓慢移动时将吸附 0 ↔ 1；停止移动后画面不应随机抖动。贴近物体检查近裁面。
4. 旋转棋盘墙，仿射 0 ↔ 1；确认纹理能认出、字不歪。
5. 对照原/变体僵尸，检查动画、皮肤/衣物纹理，单独调整强度。
6. 实际关卡开默认预设，输入半个单词、切轨、观察双怪、威胁色、箭头、字幕和结算；不要给战斗相机加测试运动组件。
7. 反复关闭总开关，确认引用恢复且原场景未被保存改写。

逐项结论、性能数字、未测内容见 `Assets/RetroRendering/Documentation/Verification.md`。

## 限制与接入边界

- 本轮验证范围为 Windows / D3D12 / 当前 PC 管线 / 单个全视口 Base Camera。相机已有 RT、camera stack、非主显示器或 XR 时拒绝启用。Cinemachine 未安装，本工程轨道相机已实测。
- 当前 UI 是置顶词条：分离后的世界空间 UI 不再与 3D 场景深度遮挡。需要真实几何遮挡的其他世界 UI 不应直接接入此方案。
- Layer 5 用于高分辨率 UI；不要把普通场景几何放到这个层。3D 弱点球保留原材质/深度行为，不施加顶点吸附；它仍属于低分辨率世界，屏幕空间准星和取景框保留原生分辨率。
- `Camera.main` 仍是原相机，但启用时其 pixelWidth/Height 是内部 RT 尺寸。未来鼠标屏幕射线/屏幕坐标逻辑应先利用 `ContentRect` 映射，不能假设等于 Screen 尺寸。
- 只保护属于源相机或未指定相机的 Canvas；不会抢走其他相机的 UI。其他多相机合成没有验证。
- 环境 Shader 是刻意的简化光照；角色使用 PBR。烘焙 lightmap、APV、动态运动矢量、TAA、XR、粒子专用 Shader 不在本轮支持范围。阴影投射保持未吸附、透视正确 UV，极端裁切/变形的影子与轮廓可能不完全一致。
- 普通 alpha 混合/法线图/发光图有实现，但现有僵尸没有这些贴图/透明状态；其实际视觉案例未验证，不能视为已验收。
- Shader Model 3.5；移动端、其他图形 API、独立 Player 构建未测试。第一次使用新 Shader 可能有编译卡顿。
- 原生级 UI 的额外相机和材质切换有 CPU 成本；不要假设降分辨率必然提高总帧率。
- 不支持正在运行时任意改 Binding 的 Renderer/材质槽或切换到另一质量管线；这些接线请在 Edit Mode 完成。

## 回滚

1. 取消 `retroEnabled`、禁用控制器，或退出 Play Mode：恢复原目标纹理、视口、相机比例、cullingMask、MSAA、UI 层、Canvas camera、材质引用和原 Quality 管线引用，释放自有 RT/材质/相机。
2. 游戏场景的接入是唯一被保存的改动：`PolePosition.unity` 新增 `Retro Rendering` 对象与各 Venue Renderer 上的 `RetroMaterialBinding`。执行 **Tools > Retro Rendering > Remove from PolePosition scene** 会删除这些组件/对象和 `Materials/PolePosition/`（先保存场景），恢复原始外观；原材质、预制件、FBX/贴图、灯光、`Assets/Settings/` 与 `ProjectSettings/` 从未被修改。
3. 若不保留此功能，先移除自己后来添加的控制器/Binding 和相关引用，再从 Unity 删除本轮新增的 `Assets/RetroRendering/` 内容。**该目录在任务前已有原型，不应直接整目录删除而丢掉原有文件。**
4. 任务前原型及 `.meta` 完整备份在 `Library/Locus/tmp/retro-before/`；需要恢复任务前状态时，用它恢复原有文件，移除本轮新增文件并 Refresh。保留备份，勿用 `git reset --hard`，以免损坏当前未提交的 Locus 包更新。
5. 如果异常退出打断了生命周期，检查 Quality/Graphics 的活动资产应为 `Assets/Settings/PC_RPAsset.asset`，而不是 Retro 副本。
