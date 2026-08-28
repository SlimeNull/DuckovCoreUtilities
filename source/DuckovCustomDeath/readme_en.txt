DuckovCustomDeath

Controls item loss and tomb retention after the player dies.

Death drops:
- Normal drops: use the original game rules without changes.
- Low-quality backpack items only: move only quality 0-2 backpack items into the tomb; equipped and other backpack items are retained.
- Backpack items only: move backpack items into the tomb while retaining equipped items.
- Drop nothing: retain every equipped and backpack item without recording or creating a tomb.
- Drop all: move the backpack and every equipped item into the tomb, including the totem, melee weapon, and items protected by Sticky or DontDropOnDeadInSlot.

Tomb retention:
- Do not retain: process item loss normally but do not save the tomb; the next death also clears existing tomb records.
- Normal: retain only the newest untouched tomb. A second death removes the first record.
- Keep two / Keep three: remove the oldest record on the next death after reaching the selected limit.
- Keep all: later deaths never remove older tomb records.

Install DuckovModSettings to edit these options from the in-game Mods settings page. The mod also works without it and defaults to the original game behavior.

Source: https://github.com/SlimeNull/DuckovMods
