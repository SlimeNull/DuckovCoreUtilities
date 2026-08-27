DuckovInterop hosts a local TCP JSON RPC service inside Escape from Duckov. It allows the hierarchy inspector, MCP bridge, and other development tools to inspect and manipulate the running game state.

This mod is intended primarily for mod development, debugging, and automation rather than regular gameplay enhancement.

[h1]Capabilities[/h1]

[list]
[*]Read loaded scenes and their complete GameObject hierarchies, including inactive objects.
[*]Inspect attached components, Unity-serialized fields, and visible properties.
[*]Search the scene by GameObject name or component type.
[*]Read and modify GameObject active state, component state, fields, and properties.
[*]Read or write nested paths and correctly write back value types, such as [b]transform.position.x[/b].
[*]Invoke instance or static methods and retain returned object references for later requests.
[*]Execute CLR-enabled Jint JavaScript on the Unity main thread for advanced inspection and manipulation.
[/list]

Unity object access is dispatched to the game main thread, while network listening and client connections run on background threads.

[h1]Companion tools[/h1]

The source repository contains two standalone tools:

[list]
[*][b]DockovInterop.HierarchyInspector[/b]: a Windows desktop interface modeled after Unity's Hierarchy and Inspector for browsing scenes, inspecting components, and editing supported values.
[*][b]DuckovInterop.Bridge[/b]: exposes the RPC interface as stdio MCP tools for Codex, Claude, Copilot, and other MCP-capable agents.
[/list]

These tools are not installed with this mod and must be built from the [url=https://github.com/SlimeNull/DuckovMods]GitHub repository[/url].

[h1]Subscription and usage[/h1]

[olist]
[*]Subscribe to this mod and enable it in game.
[*]Start the game. The default RPC endpoint is [b]127.0.0.1:37620[/b].
[*]Start the hierarchy inspector, or configure DuckovInterop.Bridge/your MCP client to connect to that endpoint.
[/olist]

After subscribing to [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3789464208]Duckov Mod Settings[/url], Options > Mods > DuckovInterop exposes:

[list]
[*]RPC service enabled state;
[*]listen address;
[*]listen port (1024 to 65535);
[*]diagnostic logging.
[/list]

Restart the RPC service or the game after changing the listen address or port. Without Duckov Mod Settings, the service runs with its defaults.

[h1]RPC overview[/h1]

The public contract includes scene hierarchies and snapshots, component details, name and type searches, value reads and writes, GameObject activation, method invocation, and Jint evaluation. Shared contracts and data models live in the DuckovInterop.Abstractions project and can be referenced by custom clients.

[h1]Security warning[/h1]

DuckovInterop provides no authentication or transport encryption and exposes reflection-based method calls and CLR JavaScript execution. The default loopback endpoint is local-only. [b]Never bind the service to the public internet or an untrusted network.[/b] For remote development, use only a trusted LAN or controlled VPN and provide your own network isolation and access controls.

[h1]Feedback and source[/h1]

If you encounter a problem, leave a comment below or open an Issue in the [url=https://github.com/SlimeNull/DuckovMods]GitHub repository[/url].

This module uses GPT 5.6 Sol to assist in development and generate documentation.

[h1]Contributing a translation[/h1]

To add another language:

[olist]
[*]Copy [b]Localization/SettingsText.resx[/b] from the project, or start from an existing translation.
[*]Name the copy [b]SettingsText.<language>.resx[/b], such as [b]ja[/b], [b]ko[/b], or [b]ru[/b].
[*]Translate resource values only. Preserve every key and placeholders such as [b]{0}[/b] and [b]{1}[/b].
[*]Submit the completed file as a Pull Request to the [url=https://github.com/SlimeNull/DuckovMods]GitHub repository[/url].
[/olist]
