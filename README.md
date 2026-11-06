# MelonAdapter

> [!WARNING]
> 本 Mod 使用过程中可能遇到 Bug、兼容性问题或其他异常，请谨慎使用。

MelonAdapter 让 UnityModManager（UMM）加载使用 MelonLoader 0.7.3.0 API 编写的 `MelonMod`。

| 项目 | 信息 |
|---|---|
| 适配器版本 | 1.2.2 |
| API 基线 | MelonLoader 0.7.3.0 |
| 运行时 | Unity Mono / .NET Framework 4.7.2 |
| 宿主 | UnityModManager 0.32.x |
| Harmony | Harmony 2.x，由 `Dependencies/0Harmony.dll` 提供 |

> [!IMPORTANT]
> **不支持 IL2CPP 游戏，仅适用于 Unity Mono 游戏。**
> 
> **不支持 MelonPlugin、与真实 MelonLoader 共存或运行期卸载程序集。**



## 架构

```text
Unity Mono 游戏
└── UnityModManager
    └── MelonAdapter.dll              UMM 入口、调度器、设置界面
        └── MelonAdapter.Runtime.dll  扫描、依赖、生命周期、日志、偏好、Harmony
            ├── MelonLoader.dll       0.7.3.0 API facade
            ├── 0Harmony.dll          共享 Harmony provider
            └── <适配器目录>/Mods/*.dll
```

Host 在读取模组类型前安装 `AssemblyResolve`，确保能解析 `MelonLoader`、`MelonAdapter.Runtime` 和 `0Harmony`。解析器不替换游戏程序集。

## 仓库结构

| 路径 | 内容 |
|---|---|
| `src/MelonLoader.Facade/` | `MelonLoader.dll` 公共 API |
| `src/MelonAdapter.Runtime/` | 扫描、加载、生命周期与运行时服务 |
| `src/MelonAdapter.Host/` | UMM 入口、Unity 调度器和设置界面 |
| `samples/` | 示例模组及私有依赖 |
| `tests/MelonAdapter.SelfTest/` | 无 Unity 的自检程序 |
| `spec/`、`docs/` | API、兼容性、生命周期、配置与验证说明 |

## 构建

```powershell
./build.ps1            # 构建、自检并整理发布包
./build.ps1 -SkipTest  # 仅构建
```

手工构建：

```powershell
dotnet build MelonAdapter.slnx -m:1 -nodeReuse:false
.\\tests\\MelonAdapter.SelfTest\\bin\\Debug\\net472\\MelonAdapter.SelfTest.exe
```

使用 `-m:1` 可避免部分沙箱和 CI 环境中的 MSBuild 多节点管道故障；`build.ps1` 已包含此参数。构建需要 `libs/` 中的 UMM、Unity、Harmony 和 Newtonsoft.Json 程序集；`libs/` 不入库，运行时引用不随发布包分发。

## 安装

1. 将发布包放入 `<Game>/Mods/MelonAdapter/`。
2. 首次启动后，适配器会在数据目录创建配置、日志、模组和用户数据目录。默认数据目录是 `<Game>/Mods/MelonAdapter/`；已有 `<Game>/MelonAdapter/` 安装时会继续使用该目录。
3. 将 Melon 模组放入 `<适配器目录>/Mods/`。私有依赖可放在模组子目录中。
4. 重启游戏，在 UMM 中启用 MelonAdapter。

发布包需要 `MelonAdapter.dll`、`MelonAdapter.Runtime.dll`、`MelonLoader.dll`、`lang.json` 和 `Info.json`。将 `0Harmony.dll` 放入 `Dependencies/`；分发说明见 [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md)。

检测到真实 MelonLoader、IL2CPP 布局或残留的 `<Game>/MelonLoader/` 时，适配器会拒绝加载模组。

## 使用与设置

| 内容 | 位置 |
|---|---|
| 运行日志 | `<适配器目录>/Logs/MelonAdapter.log` |
| 兼容报告 | `<适配器目录>/Logs/CompatibilityReport.txt` |
| 配置 | `<适配器目录>/Config.toml` |
| 偏好设置 | `<适配器目录>/UserData/MelonPreferences.cfg` |
| 界面语言 | `<适配器目录>/lang.json`；缺失或损坏时使用内嵌副本 |

点击 UMM 中的 MelonAdapter 条目可打开设置界面，查看模组状态和日志、编辑偏好、调整配置或切换中英文。完整选项见 [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)。

## 文档

- [`spec/SUPPORTED_API.md`](spec/SUPPORTED_API.md)：支持的 MelonLoader API 及差异。
- [`spec/COMPATIBILITY.md`](spec/COMPATIBILITY.md)：支持范围、冲突条件和已知限制。
- [`spec/LIFECYCLE.md`](spec/LIFECYCLE.md)：回调顺序、异常处理和关闭行为。
- [`spec/ASSEMBLY_RESOLUTION.md`](spec/ASSEMBLY_RESOLUTION.md)：依赖解析和诊断。
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)：配置项、目录和界面设置。
- [`docs/VALIDATION.md`](docs/VALIDATION.md)：自检、游戏内验证和发布门禁。

## 许可

MelonAdapter 是独立实现，不包含 MelonLoader 官方二进制。Harmony、UnityModManager 和 Unity 程序集由用户自备；许可信息见 [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md)。
