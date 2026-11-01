# MelonAdapter

**在 UnityModManager（UMM）宿主里运行 Mono 版 MelonLoader `MelonMod` 的适配框架。**

MelonAdapter 不是 MelonLoader 的替代品，而是一层有明确 API 边界的适配运行时：它提供身份与 MelonLoader **0.7.3.0** 一致的 `MelonLoader.dll` facade，让按该基线编译的模组在 UMM 加载的 Mono 游戏里完成发现、依赖解析、实例化、生命周期回调、日志、偏好设置、事件、协程与 Harmony 接入。

| 项目 | 取值 |
|---|---|
| 产品名 | MelonAdapter（MelonMod Adapter for UMM） |
| 程序集/命名空间前缀 | `MelonAdapter.*` |
| MelonLoader API 基线 | **0.7.3.0**（facade 程序集名 `MelonLoader`，AssemblyVersion 0.7.3.0，无强名） |
| 运行时 | Unity Mono / .NET Framework 4.7.2 |
| 宿主 | UnityModManager 0.32.x（`Load(ModEntry)` 入口） |
| Harmony | 单一提供方（Harmony 2.x `0Harmony.dll`），按 melon 跟踪 owner |
| 后端 | **仅 Mono**；IL2CPP、MelonPlugin、与真实 MelonLoader 共存均不支持 |
| 适配器版本 | 1.2.2 |

## 当前状态

- `dotnet build MelonAdapter.slnx -m:1 -nodeReuse:false`：**0 警告 0 错误**。
- `MelonAdapter.SelfTest`（无 Unity 的 headless 驱动）：**74 项检查全部通过**，覆盖发现、依赖图、生命周期、偏好设置迁移与归属、语言文件加载与回退、Harmony、异常隔离、手动禁用、UMM 开关与退出清理。
- UMM 面板内的设置界面已实现：状态总览与日志尾、模组详情与即时禁用、按模组分组的偏好编辑、调试开关、Harmony 策略与中/英界面语言切换；页面使用固定高度的内部滚动视口（不再被 UMM 的模组行压成一条）。
- API 保真：facade 与 0.7.3.0 基线的成员签名逐条比对，参数形状相同但完整签名不同的方法为 **0**；对游戏内真实模组的 `MelonLoader` 引用做成员级校验，29 个引用全部可绑定（见 `docs/VALIDATION.md` §1.1）。
- 已在真实游戏（*A Dance of Fire and Ice* + UMM）上验证：TimingShow 1.9.3 正常加载；Jipper Key Viewer 因八参 `CreateEntry` 缺失抛出的 `MissingMethodException` 由 1.2.2 修复。`spec/COMPATIBILITY.md` 与 `docs/VALIDATION.md` 仍把未覆盖的场景标注为"待游戏验证"。

## 架构

```text
Unity 游戏（Mono）
└── UnityModManager
    └── MelonAdapter.dll              UMM 入口：Load(ModEntry) / OnToggle / OnUnload / OnGUI 设置面板 / 冲突检测
        ├── MelonAdapter.Ticker      隐藏的 MonoBehaviour，唯一 Unity 主线程调度器
        └── MelonAdapter.Runtime.dll 扫描、依赖图、加载、生命周期、日志、偏好、事件清理、Harmony 归属
            ├── MelonLoader.dll      API facade（身份与基线一致，零 UnityEngine 依赖）
            ├── 0Harmony.dll         单一 Harmony 提供方（由用户放在 Dependencies/）
            └── <Game>/MelonAdapter/Mods/*.dll   被适配的 MelonMod 程序集
```

关键约束：目标模组在被反射读取类型**之前**就必须能解析 `MelonLoader` 引用，因此 `MelonAdapter.dll` 通过 `[ModuleInitializer]` 安装自己的 `AssemblyResolve`（只处理 `MelonLoader`、`MelonAdapter.Runtime`、`0Harmony`），不依赖 UMM 的私有实现细节。

## 仓库结构

```text
src/
  MelonLoader.Facade/        MelonLoader.dll：属性、MelonBase/MelonMod、日志、偏好、事件、协程、工具
  MelonAdapter.Runtime/     发现/依赖图/加载/生命周期/偏好存储/兼容报告/Harmony 提供方
  MelonAdapter.Host/        UMM 入口（AssemblyName 为 MelonAdapter）、Unity 调度器、冲突检测、设置面板（AdapterUI）
samples/
  SampleMod/                回调 + 偏好 + 事件 + 协程 + Harmony 的示例模组
  SampleMod.MissingDependency/  声明了不存在依赖的示例（应被拒绝）
  SampleMod.Throwing/       每次 Update 抛异常的示例（应被限次禁用）
  SupportLib/               SampleMod 的私有依赖（放在 Mods/SampleMod/ 下）
tests/MelonAdapter.SelfTest/    headless 验收程序（74 项检查）
spec/                       SUPPORTED_API.md / COMPATIBILITY.md / LIFECYCLE.md / ASSEMBLY_RESOLUTION.md
docs/                       CONFIGURATION.md / VALIDATION.md / THIRD-PARTY-NOTICES.md
packaging/UMM/MelonAdapter/ UMM 可安装目录（Info.json + README.txt，DLL 由构建脚本放入 dist/）
```

## 构建

```powershell
# Windows PowerShell / pwsh 均可；本仓库已固定 -m:1，原因见下
./build.ps1            # 还原、构建、跑 SelfTest，并把发布包整理到 dist/
./build.ps1 -SkipTest  # 只构建
```

或手工：

```powershell
dotnet build MelonAdapter.slnx -m:1 -nodeReuse:false
.\tests\MelonAdapter.SelfTest\bin\Debug\net472\MelonAdapter.SelfTest.exe
```

**为什么必须 `-m:1`：**在受限沙箱（以及部分 CI）里，MSBuild 的多节点构建需要命名管道，节点进程会静默崩溃，表现为"生成失败，0 个错误"这种没有诊断的失败。单节点构建稳定；`build.ps1` 已内置该参数，用 Visual Studio 的 MSBuild 并行构建通常没有问题。

依赖：`libs/` 下需存在 `UnityModManager.dll`、`UnityEngine*.dll`、`0Harmony.dll`（构建期引用，`Private=false`，不会复制到输出）。`libs/` 不入库。

## 安装（UMM）

1. 把发布包内容放进 `<Game>/Mods/MelonAdapter/`：

   ```text
   Mods/MelonAdapter/
   ├── Info.json
   ├── MelonAdapter.dll
   ├── MelonAdapter.Runtime.dll
   ├── MelonLoader.dll
   ├── lang.json                     界面语言文件（可编辑；缺失或损坏时用程序集内嵌副本）
   ├── README.txt
   └── Dependencies/0Harmony.dll     由用户/发行方提供，见 docs/THIRD-PARTY-NOTICES.md
   ```

2. 启动游戏一次：适配器创建 `<Game>/MelonAdapter/{Config.toml, Logs/, Mods/, UserData/}`。
3. 把要运行的 MelonMod 放到 `<Game>/MelonAdapter/Mods/`；有私有依赖的模组可用子目录（`Mods/MyMod/MyMod.dll` + 依赖）。
4. 重启游戏。UMM 的 Mods 列表里可以看到 `MelonAdapter`，开关状态写入 UMM 自己的配置。

**不要**同时安装真实 MelonLoader：发现真实 MelonLoader 组件、IL2CPP 游戏（`GameAssembly.dll` / `il2cpp_data`）或残留的 `<Game>/MelonLoader/` 时，适配器会打印冲突原因并拒绝加载任何子模组。

## 使用

| 想知道什么 | 看哪里 |
|---|---|
| 适配器与模组加载顺序 | `<Game>/MelonAdapter/Logs/MelonAdapter.log`（头部有游戏、Unity、Mono、UMM、适配器、API 基线、Harmony 版本横幅） |
| 每个模组为什么成功/失败 | 同上的 `[mods]` / `[graph]` / `[callbacks]` 行，以及兼容报告 |
| 结构化兼容结论 | `<Game>/MelonAdapter/Logs/CompatibilityReport.txt` |
| 模组设置 | `<Game>/MelonAdapter/UserData/MelonPreferences.cfg`（TOML，首次启动可从 `<Game>/UserData/MelonPreferences.cfg` 单向迁移并留 `.bak` 备份） |
| 模组私有数据 | `<Game>/MelonAdapter/UserData/<mod-id>/`（启动时为每个 melon 预先创建） |
| 游戏内调参 | UMM 面板里点击 MelonAdapter 打开的设置界面（Overview / Mods / Preferences / Settings） |
| 界面语言 | `<Game>/MelonAdapter/lang.json`（中/英，可自行修改）；当前选择写入 `Config.toml` 的 `[ui] language` |

UMM 里关闭 MelonAdapter：停止派发、保存偏好、退订事件、撤销可追踪的 Harmony patch；**程序集无法卸载**，再次启用只会提示"需要重启游戏"，不会假装已恢复。

## 游戏内设置界面

在 UMM 的 Mods 列表里点击 MelonAdapter 的条目（UMM 的 "Show GUI" 开关）即可打开设置界面，共四个标签页：

| 标签页 | 能做什么 |
|---|---|
| Overview | 版本横幅（游戏 / Unity / Mono / UMM / 适配器 / API 基线 / Harmony）、模组状态计数、一键打开适配器/模组/用户数据目录、日志尾部 40 行（每 2 秒自动刷新） |
| Mods | 模组列表与详情（状态、失败原因、程序集路径、依赖、优先级、Hash、注册的 melon 类型、备注/警告）；打开模组目录或该模组的私有数据目录；跳到该模组的偏好设置；**立即禁用**；加入/移出下次启动禁用列表；写兼容报告 |
| Preferences | 按"创建该分类的程序集"分组，逐条目编辑布尔 / 字符串 / 数字 / 枚举值，支持 Apply、重置为默认、保存到磁盘与从磁盘重新载入 |
| Settings | 界面语言（中文 / English，切换立即生效并写回配置）、重新加载 `lang.json`、即时开关调试日志（写入后续日志与 `MelonDebug.IsEnabled()`）、关闭适配器、Harmony 冲突策略、异常阈值、解析器诊断与"允许无元数据 DLL"开关、保存 Config.toml、打开配置与日志 |

几个必须知道的行为：

- 界面语言来自 `<Game>/MelonAdapter/lang.json`：分 `zh` / `en` 两张表，键缺失时按"当前语言 → 英文 → 任一语言 → 键名"回退。发布包自带该文件，删掉也能启动（使用程序集内嵌副本）。默认 `[ui] language = "auto"`：系统 UI 语言是中文就用中文，否则英文；在 Settings 里选择后写入显式 `zh` / `en`。
- 个别 Unity 版本的内置字体没有中文字形，中文会显示成方块；此时把语言切到 English 即可，界面不会因此出错。
- **立即禁用**（`AdapterRuntime.DisableMod`）只对当前会话生效，程序集仍驻留内存；要让下次启动不再加载，用 **Skip next launch**（写入 `[lifecycle] disabledMods` 并保存配置）。
- 适配器一旦在本次会话里被关闭，无法在界面中重新启用（Mono 不能卸载程序集），只能重启游戏。
- 界面只读写适配器自己的 `Config.toml` 与 `UserData/MelonPreferences.cfg`，不会碰 UMM 的配置，也不会碰真实 MelonLoader 的目录。
- 设置面板自带固定高度的滚动视口（约为屏幕高度的 62% 减 150 个 GUI 单位，并限制在 240–520 之间）。UMM 把模组页面放在自己的滚动视图里，没有显式高度的 `BeginScrollView` 会被压成几十像素，因此这里必须自己给高度；内容超长时在面板内部滚动，切换标签页会回到顶部。

## 文档

- [`spec/SUPPORTED_API.md`](spec/SUPPORTED_API.md)：已实现/有差异/未实现的 API 清单。
- [`spec/COMPATIBILITY.md`](spec/COMPATIBILITY.md)：兼容矩阵、冲突检测、已知限制与逐模组验收记录模板。
- [`spec/LIFECYCLE.md`](spec/LIFECYCLE.md)：固定回调顺序、异常策略、开关与退出语义。
- [`spec/ASSEMBLY_RESOLUTION.md`](spec/ASSEMBLY_RESOLUTION.md)：程序集解析范围、探测顺序、版本/重复/网络路径规则。
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)：`Config.toml` 全部键、默认值、损坏处理、路径规则。
- [`docs/VALIDATION.md`](docs/VALIDATION.md)：阶段 8 验证矩阵与首版发布门禁的对照结果。

## 许可与第三方

本仓库代码为 MelonAdapter 自有实现；不包含、也不捆绑 MelonLoader 官方二进制。Harmony（`0Harmony.dll`）、UnityModManager、Unity 引擎程序集均由用户自备，授权与分发约束见 [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md)。
