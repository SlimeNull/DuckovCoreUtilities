Core Utilities is a configurable quality-of-life collection for Escape from Duckov. It brings item information, inventory tools, map assistance, combat feedback, and UI improvements together in one mod. Every feature can be enabled and configured independently.

[h1]Subscription and configuration[/h1]

[olist]
[*]Subscribe to this mod and its required items.
[*]Subscribing to [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3789464208]Duckov Mod Settings[/url] is strongly recommended.
[*]Start the game and select Core Utilities under Options > Mod Settings to configure each feature.
[/olist]

Core Utilities still runs with its defaults when Duckov Mod Settings is absent, but its options cannot be changed in game. All features are enabled by default except the kill feed and minimap.

[h1]Features[/h1]

[h2]Items and economy[/h2]

[list]
[*][b]Item prices[/b]: show either merchant sell price or raw item value in item details.
[*][b]Black market comparison[/b]: display percentage and absolute differences below black-market offers. Supply offers are compared with raw value; demand offers can use either merchant buyback value or raw value as the baseline. The total difference for all remaining transactions is also shown.
[*][b]Inventory counts[/b]: show the number of matching items in the backpack and storage.
[*][b]Item quality[/b]: represent item quality with a border, background, or corner badge, with a configurable color for every quality.
[*][b]Item search sounds[/b]: play a quality-specific sound when an item search finishes. Each quality can select a local WAV, OGG, MP3, or AIFF file and set its own volume; unavailable local files fall back to the corresponding FMOD event.
[*][b]Item uses[/b]: show current and maximum uses for durability-based items, including maximum uses after durability loss when applicable.
[*][b]Recorded key and blueprint indicator[/b]: mark registered keys, keycards, and blueprints with a configurable check indicator.
[*][b]Quest item requirements[/b]: summarize quantities needed by unfinished quests, perks, and buildings; hold [b]Shift[/b] to see each requirement source.
[/list]

[h2]Loot and inventory[/h2]

[list]
[*][b]Loot outlines[/b]: outline visible loot containers and ground items with optional quality colors, pulsing, configurable period, and minimum opacity.
[*][b]Inventory sorting buttons[/b]: add value, weight, and value-to-weight sorting to backpack and storage views while consolidating stackable items.
[*][b]Auto-close inventory[/b]: close backpack and loot views when the player moves or takes damage; both triggers are independently configurable.
[/list]

[h2]Combat and HUD[/h2]

[list]
[*][b]Fade HUD while aiming[/b]: reduce HUD opacity while aiming, with configurable target opacity and transition duration.
[*][b]Ammo crosshair color[/b]: gradually change the crosshair color as the magazine enters a configurable low-ammo range.
[*][b]Low-health screen shadow[/b]: show a configurable colored shadow around the screen as health falls.
[*][b]Kill feed[/b]: show recent kills on the HUD with configurable text, duration, and entry count. Disabled by default.
[*][b]Grenade radius[/b]: show the actual blast radius and fuse progress for thrown explosives, plus remaining duration for smoke zones, with configurable colors and progress options.
[/list]

[h2]Map and time[/h2]

[list]
[*][b]Minimap[/b]: add a rounded minimap below the raid time and storm information. Configure size, opacity, fixed or player-relative orientation, and keyboard zoom controls. Disabled by default.
[*][b]BOSS locations[/b]: mark living bosses on the map with optional names and a configurable color. Static mode preserves each spawn position; dynamic mode updates live positions while the map is open.
[*][b]Quick sleep[/b]: add two configurable time presets plus buttons for rainy weather, Storm I, Storm II, and the end of the storm. A rainy-day search can be cancelled with [b]Esc[/b].
[*][b]Quest favorites[/b]: right-click a quest to favorite it; favorited quests show a star or heart, move to the top, and are stored per save.
[/list]

[h2]Window behavior[/h2]

[list]
[*][b]When the game loses focus[/b]: automatically mute the game and optionally open the pause menu.
[/list]

[h1]Localization[/h1]

Settings labels and the main UI text added by the mod support Simplified Chinese, Traditional Chinese, and English, and refresh when the game language changes.

[h1]Feedback and source[/h1]

If you encounter a problem, leave a comment below or open an Issue in the [url=https://github.com/SlimeNull/DuckovMods]GitHub repository[/url].

The mod is actively maintained, and Pull Requests are welcome.

This module uses GPT 5.6 Sol to assist in development and generate documentation.

[h1]Contributing a translation[/h1]

To add another language:

[olist]
[*]Copy [b]Localization/SettingsText.resx[/b] from the project, or start from an existing translation.
[*]Name the copy [b]SettingsText.<language>.resx[/b], such as [b]ja[/b], [b]ko[/b], or [b]ru[/b].
[*]Translate resource values only. Preserve every key and placeholders such as [b]{0}[/b] and [b]{1}[/b].
[*]Submit the completed file as a Pull Request to the [url=https://github.com/SlimeNull/DuckovMods]GitHub repository[/url].
[/olist]
