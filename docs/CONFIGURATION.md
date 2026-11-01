# 配置参考

配置文件位于 `<适配器目录>/Config.toml`，首次启动自动生成。解码器是适配器自带的轻量 TOML 读取器，支持注释、字符串、整数、浮点、布尔与字符串数组。

## 1. 适配器目录

`<适配器目录>` 由 Host 按以下顺序解析：

1. 环境变量 `MELONADAPTER_HOME`（绝对或相对路径）；
2. `<游戏根目录>/MelonAdapter/`，前提是该目录已存在且包含 `Config.toml` 或 `Mods/`；
3. 回落到 UMM 的模组目录（即 `Mods/MelonAdapter/`）。

游戏根目录由 UMM 提供的模组路径向上取两级（`Mods/<mod>/` 的父目录的父目录）得到。UMM 传入的路径可能带尾部分隔符（`.../Mods/MelonAdapter/`），而 `Directory.GetParent` 对带尾分隔符的路径返回它自己，所以 Host 会先去掉尾分隔符再取父目录；否则游戏根目录会落到 `Mods/`，所有声明 `[MelonGame]` 的模组都会被判成“其它游戏”（1.2.1 修复）。

游戏开发商与游戏名来自 Unity 的 `Application.companyName` / `Application.productName`，缺失时读 `<游戏根目录>/<游戏名>_Data/app.info`（第一行开发商、第二行游戏名），再缺失才退化为游戏文件夹名。`MelonGameAttribute` 的兼容判断依赖这两个值。

## 2. 键一览

| 键 | 类型 | 默认 | 含义 |
|---|---|---|---|
| `enabled` | bool | `true` | `false` 时完全不扫描、不加载、不派发；启动日志会说明原因 |
| `debugLogging` | bool | `false` | 输出调试级日志；同时开启 `MelonDebug.IsEnabled()` |
| `modsDirectory` | string | `"Mods"` | 模组目录；相对路径相对于适配器目录解析 |
| `userDataDirectory` | string | `"UserData"` | 用户数据目录；相对路径相对于适配器目录解析 |
| `[api] baseline` | string | `"0.7.3.0"` | 向模组宣告的 MelonLoader API 版本；`VerifyLoaderVersion` 按它比较 |
| `[resolver] diagnostics` | bool | `true` | 记录每次程序集解析尝试；关闭后只保留失败记录 |
| `[resolver] allowUnannotatedMods` | bool | `false` | 允许没有 `MelonInfo` 的 DLL 以推断身份加载（仅建议开发期使用） |
| `[harmony] conflictPolicy` | string | `"refuse"` | `refuse` / `disable` / `report`，见 `spec/COMPATIBILITY.md` 第 3 节 |
| `[preferences] migrateFromMelonLoader` | bool | `true` | 首次启动时从 `<Game>/UserData/MelonPreferences.cfg` 单向导入 |
| `[ui] language` | string | `"auto"` | 设置界面语言：`auto`（跟随系统 UI 语言）/ `zh` / `en`；未知值记警告并回落 `auto` |
| `[lifecycle] maxConsecutiveExceptions` | int | `5` | 单个模组单个回调连续异常多少次后被禁用；小于 1 会被修正为 1 |
| `[lifecycle] disabledMods` | string[] | `[]` | 手动禁用的模组 id 或程序集简单名（不区分大小写） |

游戏内设置界面能改的键：`[ui] language`（切换即写盘并立即生效）、`debugLogging`（立即生效）、`[harmony] conflictPolicy`、`[resolver] diagnostics`、`[resolver] allowUnannotatedMods`、`[preferences] migrateFromMelonLoader`、`[lifecycle] maxConsecutiveExceptions`、`[lifecycle] disabledMods`，以及 `enabled`（只能由开变关）。`modsDirectory`、`userDataDirectory`、`[api] baseline` 只在启动时读取，改了必须重启。

## 3. 默认文件

```toml
# MelonAdapter configuration. Generated defaults are safe to edit; a corrupt file is preserved as Config.toml.invalid-<timestamp>.
enabled = true
debugLogging = false
modsDirectory = "Mods"
userDataDirectory = "UserData"

[api]
baseline = "0.7.3.0"

[resolver]
diagnostics = true
allowUnannotatedMods = false

[harmony]
conflictPolicy = "refuse"

[preferences]
migrateFromMelonLoader = true

[ui]
language = "auto"

[lifecycle]
maxConsecutiveExceptions = 5
disabledMods = []
```

## 4. 错误处理

| 情况 | 行为 |
|---|---|
| 文件不存在 | 写入默认配置，日志记录 `Created the default configuration at ...` |
| 文件不可读 | 使用默认值继续启动，保留原文件，警告中带上系统错误信息 |
| TOML 语法错误 | 将原文件改名为 `Config.toml.invalid-<yyyyMMdd-HHmmss>`，写入新的默认文件并继续，警告列出解析错误 |
| 未知键 | 忽略并保留在原文件中（不会删除用户手写的内容） |
| 取值非法 | 该键回落到默认值，并记录一条警告 |

适配器**不会**在运行期悄悄改写用户配置，只有"缺失即生成"和"损坏即备份重建"两种情况会写盘。显式写盘发生在：游戏内设置界面点击 **Save configuration**，或在 **Mods** 页把模组加入/移出下次启动禁用列表时。

## 5. 目录布局

```text
<适配器目录>/
  Config.toml
  lang.json                 # 界面语言文件（可编辑；缺失/损坏时用程序集内嵌副本）
  Logs/
    MelonAdapter.log
    CompatibilityReport.txt
  Mods/                      # modsDirectory
    SampleMod/               # 可选的每模组子目录（私有依赖放这里）
      SampleMod.dll
      SupportLib.dll
  Dependencies/              # 适配器共享依赖，例如 0Harmony.dll
  UserData/                  # userDataDirectory
    MelonPreferences.cfg
    <melon-id>/              # 每模组私有数据目录
```

相对路径的 `modsDirectory` / `userDataDirectory` 相对于**适配器目录**解析，不是游戏根目录，避免与 UMM 的 `Mods/` 混淆。

## 6. 偏好设置文件

- 路径：`<userDataDirectory>/MelonPreferences.cfg`，TOML 格式，UTF-8（无 BOM）。
- 写入时机：启动完成、模组被禁用、UMM 开关关闭、退出、模组显式调用 `MelonPreferences.Save()`。
- 迁移：当目标文件不存在、且 `<Game>/UserData/MelonPreferences.cfg` 存在时，复制一份到适配器路径，并在旁边留下 `MelonPreferences.melonloader-<yyyyMMdd-HHmmss>.bak`；**原文件保持不动**，用户可以随时切回真实 MelonLoader。
- 迁移失败不会阻止启动，只在日志与兼容报告的 `Preferences` 行中标注原因。

## 7. 界面语言文件

- 路径：`<适配器目录>/lang.json`，UTF-8（建议无 BOM），JSON 格式。
- 结构：顶层 `languages` 数组列出可用语言（决定设置页按钮的顺序），其余每个键是一张语言表：

  ```json
  {
    "version": 1,
    "languages": ["zh", "en"],
    "zh": { "tab.mods": "模组" },
    "en": { "tab.mods": "Mods" }
  }
  ```

- 加载顺序：程序集内嵌副本 → 文件覆盖。文件只在启动与点击 **Reload language** 时读取，不会每帧访问磁盘。
- 文件缺失：正常启动，使用内嵌副本，界面的语言文件来源显示 `built-in (lang.json not found)`。
- 文件损坏：记录解析错误、使用内嵌副本，设置页显示错误文本；不会阻止启动，也不会覆盖用户的文件。
- 缺键回退：当前语言 → 英文 → 任意语言 → 键名本身；缺少的键会计数显示在设置页（`MissingKeyCount`）。
- 语言选择写在 `Config.toml` 的 `[ui] language`；`auto` 时按系统 UI 语言（`zh*` → 中文，否则英文）。只内置中文与英文，添加其它语言需要同时提供该语言的整张表，否则缺的键回退英文。

## 8. 环境变量

| 变量 | 作用 |
|---|---|
| `MELONADAPTER_HOME` | 覆盖适配器目录（优先级最高） |
| `MELONADAPTER_MODS_DIRECTORY` | 覆盖 `MelonEnvironment.ModsDirectory` 的返回值，仅供需要绝对路径的模组使用 |

## 9. 日志

- `Logs/MelonAdapter.log` 为追加写入，UTF-8，行格式：`[时间] [级别] [来源] 消息`。
- 级别：`DEBUG` / `INFO` / `WARN` / `ERROR` / `FATAL`；`debugLogging = false` 时 `DEBUG` 不落盘。
- UMM 日志同步输出 `Error`/`Warning`/`Log` 三档，便于在 UMM 控制台里看到关键事件。
- `Logs/CompatibilityReport.txt` 每次启动与每次状态变化后重写，内容见 `docs/VALIDATION.md` 第 4 节。

## 10. 游戏内设置界面

UMM 面板里的 MelonAdapter 条目会打开设置界面（`src/MelonAdapter.Host/AdapterUI.cs`），四个标签页：

| 标签页 | 覆盖内容 |
|---|---|
| Overview | 状态摘要、版本横幅、模组状态计数、打开目录、日志尾 40 行 |
| Mods | 模组列表/详情、打开模组目录与私有数据目录、跳到该模组的偏好、立即禁用、下次启动禁用、写兼容报告 |
| Preferences | 按创建者程序集分组编辑 `MelonPreferences` 条目，保存/重载偏好文件 |
| Settings | 界面语言（中文 / English）与重新加载 `lang.json`、调试日志即时开关、关闭适配器、Harmony 策略、异常阈值、解析器开关、保存 `Config.toml`、打开配置与日志 |

写盘规则：

| 操作 | 是否写盘 | 目标文件 |
|---|---|---|
| 切换界面语言 | 是（立即保存，立即生效） | `Config.toml` 的 `[ui] language` |
| 切换"调试日志" | 否（仅当前会话；想让下次生效需 Save configuration） | — |
| 修改偏好条目 | 否，直到点击 **Save** | `UserData/MelonPreferences.cfg` |
| **Save configuration** | 是 | `Config.toml` |
| **Skip next launch** / **Load next launch** | 是 | `Config.toml` 的 `[lifecycle] disabledMods` |
| **Disable now** | 否 | 仅当前会话（程序集无法卸载） |
| 关闭适配器 | 否 | 仅当前会话，重新启用需要重启游戏 |

界面不会修改 UMM 自身的配置，也不会写真实 MelonLoader 的目录。

面板高度：UMM 把模组页面画在自己的滚动视图里，页面内的 `BeginScrollView` 必须带显式高度，否则会塌缩成几十像素。`AdapterUI.PageHeight` 因此按屏幕高度（用 `GUI.matrix` 换算成 GUI 单位）的 62% 减 150 计算，并限制在 240–520 之间；概览页的日志尾固定 160。切换标签页会复位该页的滚动位置。
