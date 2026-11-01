# 验证矩阵与发布门禁

本文档对应需求文档的第 8 阶段：用可复现的样例覆盖全部生命周期与失败路径，并给出发布前必须满足的条件。

## 1. 验证方式

| 方式 | 覆盖面 | 运行条件 |
|---|---|---|
| `tests/MelonAdapter.SelfTest`（无头控制台） | 配置、扫描、依赖图、生命周期、回调、偏好、事件、协程、Harmony、界面语言、报告、开关、退出 | `dotnet build` + 直接运行 exe，无需 Unity |
| 真实游戏 + UMM | Unity 侧帧循环、场景回调、GUI、UMM 面板与适配器设置界面 | 需要一个 Mono 后端 Unity 游戏 |

无头自检用假的 `ITickSource` 驱动运行时，因此不依赖 UnityEngine；它验证的是运行时的行为契约，Unity 侧接线由第 5 节的人工步骤覆盖。

## 1.1 对真实模组的引用校验

无头自检只能证明适配器自己的契约。判断某个真实模组能否绑定到 facade，用的是元数据级别而不是“能加载就行”：

- 把 MelonLoader 0.7.3.0 基线程序集与 `MelonLoader.dll` 逐成员比对，参数形状相同但完整签名（含返回类型）不同的方法会被单独列出——CLR 按完整签名绑定，这类差异在运行期表现为 `MissingMethodException`，不会在编译期报错。1.2.2 起该列表为 0。
- 再读取真实模组程序集的 `TypeRef` / `MemberRef`，只保留 scope 为 `MelonLoader` 的引用，逐个确认 facade 中存在同名类型与同参数形状的方法/字段。仓库内的 `.scratch/apidiff` 是这套检查的本地工具（不随发布包分发）。
- 1.2.2 的实测结果：游戏目录内两个 Melon 加载器（Jipper Key Viewer、TimingShow）合计 29 个 `MelonLoader` 引用，全部可绑定。

## 2. 样例模组

| 样例 | 目的 | 期望结果 |
|---|---|---|
| `samples/SampleMod` | 全量回调、日志、偏好、事件、协程、Harmony 补丁 | 加载成功；生命周期日志齐全；补丁生效（目标方法返回 42，撤销后回到 21） |
| `samples/SampleMod.MissingDependency` | `MelonAdditionalDependencies` 声明了不存在的依赖 | 被拒绝，原因包含 `Missing required dependency`，其它模组不受影响 |
| `samples/SampleMod.Throwing` | `OnUpdate` 等回调持续抛异常 | 连续异常达到阈值后被禁用，日志包含 `consecutive`，其余模组继续运行 |
| `samples/SupportLib` | 私有依赖（放在 `Mods/Support/`） | 不当作模组；作为依赖被解析；`SampleMod` 能调用到 `support-lib-ok` |
| `SampleMod.Copy.dll`（自检时复制） | 重复 id | 后到者被拒绝，原因包含 `Duplicate mod id` |

## 3. 自检覆盖（74 项）

| 组 | 断言要点 |
|---|---|
| 配置 | 默认文件生成；包含 `[api]` / `[harmony]` / `[ui]` / `[lifecycle]` 段；默认值正确（`[ui] language = "auto"`） |
| 配置损坏 | 原文件被改名为 `Config.toml.invalid-*`；生成新默认文件；按默认值启动 |
| 启动 | 目录创建；`Initialize()` 完成；首个 `Tick` 之前未发生应用启动 |
| 扫描 | 三个模组记录；`SupportLib` 不是模组；没有把文件复制进游戏根 `Mods/` |
| 横幅 | 包含 `MelonLoader API baseline`、`Harmony:`、Host 加载器信息 |
| 帧与场景 | 10 次 Tick/FixedUpdate/LateUpdate 与一次 OnGUI；场景加载/卸载序列 |
| 异常模组 | 被禁用，原因包含 `consecutive` |
| Harmony | 补丁生效（42）；退出后撤销（21） |
| 生命周期日志 | `OnInitializeMelon` / `OnApplicationStart` / `OnUpdate` / `OnFixedUpdate` / `OnLateUpdate` / `OnGUI` / 场景初始化 / 事件 / 协程完成 / 私有依赖调用 |
| 偏好设置 | 保存后文件包含模组名与条目名 |
| 用户数据 | `<UserData>/<melon-id>/` 已创建 |
| 迁移 | 旧 `MelonPreferences.cfg` 被导入一次并留下备份；旧文件仍在；迁移值可保存 |
| 报告 | 文件存在，含模组名与已知缺口段 |
| 偏好归属 | `SampleMod` 分类记录了创建者程序集（设置界面按模组分组的前提） |
| API 形状 | 八参 `CreateEntry` 能重命名旧条目并保留值；静态 `CreateEntry` 全签名可用；`MelonLogger` 回调事件被触发；`MelonInfoAttribute.SemanticVersion` 解析正确；`ModPrefs.GetPrefs` 返回旧版表结构；`MelonEnvironment` 位于 `MelonLoader.Utils` 且指向游戏根；`FindMelon(name, author)` 可用 |
| 语言加载 | 外部 `lang.json` 被合并覆盖而非替换内置表（`custom.runtime` 与内置 `tab.mods` 同时可用）；`SourcePath` 指向文件；按配置使用 `zh` |
| 语言 | 运行时切换 `en` 后界面文案变化；拒绝未知语言；缺键回退到键名并计入 `MissingKeyCount`；内置 `zh`/`en` 键集合互相不缺；损坏文件降级为内嵌表且 `SourcePath` 为 `null`；缺失文件不算错误；`auto` 得到 `zh` 或 `en` |
| 手动禁用 | 未知 id 返回 `false`；已停止的模组不能被再次禁用 |
| 开关 | 关闭后日志包含 `A game restart is required`；后续 Tick 不再派发 |
| 退出 | `OnApplicationQuit` / `OnDeinitializeMelon` 被调用；Tick 计数不再增长；补丁撤销 |

## 4. 兼容报告结构

`Logs/CompatibilityReport.txt` 每次启动和每次状态变化后重写：

```text
[Environment]
Game / Game directory / Unity / Process / Adapter / API baseline /
Host loader / Harmony provider / Preferences / Adapter enabled

[Mods]
<文件名>  state=Loaded|Rejected|Disabled|Failed
  id / version / hash / reason / requires / optional / warnings / notes

[Assembly resolution]
<请求方 -> 目标>  resolved from <path> | not found

[Known API gaps]
...
```

报告是给人看与给支持人员看的，格式稳定、可 diff。

## 5. 人工步骤（真实游戏）

1. 按 `packaging/UMM/MelonAdapter/` 的布局安装，启动游戏；确认 UMM 面板出现 `MelonAdapter` 条目。
2. 检查 `<适配器目录>/Logs/MelonAdapter.log`：横幅、扫描结果、`Initialization complete`。
3. 放入 `SampleMod`，重启；确认日志出现 `OnInitializeMelon`、首帧 `OnApplicationStart`、每帧 `OnUpdate`。
4. 进入一个场景：确认 `OnSceneWasLoaded` / `OnSceneWasInitialized` 顺序与 `spec/LIFECYCLE.md` 一致。
5. 打开 UMM 面板里的 MelonAdapter 设置界面：
   - 面板应有明显高度（约半屏），内容超出时在面板内部滚动，而不是只露出标签栏和一两行；四个标签页来回切换时都应从顶部开始；
   - **Overview**：版本横幅完整，日志尾显示最近 40 行；
   - **Mods**：能看到 `Sample Mod`，状态 `Loaded`，能打开模组目录与私有数据目录，能跳到该模组的偏好分类；
   - **Preferences**：能改一个值并保存，重启后值仍在；
   - **Settings**：切换调试日志后日志立即出现 `DEBUG` 行；修改 Harmony 策略并 **Save configuration** 后 `Config.toml` 内容变化。
6. 在 **Settings** 页切换界面语言：
   - 选 `中文` 后整页立即变为中文，`Config.toml` 出现 `[ui]` 段与 `language = "zh"`；选 `English` 立即变回英文；
   - 语言区块下方应显示当前语言文件来源（`<适配器目录>/lang.json` 或内嵌副本）；
   - 把 `lang.json` 改成非法 JSON 后点 **Reload language**：界面仍可用（回退内嵌表），并显示解析错误与缺失键计数；恢复文件后再点一次，错误消失；
   - 删除 `lang.json` 后重启：来源显示 `built-in (lang.json not found)`，界面保持所选语言。
7. 在 Mods 页对 `Sample Mod` 点 **Disable now**：确认日志出现禁用原因、之后没有该模组的 `OnUpdate`、Harmony 补丁已撤销；点 **Skip next launch** 后重启，确认该模组不再加载。
8. 在 UMM 面板关闭适配器：确认日志给出"需要重启"的说明，且没有新的模组回调；界面里无法重新启用。
9. 退出游戏：确认 `Shutdown complete`，偏好已写入，Harmony 补丁已撤销。

## 6. 发布门禁

发布前下列条目必须全部为"是"：

- [ ] `dotnet build MelonAdapter.slnx -m:1 -nodeReuse:false` 结果 0 警告、0 错误；
- [ ] `tests/MelonAdapter.SelfTest` 退出码 0 且输出 `PASS`；
- [ ] `spec/`（4 篇）与 `docs/`（配置、验证）齐全且与实现一致；
- [ ] `packaging/UMM/MelonAdapter/` 可直接复制进 UMM 的 `Mods/` 并使用；
- [ ] 冲突检测在以下四种场景下都能拒绝加载：已有外来 MelonLoader 程序集、真实 MelonLoader 组件、IL2CPP 游戏、游戏根残留 `MelonLoader/` 或 `MelonLoader.dll`；
- [ ] 无 0Harmony 时仍能启动，并在日志中给出可执行的提示；
- [ ] 单个模组加载失败不会阻止其它模组加载；
- [ ] UMM 设置界面的四个标签页可打开、面板保持固定高度并可内部滚动，且能完成一次"禁用模组 → 保存偏好 → 保存配置"的闭环；
- [ ] 中英文切换即时生效并写入 `[ui] language`，`lang.json` 缺失或损坏时回退内嵌表且不阻止启动；
- [ ] 文档未宣称任何未验证的能力（IL2CPP、MelonPlugin、真实 MelonLoader 共存）。

门禁脚本见仓库根目录的 `build.ps1`（构建 + 自检 + 打包）。
