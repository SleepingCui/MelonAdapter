# 兼容性说明（Compatibility）

本文档列出支持范围、已知差异和拒绝加载条件。

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

- 通过 UMM 加载的 Unity Mono 游戏。
- 使用 MelonLoader 0.7.3.0 公共 API 的 `MelonMod` / `MelonBase` 模组。
- 位于模组子目录且已声明的私有依赖。
- 与 UMM 自带的其它模组共存；适配器不更改 UMM 的 `ModEntry`。
- MelonAdapter 自带的设置界面（状态、日志、偏好、调试和禁用）。

### 2.2 明确不支持

| 能力 | 原因 | 表现 |
|---|---|---|
| IL2CPP 游戏 | 需要 `Il2CppInterop` 与转译层，不在适配器范围 | 检测到 `GameAssembly.dll` / `il2cpp_data` 时拒绝加载并说明原因 |
| 与真实 MelonLoader 共存 | 两个 `MelonLoader` 程序集身份冲突，补丁与启动器互斥 | 检测到真实 MelonLoader 组件或安装目录时拒绝加载 |
| 真实 MelonLoader 的启动器功能 | 启动器负责注入与进程级初始化，UMM 已有自己的注入 | 未实现 |
| MelonPlugin（插件子系统） | 该子系统需要独立的加载阶段 | 未实现；`MelonPlugin` 类型不存在于外观程序集 |
| `MelonAssembly.LoadRawMelonAssembly` / `MelonHandler.LoadFromFile` / `LoadFromByteArray` | 不支持运行期动态加载 | 返回空并记录错误 |
| MelonLoader 内嵌的 `Tomlet` / `TinyJSON` 类型 | 不复制第三方内嵌类型 | 引用时因缺少类型而加载失败 |
| MelonLoader 内嵌的 `Harmony.*` 旧命名空间 | 0.7.3.0 已改用 `HarmonyLib` | 引用旧命名空间的模组加载失败 |
| `VerifyLoaderBuild` 哈希校验 | 适配器的构建哈希与官方发行版不同 | 只记录警告，不拒绝加载 |
| 程序集卸载 / 热重载 | Mono 不支持安全卸载 | 开关关闭后必须重启游戏 |

facade 提供 `Semver.SemVersion`。

## 3. Harmony 策略

适配器使用唯一 Harmony provider，并按 melon id 分配稳定 owner：

- `MelonBase.HarmonyInstance` 返回该 melon 的实例；其补丁归属该 owner。
- 禁用、注销、关闭适配器或退出时调用 `UnpatchAll(ownerId)`。
- 不替换进程中已加载的 `0Harmony`。模组携带不同版本时按 `[harmony] conflictPolicy` 处理：

| 策略 | 行为 |
|---|---|
| `refuse`（默认） | 拒绝该模组，原因写入兼容报告 |
| `disable` | 加载但立即置为 `Disabled`，不派发回调 |
| `report` | 仅警告，模组照常加载（可能引发补丁冲突） |

版本相同则视为共享同一份 Harmony，不触发冲突。

## 4. 与 UMM 原生模组的关系

- UMM 继续管理原生模组的 `ModEntry`、更新和 GUI；适配器不拦截或重排。
- 适配器在 UMM `Load` 中同步初始化，Melon 模组的 `OnInitializeMelon` 早于首个 `Update`。
- 关闭适配器会停止 Melon 回调、保存偏好并撤销补丁；程序集仍驻留内存。

## 5. 已知行为差异

| 项目 | MelonLoader 0.7.3.0 | MelonAdapter 1.2.2 |
|---|---|---|
| 属性标签 | 全部 | 全部（含 `MelonIncompatibleAssemblies`、`VerifyLoaderVersion`） |
| 日志颜色 | 控制台颜色 | 接受颜色参数但不输出 ANSI 序列（Unity 控制台与日志文件均为纯文本） |
| 偏好设置路径 | `<Game>/UserData/MelonPreferences.cfg` | `<适配器目录>/UserData/MelonPreferences.cfg`（可选一次性导入旧文件） |
| 用户数据目录 | `<Game>/UserData/<mod>/` | `<适配器目录>/UserData/<mod>/` |
| `MelonEnvironment.ModsDirectory` | `<Game>/Mods` | `<适配器目录>/Mods`（可用环境变量 `MELONADAPTER_MODS_DIRECTORY` 覆盖） |
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

以下场景仍需真实游戏验证：

- 真实 Unity 版本（2018 / 2019 / 2020 / 2021 LTS）下的场景回调与 GUI 回调；
- 大型模组（>50 个程序集）下的扫描与解析耗时；
- 与 BepInEx、UnityFileDialog 等其它注入器同时存在的场景；
- 32 位游戏进程。

验证矩阵与发布门禁见 `docs/VALIDATION.md`。
