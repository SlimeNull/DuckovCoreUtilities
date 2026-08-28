using ItemStatsSystem;
using ItemStatsSystem.Items;
using SlimeNull.DuckovCustomDeath.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeNull.DuckovCustomDeath.Gameplay
{
    internal static class DeathInventoryController
    {
        private sealed class InventoryEntry
        {
            public InventoryEntry(Item item, int position)
            {
                Item = item;
                Position = position;
            }

            public Item Item { get; }

            public int Position { get; }
        }

        private sealed class SlotEntry
        {
            public SlotEntry(Slot slot, Item item)
            {
                Slot = slot;
                Item = item;
            }

            public Slot Slot { get; }

            public Item Item { get; }
        }

        private sealed class PendingDeath
        {
            public PendingDeath(
                CharacterMainControl character,
                List<InventoryEntry> inventoryEntries,
                List<SlotEntry> slotEntries)
            {
                Character = character;
                InventoryEntries = inventoryEntries;
                SlotEntries = slotEntries;
            }

            public CharacterMainControl Character { get; }

            public List<InventoryEntry> InventoryEntries { get; }

            public List<SlotEntry> SlotEntries { get; }

            public bool DeathItemsProcessed { get; set; }
        }

        private static PendingDeath? _pending;

        public static void Prepare(CharacterMainControl? character)
        {
            RestorePending();

            var mode = CustomDeathOptions.DropMode;
            if (mode == DeathDropMode.Normal || character == null || character.CharacterItem == null)
            {
                return;
            }

            var characterItem = character.CharacterItem;
            var retainedInventory = new List<InventoryEntry>();
            var retainedSlots = new List<SlotEntry>();
            var inventory = characterItem.Inventory;

            if (inventory != null)
            {
                var lastPosition = inventory.GetLastItemPosition();
                for (var position = 0; position <= lastPosition; position++)
                {
                    var item = inventory.GetItemAt(position);
                    if (item != null && !DeathDropPolicy.ShouldDropBackpackItem(mode, item.Quality))
                    {
                        retainedInventory.Add(new InventoryEntry(item, position));
                    }
                }
            }

            if (characterItem.Slots != null)
            {
                foreach (var slot in characterItem.Slots)
                {
                    if (slot?.Content != null)
                    {
                        retainedSlots.Add(new SlotEntry(slot, slot.Content));
                    }
                }
            }

            _pending = new PendingDeath(character, retainedInventory, retainedSlots);
            try
            {
                // Remove inventory contents before equipment because backpacks can change capacity.
                foreach (var entry in retainedInventory)
                {
                    entry.Item.Detach();
                }

                foreach (var entry in retainedSlots)
                {
                    entry.Item.Detach();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DuckovCustomDeath] 暂存死亡物品失败，已回退到原版掉落: {ex}");
                RestorePending();
            }
        }

        public static void RestoreFor(CharacterMainControl? character)
        {
            if (_pending == null || character == null || _pending.Character != character)
            {
                return;
            }

            RestorePending();
        }

        public static void MarkDeathItemsProcessed(CharacterMainControl? character)
        {
            if (_pending != null && character != null && _pending.Character == character)
            {
                _pending.DeathItemsProcessed = true;
            }
        }

        public static void RestoreBeforeSave(CharacterMainControl? character)
        {
            if (_pending == null ||
                character == null ||
                _pending.Character != character ||
                !_pending.DeathItemsProcessed)
            {
                return;
            }

            RestorePending();
        }

        public static void RestorePending()
        {
            var pending = _pending;
            if (pending == null)
            {
                return;
            }

            _pending = null;
            var character = pending.Character;
            if (character == null || character.CharacterItem == null)
            {
                Debug.LogError("[DuckovCustomDeath] 无法恢复死亡时暂存的物品：角色物品容器不存在。");
                return;
            }

            var characterItem = character.CharacterItem;
            // Restore equipment first so inventory-capacity modifiers are active again.
            foreach (var entry in pending.SlotEntries)
            {
                if (entry.Item == null)
                {
                    continue;
                }

                if (entry.Slot.Content == entry.Item)
                {
                    continue;
                }

                if (entry.Slot.Content == null && entry.Slot.Plug(entry.Item, out var displacedItem))
                {
                    if (displacedItem != null)
                    {
                        RestoreToInventory(character, characterItem.Inventory, displacedItem, 0);
                    }
                    continue;
                }

                RestoreToInventory(character, characterItem.Inventory, entry.Item, 0);
            }

            foreach (var entry in pending.InventoryEntries)
            {
                if (entry.Item == null)
                {
                    continue;
                }

                var inventory = characterItem.Inventory;
                if (inventory != null &&
                    entry.Position >= 0 &&
                    entry.Position < inventory.Capacity &&
                    inventory.GetItemAt(entry.Position) == null &&
                    inventory.AddAt(entry.Item, entry.Position))
                {
                    continue;
                }

                RestoreToInventory(character, inventory, entry.Item, entry.Position);
            }
        }

        private static void RestoreToInventory(
            CharacterMainControl character,
            Inventory? inventory,
            Item item,
            int preferredPosition)
        {
            if (inventory != null && inventory.AddAndMerge(item, preferredPosition))
            {
                return;
            }

            Debug.LogError($"[DuckovCustomDeath] 无法将暂存物品 {item.DisplayName} 放回背包，已放置到角色脚下。");
            item.Drop(character.transform.position, createRigidbody: true, Vector3.forward, 360f);
        }
    }
}
