# Duckov Core Utilities

Quality-of-life utilities for *Escape from Duckov*, focused on inventory handling, item information, loot visibility, combat feedback, and HUD readability.

## Features

- **Item price display**: adds item price information to item UI.
- **Storage count display**: shows how many matching items are already carried or stored.
- **Quality display**: highlights item quality in item UI.
- **Loot outline**: highlights lootboxes and ground items within range.
  - Supports quality-colored outlines.
  - Supports breathing outline effects.
  - Supports separate toggles for lootbox and ground-item outlines.
- **Inventory sort buttons**: adds compact custom sort buttons near the vanilla sort button.
  - Sort by value.
  - Sort by weight.
  - Sort by value-to-weight ratio.
  - Supports player backpack and player storage inventory displays.
- **Auto close backpack**: closes active loot/inventory view when moving or when hurt.
- **Fade HUD when aiming**: fades HUD panels while aiming down sights.
- **Bullet-count crosshair color**: changes crosshair color as magazine ammo gets low.
- **Mute and pause when unfocused**: mutes the master audio bus and opens the pause menu when the game loses focus.
- **Low health inner shadow**: draws a configurable red screen-edge vignette at low health.

## Configuration

Feature defaults are defined in code and can be adjusted from the corresponding feature classes:

- `LootboxOutlineFeature`
  - `ActivationDistance`
  - `EnableLootboxOutline`
  - `EnableGroundItemOutline`
  - `UseQualityColor`
  - `LootboxBreathingEffect`
  - `GroundItemBreathingEffect`
  - `BreathingPeriod`
  - `BreathingMinAlpha`
- `LowHealthInnerShadowFeature`
  - `ShadowColor`
  - `ShadowDistance`
  - `HealthThresholdUpper`
  - `HealthThresholdLower`
- `BulletCountCrosshairColorFeature`
  - `WarnRatio`
- `AutoFadeHudWhenAimingFeature`
  - `TargetAlpha`
  - `SmoothTime`
- `AutoCloseBackpackFeature`
  - `WhenMove`
  - `WhenHurt`
- `DisplayStorageCount`
  - `DisplayItemCountInBackpack`
  - `DisplayItemCountInRepository`

## Build

Requirements:

- .NET SDK compatible with `netstandard2.1`
- Escape from Duckov installed at the path configured in `source/DuckovCoreUtilities/DuckovCoreUtilities.csproj`

Build:

```powershell
dotnet build .\source\DuckovCoreUtilities.slnx
```

The compiled mod assembly is emitted under:

```text
source/DuckovCoreUtilities/bin/Debug/netstandard2.1/
```

## Metadata

Mod metadata lives in:

```text
source/DuckovCoreUtilities/info.ini
```

Current display name:

```text
Core Utilities
```
