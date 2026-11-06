# 验证矩阵与发布门禁

本文档记录自检方式、真实游戏验收步骤和发布门禁。

## 1. 验证方式

| 方式 | 覆盖面 | 运行条件 |
|---|---|---|
| `tests/MelonAdapter.SelfTest`（无头控制台） | 配置、扫描、依赖图、生命周期、回调、偏好、事件、协程、Harmony、界面语言、报告、开关、退出 | `dotnet build` + 直接运行 exe，无需 Unity |
| 真实游戏 + UMM | Unity 侧帧循环、场景回调、GUI、UMM 面板与适配器设置界面 | 需要一个 Mono 后端 Unity 游戏 |

无头自检用 `ITickSource` 驱动运行时，不依赖 UnityEngine；Unity 接线需按第 5 节在游戏中验证。

## 1.1 对真实模组的引用校验

真实模组的 API 兼容性通过程序集元数据校验：

- 将 0.7.3.0 基线与 facade 逐成员比对，检查完整签名（包括返回类型）；1.2.2 起无签名差异。
- 检查真实模组的 `TypeRef` / `MemberRef`，确认其 `MelonLoader` 引用能在 facade 中绑定。工具 `.scratch/apidiff` 不随包分发。
- 1.2.2 校验 Jipper Key Viewer 和 TimingShow 的 29 个引用，全部可绑定。

## 2. 样例模组

| 样例 | 目的 | 期望结果 |
|---|---|---|
| `samples/SampleMod` | 全量回调、日志、偏好、事件、协程、Harmony 补丁 | 加载成功；生命周期日志齐全；补丁生效（目标方法返回 42，撤销后回到 21） |
| `samples/SampleMod.MissingDependency` | `MelonAdditionalDependencies` 声明了不存在的依赖 | 被拒绝，原因包含 `Missing required dependency`，其它模组不受影响 |
| `samples/SampleMod.Throwing` | `OnUpdate` 等回调持续抛异常 | 连续异常达到阈值后被禁用，日志包含 `consecutive`，其余模组继续运行 |
| `samples/SupportLib` | 私有依赖（放在 `Mods/Support/`） | 不当作模组；作为依赖被解析；`SampleMod` 能调用到 `support-lib-ok` |
| `SampleMod.Copy.dll`（自检时复制） | 重复 id | 后到者被拒绝，原因包含 `Duplicate mod id` |

## 3. 自检覆盖（74 项）

| 组 | 断言要点 |
|---|---|
| 启动与配置 | 默认值、损坏备份、目录创建、初始化和首帧时序 |
| 扫描与依赖 | 模组发现、私有依赖、重复 id、依赖失败和游戏根目录隔离 |
| 生命周期 | 帧、场景、事件、协程、异常隔离和自动禁用 |
| Harmony | 补丁生效并在禁用或退出时撤销 |
| 偏好与数据 | 保存、迁移、备份、用户目录及分类创建者 |
| API 兼容 | `CreateEntry` 重载、日志事件、语义版本、旧版偏好结构、类型命名空间和 `FindMelon` |
| 语言 | 加载、覆盖、切换、回退、缺键计数、损坏文件和 `auto` |
| 报告与开关 | 兼容报告、手动禁用、UMM 开关和退出清理 |

## 4. 兼容报告结构

`Logs/CompatibilityReport.txt` 每次启动和每次状态变化后重写：

```text
[Environment]
Game / Game directory / Unity / Process / Adapter / API baseline /
Host loader / Harmony provider / Preferences / Adapter enabled

[Mods]
<文件名>  state=Loaded|Rejected|Disabled|Failed
  id / version / hash / reason / requires / optional / warnings / notes

[Assembly resolution]
<请求方 -> 目标>  resolved from <path> | not found

[Known API gaps]
...
```

报告是给人看与给支持人员看的，格式稳定、可 diff。

## 5. 人工步骤（真实游戏）

1. 按 `packaging/UMM/MelonAdapter/` 安装并启动游戏，确认 UMM 列出 MelonAdapter；日志应有版本横幅和 `Initialization complete`。
2. 放入 `SampleMod` 并重启，检查初始化、首帧、逐帧和场景回调顺序（见 [`spec/LIFECYCLE.md`](../spec/LIFECYCLE.md)）。
3. 打开设置界面，检查四个标签页可滚动；编辑并保存偏好和配置，重启后确认值保留。
4. 切换中英文并重载语言文件；测试损坏和缺失的 `lang.json` 均回退到内嵌表且不阻止启动。
5. 测试即时禁用、下次启动禁用、UMM 关闭和退出：回调停止、偏好保存、Harmony 补丁撤销，并记录 `Shutdown complete`。

## 6. 发布门禁

发布前须满足：

- [ ] `dotnet build MelonAdapter.slnx -m:1 -nodeReuse:false` 无警告或错误；SelfTest 退出码为 0 并输出 `PASS`；
- [ ] `spec/` 与 `docs/` 齐全，并与实现一致；
- [ ] `packaging/UMM/MelonAdapter/` 可直接复制进 UMM 的 `Mods/` 并使用；
- [ ] 冲突检测在以下四种场景下都能拒绝加载：已有外来 MelonLoader 程序集、真实 MelonLoader 组件、IL2CPP 游戏、游戏根残留 `MelonLoader/` 或 `MelonLoader.dll`；
- [ ] 无 0Harmony 时仍能启动，并在日志中给出可执行的提示；
- [ ] 单个模组加载失败不会阻止其它模组加载；
- [ ] 设置界面、偏好保存、模组禁用和配置保存均通过游戏内验收；
- [ ] 中英文切换即时生效并写入 `[ui] language`，`lang.json` 缺失或损坏时回退内嵌表且不阻止启动；
- [ ] 文档未宣称任何未验证的能力（IL2CPP、MelonPlugin、真实 MelonLoader 共存）。

门禁脚本见仓库根目录的 `build.ps1`（构建 + 自检 + 打包）。
