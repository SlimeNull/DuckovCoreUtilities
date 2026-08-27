Duckov Mod Settings adds a unified in-game settings page for Escape from Duckov mods. It discovers settings directly from other mods' MonoBehaviour components, so mod authors do not need to reference this project, register settings, or depend on a dedicated API.

[h1]Player features[/h1]

[list]
[*]Adds a Mods tab to both the main-menu and in-game options panels.
[*]Displays discovered mods with their name and icon.
[*]Preserves the real object structure as collapsible nested setting groups.
[*]Searches setting names, tooltips, and current values.
[*]Supports toggles, numeric inputs, range sliders, strings, characters, enums, keys, and colors.
[*]System.IO.FileInfo settings can select files through the Windows or macOS system dialog, with optional file filters.
[*]Configures the initial group state: outermost groups only, all collapsed, or all expanded.
[*]Provides RGBA fields and HEX input in the color picker.
[*]Provides a Restore Defaults button for resetting every option in the selected mod.
[*]Persists values and restores them the next time each mod loads.
[*]Scans only while the Mods settings page is open and processes mods incrementally, with visible loading progress.
[/list]

Settings are stored at:

[code]Application.persistentDataPath/DuckovModSettings/settings.json[/code]

Writes use a temporary file and .bak backup.

[h1]Subscription[/h1]

Subscribe to this mod and its required items, then restart the game. No manual changes to other mods are required.

Only mods matching the discovery contract below appear on the page. A mod with no exposed configurable members is omitted.

[h1]Integration for mod authors[/h1]

For every loaded Duckov.Modding.ModBehaviour, Duckov Mod Settings scans MonoBehaviour components that:

[list]
[*]are attached to the same GameObject as the mod root; and
[*]are defined in the same assembly as the mod root.
[/list]

The following instance members become settings:

[list]
[*]public fields;
[*]public read/write properties;
[*]non-public fields marked with [b]SerializeField[/b] or [b]SerializeReference[/b].
[/list]

Static, constant, read-only, indexed, and [b]HideInInspector[/b] members are ignored. Nested classes and structs marked [b]Serializable[/b] become real nested groups. Arrays, collections, delegates, and UnityEngine.Object references are not currently exposed as editable settings.

[h2]Supported value types[/h2]

[list]
[*]bool
[*]string and char
[*]System.IO.FileInfo
[*]all integral types
[*]float, double, and decimal
[*]enums
[*]UnityEngine.KeyCode
[*]UnityEngine.InputSystem.Key
[*]UnityEngine.Color and Color32
[*]nullable forms of the value types above
[/list]

[h2]Supported attributes[/h2]

[list]
[*][b]InspectorName[/b]: visible label.
[*][b]Tooltip[/b]: hover text.
[*][b]Header[/b]: section heading.
[*][b]Range[/b]: constrained slider.
[*][b]TextArea[/b]: multiline string editor.
[*][b]System.ComponentModel.Description[/b]: provides an optional filter for a FileInfo editor in [b]Label|pattern[/b] form, such as [b]WAV File|*.wav[/b]. Separate multiple patterns with semicolons or append more label-pattern pairs. A FileInfo member displays the Open button even without this attribute and then uses an All Files filter.
[*][b]HideInInspector[/b]: excludes a member from the page.
[/list]

[h2]Minimal example[/h2]

[code][Serializable]
private sealed class NetworkOptions
{
    [InspectorName("Port")]
    [Tooltip("Applied after restarting the service")]
    [Range(1024, 65535)]
    public int Port = 37622;

    [InspectorName("Status color")]
    public Color StatusColor = Color.green;

    [InspectorName("Alert sound")]
    [Description("WAV File|*.wav")]
    public FileInfo? AlertSound;
}

[SerializeField, InspectorName("Network")]
private NetworkOptions network = new NetworkOptions();[/code]

[h1]Applying changes and notifications[/h1]

Edited members are updated immediately. A parameterless OnValidate() method is invoked when present. After the user leaves or closes the settings page, every edited component's GameObject receives one optional message:

[code]private void DuckovModSettingsUpdated()
{
    // Commit changes that should be applied when the settings page closes.
}[/code]

The message uses SendMessageOptions.DontRequireReceiver, so implementing this method is optional.

[h1]Localizing setting text[/h1]

[b]InspectorName[/b], [b]Header[/b], [b]Tooltip[/b], and enum-value [b]InspectorName[/b] text may reference resources from the settings assembly:

[list]
[*][b]@TextKey[/b]: read the key from the first ResourceManager discovered in the assembly.
[*][b]@ResourceType/TextKey[/b]: find the resource type by simple or fully qualified name, then construct or obtain its ResourceManager.
[/list]

Example:

[code][InspectorName("@SettingsText/ListenPort")]
public int Port = 37620;[/code]

When the game language changes, Duckov Mod Settings synchronizes the resource type's Culture or ResourceCulture and refreshes open pages. If a resource type or key cannot be resolved, the original @... expression remains visible for diagnostics.

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
