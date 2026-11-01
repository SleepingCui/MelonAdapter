MelonAdapter 1.2.2
==================

在 UnityModManager 里运行 Mono 后端的 MelonLoader 0.7.3 模组。

安装
----
把整个 MelonAdapter 文件夹放进游戏的 Mods/ 目录：

  <Game>/Mods/MelonAdapter/
    Info.json
    MelonAdapter.dll
    MelonAdapter.Runtime.dll
    MelonLoader.dll
    Dependencies/
      0Harmony.dll        <- 需要你自己提供 Harmony 2.x 构建

启动游戏，UMM 面板会出现 MelonAdapter 条目。首次启动会生成：

  <Game>/MelonAdapter/Config.toml
  <Game>/MelonAdapter/Logs/MelonAdapter.log
  <Game>/MelonAdapter/Logs/CompatibilityReport.txt
  <Game>/MelonAdapter/Mods/
  <Game>/MelonAdapter/UserData/

把你的 MelonLoader 模组放进 <Game>/MelonAdapter/Mods/（可以带子目录）。

注意
----
- 只支持 Mono 后端；IL2CPP 游戏会在启动时被拒绝并说明原因。
- 不能与真实 MelonLoader 共存，检测到真实安装时会拒绝加载。
- 没有 0Harmony.dll 时适配器仍能启动，但使用 Harmony 的模组会加载失败。
- 在 UMM 面板关闭适配器后，必须重启游戏才能重新加载模组。
- UMM 面板里点击 MelonAdapter 可打开设置界面：查看状态与日志尾、逐模组禁用、编辑偏好设置、打开日志/配置/报告目录。

详细文档请见仓库中的 README.md、spec/ 与 docs/。
