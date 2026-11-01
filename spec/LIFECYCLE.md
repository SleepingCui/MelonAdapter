# 生命周期与事件调度

本文档定义 MelonAdapter 1.2.2 的回调顺序契约。顺序是**稳定的、与文件系统枚举顺序无关的**，并且每个 melon 的每次回调都被单独隔离。

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

所有 mod 回调都在 Unity 主线程执行；运行时不创建线程、不排队到线程池。`AdapterTicker` 的每次转发都包了 `try/catch`，Unity 侧异常不会打断游戏主循环。

## 2. 启动：`AdapterRuntime.Initialize()`

由 Host 在 UMM 的 `Load(ModEntry)` 内调用，严格按下列顺序执行：

| # | 步骤 | 可见日志（节选） |
|---|---|---|
| 1 | 创建目录（`Config.toml` 所在目录、`Logs/`、`Mods/`、`UserData/`） | `[config]` / `[paths]` |
| 2 | 读取配置；缺失则写默认、损坏则改名保留并重建 | `[config] Created ...` / `[config] Corrupt ...` |
| 3 | 打开适配器日志 | — |
| 4 | 偏好设置迁移（可选，见 `docs/CONFIGURATION.md`） | `[preferences] Imported MelonLoader preferences ...` |
| 5 | 接线 `AdapterBridge`（日志/上下文/偏好/协程/Harmony/注册表） | — |
| 6 | 打印启动横幅 | `[banner] Game: ...` / `Unity: ...` / `Host: UnityModManager ...` / `MelonLoader API baseline: 0.7.3.0` / `Harmony: ...` |
| 7 | 若 `enabled = false`：记录说明、生成兼容报告并**直接返回**（不扫描、不加载） | `[runtime] The adapter is disabled ...` |
| 8 | 安装 `AssemblyResolve` 处理器（程序集解析器） | `[resolver] ...` |
| 9 | 扫描 `Mods/`：读取程序集身份、Melon 元数据、私有依赖 | `[scan] ...` |
| 10 | 依赖图排序；缺依赖 / 循环依赖 / 重复 id 标记为拒绝 | `[graph] Rejected 「<name>」: ...` |
| 11 | 逐个实例化并注册 melon，建立程序集与记录的映射，创建每模组 `UserData/<id>/` | `[mods] Loaded 「<name>」 ...` |
| 12 | 事件 `OnPreInitialization` | — |
| 13 | 事件 `OnPreSupportModule` | — |
| 14 | 回调 `OnEarlyInitializeMelon` → `OnInitializeMelon` → `OnLateInitializeMelon` | — |
| 15 | 事件 `OnPreModsLoaded` | — |
| 16 | 写兼容报告，输出 `Initialization complete: <状态摘要>` | `[runtime] Initialization complete: 2/3 mods active, ...` |

注意：`OnApplicationStart` 系列**不在启动时**触发，而是延迟到第一帧（见第 3 节），这与 MelonLoader 的时序一致——模组初始化时 Unity 场景尚未就绪。

## 3. 首帧：延迟的应用启动回调

`Tick` 第一次被调用时（即第一帧）执行一次：

1. `MelonEvents.OnApplicationEarlyStart` → 各 melon `OnApplicationEarlyStart()`
2. `MelonEvents.OnApplicationStart` → 各 melon `OnApplicationStart()`
3. `MelonEvents.OnApplicationLateStart` → 各 melon `OnApplicationLateStart()`
4. 各 melon `OnModSettingsApplied()`（基类没有对应通知事件）

随后 `AdapterTicker` 在第一帧报告一次活动场景（`IsValid()` 为真时），走第 5 节的场景序列。

## 4. 每帧顺序

```text
Tick(dt):    CoroutinePump.Tick() → MelonEvents.OnUpdate → 各 melon OnUpdate()
FixedUpdate: MelonEvents.OnFixedUpdate → 各 melon OnFixedUpdate()
LateUpdate:  MelonEvents.OnLateUpdate → 各 melon OnLateUpdate()
OnGUI:       MelonEvents.OnGUI → 各 melon OnGUI()
```

统一规则：**同一回调的事件订阅者先执行，然后才是各 melon 的覆写方法**；每一层内部按第 6 节的稳定顺序派发。协程只在 `Tick` 中推进一次，`FixedUpdate` / `LateUpdate` 不推进协程。

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

加载顺序与派发顺序同源，由依赖图产生，排序键依次为：

1. `MelonPriorityAttribute` 的优先级（降序，默认 0）；
2. 显示名（`OrdinalIgnoreCase`）；
3. 依赖深度（升序）；
4. 程序集路径（`OrdinalIgnoreCase`）。

以满足依赖约束（被依赖者先加载）为前提。同一批模组在不同启动、不同目录枚举顺序下得到相同顺序；重复 id 时先到者胜出，后到者以 `Duplicate mod id ...` 被拒绝。

## 7. 异常隔离与自动禁用

- 每次回调都包在 `try/catch` 中；异常不会传播给 Unity 或其它模组。
- 每个「模组 + 回调名」维护**连续**异常计数；成功执行一次即清零。
- 计数达到 `[lifecycle] maxConsecutiveExceptions`（默认 5）时，该模组被禁用：

  ```text
  [callbacks] <name> threw in <callback> (<melon type>, 5/5): <exception>
  [lifecycle] <name> Disabled after 5 consecutive callback exceptions (last in OnUpdate).
  ```

- 禁用动作 = 标记 `Disabled`、记录失败原因、退订该程序集的所有 `MelonEvents` 订阅、撤销该 owner 的 Harmony patch、调用 `Unregister(reason, important: true)`、重写兼容报告。
- `mods` 表中状态不是 `Loaded` 的模组（`Rejected` / `Disabled` / `Failed`）不再收到任何回调。

### 7.1 手动禁用单个模组

运行期手动禁用与自动禁用共用同一套清理动作（`AdapterRuntime.DisableRecord`）：

- API：`AdapterRuntime.DisableMod(string modId, string reason)`。`modId` 接受 mod id（`MelonInfo` 名字）、显示名或程序集简单名，不区分大小写；只有状态为 `Loaded` 的模组会被禁用，返回 `true`，否则 `false`。
- 清理顺序：把记录置为 `Disabled` 并写明原因 → 退订该程序集的所有 `MelonEvents` 订阅 → 撤销该 owner 的 Harmony patch（即使前面步骤抛异常也会继续）→ 逐个 `Unregister(reason, important: true)`（触发 `OnDeinitializeMelon`）→ 重写兼容报告。
- UMM 设置界面的 **Mods → Disable now** 就是调用该 API；**Skip next launch** 还会把 id 写入 `[lifecycle] disabledMods` 并保存 `Config.toml`。
- 禁用只作用于当前会话，程序集仍留在进程里；重新加载需要重启游戏。

## 8. 运行期注册与注销

模组可以在回调里调用 `SomeMelon.Register()` / `Unregister(...)`（例如动态创建第二个 melon）：

- `NotifyRegistered` 把 melon 关联到所属程序集的记录；未由适配器加载的程序集只会收到回调并记录警告，不进入兼容报告；
- `NotifyUnregistered` 移除该 melon；当一条 `Loaded` 记录的 melon 全部注销时，记录转为 `Disabled`（原因 `All melons unregistered themselves.`）并停止回调。

## 9. UMM 开关（`OnToggle`）

关闭：

1. 记录 `[lifecycle] MelonAdapter was disabled. Callbacks stop, preferences are saved and Harmony patches are removed. A game restart is required to load mods again.`
2. `MelonPreferences.Save()`；
3. 停止并清空所有协程；
4. 逐模组退订 `MelonEvents` 订阅、撤销 Harmony patch；
5. 重写兼容报告；此后 `Tick` / `FixedUpdate` / `LateUpdate` / `OnGUI` / 场景回调全部直接返回。

再次开启：**不重新加载任何程序集**，只记录 `Re-enabling MelonAdapter is not possible at runtime: mod assemblies cannot be unloaded. Restart the game to load mods again.`。Mono 进程内没有安全的程序集卸载手段，适配器不假装已经恢复。

## 10. 退出

游戏退出或 UMM 卸载模组时（`OnUnload` → `Shutdown()`，幂等）：

1. `[runtime] Shutting down; dispatching quit callbacks.`
2. `MelonEvents.OnApplicationQuit` → 各 melon `OnApplicationQuit()`
3. 保存偏好设置（模组在 quit 回调里修改的值会被保存）
4. `MelonEvents.OnApplicationDefiniteQuit` → 各 melon `OnApplicationDefiniteQuit()`
5. 逐 melon `Unregister("MelonAdapter shutdown", important: false)`，`OnDeinitializeMelon` 由 `Unregister` 触发；记录转为 `Disabled` 并标注 `Unregistered during shutdown.`
6. 撤销所有 Harmony owner 的 patch、停止协程、卸下 resolver、写兼容报告
7. `Shutdown complete: <状态摘要>`

退出路径不创建新的 Unity 对象；`AdapterTicker` 先被停用再调用 `Shutdown()`。

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
- 尚未在真实游戏中验证 Unity 版本区间，见 `spec/COMPATIBILITY.md`。
