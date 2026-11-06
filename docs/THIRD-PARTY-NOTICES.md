# 第三方声明

MelonAdapter 为独立实现。第三方组件的使用与许可如下。

## 0Harmony (Harmony 2.x)

- 用途：Harmony provider，从 `Dependencies/0Harmony.dll` 加载；bootstrapper 保留编译引用。
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

## Newtonsoft.Json (Json.NET 13.0.0.0)

- 用途：读取 `lang.json`。游戏通常在 `<Game>_Data/Managed/Newtonsoft.Json.dll` 提供该程序集；`libs/` 中的副本仅供编译，**不随包分发**。运行时使用游戏进程中的程序集。
- 许可证：MIT。
- 版权：Copyright (c) 2007 James Newton-King。

```text
MIT License

Copyright (c) 2007 James Newton-King

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

- 用途：API 兼容目标。`src/MelonLoader.Facade` 独立实现 0.7.3.0 公共类型和成员签名，未复制源码；仓库不含 MelonLoader 二进制。
- 许可证：Apache License 2.0。
- 兼容性声明：MelonAdapter 与 MelonLoader 项目无隶属关系，也不代表其官方立场。

## UnityModManager

- 用途：宿主加载器。`libs/UnityModManager.dll` 仅用于编译，不随包分发；用户自行安装 UMM。
- 许可证：以 UMM 发行包内声明为准。

## .NET Framework / Mono

- 用途：目标运行时；不随本仓库分发。
