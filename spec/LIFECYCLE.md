# 生命周期与事件调度

本文档定义 MelonAdapter 1.2.2 的回调顺序和异常处理规则。

## 1. 调度器

运行时本身不引用 UnityEngine。Host 创建唯一的 `MonoBehaviour`（`AdapterTicker`，GameObject 名为 `MelonAdapter.Ticker`，`HideFlags.HideAndDontSave` + `DontDestroyOnLoad`），把 Unity 的帧循环翻译成运行时调用：

| Unity 回调 | 运行时调用 |
|---|---|
| `Update` | `Tick(Time.deltaTime)`，并在第一帧报告一次当前场景 |
| `FixedUpdate` | `FixedUpdate(Time.fixedDeltaTime)` |
| `LateUpdate` | `LateUpdate(Time.deltaTime)` |
| `OnGUI` | `OnGUI()` |
| `SceneManager.sceneLoaded` | `OnSceneWasLoaded(buildIndex, name)` |
| `SceneManager.sceneUnloaded` | `OnSceneWasUnloaded(buildIndex, name)` |
| `OnDestroy` | 退订场景回调（不触发 mod 回调） |

所有模组回调均在 Unity 主线程执行。Host 转发时捕获异常，避免打断游戏主循环。

## 2. 启动：`AdapterRuntime.Initialize()`

由 Host 在 UMM 的 `Load(ModEntry)` 内调用，严格按下列顺序执行：

| # | 步骤 |
|---|---|
| 1 | 创建目录、读取配置（缺失则生成，损坏则备份重建）、打开日志。 |
| 2 | 迁移偏好（如启用）、接线 `AdapterBridge`、打印启动横幅。 |
| 3 | 若 `enabled = false`，写兼容报告并返回，不扫描或加载模组。 |
| 4 | 安装 `AssemblyResolve`，扫描 `Mods/` 并读取程序集、元数据和私有依赖。 |
| 5 | 建立依赖顺序；拒绝缺失依赖、循环依赖和重复 id。 |
| 6 | 实例化并注册模组，建立记录映射并创建各自的 `UserData/<id>/`。 |
| 7 | 依次派发 `OnPreInitialization`、`OnPreSupportModule`、`OnEarlyInitializeMelon`、`OnInitializeMelon`、`OnLateInitializeMelon` 和 `OnPreModsLoaded`。 |
| 8 | 写兼容报告并记录初始化状态摘要。 |

`OnApplicationStart` 系列不在模组初始化阶段触发，而是在首帧派发（见第 3 节）。

## 3. 首帧：延迟的应用启动回调

首次调用 `Tick` 时依次执行：

1. `MelonEvents.OnApplicationEarlyStart` → 各 melon `OnApplicationEarlyStart()`
2. `MelonEvents.OnApplicationStart` → 各 melon `OnApplicationStart()`
3. `MelonEvents.OnApplicationLateStart` → 各 melon `OnApplicationLateStart()`
4. 各 melon `OnModSettingsApplied()`（基类没有对应通知事件）

随后，若活动场景有效，`AdapterTicker` 按第 5 节派发场景回调。

## 4. 每帧顺序

```text
Tick(dt):    CoroutinePump.Tick() → MelonEvents.OnUpdate → 各 melon OnUpdate()
FixedUpdate: MelonEvents.OnFixedUpdate → 各 melon OnFixedUpdate()
LateUpdate:  MelonEvents.OnLateUpdate → 各 melon OnLateUpdate()
OnGUI:       MelonEvents.OnGUI → 各 melon OnGUI()
```

事件订阅者先于模组覆写方法执行，两者均按第 6 节排序。协程只在 `Tick` 中推进。

## 5. 场景回调

`OnSceneWasLoaded(index, name)`：

```text
MelonEvents.OnSceneWasLoaded
  → MelonMod.OnSceneWasLoaded(int, string)
  → MelonMod.OnLevelWasLoaded(int)
MelonEvents.OnSceneWasInitialized
  → MelonMod.OnSceneWasInitialized(int, string)
  → MelonMod.OnLevelWasInitialized(int)
```

`OnSceneWasUnloaded(index, name)`：

```text
MelonEvents.OnSceneWasUnloaded → MelonMod.OnSceneWasUnloaded(int, string)
```

Unity 5.x 之前没有 sceneUnloaded；Host 订阅失败时只记录 `[unity] Scene callbacks are unavailable in this Unity version`，其余回调继续工作。

## 6. 派发顺序（确定性规则）

依赖图决定加载和派发顺序，排序键依次为：

1. `MelonPriorityAttribute` 的优先级（降序，默认 0）；
2. 显示名（`OrdinalIgnoreCase`）；
3. 依赖深度（升序）；
4. 程序集路径（`OrdinalIgnoreCase`）。

排序同时满足依赖约束（依赖项先加载）。重复 id 时保留排序靠前者并拒绝其余项。

## 7. 异常隔离与自动禁用

- 回调异常不会传播给 Unity 或其它模组。
- 每个模组和回调分别计数；成功执行后清零。达到 `[lifecycle] maxConsecutiveExceptions`（默认 5）时禁用模组：

  ```text
  [callbacks] <name> threw in <callback> (<melon type>, 5/5): <exception>
  [lifecycle] <name> Disabled after 5 consecutive callback exceptions (last in OnUpdate).
  ```

- 禁用时标记状态和原因、退订事件、撤销 Harmony patch、调用 `Unregister` 并重写兼容报告。
- 状态不为 `Loaded` 的模组不再收到回调。

### 7.1 手动禁用单个模组

手动禁用与自动禁用执行相同清理：

- `AdapterRuntime.DisableMod(string modId, string reason)` 接受 mod id、显示名或程序集简单名（不区分大小写）；仅禁用 `Loaded` 模组，成功返回 `true`，否则返回 `false`。
- 清理顺序：标记禁用 → 退订事件 → 撤销 Harmony patch → 调用 `Unregister`（触发 `OnDeinitializeMelon`）→ 更新兼容报告。某一步失败不会阻止后续清理。
- 设置界面的 **Disable now** 调用此 API；**Skip next launch** 还会保存 id 到 `[lifecycle] disabledMods`。
- 程序集仍驻留内存；重载需重启游戏。

## 8. 运行期注册与注销

模组可在回调中调用 `Register()` / `Unregister()`：

- `NotifyRegistered` 将 melon 关联到所属程序集；未由适配器加载的程序集只记录警告。
- 当记录中的 melon 全部注销时，该记录转为 `Disabled` 并停止回调。

## 9. UMM 开关（`OnToggle`）

关闭时保存偏好、清空协程、退订事件、撤销补丁并更新报告；后续帧和场景回调直接返回。

再次开启不会重新加载模组；Mono 无法安全卸载程序集，需重启游戏。

## 10. 退出

游戏退出或 UMM 卸载模组时，`OnUnload` 调用幂等的 `Shutdown()`：

1. `[runtime] Shutting down; dispatching quit callbacks.`
2. `MelonEvents.OnApplicationQuit` → 各 melon `OnApplicationQuit()`
3. 保存偏好设置（模组在 quit 回调里修改的值会被保存）
4. `MelonEvents.OnApplicationDefiniteQuit` → 各 melon `OnApplicationDefiniteQuit()`
5. 逐 melon `Unregister("MelonAdapter shutdown", important: false)`，`OnDeinitializeMelon` 由 `Unregister` 触发；记录转为 `Disabled` 并标注 `Unregistered during shutdown.`
6. 撤销所有 Harmony owner 的 patch、停止协程、卸下 resolver、写兼容报告
7. 记录关闭状态摘要。

`AdapterTicker` 在调用 `Shutdown()` 前停用；退出路径不创建 Unity 对象。

## 11. 完整时序

```text
UMM Load(ModEntry)
  └─ ConflictDetector → AdapterConfig.Load → new AdapterRuntime
       ├─ dirs / logs / preferences migration / bridge / banner
       ├─ resolver install → scan → dependency graph → load + register
       ├─ events: OnPreInitialization, OnPreSupportModule
       ├─ callbacks: OnEarlyInitializeMelon, OnInitializeMelon, OnLateInitializeMelon
       └─ event: OnPreModsLoaded  → report
  └─ AdapterTicker.Create

frame #1: Tick → StartApplication
  ├─ events+callbacks: OnApplicationEarlyStart / OnApplicationStart / OnApplicationLateStart
  ├─ callbacks: OnModSettingsApplied
  └─ scene #1: OnSceneWasLoaded → ... → OnSceneWasInitialized

frame #N: Tick → coroutines → OnUpdate (event → callbacks)
          FixedUpdate / LateUpdate / OnGUI 同理

UMM UI 'Disable now' → DisableMod: unsubscribe + unpatch + unregister (restart to reload)
UMM toggle off → SetEnabled(false): save + unsubscribe + unpatch (restart required)
UMM unload / game exit → Shutdown(): quit events, save, unregister, unpatch, report
```

## 12. 兼容性说明

- 回调签名逐字对齐 0.7.3.0（含参数类型与可见性）；`OnGUI` 无参数、`OnFixedUpdate` 无参数、场景回调为 `(int buildIndex, string sceneName)`。
- `OnApplicationDefiniteQuit` 只在真正退出时触发；适配器在 `Shutdown()` 中与 `OnApplicationQuit` 相邻触发，二者顺序固定。
- Unity 版本覆盖范围仍需验证，见 `spec/COMPATIBILITY.md`。
