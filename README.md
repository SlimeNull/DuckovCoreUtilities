# DuckovMods

《逃离鸭科夫》的实用功能、游戏互操作工具与场景检视器。

仓库地址：https://github.com/SlimeNull/DuckovMods

[中文](#中文) | [English](#english)

## 中文

本仓库包含两个游戏模组，以及配套的 Windows 场景检视器和互操作库：

| 项目 | 说明 |
| --- | --- |
| **DuckovCoreUtilities** | 面向日常游玩的综合实用功能模组。 |
| **DuckovInterop** | 在游戏内提供本地 JSON RPC 服务，让外部程序可以读取和操作游戏状态。 |
| **DockovInterop.HierarchyInspector** | 基于 WPF 的场景检视器，用于浏览和编辑正在运行的游戏场景。 |
| **DuckovInterop.Bridge** | 将 DuckovInterop RPC 接口转换为 stdio MCP 工具的桥接程序。 |
| **DuckovInterop.Abstractions** | RPC 接口及共享数据模型。 |
| **JsonRpc** | 基于 Stream 的 JSON RPC 通信库。 |

### DuckovCoreUtilities

Core Utilities 提供一组可以在 ModSetting 界面中独立启用和配置的功能：

- 在物品界面显示出售价格或原始价格。
- 显示背包和仓库中同类物品的数量。
- 在物品界面显示品质信息。
- 为玩家当前可见的战利品箱和地面物品显示轮廓，支持品质颜色和呼吸效果。
- 为背包与仓库添加按价值、重量和价重比排序的按钮。
- 在移动或受伤时自动关闭背包和拾取界面。
- 瞄准时淡出 HUD，并允许调整目标透明度和过渡速度。
- 根据弹匣剩余弹药改变准星颜色。
- 游戏失去焦点时静音，并可自动打开暂停菜单。
- 低生命值时显示可配置的屏幕边缘阴影。
- 在 HUD 中显示近期击杀记录。

### DuckovInterop 与场景检视器

DuckovInterop 不限定于 MCP。它在游戏进程内运行一个本地 TCP JSON RPC 服务，为场景检视器、MCP 服务以及其他自动化或调试工具提供统一接口。

主要能力包括：

- 一次获取场景摘要，包括场景树、GameObject、组件及检视器字段。
- 按名称或组件类型搜索场景对象，包括未启用对象。
- 读取和修改 GameObject 的启用状态、组件启用状态及字段或属性。
- 修改结构类型的成员时自动回写完整结构，例如 `transform.position.x`。
- 调用实例或静态方法，并可保存返回的对象引用以供后续请求使用。
- 在游戏主线程执行 Jint JavaScript，用于高级查询和互操作。
- 通过 ModSetting 配置 RPC 服务开关、监听地址、端口和诊断日志。

`DockovInterop.HierarchyInspector` 提供接近 Unity Hierarchy 与 Inspector 的桌面界面：

- 左侧浏览完整场景树，并按启用状态、渲染器和 UI 对象进行过滤。
- 右侧查看 GameObject 上的组件及 Unity 可显示的序列化字段和 NativeProperty 属性。
- 直接修改 GameObject 启用状态、组件启用状态以及支持编辑的字段。
- 使用暗色主题呈现常见 WPF 控件和加载状态。

默认服务仅监听 `127.0.0.1:37620`。DuckovInterop 允许执行反射调用和 CLR JavaScript，不应将监听地址开放给不受信任的网络。

### 安装与配置

从 Steam 创意工坊安装模组时，Steam 会自动安装前置模组 ModSetting。进入游戏的 ModSetting 页面后，可以调整 Core Utilities 的各项功能以及 DuckovInterop 的服务配置。

场景检视器是独立的 Windows 应用。启动游戏并启用 DuckovInterop 后，再运行 `DockovInterop.HierarchyInspector.exe` 进行连接。

### 从源码构建

构建环境：

- Windows
- .NET 10 SDK
- 已安装《逃离鸭科夫》
- 已通过 Steam 创意工坊安装 ModSetting

游戏和 ModSetting 的默认路径位于 [`source/Global.props`](source/Global.props)。如安装位置不同，可通过 MSBuild 属性 `DuckovPath` 和 `ModSettingPath` 覆盖。

构建整个解决方案：

```powershell
dotnet build .\source\DuckovCoreUtilities.slnx
```

也可以单独构建项目：

```powershell
dotnet build .\source\DuckovCoreUtilities\DuckovCoreUtilities.csproj
dotnet build .\source\DuckovInterop\DuckovInterop.csproj
dotnet build .\source\DockovInterop.HierarchyInspector\DockovInterop.HierarchyInspector.csproj
```

## English

This repository contains two *Escape from Duckov* mods, a companion Windows hierarchy inspector, and the libraries used for external interoperability.

| Project | Description |
| --- | --- |
| **DuckovCoreUtilities** | A configurable collection of quality-of-life features for regular gameplay. |
| **DuckovInterop** | Hosts a local JSON RPC service inside the game so external applications can inspect and modify game state. |
| **DockovInterop.HierarchyInspector** | A WPF hierarchy inspector for browsing and editing the running game scene. |
| **DuckovInterop.Bridge** | A bridge that exposes the DuckovInterop RPC interface as stdio MCP tools. |
| **DuckovInterop.Abstractions** | Shared RPC contracts and data models. |
| **JsonRpc** | The Stream-based JSON RPC transport library. |

### DuckovCoreUtilities

Core Utilities provides individually configurable features through the ModSetting interface:

- Show sell price or raw price in item interfaces.
- Show matching item counts in the backpack and storage.
- Display item quality information.
- Outline loot containers and ground items currently visible to the player, with quality colors and breathing effects.
- Add inventory sorting buttons for value, weight, and value-to-weight ratio.
- Automatically close backpack and loot views when moving or taking damage.
- Fade HUD panels while aiming, with configurable opacity and transition speed.
- Change crosshair color according to the remaining magazine ammunition.
- Mute the game and optionally open the pause menu when the game loses focus.
- Display a configurable screen-edge shadow at low health.
- Show recent kill records on the HUD.

### DuckovInterop and Hierarchy Inspector

DuckovInterop is not limited to MCP use. It hosts a local TCP JSON RPC service inside the game and exposes a shared interface for the hierarchy inspector, MCP servers, automation, and debugging tools.

Its main capabilities include:

- Capture a scene overview containing the hierarchy, GameObjects, components, and inspector fields.
- Search scene objects by name or component type, including inactive objects.
- Read and modify GameObject active state, component enabled state, fields, and properties.
- Write back complete value types when editing nested members such as `transform.position.x`.
- Invoke instance or static methods and retain returned object references for later requests.
- Execute Jint JavaScript on the game main thread for advanced queries and interoperability.
- Configure the RPC service, listen address, port, and diagnostic logging through ModSetting.

`DockovInterop.HierarchyInspector` provides a desktop interface modeled after the Unity Hierarchy and Inspector:

- Browse the complete scene tree and filter by active state, renderer presence, or UI objects.
- Inspect components, Unity-serialized fields, and NativeProperty properties.
- Edit GameObject active state, component enabled state, and supported field values.
- Use a consistent dark theme across common WPF controls and loading states.

The service listens on `127.0.0.1:37620` by default. DuckovInterop exposes reflection calls and CLR-enabled JavaScript, so it must not be bound to an untrusted network.

### Installation and configuration

When the mods are installed from Steam Workshop, Steam automatically installs the ModSetting prerequisite. Use the ModSetting page in game to configure Core Utilities features and DuckovInterop service options.

The hierarchy inspector is a separate Windows application. Start the game with DuckovInterop enabled, then launch `DockovInterop.HierarchyInspector.exe` to connect.

### Building from source

Requirements:

- Windows
- .NET 10 SDK
- *Escape from Duckov* installed
- ModSetting installed from Steam Workshop

Default game and ModSetting paths are defined in [`source/Global.props`](source/Global.props). Override the `DuckovPath` and `ModSettingPath` MSBuild properties when using different installation locations.

Build the complete solution:

```powershell
dotnet build .\source\DuckovCoreUtilities.slnx
```

Or build individual projects:

```powershell
dotnet build .\source\DuckovCoreUtilities\DuckovCoreUtilities.csproj
dotnet build .\source\DuckovInterop\DuckovInterop.csproj
dotnet build .\source\DockovInterop.HierarchyInspector\DockovInterop.HierarchyInspector.csproj
```

Source repository: https://github.com/SlimeNull/DuckovMods
