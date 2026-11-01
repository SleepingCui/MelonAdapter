# 兼容性说明（Compatibility）

本文档回答三个问题：能跑什么、跑不了什么、以及在什么条件下会拒绝加载。

## 1. 版本基线

| 组件 | 基线 | 说明 |
|---|---|---|
| MelonLoader API | 0.7.3.0 | 外观程序集 `MelonLoader.dll` 的 AssemblyVersion 与之相同，模组按名称绑定 |
| UnityModManager | 0.32.5.0 | 唯一的宿主加载器；使用 `ModEntry`、`OnToggle`、`OnUnload`、`ModLogger` |
| .NET Framework | 4.7.2（编译）/ 4.8 运行时 | 目标框架为 `net472`，游戏运行时通常为 4.7.2 或更高 |
| Harmony | 2.x（基线 2.3.6.0） | 由用户放入 `Dependencies/0Harmony.dll`；未提供时适配器仍可启动 |
| Mono | 游戏自带的 Mono | 仅支持 Mono 后端 |

## 2. 支持范围

### 2.1 支持

- **Mono 后端**的 Unity 游戏，通过 UMM 加载。
- 依赖 `MelonLoader.dll` 0.7.3.0 公共 API 的 `MelonMod` / `MelonBase` 模组（属性、生命周期回调、日志、偏好设置、事件、协程、Harmony）。
- 私有依赖放在模组自己的子目录里（`Mods/MyMod/...`），并声明在 `MelonAdditionalDependencies`。
- 单个游戏实例内同时使用 UMM 的其它模组——适配器只接管 `MelonLoader` 身份，不修改 UMM 的 `ModEntry` 行为。
- UMM 面板内的适配器设置界面（查看状态与日志、逐模组禁用、编辑偏好、调试开关）；这是适配器自身的能力，不属于 MelonLoader API。

### 2.2 明确不支持

| 能力 | 原因 | 表现 |
|---|---|---|
| IL2CPP 游戏 | 需要 `Il2CppInterop` 与转译层，不在适配器范围 | 检测到 `GameAssembly.dll` / `il2cpp_data` 时拒绝加载并说明原因 |
| 与真实 MelonLoader 共存 | 两个 `MelonLoader` 程序集身份冲突，补丁与启动器互斥 | 检测到真实 MelonLoader 组件或安装目录时拒绝加载 |
| 真实 MelonLoader 的启动器功能 | 启动器负责注入与进程级初始化，UMM 已有自己的注入 | 未实现 |
| MelonPlugin（插件子系统） | 该子系统需要独立的加载阶段 | 未实现；`MelonPlugin` 类型不存在于外观程序集 |
| `MelonAssembly.LoadRawMelonAssembly` / `MelonHandler.LoadFromFile` / `LoadFromByteArray` | 运行期动态加载会破坏"一次扫描、一次加载"的确定性 | 返回空并在日志中给出错误说明 |
| MelonLoader 内嵌的 `Tomlet` / `TinyJSON` / `Semver` 类型 | 外观程序集不复制第三方内嵌类型 | 引用这些类型的模组会在加载时失败并给出缺失类型说明 |
| MelonLoader 内嵌的 `Harmony.*` 旧命名空间 | 0.7.3.0 已改用 `HarmonyLib` | 引用旧命名空间的模组加载失败 |
| `VerifyLoaderBuild` 哈希校验 | 适配器的构建哈希与官方发行版不同 | 只记录警告，不拒绝加载 |
| 程序集卸载 / 热重载 | Mono 不支持安全卸载 | 开关关闭后必须重启游戏 |

## 3. Harmony 策略

适配器提供**唯一固定**的 Harmony provider，用 `HarmonyLib.Harmony` 实例，按 melon id 分配稳定 owner：

- `MelonBase.HarmonyInstance` 返回该 melon 专属实例；`PatchAll()` 打上的补丁都归属该 owner。
- 撤销时调用 `UnpatchAll(ownerId)`；模组被禁用、注销、开关关闭、退出时都会撤销。
- 不加载、不替换游戏进程里已经存在的 `0Harmony`；如果模组自带私有 `0Harmony.dll` 且版本与 provider 不同，按 `[harmony] conflictPolicy` 处理：

| 策略 | 行为 |
|---|---|
| `refuse`（默认） | 拒绝该模组，原因写入兼容报告 |
| `disable` | 加载但立即置为 `Disabled`，不派发回调 |
| `report` | 仅警告，模组照常加载（可能引发补丁冲突） |

版本相同则视为共享同一份 Harmony，不触发冲突。

## 4. 与 UMM 原生模组的关系

- UMM 模组继续由 UMM 管理其 `ModEntry`、`OnUpdate` 与 GUI；适配器不拦截、不重排它们。
- 适配器在 UMM 的 `Load` 里同步完成初始化，因此 Melon 模组的 `OnInitializeMelon` 早于第一个 `Update`。
- UMM 的开关只影响适配器本身：关闭后 Melon 模组停止回调、保存偏好、撤销补丁，但程序集仍驻留内存。

## 5. 已知行为差异

| 项目 | MelonLoader 0.7.3.0 | MelonAdapter 1.2.2 |
|---|---|---|
| 属性标签 | 全部 | 全部（含 `MelonIncompatibleAssemblies`、`VerifyLoaderVersion`） |
| 日志颜色 | 控制台颜色 | 接受颜色参数但不输出 ANSI 序列（Unity 控制台与日志文件均为纯文本） |
| 偏好设置路径 | `<Game>/UserData/MelonPreferences.cfg` | `<Game>/MelonAdapter/UserData/MelonPreferences.cfg`（可选一次性导入旧文件） |
| 用户数据目录 | `<Game>/UserData/<mod>/` | `<Game>/MelonAdapter/UserData/<mod>/` |
| `MelonEnvironment.ModsDirectory` | `<Game>/Mods` | `<Game>/MelonAdapter/Mods`（可用环境变量 `MELONADAPTER_MODS_DIRECTORY` 覆盖） |
| 启动横幅 | MelonLoader 版本与游戏信息 | 额外包含 Host 加载器与 API 基线 |
| `OnPreferencesLoaded` / `OnPreferencesSaved` | 文件变化时触发 | 显式 `Load()` / `Save()` 时触发 |
| 运行期禁用单个模组 | 无（需改配置并重启） | `AdapterRuntime.DisableMod` + UMM 设置界面的 **Disable now**；程序集仍驻留内存 |
| 界面语言 | 无图形界面 | 设置界面内置中文 / English，语言表来自 `<适配器目录>/lang.json`（缺失或损坏时用程序集内嵌副本），选择写入 `[ui] language` |

## 6. 拒绝加载的条件

启动前（`ConflictDetector`）命中任一条即拒绝，并在 UMM 日志与控制台说明：

1. 已加载来自其它目录的 `MelonLoader` 程序集；
2. 已加载真实 MelonLoader 组件（`MelonLoader.Core`、`MelonStartScreen`、`MelonLoader.ModHandler`、`MelonLoader.Utils`、`MelonLoader.Bootstrap`）；
3. 游戏根目录存在 `GameAssembly.dll` 或 `il2cpp_data`（IL2CPP）；
4. 游戏根目录存在 `MelonLoader/` 安装目录或 `MelonLoader.dll`。

单个模组层面（不影响其它模组）的拒绝条件见 `spec/ASSEMBLY_RESOLUTION.md` 第 7 节与 `spec/LIFECYCLE.md` 第 7 节。

## 7. 未验证项

以下项目在文档写作时尚未在真实游戏上验证，属于发布前的待办：

- 真实 Unity 版本（2018 / 2019 / 2020 / 2021 LTS）下的场景回调与 GUI 回调；
- 大型模组（>50 个程序集）下的扫描与解析耗时；
- 与 BepInEx、UnityFileDialog 等其它注入器同时存在的场景；
- 32 位游戏进程。

验证矩阵与发布门禁见 `docs/VALIDATION.md`。
