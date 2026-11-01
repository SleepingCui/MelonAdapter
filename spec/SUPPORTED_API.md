# 支持的 MelonLoader API（基线 0.7.3.0）

本文档描述 MelonAdapter 1.2.2 对外暴露的 `MelonLoader.dll` facade 与真实 MelonLoader **0.7.3.0** 的对应关系。清单由对 facade 程序集的反射枚举 + 逐类型实现审查生成，与代码一致；任何一项改动都必须同步更新本文件与 `spec/COMPATIBILITY.md`。

## 1. 身份与绑定

| 项 | 值 |
|---|---|
| 程序集简单名 | `MelonLoader` |
| AssemblyVersion | `0.7.3.0` |
| 强名称 | 无（`PublicKeyToken=null`），与官方程序集一致 |
| 目标框架 | .NET Framework 4.7.2 |
| UnityEngine 依赖 | **无** |

因为 facade 与基线同名、同版本、同无强名身份，按 0.7.x 编译的模组在绑定时会解析到它。若模组编译时所依赖的成员在当前 facade 中不存在，会在加载阶段抛出 `TypeLoadException` / `MissingMethodException` / `MissingFieldException`，由运行时归类为"缺少 API"并拒绝该模组（见 `docs/VALIDATION.md` 的错误分类）。

状态图例：**支持** = 行为与基线一致；**差异** = 可调用但语义/来源与官方不同（下文逐条列明）；**未实现** = 调用会被明确拒绝并输出诊断，不返回伪成功结果。

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

`MelonModInfoAttribute` / `MelonModGameAttribute` 作为已弃用的别名保留（与 0.7.x 一致）。

## 3. 核心类型

### 3.1 `MelonBase`（抽象基类）

- 静态成员：`RegisteredMelons`、`OnRegister` / `OnUnregister`（`MelonEvent`）、`OnMelonRegistered` / `OnMelonUnregistered` / `OnMelonInitializing`（`MelonEvent<MelonBase>`）、`CreateWrapper<T>()`、`FindMelon(string)`、`ExecuteAll(Action<MelonBase>)`、`SendMessageAll(string, object[])`、`RegisterSorted<T>(List<T>, bool)`、`PrintIncompatibilities(...)`。
- 实例属性：`MelonAssembly`、`Assembly`、`Location`、`Hash`（SHA-256）、`ID`、`Registered`、`MelonTypeName`、`Priority`、`ConsoleColor` / `AuthorConsoleColor`（`Logging.ColorARGB`）、`Info`、`AdditionalCredits`、`SupportedProcesses`、`Games`、`SupportedGameVersions`、`OptionalDependencies`、`SupportedPlatforms`、`SupportedDomain`、`SupportedMLVersion`、`SupportedMLBuild`、`HarmonyDontPatchAll`、`LoggerInstance`、`HarmonyInstance`。
- 实例方法：`bool Register()`、`void Unregister(string reason, bool important)`、`object SendMessage(string, params object[])`。
- 生命周期虚方法：`OnEarlyInitializeMelon`、`OnInitializeMelon`、`OnLateInitializeMelon`、`OnApplicationEarlyStart`、`OnApplicationStart`、`OnApplicationLateStart`、`OnApplicationQuit`、`OnApplicationDefiniteQuit`、`OnUpdate`、`OnFixedUpdate`、`OnLateUpdate`、`OnGUI`、`OnDeinitializeMelon`、`OnModSettingsApplied`、`OnPreferencesLoaded`（无参/`string`）、`OnPreferencesSaved`（无参/`string`）、`OnPreSupportModule`。
- 嵌套枚举：`MelonBase.Incompatibility`（`MLVersion`/`MLBuild`/`Game`/`GameVersion`/`ProcessName`/`Domain`/`Platform`）。

差异：`HarmonyInstance` 返回 `HarmonyLib.Harmony`，由运行时为每个 melon 提供稳定 owner 的单一 Harmony 实例；若没有可用 Harmony 提供方，则记录错误并返回 `null`（见 §11）。`Register()` 在缺少 `MelonInfo` 或 `MelonAssembly` 时返回 `false` 并记录原因。

### 3.2 `MelonMod` / `Melon<T>` / `MelonTypeBase<T>`

`MelonMod : MelonTypeBase<MelonMod>` 增加场景回调：`OnLevelWasInitialized(int)`、`OnLevelWasLoaded(int)`、`OnSceneWasInitialized(int, string)`、`OnSceneWasLoaded(int, string)`、`OnSceneWasUnloaded(int, string)`，以及便捷属性 `InfoAttribute`、`GameAttributes`。

`MelonTypeBase<T>` 提供 `static T Instance` 与 `static MelonLogger.Instance Logger`；`Melon<T>` 提供 `Instance` 与 `Logger`。实例在注册时赋值。

### 3.3 `MelonAssembly`

支持：`LoadedAssemblies`、`RegisteredMelons`、`Assembly`、`Location`、`Hash`、`LoadedMelons`、`RottenMelons`、`FindMelonInstance<T>()`、`GetMelonAssemblyOfMember(MemberInfo, object)`。

**未实现**：`LoadMelonAssembly(string, bool)`、`LoadRawMelonAssembly(string, byte[])`。调用会写入错误日志（内容形如“loading assemblies at runtime is not supported by MelonAdapter v1 … restart the game”）并返回 `null`。

### 3.4 `MelonHandler` / `Main`

支持：`MelonHandler.Mods`、`Plugins`、`Assemblies`、`IsModAlreadyLoaded(string)`、`GetMelonHash(string)`；`Main.GetUnityVersion()`、`Main.GetUserPath()`、`Main.Mods`。

**未实现**：`MelonHandler.LoadFromFile(string, bool)`、`LoadFromAssembly(Assembly)`、`LoadFromByteArray(byte[], string)` —— 记录同样的“不支持运行期加载”错误并返回空数组。

## 4. 日志

`MelonLogger`（静态）与 `MelonLogger.Instance` 支持：`Msg` / `Log` / `MsgPastel` / `MsgDirect` / `MsgPastelDirect` / `Warning` / `LogWarning` / `Error` / `LogError` / `BigError` / `WriteLine` / `WriteSpacer`，含 `ConsoleColor` 与 `Logging.ColorARGB` 重载和 `params object[]` 格式化。

- 输出统一进入适配器日志（`<adapter>/Logs/MelonAdapter.log`）并镜像到 UMM 日志；来源前缀为 melon 名称。
- `BigError` 映射为 `Critical` 级别。
- `MelonDebug.IsEnabled()` 读取 `debugLogging`；`MelonDebug.Msg` 仅在开启时输出，并带 `[DEBUG]` 前缀。
- `Logging.ColorARGB` 支持 `FromArgb` 各重载与常用命名色；颜色仅作为数据保存，**不产生 ANSI 颜色**。
- `MelonModLogger` 作为兼容空壳保留。
- 回调事件与基线同名同形：`MsgCallbackHandler`（`Action<ConsoleColor, ConsoleColor, string, string>`）、`MsgDrawingCallbackHandler`（`Action<ColorARGB, ColorARGB, string, string>`）、`WarningCallbackHandler` / `ErrorCallbackHandler`（`Action<string, string>`），参数为 `(来源, 文本)`；回调抛异常只记录一条警告，不递归、不影响日志输出。
- `MelonLoader.Pastel.ConsoleExtensions`（`Enable` / `Disable` / `Pastel` / `PastelBg`）与 `MelonLoader.Properties.BuildInfo`（`Version` = `0.7.3`、`VersionNumber`）提供；适配器日志不输出 ANSI 颜色，`Pastel` 在 `Enable()` 之前原样返回文本。

## 5. 偏好设置

支持：`MelonPreferences.CreateCategory`（3 个重载）、`CreateCategory<T>`（反射式，`MelonPreferences_ReflectiveCategory` 位于 `MelonLoader.Preferences`）、`GetCategory` / `GetCategory<T>`（`T` 无约束：可返回分类本身，也可返回反射式分类对象）、`GetEntry` / `GetEntry<T>`、`GetEntryValue<T>`、`SetEntryValue<T>`（含 validator 重载）、`HasEntry`、`CreateEntry<T>`（3 个重载：5 参返回基类、8 参全可选）、`Load`、`Save`、`SaveCategory` / `SaveCategory<T>`、`RemoveCategoryFromFile`、`Categories`、`ReflectiveCategories`、`FilePath`、`OnPreferencesLoaded` / `OnPreferencesSaved`。

`MelonPreferences_Category`：`Identifier`、`DisplayName`、`IsHidden`、`IsInlined`、`Entries`、`CreateEntry<T>`（4 参返回基类、7 参、8 参含 `oldIdentifier`）、`GetEntry`、`HasEntry`、`DeleteEntry`、`RenameEntry`、`LoadFromFile`、`SaveToFile`、`SetFilePath`（3 个重载）、`ResetFilePath`、`DestroyFileWatcher`。

`oldIdentifier` 的语义：新建条目时若旧名字存在于内存中则改名保留值；若旧名字只存在于已保存文件里（上一次启动保存的），适配器读取旧值写入新条目，改名不会把设置重置为默认值。

`MelonPreferences_Entry` / `MelonPreferences_Entry<T>`：`Identifier`、`DisplayName`、`Description`、`Comment`、`IsHidden`、`DontSaveDefault`、`Category`、`BoxedValue` / `BoxedEditedValue`、`Value` / `EditedValue` / `DefaultValue`、`Validator`、`OnEntryValueChanged`、`OnEntryValueChangedUntyped`、`ResetToDefault` 等。

`Preferences.ValueValidator`、`IValueRange`、`ValueRange<T>` 支持（越界值被夹到区间内）。

差异：存储文件是 `<adapter>/UserData/MelonPreferences.cfg`（TOML，由适配器读写），不是 MelonLoader 的 `UserData/MelonPreferences.cfg`；首次启动可单向迁移，见 `docs/CONFIGURATION.md`。

## 6. 事件

`MelonEvents` 暴露全部 0.7.x 字段：`OnPreInitialization`、`OnApplicationEarlyStart`、`OnPreSupportModule`、`OnApplicationStart`、`OnApplicationLateStart`、`OnApplicationDefiniteQuit`、`OnApplicationQuit`、`OnUpdate`、`OnFixedUpdate`、`OnLateUpdate`、`OnGUI`、`OnSceneWasLoaded`、`OnSceneWasInitialized`、`OnSceneWasUnloaded`、`OnPreModsLoaded`。

`MelonEventBase<T>` 提供 `Subscribe(delegate, int priority = 0, bool unsubscribeOnFirstInvocation = false)`、`Unsubscribe(delegate)`、`Unsubscribe(MethodInfo, object)`、`UnsubscribeAll()`、`CheckIfSubscribed`、`GetSubscribers`、`Invoke`、`Dispose`、`oneTimeUse`，嵌套订阅者类型为 `MelonEventBase<T>.MelonEventSubscriber`（公开字段 `del` / `priority` / `unsubscribeOnFirstInvocation` / `melonAssembly`）。委托类型 `LemonAction`、`LemonAction<T1..T8>`、`LemonFunc<TResult>`、`LemonFunc<T1..T8, TResult>` 同基线；`MelonEvent`、`MelonEvent<T1..T8>` 也按基线提供。

运行时保证：事件先于对应 melon 回调触发；订阅者抛异常时记录 `Exception while invoking event subscriber` 并移除该订阅，不影响其他订阅者与后续派发。模组被禁用、异常停用或退出时，运行时按程序集批量退订（`EventSubscriptionTracker`）。

## 7. 协程

`MelonCoroutines.Start(IEnumerator)` / `Stop(object)` 由运行时的 `CoroutinePump` 在主线程每帧推进，支持嵌套 `IEnumerator`；Unity 的 `yield return null` / `WaitForSeconds` 等由 Host 安装的翻译器判定完成时机，无法识别时按一帧等待。协程抛异常会被记录并停止该协程。

## 8. 工具与环境

`MelonUtils`：`GetApplicationPath`、`GetGameDataDirectory`、`GetManagedDirectory`、`GetUnityVersion`、`IsGameIl2Cpp()`（恒为 `false`）、`IsOldMono()`（恒为 `false`）、`Clamp<T>`、`ComputeHash(...)`，属性 `BaseDirectory`、`GameDirectory`、`MelonLoaderDirectory`、`UserDataDirectory`、`UserLibsDirectory`、`GameDeveloper`、`GameName`、`GameVersion`、`CurrentGameAttribute`、`CurrentPlatform`、`CurrentDomain`（恒为 `MONO`）、`IsUnix` / `IsWindows` / `IsMac`。

`MelonEnvironment`（`namespace MelonLoader.Utils`）：`IsDotnetRuntime`、`IsMonoRuntime`、`MelonBaseDirectory`、`GameExecutablePath`、`MelonLoaderDirectory`、`GameRootDirectory`、`DependenciesDirectory`、`SupportModuleDirectory`、`CompatibilityLayerDirectory`、`Il2CppAssemblyGeneratorDirectory`、`ModsDirectory`、`PluginsDirectory`、`UserLibsDirectory`、`UserDataDirectory`、`MelonLoaderLogsDirectory`、`OurRuntimeDirectory`、`GameExecutableName`、`UnityGameDataDirectory`、`UnityGameManagedDirectory`、`Il2CppDataDirectory`、`UnityPlayerPath`、`MelonManagedDirectory`、`Il2CppAssembliesDirectory`。IL2CPP 专属目录返回空路径，不代表已支持 IL2CPP。

`MelonLoaderBase`：`UserDataPath`、`UnityVersion`；`MelonConsole.SetTitle`。

## 9. 旧版 API（0.5/0.6 时代）

`MelonPrefs`：`GetBool` / `GetFloat` / `GetInt` / `GetString`、`HasKey`、`GetCategoryDisplayName`、`RegisterCategory`、`RegisterBool` / `RegisterFloat` / `RegisterInt` / `RegisterString`、`SetBool` / `SetFloat` / `SetInt` / `SetString`、`SaveConfig`；嵌套 `MelonPreference`（构造函数 `(MelonPreferences_Entry)` / `(MelonPreference)`，`Value` / `ValueEdited` 为 `string`，`Type` 为 `MelonPreferenceType`）与枚举 `MelonPreferenceType`。

`ModPrefs`：`GetPrefs()`（返回 `Dictionary<string, Dictionary<string, ModPrefs.PrefDesc>>`）、`RegisterPrefBool` / `RegisterPrefFloat` / `RegisterPrefInt` / `RegisterPrefString`、`GetBool` / `GetFloat` / `GetInt` / `GetString`；嵌套 `PrefDesc : MelonPrefs.MelonPreference` 与枚举 `PrefType`。

旧版调用被映射到 `MelonPreferences` 的条目上；未注册的键会记录警告并返回默认值。

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

1. **颜色不渲染**：Unity 控制台与日志文件都没有 ANSI 颜色，`ConsoleColor` / `ColorARGB` 只作为 API 形状保留。
2. **颜色默认值**：`MelonLogger.DefaultMelonColor` / `DefaultTextColor` 为适配器常量，不读取 MelonLoader 主题配置。
3. **运行期程序集加载被拒绝**（见 §10），因为程序集无法卸载、也无法在 UMM 单宿主下安全重入。
4. **Harmony 单一提供方**：不是 MelonLoader 自带的那份；版本由 `Dependencies/0Harmony.dll` 决定并写入启动横幅与兼容报告。
5. **偏好设置文件位置**：适配器 `UserData/`，形如 TOML；与官方格式字段一致但由适配器序列化（未知 section 会原样保留）。
6. **`MelonUtils.IsGameIl2Cpp()` 恒为 false**，`CurrentDomain` 恒为 `MONO`：适配器不运行在 IL2CPP 下。
7. **`MelonBase.Register()` 的失败路径**：缺元数据时返回 `false` 并记录原因，而不是抛出异常。

## 12. 适配器内部接口（非 MelonLoader API）

`MelonAdapter` 在 `MelonLoader.dll` 中额外暴露一组用于进程内接线的类型，模组不应使用：

`AdapterBridge`（静态接线点：`LogSink`、`Context`、`PreferencesStorage`、`CoroutineRunner`、`HarmonyProvider`、`Registry`、`DescribeMissingService`）、`AdapterLogLevel`、`IAdapterLogSink`、`IAdapterContext`、`IAdapterPreferencesStorage`、`IAdapterCoroutineRunner`、`IAdapterHarmonyProvider`、`IAdapterMelonRegistry`。

它们随 facade 一起分发是为了避免 facade → runtime 的反向依赖；在真实 MelonLoader 中不存在这些类型，依赖它们的模组不具备可移植性。
