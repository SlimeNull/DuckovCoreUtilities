DuckovInterop 在《逃离鸭科夫》进程内提供本地 TCP JSON RPC 服务，使场景检视器、MCP Bridge 以及其他开发工具能够读取和操作正在运行的游戏状态。

此模组主要面向模组开发、调试和自动化，而不是普通玩法增强。

[h1]主要能力[/h1]

[list]
[*]获取已加载场景及完整 GameObject 层级，包括未激活对象。
[*]查看 GameObject 上的组件、Unity 序列化字段与可见属性。
[*]按对象名称或组件类型搜索场景。
[*]读取和修改 GameObject 激活状态、组件状态、字段及属性。
[*]读取或写入嵌套路径；修改结构体成员时自动回写完整值，例如 [b]transform.position.x[/b]。
[*]调用实例方法或静态方法，并保存返回的对象引用供后续请求使用。
[*]在 Unity 主线程中执行允许访问 CLR 的 Jint JavaScript，以完成高级查询和操作。
[/list]

所有 Unity 对象访问都会调度到游戏主线程；网络监听和客户端连接在后台线程中运行。

[h1]配套工具[/h1]

源代码仓库包含两个独立工具：

[list]
[*][b]DockovInterop.HierarchyInspector[/b]：接近 Unity Hierarchy 与 Inspector 的 Windows 桌面界面，可浏览场景树、检查组件并编辑支持的值。
[*][b]DuckovInterop.Bridge[/b]：将 DuckovInterop 的 RPC 接口转换为 stdio MCP 工具，可接入 Codex、Claude、Copilot 及其他支持 MCP 的 Agent。
[/list]

这些工具不会随此模组自动安装，需要从 [url=https://github.com/SlimeNull/DuckovMods]GitHub 仓库[/url]自行构建。

[h1]订阅与使用[/h1]

[olist]
[*]订阅此模组并在游戏中启用。
[*]启动游戏。默认 RPC 地址为 [b]127.0.0.1:37620[/b]。
[*]启动场景检视器，或配置 DuckovInterop.Bridge/MCP 客户端连接该地址。
[/olist]

订阅 [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3789464208]Duckov Mod Settings[/url] 后，可在“设置 > 模组 > DuckovInterop”中调整：

[list]
[*]是否启用 RPC 服务；
[*]监听地址；
[*]监听端口（1024 至 65535）；
[*]诊断日志。
[/list]

监听地址或端口修改后，需要重启 RPC 服务或游戏才能生效。未订阅或未启用 Duckov Mod Settings 时，服务使用默认配置运行。

[h1]RPC 接口概览[/h1]

公开接口包括场景层级与快照、组件详情、名称和类型搜索、值读取与写入、GameObject 激活状态、方法调用以及 Jint 执行。共享契约和数据模型位于 DuckovInterop.Abstractions 项目中，可直接用于自定义客户端。

[h1]安全警告[/h1]

DuckovInterop 没有身份验证和传输加密，并且能够执行反射方法调用及 CLR JavaScript。默认的回环地址只能由本机访问；[b]不要将监听地址改为面向公网或不受信任网络的地址[/b]。如确需远程连接，请仅在受信任的局域网或受控 VPN 中使用，并自行添加网络隔离和访问控制。

[h1]反馈与源码[/h1]

遇到问题时，请在评论区留言，或到 [url=https://github.com/SlimeNull/DuckovMods]GitHub 仓库[/url]提交 Issue。
