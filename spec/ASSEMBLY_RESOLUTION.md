# 程序集解析（Assembly Resolution）

模组 DLL 在运行时加载，基类和属性来自 `MelonLoader.dll`。适配器须在读取模组类型前解析自身依赖，并避免替换游戏程序集。

## 1. 三层解析

| 层 | 安装时机 | 负责 |
|---|---|---|
| A. 适配器自身解析 | `MelonAdapter.dll` 的 `[ModuleInitializer]`，即 UMM 首次触碰该模块时 | `MelonLoader`、`MelonAdapter.Runtime`、`0Harmony` 从适配器目录与 `Dependencies/` 解析 |
| B. 模组扫描期解析 | `ModScanner` 读取每个 DLL 时，`ModAssemblyResolver.ProbingDirectory` 指向该文件所在目录 | 模组的私有依赖（同目录或子目录） |
| C. 运行期解析 | 初始化时安装的 `AppDomain.AssemblyResolve` | 加载后才触发的依赖请求、依赖图登记的私有程序集 |

A 层使用字节加载（`Assembly.Load(byte[])`），因此支持网络位置上的游戏（见第 6 节）。

## 2. 探测顺序

对非受保护程序集，按下列顺序尝试，命中即停止并记录来源：

| # | 目录 | 说明 |
|---|---|---|
| 1 | 拥有者模组目录 `record.DirectoryPath` | `Mods/MyMod/MyMod.dll` 的私有依赖 |
| 2 | 当前探测目录 `ProbingDirectory` | 扫描到该 DLL 时所在的目录 |
| 3 | 依赖图登记的路径 | `MelonAdditionalDependencies` 解析到的私有程序集 |
| 4 | `<adapter>/Dependencies/` | 适配器共享依赖（例如 `0Harmony.dll`） |
| 5 | `<adapter>/Mods/` | 适配器管理的模组根目录 |
| 6 | `<Game>/` | 游戏根目录 |
| 7 | `<Game>/Managed/` | 旧式（Unity 4/5 早期）托管程序集目录 |
| 8 | `<Game>/<Product>_Data/Managed/` | Unity 标准构建的托管程序集目录 |
| 9 | `<Game>/<Product>_Data/Managed/UnityModManager/` | UMM 自带的 Harmony 等程序集 |

第 8、9 项从 `<Game>` 下的 `*_Data` 目录枚举。未命中时回落到 .NET 默认绑定（GAC 和已加载程序集）。

## 3. 受保护程序集（绝不从模组目录解析）

```text
mscorlib, System, netstandard, Mono.*, UnityEngine, Unity.*,
Assembly-CSharp, UnityModManager, MelonLoader, MelonAdapter
```

受保护名称始终交给游戏默认解析，防止模组私有副本替换核心程序集并造成类型身份冲突。

## 4. 版本与重复

- 同一简单名只加载一次并缓存，不会静默切换版本。
- 同名异版本的私有程序集会记入诊断和兼容报告，由扫描器与依赖图判定是否拒绝；适配器不会静默择一。
- 重复 mod id：按加载顺序排序后，后到者被拒绝（`Duplicate mod id ...: already provided by ...`）。
- 适配器自身的程序集（`MelonLoader.dll`、`MelonAdapter.Runtime.dll`、`MelonAdapter.Harmony.dll`、`0Harmony.dll`）即使在 `Mods/` 下被发现也会被跳过，不会被当作模组。

## 5. 诊断

默认开启 `[resolver] diagnostics`，记录每次解析：

```text
[resolver] Requested <name>, Version=... by <requesting assembly>
[resolver] Resolved <name> from <absolute path>
[resolver] Not found <name>; falling back to the default binding
```

报告最多保留 500 条，含请求方、程序集、命中路径和决策。关闭诊断后只记录失败请求。

## 6. 网络位置与 0x80131515

在部分被 .NET Framework 视为网络位置的卷上，`Assembly.LoadFrom` / `LoadFile` 会抛 `FileLoadException`（HRESULT `0x80131515`）：

```text
NotSupportedException: An attempt was made to load an assembly from a network location which
would have caused the assembly to be sandboxed in previous versions of the .NET Framework. ...
(HRESULT: 0x80131515)
```

适配器扫描和 A 层解析使用字节加载，不受此限制影响。模组自行调用 `Assembly.LoadFrom` 时仍可能遇到；异常只记录为该模组的失败原因。

## 7. 加载错误分类

`ModScanner` / `ModLoader` 将下列错误写入日志、`ModRecord.FailureReason` 和兼容报告：

| 分类 | 触发 | 记录示例 |
|---|---|---|
| 非托管 DLL | `BadImageFormatException`（子目录）或被跳过的根目录文件 | `Not a managed .NET assembly.` |
| 缺少依赖 | `FileNotFoundException` / `FileLoadException` / `TypeLoadException` | `Failed to inspect assembly: FileNotFoundException: ...` |
| 缺少 API | `MissingMethodException` / `MissingFieldException` / 成员级 `TypeLoadException` | 归类为缺少依赖或 API，提示检查 API 基线 |
| 类型冲突 | 同名类型来自不同程序集（例如真实 MelonLoader 已加载） | 冲突检测在启动前拦截，或记录 `TypeLoadException` |
| 构造失败 | 模组构造函数抛异常 | `The constructor of ... threw ...` |
| 不支持的后端 | `MelonPlatformDomainAttribute(IL2CPP)` | `The mod requires the IL2CPP domain, which MelonAdapter does not support.` |
| 元数据缺失 | 无 `MelonInfo` 且未开启 `allowUnannotatedMods` | `No MelonInfo attribute. Set resolver.allowUnannotatedMods = true ...` |
| 游戏不匹配 | `MelonGameAttribute` 与当前游戏不符 | `The mod targets ..., but the running game is ...` |
| Harmony 冲突 | 模组自带不同版本 `0Harmony.dll` | 见 `spec/COMPATIBILITY.md` 的 Harmony 一节 |

单个模组失败不会阻止其它模组加载；该记录进入 `Rejected` 或 `Failed`。

## 8. 适配器自己的解析器（A 层）

`AdapterInstall.InstallAssemblyResolver()` 幂等安装，仅处理：

- `MelonLoader`、`MelonAdapter.Runtime`、`0Harmony`（按简单名匹配）；
- 探测 `MelonAdapter.dll` 所在目录及其 `Dependencies/`；
- 使用 `Assembly.Load(File.ReadAllBytes(path))`，避开网络位置限制和文件锁定；
- 失败时返回 `null`，让默认绑定继续尝试。

`ModuleInitializer` 在 `net472` 上不内置，仓库内提供了一个内部 polyfill 特性（`System.Runtime.CompilerServices.ModuleInitializerAttribute`）。
