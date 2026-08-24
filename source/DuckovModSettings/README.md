# DuckovModSettings

DuckovModSettings discovers settings from other mods without a registration API or assembly reference.

## Discovery contract

For each loaded `Duckov.Modding.ModBehaviour`, the mod inspects `MonoBehaviour` components that:

- are attached to the same `GameObject` as the mod root; and
- are defined in the same assembly as that root.

It exposes public instance fields, public read/write instance properties, and non-public fields marked with `[SerializeField]` or `[SerializeReference]`. Static, constant, read-only, indexed, and `[HideInInspector]` members are ignored. Serializable nested classes and structs become real nested groups in the UI.

Supported leaf values are booleans, strings, characters, integral and floating-point numbers, enums, `KeyCode`, Input System `Key`, `Color`, and `Color32`.

Unity attributes provide the presentation metadata:

- `[InspectorName]` changes the visible label.
- `[Tooltip]` supplies hover text.
- `[Header]` inserts a section heading.
- `[Range]` selects and constrains a slider.
- `[TextArea]` creates a multiline string editor.
- `[FormerlySerializedAs]` adds a key used to import renamed or legacy settings.

Example:

```csharp
[Serializable]
private sealed class NetworkOptions
{
    [InspectorName("端口")]
    [Tooltip("重新启动监听后生效")]
    [Range(1024, 65535)]
    [FormerlySerializedAs("Network.Port")]
    public int Port = 37622;

    [InspectorName("状态颜色")]
    public Color StatusColor = Color.green;
}

[SerializeField, InspectorName("网络")]
private NetworkOptions network = new NetworkOptions();
```

## Change notification

Edits are written to the reflected member immediately and the owning component's parameterless `OnValidate()` method is invoked when present. After the user leaves or closes the settings page, each edited component `GameObject` receives this optional Unity message once:

```csharp
private void DuckovModSettingsUpdated()
{
    // Apply settings that should be committed when the page closes.
}
```

The message is sent with `SendMessageOptions.DontRequireReceiver`. A component can instead poll its fields or properties if that better matches its runtime behavior.

Settings are saved under `Application.persistentDataPath/DuckovModSettings/settings.json`. Writes use a temporary file and backup, and old values from `ModSetting/ModSetting.json` are imported through current paths or `[FormerlySerializedAs]` aliases.
