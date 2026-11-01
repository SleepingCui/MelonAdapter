# 变更记录

## 1.2.2 - API 保真修复（真实模组加载失败）

Jipper Key Viewer 1.7.1 在 1.2.1 上初始化时抛 `MissingMethodException`（`MelonPreferences_Category.CreateEntry<T>` 的八参重载），随后因字段为 `null` 在 `OnUpdate` 连续空引用被禁用。原因是 facade 的签名与 MelonLoader 0.7.3.0 逐字不一致：CLR 按**完整签名**（含返回类型）绑定成员引用，参数形状相同但类型或返回类型不同的方法不会报“缺少重载”，而是抛 `MissingMethodException`。

### 修复

- **偏好 API 签名**：`MelonPreferences_Category.CreateEntry<T>` 的 4 参重载返回基类 `MelonPreferences_Entry`、7 参去掉 `oldIdentifier`、新增 8 参（`oldIdentifier` 先重命名再创建）；`MelonPreferences.CreateEntry<T>` 的静态 5 参/8 参重载同步；新增 `MelonPreferences.SaveCategory<T>(string, bool)`。
- **旧版偏好形状**：`MelonPrefs.MelonPreference` 的 `Value` / `ValueEdited` 改为 `string`，新增 `MelonPreferenceType`；`ModPrefs.GetPrefs()` 返回 `Dictionary<string, Dictionary<string, ModPrefs.PrefDesc>>`，`PrefDesc` 继承 `MelonPreference` 并提供 `PrefType`。
- **类型位置**：`MelonPreferences_ReflectiveCategory` 移到 `MelonLoader.Preferences`，`MelonEnvironment` 移到 `MelonLoader.Utils` 并补齐 23 个成员；`MelonHandler.PluginsDirectory` 不再返回 Mods 目录；`MelonPreferences.GetCategory<T>` 改为无约束返回 `T`。
- **日志与元数据**：`MelonLogger` 新增 `MsgCallbackHandler` / `MsgDrawingCallbackHandler` / `WarningCallbackHandler` / `ErrorCallbackHandler` 事件（含递归保护）；`MelonInfoAttribute.SemanticVersion` 与 `VerifyLoaderVersionAttribute.SemVer` 改为 `Semver.SemVersion`；新增 `Semver.SemVersion` 实现。
- **辅助类型**：`MelonLoader.Pastel.ConsoleExtensions`、`MelonLoader.Properties.BuildInfo`、`MelonLoader.PatchShield`、`MelonLoader.MelonEvent<T1..T8>` / `LemonFunc<...>` / `MelonAssembly.RottenMelon` / `MelonEventBase<T>.MelonEventSubscriber`。

### 验证

- 自检脚本新增 7 项 API 形状断言（八参 `CreateEntry` 重命名、静态 `CreateEntry`、日志回调、`SemanticVersion`、`ModPrefs.GetPrefs`、`MelonEnvironment` 位置、`FindMelon(name, author)`），共 74 项全部通过。
- 另用 Mono.Cecil 对游戏内两个真实 Melon 模组的所有 `MelonLoader` 引用做成员级校验：29 个引用、0 缺失（1.2.2 前 Jipper 的八参 `CreateEntry` 会在此暴露）。
- 适配器版本 1.2.2（`MelonAdapter.dll` / `MelonAdapter.Runtime.dll`）；`MelonLoader.dll` 仍为 0.7.3.0。

## 1.2.1 - 游戏身份与模组加载修复
## 1.2.1 - 游戏身份与模组加载修复

修复“模组明明兼容却加载不了”的两个根因，均为实测发现：

### 修复

- **游戏根目录被解析成 `Mods`**：UnityModManager 传给 `ModEntry` 的 `Path` 带尾部分隔符（`.../Mods/MelonAdapter/`），而 `Directory.GetParent` 对带尾分隔符的路径返回它自己，于是 `ResolveGameRoot` 连退两级落在 `.../Mods`，游戏名变成文件夹名 `Mods`、开发商永远是 `unknown`，所有声明了 `[MelonGame]` 的模组都被判成“别的游戏”。`AdapterInstall.TrimTrailingSeparators` 先去掉尾分隔符再取父目录。
- **游戏身份没有真值**：新增 `src/MelonAdapter.Runtime/GameIdentity.cs`，依次使用 Unity 的 `Application.companyName` / `Application.productName`、`<Game>/<Game>_Data/app.info`（第一行开发商、第二行游戏名，Unity 自己写的清单）、游戏文件夹名，只填补 `unknown`/空值。
- **UMM 侧的模组被误判为“非法 Melon 模组”**：不含 `MelonMod` 子类、也不引用 `MelonLoader` 的程序集（例如 Melon 加载器同目录下的 `TimingShow.dll`）现在记为 `IsDependencyOnly`，只在被依赖时解析，不再写 ERROR；只有“引用了 MelonLoader 却没有 Melon 类型”才判定为非法。
- **依赖程序集重复加载**：`ModAssemblyResolver` 在探测文件之前先复用进程中已加载的同名同版本程序集，避免 UMM 与 Melon 两半各持一份静态状态。

### 变更

- 适配器版本 1.2.1（`MelonAdapter.dll` / `MelonAdapter.Runtime.dll` 的 AssemblyVersion 与 FileVersion）。
- 自检仍为 67 项，全部通过。

## 1.2.0 - 界面语言（中文 / English）

设置界面不再只有英文：全部文案改为查表，语言文件为 `<Game>/MelonAdapter/lang.json`，内置中文与英文两张表。

### 新增

- `src/MelonAdapter.Runtime/Localization/lang.json`：123 个键的界面文案（`tab.*` / `ov.*` / `mods.*` / `pref.*` / `set.*` / `err.*` / `state.*` / `common.*`），顶层 `languages` 数组决定界面里可选的语言与顺序；文件同时以 `EmbeddedResource` 编入 `MelonAdapter.Runtime.dll`，发布包另带一份可编辑副本。
- `src/MelonAdapter.Runtime/Localization/LocalizationCatalog.cs`：加载与合并语言表（内嵌副本 → 文件覆盖）、`auto` 按系统 UI 语言选择、`SetLanguage` / `T(key, args)` / `KeysMissingFrom` / `Reload`。查表回退顺序为"当前语言 → 英文 → 任一语言 → 键名"，缺键不会被静默吞掉（计入 `MissingKeyCount`，设置页可见）。
- `src/MelonAdapter.Runtime/Localization/JsonLite.cs`：只读 JSON 解析器，避免把 JSON 库带进游戏（运行时只自带 `TomlLite`）。文件损坏时抛出带偏移量的 `FormatException`，由调用方降级到内嵌副本。
- `Config.toml` 新增 `[ui] language`（`auto` / `zh` / `en`，默认 `auto`）；无法识别的值会记一条警告并按系统语言处理。
- `AdapterRuntime.Language`、`AdapterRuntime.LanguageError`、`AdapterRuntime.ReloadLanguage(out string)`。
- 设置页 Settings 标签的语言区块：语言选择（`中文` / `English`）、语言文件来源、损坏提示、缺键计数、"重新加载 lang.json"与错误提示；切换语言立即生效并写回 `Config.toml`。
- Overview 增加当前语言与语言文件来源一行。
- 自检新增 13 项断言（共 67 项）：`[ui]` 落盘与默认值、显式语言往返、外部 `lang.json` 被加载并扩展内置表、单键覆盖、运行时切换语言、拒绝未知语言代码、缺键计数、损坏/缺失文件的降级、`auto` 结果合法。

### 变更

- 适配器版本 1.2.0（`MelonAdapter.dll` / `MelonAdapter.Runtime.dll` 的 AssemblyVersion 与 FileVersion）。
- 界面全部文案改走 `LocalizationCatalog`，`AdapterUI` 不再出现硬编码英文句子。
- `build.ps1` 把 `lang.json` 复制进 `dist/MelonAdapter/`，随包分发。
- `README.md`、`docs/CONFIGURATION.md`、`docs/VALIDATION.md`、`spec/COMPATIBILITY.md` 同步语言相关说明。

### 已知限制

- 只有中文与英文两种翻译；其它语言的键会回退英文，界面不会空白。
- Unity 自带字体是否含中文字形取决于游戏；缺字形时显示方块，切换到 English 即可。

## 1.1.1 - 设置界面高度修复

修复 1.1.0 的设置面板在 UMM 模组行里只显示约 100 像素、内容被截断的问题。

### 修复

- UMM 在模组行内部调用 `ModEntry.OnGUI`，其父级是一个内容高度不受约束的滚动视图；不带高度的 `GUILayout.BeginScrollView` 在这种上下文里会塌缩到几十像素，导致面板只露出标签栏和一两行日志。四个页面现在都使用显式视口高度：`AdapterUI.PageHeight` 取屏幕高度（按 `GUI.matrix` 换算成 GUI 单位）的 62% 减 150，并限制在 240–520 之间；内容在面板内部滚动，高度不再依赖 UMM 的布局。
- 概览页的日志尾固定为 160 GUI 单位，不再与页面视口互相挤压。
- 切换标签页时复位该页的滚动位置，不再停在上一页的滚动偏移上（之前可能一打开就落在页面底部）。

### 变更

- 适配器版本 1.1.1（`MelonAdapter.dll` / `MelonAdapter.Runtime.dll` 的 AssemblyVersion 与 FileVersion）。
- `README.md` 补充面板高度的行为说明；`docs/CONFIGURATION.md` 同步。

## 1.1.0 - 游戏内设置界面

在 UMM 面板里提供调试与模组配置界面，并补上运行期禁用单个模组的 API。

### 新增

- `src/MelonAdapter.Host/AdapterUI.cs`：UMM 设置界面（`ModEntry.OnGUI` 接线），共 Overview / Mods / Preferences / Settings 四个标签页。可查看版本横幅与日志尾、按模组查看状态与失败原因、打开模组目录与私有数据目录、立即禁用模组、加入下次启动禁用列表、按创建者程序集分组编辑偏好设置、即时切换调试日志、修改 Harmony 策略并保存配置。
- `AdapterRuntime.DisableMod(string modId, string reason)`：运行期禁用单个已加载模组（退订事件、撤销 Harmony patch、注销 melon、重写兼容报告），返回是否真的禁用了一个 `Loaded` 模组。
- `MelonPreferences_Category.CreatorAssembly`：记录创建分类的程序集，供界面按模组分组；旧版 `MelonPrefs`/`ModPrefs` 注册的分类同样记录调用方。
- 自检新增 3 项断言：分类归属、未知模组不可禁用、已停止的模组不可再次禁用（共 54 项）。

### 变更

- 适配器版本 1.1.0（`MelonAdapter.dll` / `MelonAdapter.Runtime.dll` 的 AssemblyVersion 与 FileVersion）。
- Host 新增对 `UnityEngine.IMGUIModule` 与 `UnityEngine.TextRenderingModule` 的编译引用（`Private=false`，不复制）。
- `docs/CONFIGURATION.md` 说明界面能改哪些键；`docs/VALIDATION.md` 增加界面的人工验收步骤。

## 1.0.0 - 初始版本

首个可交付版本，按需求文档第 0-8 阶段完成。

### 新增

- `MelonLoader.dll` 外观程序集：0.7.3.0 公共 API（属性、`MelonBase`/`MelonMod`、日志、偏好设置、事件、协程、`MelonUtils`、旧版 `MelonPrefs`/`ModPrefs`）。
- `MelonAdapter.Runtime.dll`：配置、扫描、依赖图、程序集解析、生命周期派发、异常隔离、兼容报告、偏好设置存储与迁移。
- `MelonAdapter.dll`：UMM 入口（`AdapterHost.Load`）、冲突检测、`AdapterTicker`、UMM 开关接线。
- 适配器自身的程序集解析（`ModuleInitializer` + `AssemblyResolve`），支持网络位置上的游戏。
- Harmony：唯一 provider，按 melon id 分配 owner，支持撤销；缺失时优雅降级。
- 样例模组：基础回调、偏好设置、事件、协程、Harmony、缺依赖、重复依赖、抛异常。
- 无头自检 `tests/MelonAdapter.SelfTest`（51 项断言）。
- 文档：`README.md`、`spec/`（API、兼容性、生命周期、程序集解析）、`docs/`（配置、验证）。
- 打包与脚本：`packaging/UMM/MelonAdapter/`、`build.ps1`、`build.cmd`。

### 已知限制

- 不支持 IL2CPP、MelonPlugin、真实 MelonLoader 共存、运行期程序集卸载。
- 未在真实游戏中做版本矩阵验证，见 `docs/VALIDATION.md` 第 7 节所列待办。
