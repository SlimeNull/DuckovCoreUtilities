using Duckov.UI;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Features.Abstraction;
using System.Collections.Generic;
using System.Linq;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class DisplayStorageCount : ItemInfoDisplayFeature
    {
        private const string PatchCatagory = nameof(DisplayStorageCount);
        private record struct ItemStorageNameAndCount(string StorageName, int ItemCount);

        private readonly List<ItemStorageNameAndCount> _storageCountCache = new List<ItemStorageNameAndCount>();

        public override string Name => "Display storage count";

        /// <summary>
        /// 显示物品在背包中的数量
        /// </summary>
        public bool DisplayItemCountInBackpack { get; set; } = true;

        /// <summary>
        /// 显示物品在仓库中的数量
        /// </summary>
        public bool DisplayItemCountInRepository { get; set; } = true;

        protected override void OnEnable()
        {
            base.OnEnable();

            Context.Harmony.PatchCategory(PatchCatagory);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Context.Harmony.UnpatchCategory(PatchCatagory);
        }

        private int GetItemCountInInventory(Inventory? inventoryToSearch, int itemTypeID)
        {
            if (inventoryToSearch is null)
            {
                return 0;
            }

            int itemTotalCount = 0;
            foreach (var itemInRepository in inventoryToSearch)
            {
                if (itemInRepository == null ||
                    itemInRepository.TypeID != itemTypeID)
                {
                    continue;
                }

                itemTotalCount += itemInRepository.StackCount;
            }

            return itemTotalCount;
        }

        private int GetItemCountInBackpack(Item item)
        {
            return GetItemCountInInventory(LevelManager.Instance?.MainCharacter?.CharacterItem?.Inventory, item.TypeID);
        }

        private int GetItemCountInRepository(Item item)
        {
            if (PlayerStorage.Inventory == null)
            {
                int count = 0;
                foreach (var cacheItem in PlayerStorageDestroyPatch.CachedRepoItemCount)
                {
                    if (cacheItem.Key == item.TypeID)
                    {
                        count += cacheItem.Value;
                    }
                }

                return count;
            }

            return GetItemCountInInventory(PlayerStorage.Inventory, item.TypeID);
        }

        protected override string GetText(ItemHoveringUI uiInstance, Item item)
        {
            _storageCountCache.Clear();

            if (DisplayItemCountInBackpack)
                _storageCountCache.Add(new ItemStorageNameAndCount("Carry", GetItemCountInBackpack(item)));
            if (DisplayItemCountInRepository)
                _storageCountCache.Add(new ItemStorageNameAndCount("Repo", GetItemCountInRepository(item)));

            return string.Join(", ", _storageCountCache.Select(kv => $"{kv.StorageName}: {kv.ItemCount}"));
        }

        [HarmonyPatchCategory(PatchCatagory)]
        [HarmonyPatch(typeof(PlayerStorage), "OnDestroy")]
        private class PlayerStorageDestroyPatch
        {
            public static List<KeyValuePair<int, int>> CachedRepoItemCount { get; } = new List<KeyValuePair<int, int>>();

            private static void Postfix()
            {
                CachedRepoItemCount.Clear();
                foreach (var item in PlayerStorage.Inventory)
                {
                    CachedRepoItemCount.Add(new KeyValuePair<int, int>(item.TypeID, item.StackCount));
                }
            }
        }
    }
}
