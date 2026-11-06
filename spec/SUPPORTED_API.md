# 支持的 MelonLoader API（基线 0.7.3.0）

本文档列出 MelonAdapter 1.2.2 的 `MelonLoader.dll` facade 对 MelonLoader 0.7.3.0 的支持情况。API 变更时同步更新本文和 [`COMPATIBILITY.md`](COMPATIBILITY.md)。

## 1. 身份与绑定

| 项 | 值 |
|---|---|
| 程序集简单名 | `MelonLoader` |
| AssemblyVersion | `0.7.3.0` |
| 强名称 | 无（`PublicKeyToken=null`），与官方程序集一致 |
| 目标框架 | .NET Framework 4.7.2 |
| UnityEngine 依赖 | **无** |

0.7.x 模组按程序集身份绑定到该 facade。缺少成员时会抛出 `TypeLoadException`、`MissingMethodException` 或 `MissingFieldException`，模组会因 API 缺失被拒绝（见 [`docs/VALIDATION.md`](../docs/VALIDATION.md)）。

标记含义：**支持**表示行为与基线一致；**差异**表示可调用但语义不同；**未实现**表示记录诊断并拒绝调用。

## 2. 元数据属性（全部支持）

| 类型 | 成员 | 说明 |
|---|---|---|
| `MelonInfoAttribute` | 构造函数 `(Type, string name, string version, string author, string downloadLink)`、`(Type, string name, int versionMajor, int versionMinor, int versionPatch, string author, string downloadLink)` | `SystemType` / `Name` / `Version` / `SemanticVersion` / `Author` / `DownloadLink`；运行时用以确定 mod id 与显示信息 |
| `MelonGameAttribute` | `(string developer, string name)`、`(string name)` | `Developer` / `Name` / `Universal`；`IsCompatible` 与实际游戏比对 |
| `MelonGameVersionAttribute` | `(string version)` | 只作为元数据记录（当前不做区间校验） |
| `MelonIDAttribute` | `(string id)` / `(int id)` | 覆盖 `MelonBase.ID` |
| `MelonPriorityAttribute` | `(int priority)` | 决定加载与派发顺序（降序） |
| `MelonColorAttribute` / `MelonAuthorColorAttribute` | `ConsoleColor` | 写入 `ConsoleColor` / `AuthorConsoleColor`；颜色不外显（见 §11） |
| `MelonAdditionalCreditsAttribute` | `(string credits)` | `AdditionalCredits` |
| `MelonAdditionalDependenciesAttribute` | `(params string[] assemblies)` | 硬依赖；进依赖图，缺失即拒绝 |
| `MelonOptionalDependenciesAttribute` | `(params string[] assemblies)` | 软依赖；仅记录，不参与排序，`OptionalDependencies` 返回等价的自定义依赖属性 |
| `MelonIncompatibleAssembliesAttribute` | `(params string[] assemblies)` | 记录到兼容报告的 warnings，不阻止加载 |
| `MelonPlatformAttribute` | `CompatiblePlatforms`（`UNIVERSAL`/`WINDOWS_X86`/`WINDOWS_X64`/`ANDROID`/`LINUX`/`MAC`） | 不含 `UNIVERSAL` 且不含当前平台则拒绝 |
| `MelonPlatformDomainAttribute` | `CompatibleDomains`（`MONO`/`IL2CPP`/`UNIVERSAL`） | 要求 IL2CPP 的模组被拒绝（适配器只支持 MONO） |
| `MelonProcessAttribute` | `(string exeName)` | 非 `Universal` 且与进程名不符则拒绝 |
| `VerifyLoaderVersionAttribute` | `(int major, int minor, int patch)` 等 | 与 `[api] baseline` 比较，基线过低则拒绝 |
| `VerifyLoaderBuildAttribute` | `(string hash)` | **差异**：只输出警告，不校验 build hash |
| `HarmonyDontPatchAllAttribute` | — | 设置 `MelonBase.HarmonyDontPatchAll`；运行时不自动 `PatchAll` |
| `MelonPreferences_DontSaveAttribute` | — | 反射式偏好设置时跳过该字段/属性 |

保留已弃用别名 `MelonModInfoAttribute` / `MelonModGameAttribute`。

## 3. 核心类型

### 3.1 `MelonBase`（抽象基类）

- 静态成员：`RegisteredMelons`、`OnRegister` / `OnUnregister`（`MelonEvent`）、`OnMelonRegistered` / `OnMelonUnregistered` / `OnMelonInitializing`（`MelonEvent<MelonBase>`）、`CreateWrapper<T>()`、`FindMelon(string)`、`ExecuteAll(Action<MelonBase>)`、`SendMessageAll(string, object[])`、`RegisterSorted<T>(List<T>, bool)`、`PrintIncompatibilities(...)`。
- 实例属性：`MelonAssembly`、`Assembly`、`Location`、`Hash`（SHA-256）、`ID`、`Registered`、`MelonTypeName`、`Priority`、`ConsoleColor` / `AuthorConsoleColor`（`Logging.ColorARGB`）、`Info`、`AdditionalCredits`、`SupportedProcesses`、`Games`、`SupportedGameVersions`、`OptionalDependencies`、`SupportedPlatforms`、`SupportedDomain`、`SupportedMLVersion`、`SupportedMLBuild`、`HarmonyDontPatchAll`、`LoggerInstance`、`HarmonyInstance`。
- 实例方法：`bool Register()`、`void Unregister(string reason, bool important)`、`object SendMessage(string, params object[])`。
- 生命周期虚方法：`OnEarlyInitializeMelon`、`OnInitializeMelon`、`OnLateInitializeMelon`、`OnApplicationEarlyStart`、`OnApplicationStart`、`OnApplicationLateStart`、`OnApplicationQuit`、`OnApplicationDefiniteQuit`、`OnUpdate`、`OnFixedUpdate`、`OnLateUpdate`、`OnGUI`、`OnDeinitializeMelon`、`OnModSettingsApplied`、`OnPreferencesLoaded`（无参/`string`）、`OnPreferencesSaved`（无参/`string`）、`OnPreSupportModule`。
- 嵌套枚举：`MelonBase.Incompatibility`（`MLVersion`/`MLBuild`/`Game`/`GameVersion`/`ProcessName`/`Domain`/`Platform`）。

差异：`HarmonyInstance` 使用共享 provider 为每个 melon 创建稳定 owner；provider 不可用时记录错误并返回 `null`。`Register()` 缺少 `MelonInfo` 或 `MelonAssembly` 时返回 `false` 并记录原因。

### 3.2 `MelonMod` / `Melon<T>` / `MelonTypeBase<T>`

`MelonMod : MelonTypeBase<MelonMod>` 提供场景回调 `OnLevelWasInitialized(int)`、`OnLevelWasLoaded(int)`、`OnSceneWasInitialized(int, string)`、`OnSceneWasLoaded(int, string)`、`OnSceneWasUnloaded(int, string)`，以及 `InfoAttribute`、`GameAttributes`。

`MelonTypeBase<T>` 和 `Melon<T>` 提供 `Instance` 与 `Logger`；注册时赋值。

### 3.3 `MelonAssembly`

支持：`LoadedAssemblies`、`RegisteredMelons`、`Assembly`、`Location`、`Hash`、`LoadedMelons`、`RottenMelons`、`FindMelonInstance<T>()`、`GetMelonAssemblyOfMember(MemberInfo, object)`。

**未实现**：`LoadMelonAssembly(string, bool)`、`LoadRawMelonAssembly(string, byte[])`。调用会记录错误并返回 `null`；需重启游戏加载程序集。

### 3.4 `MelonHandler` / `Main`

支持：`MelonHandler.Mods`、`Plugins`、`Assemblies`、`IsModAlreadyLoaded(string)`、`GetMelonHash(string)`；`Main.GetUnityVersion()`、`Main.GetUserPath()`、`Main.Mods`。

**未实现**：`MelonHandler.LoadFromFile(string, bool)`、`LoadFromAssembly(Assembly)`、`LoadFromByteArray(byte[], string)`。调用会记录错误并返回空数组。

## 4. 日志

`MelonLogger`（静态）与 `MelonLogger.Instance` 支持：`Msg` / `Log` / `MsgPastel` / `MsgDirect` / `MsgPastelDirect` / `Warning` / `LogWarning` / `Error` / `LogError` / `BigError` / `WriteLine` / `WriteSpacer`，含 `ConsoleColor` 与 `Logging.ColorARGB` 重载和 `params object[]` 格式化。

- 输出写入 `<adapter>/Logs/MelonAdapter.log` 并镜像到 UMM 日志，前缀为 melon 名称。
- `BigError` 映射为 `Critical`；`MelonDebug` 使用 `debugLogging`，输出带 `[DEBUG]` 前缀。
- `Logging.ColorARGB` 支持 `FromArgb` 重载和常用命名色；颜色不渲染为 ANSI 序列。
- `MelonModLogger` 作为兼容空壳保留。
- 回调事件：`MsgCallbackHandler`（`Action<ConsoleColor, ConsoleColor, string, string>`）、`MsgDrawingCallbackHandler`（`Action<ColorARGB, ColorARGB, string, string>`）、`WarningCallbackHandler` / `ErrorCallbackHandler`（`Action<string, string>`）。回调异常只记录警告，不影响日志输出。
- 提供 `MelonLoader.Pastel.ConsoleExtensions` 和 `MelonLoader.Properties.BuildInfo`；未启用 Pastel 时文本原样返回。

## 5. 偏好设置

支持：`MelonPreferences.CreateCategory`（3 个重载）、`CreateCategory<T>`（反射式，`MelonPreferences_ReflectiveCategory` 位于 `MelonLoader.Preferences`）、`GetCategory` / `GetCategory<T>`（`T` 无约束：可返回分类本身，也可返回反射式分类对象）、`GetEntry` / `GetEntry<T>`、`GetEntryValue<T>`、`SetEntryValue<T>`（含 validator 重载）、`HasEntry`、`CreateEntry<T>`（3 个重载：5 参返回基类、8 参全可选）、`Load`、`Save`、`SaveCategory` / `SaveCategory<T>`、`RemoveCategoryFromFile`、`Categories`、`ReflectiveCategories`、`FilePath`、`OnPreferencesLoaded` / `OnPreferencesSaved`。

`MelonPreferences_Category`：`Identifier`、`DisplayName`、`IsHidden`、`IsInlined`、`Entries`、`CreateEntry<T>`（4 参返回基类、7 参、8 参含 `oldIdentifier`）、`GetEntry`、`HasEntry`、`DeleteEntry`、`RenameEntry`、`LoadFromFile`、`SaveToFile`、`SetFilePath`（3 个重载）、`ResetFilePath`、`DestroyFileWatcher`。

`oldIdentifier` 可从内存或偏好文件中迁移旧条目的值，避免重命名后恢复默认值。

`MelonPreferences_Entry` / `MelonPreferences_Entry<T>`：`Identifier`、`DisplayName`、`Description`、`Comment`、`IsHidden`、`DontSaveDefault`、`Category`、`BoxedValue` / `BoxedEditedValue`、`Value` / `EditedValue` / `DefaultValue`、`Validator`、`OnEntryValueChanged`、`OnEntryValueChangedUntyped`、`ResetToDefault` 等。

`Preferences.ValueValidator`、`IValueRange`、`ValueRange<T>` 支持（越界值被夹到区间内）。

差异：存储文件是 `<adapter>/UserData/MelonPreferences.cfg`（TOML，由适配器读写），不是 MelonLoader 的 `UserData/MelonPreferences.cfg`；首次启动可单向迁移，见 `docs/CONFIGURATION.md`。

## 6. 事件

`MelonEvents` 暴露全部 0.7.x 字段：`OnPreInitialization`、`OnApplicationEarlyStart`、`OnPreSupportModule`、`OnApplicationStart`、`OnApplicationLateStart`、`OnApplicationDefiniteQuit`、`OnApplicationQuit`、`OnUpdate`、`OnFixedUpdate`、`OnLateUpdate`、`OnGUI`、`OnSceneWasLoaded`、`OnSceneWasInitialized`、`OnSceneWasUnloaded`、`OnPreModsLoaded`。

`MelonEventBase<T>` 提供 `Subscribe(delegate, int priority = 0, bool unsubscribeOnFirstInvocation = false)`、`Unsubscribe(delegate)`、`Unsubscribe(MethodInfo, object)`、`UnsubscribeAll()`、`CheckIfSubscribed`、`GetSubscribers`、`Invoke`、`Dispose`、`oneTimeUse`，嵌套订阅者类型为 `MelonEventBase<T>.MelonEventSubscriber`（公开字段 `del` / `priority` / `unsubscribeOnFirstInvocation` / `melonAssembly`）。委托类型 `LemonAction`、`LemonAction<T1..T8>`、`LemonFunc<TResult>`、`LemonFunc<T1..T8, TResult>` 同基线；`MelonEvent`、`MelonEvent<T1..T8>` 也按基线提供。

事件先于对应模组回调触发。订阅者异常会被记录并移除，不影响其它订阅者；模组禁用或退出时按程序集退订。

## 7. 协程

`MelonCoroutines.Start(IEnumerator)` / `Stop(object)` 由主线程上的 `CoroutinePump` 推进，支持嵌套迭代器。Host 翻译 Unity 的等待对象；无法识别时等待一帧。异常会记录并停止协程。

## 8. 工具与环境

`MelonUtils`：`GetApplicationPath`、`GetGameDataDirectory`、`GetManagedDirectory`、`GetUnityVersion`、`IsGameIl2Cpp()`（恒为 `false`）、`IsOldMono()`（恒为 `false`）、`Clamp<T>`、`ComputeHash(...)`，属性 `BaseDirectory`、`GameDirectory`、`MelonLoaderDirectory`、`UserDataDirectory`、`UserLibsDirectory`、`GameDeveloper`、`GameName`、`GameVersion`、`CurrentGameAttribute`、`CurrentPlatform`、`CurrentDomain`（恒为 `MONO`）、`IsUnix` / `IsWindows` / `IsMac`。

`MelonEnvironment`（`namespace MelonLoader.Utils`）：`IsDotnetRuntime`、`IsMonoRuntime`、`MelonBaseDirectory`、`GameExecutablePath`、`MelonLoaderDirectory`、`GameRootDirectory`、`DependenciesDirectory`、`SupportModuleDirectory`、`CompatibilityLayerDirectory`、`Il2CppAssemblyGeneratorDirectory`、`ModsDirectory`、`PluginsDirectory`、`UserLibsDirectory`、`UserDataDirectory`、`MelonLoaderLogsDirectory`、`OurRuntimeDirectory`、`GameExecutableName`、`UnityGameDataDirectory`、`UnityGameManagedDirectory`、`Il2CppDataDirectory`、`UnityPlayerPath`、`MelonManagedDirectory`、`Il2CppAssembliesDirectory`。IL2CPP 专属目录返回空路径，不代表已支持 IL2CPP。

`MelonLoaderBase`：`UserDataPath`、`UnityVersion`；`MelonConsole.SetTitle`。

## 9. 旧版 API（0.5/0.6 时代）

`MelonPrefs`：`GetBool` / `GetFloat` / `GetInt` / `GetString`、`HasKey`、`GetCategoryDisplayName`、`RegisterCategory`、`RegisterBool` / `RegisterFloat` / `RegisterInt` / `RegisterString`、`SetBool` / `SetFloat` / `SetInt` / `SetString`、`SaveConfig`；嵌套 `MelonPreference`（构造函数 `(MelonPreferences_Entry)` / `(MelonPreference)`，`Value` / `ValueEdited` 为 `string`，`Type` 为 `MelonPreferenceType`）与枚举 `MelonPreferenceType`。

`ModPrefs`：`GetPrefs()`（返回 `Dictionary<string, Dictionary<string, ModPrefs.PrefDesc>>`）、`RegisterPrefBool` / `RegisterPrefFloat` / `RegisterPrefInt` / `RegisterPrefString`、`GetBool` / `GetFloat` / `GetInt` / `GetString`；嵌套 `PrefDesc : MelonPrefs.MelonPreference` 与枚举 `PrefType`。

旧版 API 映射到 `MelonPreferences`；未注册键会警告并返回默认值。

## 10. 明确未实现

| 面 | 行为 |
|---|---|
| `MelonAssembly.LoadMelonAssembly` / `LoadRawMelonAssembly` | 错误日志 + 返回 `null` |
| `MelonHandler.LoadFromFile` / `LoadFromAssembly` / `LoadFromByteArray` | 错误日志 + 返回空数组 |
| MelonLoader 内嵌的 `Tomlet` / `TinyJSON` 库 | 不提供（模组若直接引用会得到 `TypeLoadException`，归类为“缺少依赖”）；`Semver.SemVersion` 则按基线提供 |
| MelonLoader 内嵌的旧 `Harmony.*` 命名空间 | 不提供；只提供 `HarmonyLib`（0Harmony 2.x） |
| `MelonPlugin`、Support Module、启动代理、原生 hook、IL2CPP 桥接 | 不提供；检测到相关布局时拒绝启动 |
| `VerifyLoaderBuild` 的 build hash 校验 | 只警告 |

## 11. 与官方实现的行为差异

1. **颜色**：颜色参数只保留 API 形状，不渲染；默认色为适配器常量。
2. **动态加载**：运行期程序集加载不支持（见 §10）。
3. **Harmony**：使用 `Dependencies/0Harmony.dll`，版本写入启动横幅和兼容报告。
4. **偏好文件**：存放于适配器 `UserData/`，使用 TOML；未知 section 会保留。
5. **运行时标记**：`IsGameIl2Cpp()` 恒为 `false`，`CurrentDomain` 恒为 `MONO`。
6. **注册失败**：缺少元数据时 `MelonBase.Register()` 返回 `false` 并记录原因。

## 12. 适配器内部接口（非 MelonLoader API）

`MelonLoader.dll` 还暴露以下内部接线类型；模组不应依赖它们：

`AdapterBridge`（静态接线点：`LogSink`、`Context`、`PreferencesStorage`、`CoroutineRunner`、`HarmonyProvider`、`Registry`、`DescribeMissingService`）、`AdapterLogLevel`、`IAdapterLogSink`、`IAdapterContext`、`IAdapterPreferencesStorage`、`IAdapterCoroutineRunner`、`IAdapterHarmonyProvider`、`IAdapterMelonRegistry`。

这些类型用于避免 facade 对 runtime 的反向依赖，在真实 MelonLoader 中不存在。
