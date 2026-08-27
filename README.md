# DuckovMods

《逃离鸭科夫》的实用功能、游戏互操作工具与场景检视器。

仓库地址：https://github.com/SlimeNull/DuckovMods

[中文](#中文) | [English](#english)

## 界面预览 / Preview

<p align="center">
  <a href="images/hierarchy_inspector.png"><img src="images/hierarchy_inspector.png" alt="场景检视器 / Hierarchy Inspector" width="57%"></a>
  <a href="images/mcp_bridge.png"><img src="images/mcp_bridge.png" alt="MCP Bridge" width="40%"></a>
</p>

## 中文

本仓库包含四个游戏模组，以及配套的 Windows 场景检视器和互操作库：

| 项目 | 说明 |
| --- | --- |
| **DuckovCoreUtilities** | 面向日常游玩的综合实用功能模组。 |
| **DuckovModSettings** | 自动发现 MonoBehaviour 设置并提供分层设置界面。 |
| **DockovParty** | 为游戏添加服主权威的双人合作联机。 |
| **DuckovInterop** | 在游戏内提供本地 JSON RPC 服务，让外部程序可以读取和操作游戏状态。 |
| **DockovInterop.HierarchyInspector** | 基于 WPF 的场景检视器，用于浏览和编辑正在运行的游戏场景。 |
| **DuckovInterop.Bridge** | 将 DuckovInterop RPC 接口转换为 stdio MCP 工具的桥接程序。 |
| **DuckovInterop.Abstractions** | RPC 接口及共享数据模型。 |
| **JsonRpc** | 基于 Stream 的 JSON RPC 通信库。 |

### DuckovCoreUtilities

Core Utilities 提供一组可以在 Duckov Mod Settings 界面中独立启用和配置的功能：

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
- 在关卡内的时间与风暴信息下方显示可配置的圆角小地图，支持固定方向、跟随玩家朝向、透明度和快捷键缩放。
- 显示手雷爆炸范围、引信进度和烟雾区域剩余时间。
- 为已录入的钥匙、钥匙卡和蓝图添加标识。
- 汇总物品在未完成任务、天赋和建筑中的需求数量。
- 支持右键收藏任务、显示收藏标记、按存档保存并将收藏任务置顶。

### DockovParty

DockovParty 是面向当前游戏版本的实验性双人联机模组。玩家从主菜单继续自己的存档时会直接监听联机；另一名玩家点击新增的“加入游戏”按钮后，使用 Duckov Mod Settings 中保存的地址连接，不会出现额外配置弹窗。

当前实现包括：

- 基于 `Stream` 的可替换长连接传输层，提供 `Listen`、`AcceptAsync` 与 `ConnectAsync` 接口；默认使用启用 keepalive 的 TCP Stream。
- 带魔数、版本、消息类型、长度上限和心跳超时的二进制帧协议。
- 双方选择同一目标后才提交的场景闸门；阵亡玩家由仍存活的一方带回基地。
- 玩家位置、朝向、生命、角色物品树和远端角色副本同步，并阻止同阵营友军伤害。
- 服主权威的 NPC 状态与伤害处理，以及共享容器租约、版本冲突纠正、地面物品认领和动态战利品箱同步。
- 客户端角色、共享仓库和战利品写入服主存档；客户端会话期间禁止写入其本地存档，并在返回主菜单时重新载入原缓存。
- 阵亡后持续观战另一名玩家，直到队伍返回基地；双方同时阵亡时由服主统一提交回基地。

所有可调配置均位于 Duckov Mod Settings：联机昵称、监听地址、加入地址、端口、状态同步频率、插值延迟和诊断日志。默认端口为 `37622`。

这是 Alpha 实现，目前固定为两名玩家，不支持主机迁移，也没有加密、账号认证或 NAT 穿透。任务、商店、基地建设、时间和其他全局系统尚未全部网络化；请仅在可信局域网或受信任的组网/VPN 中测试。详细设计和边界见 [`source/DockovParty/README.md`](source/DockovParty/README.md)。

### DuckovInterop 与场景检视器

DuckovInterop 不限定于 MCP。它在游戏进程内运行一个本地 TCP JSON RPC 服务，为场景检视器、MCP 服务以及其他自动化或调试工具提供统一接口。

主要能力包括：

- 一次获取场景摘要，包括场景树、GameObject、组件及检视器字段。
- 按名称或组件类型搜索场景对象，包括未启用对象。
- 读取和修改 GameObject 的启用状态、组件启用状态及字段或属性。
- 修改结构类型的成员时自动回写完整结构，例如 `transform.position.x`。
- 调用实例或静态方法，并可保存返回的对象引用以供后续请求使用。
- 在游戏主线程执行 Jint JavaScript，用于高级查询和互操作。
- 通过 Duckov Mod Settings 配置 RPC 服务开关、监听地址、端口和诊断日志。

`DockovInterop.HierarchyInspector` 提供接近 Unity Hierarchy 与 Inspector 的桌面界面：

- 左侧浏览完整场景树，并按启用状态、渲染器和 UI 对象进行过滤。
- 右侧查看 GameObject 上的组件及 Unity 可显示的序列化字段和 NativeProperty 属性。
- 直接修改 GameObject 启用状态、组件启用状态以及支持编辑的字段。
- 使用暗色主题呈现常见 WPF 控件和加载状态。

默认服务仅监听 `127.0.0.1:37620`。DuckovInterop 允许执行反射调用和 CLR JavaScript，不应将监听地址开放给不受信任的网络。

### 安装与配置

安装 DuckovModSettings 后，主菜单和游戏内设置面板会出现“模组”标签页。它会自动读取模组根对象上同程序集 MonoBehaviour 的公开字段、公开可读写属性，以及带 `[SerializeField]` 的非公开字段；其他模组不需要引用或调用设置 API。未安装 DuckovModSettings 时，Core Utilities、DockovParty 和 DuckovInterop 仍会按默认配置运行。

设置支持 Unity 的 `[HideInInspector]`、`[Header]`、`[Tooltip]`、`[Range]`、`[TextArea]`、`[InspectorName]` 和 `[FormerlySerializedAs]` 特性。用户修改设置并关闭页面后，设置组件所在对象会收到 `DuckovModSettingsUpdated` Unity 消息。

`[InspectorName]`、`[Header]` 和 `[Tooltip]` 的文本可以使用资源键：`@TextKey` 会从设置所属程序集发现的第一个 `ResourceManager` 中读取，`@ResourceType/TextKey` 会先按简单名称或全限定名称找到资源类型，再通过该类型读取资源。枚举项的 `[InspectorName]` 同样支持此语法。Duckov 切换语言时，DuckovModSettings 会同步资源类型的 `Culture` 并刷新已打开的页面；找不到资源或键时保留原始 `@...` 文本以便诊断。

场景检视器是独立的 Windows 应用。启动游戏并启用 DuckovInterop 后，再运行 `DockovInterop.HierarchyInspector.exe` 进行连接。

### 从源码构建

构建环境：

- Windows
- .NET 10 SDK
- 已安装《逃离鸭科夫》

游戏的默认路径位于 [`source/Global.props`](source/Global.props)。如安装位置不同，可通过 MSBuild 属性 `DuckovPath` 覆盖。

构建整个解决方案：

```powershell
dotnet build .\source\DuckovCoreUtilities.slnx
```

也可以单独构建项目：

```powershell
dotnet build .\source\DuckovCoreUtilities\DuckovCoreUtilities.csproj
dotnet build .\source\DuckovModSettings\DuckovModSettings.csproj
dotnet build .\source\DockovParty\DockovParty.csproj
dotnet build .\source\DuckovInterop\DuckovInterop.csproj
dotnet build .\source\DockovInterop.HierarchyInspector\DockovInterop.HierarchyInspector.csproj
```

运行 DockovParty 的协议与 TCP 回环测试：

```powershell
dotnet test .\source\DockovParty.Tests\DockovParty.Tests.csproj
```

## English

This repository contains four *Escape from Duckov* mods, a companion Windows hierarchy inspector, and the libraries used for external interoperability.

| Project | Description |
| --- | --- |
| **DuckovCoreUtilities** | A configurable collection of quality-of-life features for regular gameplay. |
| **DuckovModSettings** | Automatically discovers MonoBehaviour settings and presents a hierarchical settings UI. |
| **DockovParty** | Adds host-authoritative two-player cooperative multiplayer. |
| **DuckovInterop** | Hosts a local JSON RPC service inside the game so external applications can inspect and modify game state. |
| **DockovInterop.HierarchyInspector** | A WPF hierarchy inspector for browsing and editing the running game scene. |
| **DuckovInterop.Bridge** | A bridge that exposes the DuckovInterop RPC interface as stdio MCP tools. |
| **DuckovInterop.Abstractions** | Shared RPC contracts and data models. |
| **JsonRpc** | The Stream-based JSON RPC transport library. |

### DuckovCoreUtilities

Core Utilities provides individually configurable features through Duckov Mod Settings:

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
- Show a configurable rounded minimap below the time and storm information while in raid levels, with fixed or player-relative orientation, opacity, and keyboard zoom controls.
- Show grenade blast radii, fuse progress, and remaining smoke-zone duration.
- Mark keys, keycards, and blueprints that have already been registered.
- Summarize item quantities required by unfinished quests, perks, and buildings.
- Favorite quests by right-clicking them, persist favorites per save, and keep them at the top of the quest list.

### DockovParty

DockovParty is an experimental two-player multiplayer mod for the current game build. Continuing a local save immediately starts listening for a peer. The second player uses the new Join Game button, which connects to the address stored in Duckov Mod Settings without opening another configuration dialog.

The current implementation provides:

- A replaceable, long-lived `Stream` transport with `Listen`, `AcceptAsync`, and `ConnectAsync`; TCP Streams with keepalive are the default implementation.
- A binary framed protocol with a magic value, version, message kind, payload limit, keepalive, and connection timeout.
- A scene barrier that commits a transition only after both living players choose the same destination; a dead player follows the surviving player back to base.
- Player transform, health, character item-tree, and remote replica synchronization with same-team friendly-fire prevention.
- Host-authoritative NPC state and damage, shared-container leases with version correction, ground-item claims, and dynamically spawned loot containers.
- Host-side persistence for the client character, shared storage, and loot. Client disk writes are suppressed for the session and its original local cache is reloaded on return to the main menu.
- Spectating after death until the party returns to base, including a host-coordinated party-wipe transition.

Every adjustable value lives in Duckov Mod Settings: player name, listen address, join address, port, state rate, interpolation delay, and diagnostic logging. The default port is `37622`.

This is an alpha implementation. It is fixed to two players and does not provide host migration, encryption, account authentication, or NAT traversal. Quests, merchants, base construction, time, and other global systems are not all networked yet. Test only on a trusted LAN or trusted overlay/VPN. See [`source/DockovParty/README.md`](source/DockovParty/README.md) for the detailed design and current boundaries.

### DuckovInterop and Hierarchy Inspector

DuckovInterop is not limited to MCP use. It hosts a local TCP JSON RPC service inside the game and exposes a shared interface for the hierarchy inspector, MCP servers, automation, and debugging tools.

Its main capabilities include:

- Capture a scene overview containing the hierarchy, GameObjects, components, and inspector fields.
- Search scene objects by name or component type, including inactive objects.
- Read and modify GameObject active state, component enabled state, fields, and properties.
- Write back complete value types when editing nested members such as `transform.position.x`.
- Invoke instance or static methods and retain returned object references for later requests.
- Execute Jint JavaScript on the game main thread for advanced queries and interoperability.
- Configure the RPC service, listen address, port, and diagnostic logging through Duckov Mod Settings.

`DockovInterop.HierarchyInspector` provides a desktop interface modeled after the Unity Hierarchy and Inspector:

- Browse the complete scene tree and filter by active state, renderer presence, or UI objects.
- Inspect components, Unity-serialized fields, and NativeProperty properties.
- Edit GameObject active state, component enabled state, and supported field values.
- Use a consistent dark theme across common WPF controls and loading states.

The service listens on `127.0.0.1:37620` by default. DuckovInterop exposes reflection calls and CLR-enabled JavaScript, so it must not be bound to an untrusted network.

### Installation and configuration

With DuckovModSettings installed, a Mods tab is added to both the main-menu and in-game options panels. It automatically reads public fields, public read/write properties, and non-public `[SerializeField]` fields from same-assembly MonoBehaviours on each mod root; other mods do not reference or call a settings API. Core Utilities, DockovParty, and DuckovInterop still run with defaults when DuckovModSettings is absent.

The UI understands Unity's `[HideInInspector]`, `[Header]`, `[Tooltip]`, `[Range]`, `[TextArea]`, `[InspectorName]`, and `[FormerlySerializedAs]` attributes. After edited settings are closed, the owning GameObject receives the `DuckovModSettingsUpdated` Unity message.

Text in `[InspectorName]`, `[Header]`, and `[Tooltip]` may reference assembly resources. `@TextKey` uses the first `ResourceManager` discovered in the settings assembly; `@ResourceType/TextKey` first resolves the resource type by simple or fully qualified name. Enum-value `[InspectorName]` attributes use the same syntax. DuckovModSettings synchronizes the resource type's `Culture` and refreshes open pages when Duckov changes language. An unresolved resource or key remains visible as its original `@...` expression for diagnostics.

The hierarchy inspector is a separate Windows application. Start the game with DuckovInterop enabled, then launch `DockovInterop.HierarchyInspector.exe` to connect.

### Building from source

Requirements:

- Windows
- .NET 10 SDK
- *Escape from Duckov* installed

The default game path is defined in [`source/Global.props`](source/Global.props). Override the `DuckovPath` MSBuild property when using a different installation location.

Build the complete solution:

```powershell
dotnet build .\source\DuckovCoreUtilities.slnx
```

Or build individual projects:

```powershell
dotnet build .\source\DuckovCoreUtilities\DuckovCoreUtilities.csproj
dotnet build .\source\DuckovModSettings\DuckovModSettings.csproj
dotnet build .\source\DockovParty\DockovParty.csproj
dotnet build .\source\DuckovInterop\DuckovInterop.csproj
dotnet build .\source\DockovInterop.HierarchyInspector\DockovInterop.HierarchyInspector.csproj
```

Run DockovParty protocol and TCP loopback tests:

```powershell
dotnet test .\source\DockovParty.Tests\DockovParty.Tests.csproj
```

Source repository: https://github.com/SlimeNull/DuckovMods
