# 第三方声明

本仓库的代码为独立实现；下列第三方组件被引用、随包分发或作为兼容目标。

## 0Harmony (Harmony 2.x)

- 用途：Harmony provider，运行时通过 `Dependencies/0Harmony.dll` 加载；`bootstrapper` 中也保留编译期引用。
- 许可证：MIT。
- 版权：Copyright (c) 2017 Andreas Pardeike。

```text
MIT License

Copyright (c) 2017 Andreas Pardeike

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## MelonLoader

- 用途：API 兼容目标。`src/MelonLoader.Facade` 按 0.7.3.0 的公共 API 重新实现类型与成员签名，**没有复制其源码**；仓库内不含 MelonLoader 二进制。
- 许可证：Apache License 2.0。
- 兼容性声明：MelonAdapter 与 MelonLoader 项目无隶属关系，也不代表其官方立场。

## UnityModManager

- 用途：宿主加载器。仓库只引用 `libs/UnityModManager.dll` 进行编译，不分发该程序集；玩家自行安装 UMM。
- 许可证：以 UMM 发行包内声明为准。

## .NET Framework / Mono

- 用途：目标运行时。按各自发行方的许可使用，不随本仓库分发。
