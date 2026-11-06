# 配置参考

配置文件位于 `<适配器目录>/Config.toml`，首次启动时生成。内置读取器支持注释、字符串、数值、布尔值和字符串数组。

## 1. 适配器目录

Host 按以下顺序确定适配器目录：

1. 环境变量 `MELONADAPTER_HOME`（绝对或相对路径）；
2. `<游戏根目录>/MelonAdapter/`，前提是该目录已存在且包含 `Config.toml` 或 `Mods/`；
3. 回落到 UMM 的模组目录（即 `Mods/MelonAdapter/`）。

游戏根目录从 UMM 提供的模组路径向上两级解析。Host 会先去掉路径末尾的分隔符，避免根目录误判为 `Mods/`（1.2.1 修复）。

游戏名和开发商依次取自 Unity `Application`、`<Game>/<Game>_Data/app.info`、游戏文件夹名。`MelonGameAttribute` 用这两个值判断兼容性。

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

设置界面可修改表中除 `modsDirectory`、`userDataDirectory` 和 `[api] baseline` 外的选项。语言切换会立即生效并保存；`enabled` 只能在界面中关闭。修改目录或 API 基线后需重启。

## 3. 默认文件

```toml
# 损坏的配置会备份为 Config.toml.invalid-<timestamp>。
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

适配器只会在配置缺失或损坏时自动写盘。其它更改需在设置页点击 **Save configuration**；更改下次启动禁用列表会立即保存。

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

- 路径：`<userDataDirectory>/MelonPreferences.cfg`，UTF-8 TOML。
- 启动完成、禁用模组、关闭适配器、退出或调用 `MelonPreferences.Save()` 时写入。
- 仅当目标文件不存在时迁移：复制 `<Game>/UserData/MelonPreferences.cfg`，并保留带时间戳的 `.bak`；原文件不改动。
- 迁移失败不会阻止启动，原因会写入日志和兼容报告。

## 7. 界面语言文件

- 路径：`<适配器目录>/lang.json`，UTF-8 JSON（建议无 BOM）。
- 结构：顶层 `languages` 数组列出可用语言（决定设置页按钮的顺序），其余每个键是一张语言表：

  ```json
  {
    "version": 1,
    "languages": ["zh", "en"],
    "zh": { "tab.mods": "模组" },
    "en": { "tab.mods": "Mods" }
  }
  ```

- 加载顺序：内嵌表，再应用文件覆盖。仅启动或点击 **Reload language** 时读取。
- 文件缺失：正常启动，使用内嵌副本，界面的语言文件来源显示 `built-in (lang.json not found)`。
- 文件损坏：记录错误并使用内嵌表；不阻止启动，也不覆盖原文件。
- 缺键回退顺序：当前语言 → 英文 → 任一语言 → 键名；缺失数显示在设置页。
- `[ui] language` 支持 `auto`、`zh`、`en`。`auto` 根据系统 UI 语言选择；其它语言需提供完整键表。

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

UMM 面板中的 MelonAdapter 条目打开四个设置页：

| 标签页 | 覆盖内容 |
|---|---|
| Overview | 状态、版本、目录和最近 40 行日志 |
| Mods | 模组详情、偏好入口、即时禁用、下次启动禁用、兼容报告 |
| Preferences | 按创建程序集分组编辑和保存偏好 |
| Settings | 语言、日志、Harmony、异常阈值、解析器和配置 |

写盘规则：

| 操作 | 是否写盘 | 目标文件 |
|---|---|---|
| 切换界面语言 | 是（立即保存，立即生效） | `Config.toml` 的 `[ui] language` |
| 切换调试日志 | 否，除非再保存配置 | — |
| 修改偏好条目 | 否，直到点击 **Save** | `UserData/MelonPreferences.cfg` |
| **Save configuration** | 是 | `Config.toml` |
| **Skip next launch** / **Load next launch** | 是 | `Config.toml` 的 `[lifecycle] disabledMods` |
| **Disable now** | 否 | 仅当前会话（程序集无法卸载） |
| 关闭适配器 | 否 | 仅当前会话；重启后才能启用 |

界面只读写 MelonAdapter 自己的配置与数据。内容在固定高度的面板内滚动；切换标签页会回到顶部。
