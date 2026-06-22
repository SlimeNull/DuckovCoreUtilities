using Duckov.UI;
using Duckov.Utilities;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class InventorySortButtonsFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(InventorySortButtonsFeature);
        private const string ButtonPrefix = "DCU_InventorySort_";
        private const float CompactButtonWidth = 40f;
        private static readonly FieldInfo OnInventorySortedField = AccessTools.Field(typeof(Inventory), "onInventorySorted");

        public override string Name => "Inventory sort buttons";

        protected override void OnEnable()
        {
            Context.Harmony.PatchCategory(HarmonyCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
        }

        private static void EnsureButtons(InventoryDisplay inventoryDisplay)
        {
            if (!ShouldAddButtons(inventoryDisplay))
            {
                return;
            }

            var sortButton = AccessTools.Field(typeof(InventoryDisplay), "sortButton")?.GetValue(inventoryDisplay) as Button;
            if (sortButton == null ||
                sortButton.transform.parent == null)
            {
                return;
            }

            ModifyText(sortButton.gameObject, "↕");
            SetCompactButtonLayout(sortButton.gameObject);
            AddSortButton(inventoryDisplay, sortButton, SortMode.Value, "＄", 1);
            AddSortButton(inventoryDisplay, sortButton, SortMode.Weight, "W", 2);
            AddSortButton(inventoryDisplay, sortButton, SortMode.ValuePerWeight, "R", 3);
        }

        private static bool ShouldAddButtons(InventoryDisplay inventoryDisplay)
        {
            var playerInventory = LevelManager.Instance?.MainCharacter?.CharacterItem?.Inventory;
            var storageInventory = PlayerStorage.Inventory;
            return inventoryDisplay != null &&
                inventoryDisplay.Target != null &&
                inventoryDisplay.Editable &&
                (inventoryDisplay.ShowSortButton ||
                    inventoryDisplay.Target == playerInventory ||
                    inventoryDisplay.Target == storageInventory);
        }

        private static void SetCompactButtonLayout(GameObject gameObject)
        {
            var contentSizeFitter = gameObject.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter != null)
            {
                contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            var layoutElement = gameObject.GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = false;
                layoutElement.minWidth = CompactButtonWidth;
                layoutElement.preferredWidth = CompactButtonWidth;
                layoutElement.flexibleWidth = 0f;
            }

            if (gameObject.transform is RectTransform rectTransform)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CompactButtonWidth);
                rectTransform.sizeDelta = new Vector2(CompactButtonWidth, rectTransform.sizeDelta.y);
            }
        }

        private static void ModifyText(GameObject gameObject, string text)
        {
            var labelText = gameObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (labelText != null)
            {
                labelText.text = text;
                labelText.enableAutoSizing = true;
                labelText.fontSizeMin = 12f;
                labelText.fontSizeMax = Mathf.Max(labelText.fontSize, 12f);
                labelText.enableWordWrapping = false;
                labelText.overflowMode = TextOverflowModes.Overflow;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.margin = Vector4.zero;

                var labelContentSizeFitter = labelText.GetComponent<ContentSizeFitter>();
                if (labelContentSizeFitter != null)
                {
                    labelContentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                }

                var labelLayoutElement = labelText.GetComponent<LayoutElement>();
                if (labelLayoutElement != null)
                {
                    labelLayoutElement.minWidth = 0f;
                    labelLayoutElement.preferredWidth = CompactButtonWidth;
                    labelLayoutElement.flexibleWidth = 0f;
                }
            }
        }

        private static void AddSortButton(InventoryDisplay inventoryDisplay, Button sortButton, SortMode sortMode, string label, int offset)
        {
            var buttonName = ButtonPrefix + sortMode;
            var parent = sortButton.transform.parent;
            var existingTransform = parent.Find(buttonName);
            var button = existingTransform == null
                ? UnityEngine.Object.Instantiate(sortButton, parent)
                : existingTransform.GetComponent<Button>();

            if (button == null)
            {
                return;
            }

            button.name = buttonName;
            button.transform.SetSiblingIndex(sortButton.transform.GetSiblingIndex() + offset);
            button.gameObject.SetActive(sortButton.gameObject.activeSelf);

            ModifyText(button.gameObject, label);
            SetCompactButtonLayout(button.gameObject);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Sort(inventoryDisplay, sortMode));
        }

        private static void Sort(InventoryDisplay inventoryDisplay, SortMode sortMode)
        {
            if (inventoryDisplay == null ||
                !inventoryDisplay.Editable ||
                inventoryDisplay.Target == null ||
                inventoryDisplay.Target.Loading)
            {
                return;
            }

            SortInventory(inventoryDisplay.Target, (left, right) => CompareItems(left, right, sortMode));
        }

        private static void SortInventory(Inventory inventory, Comparison<Item> comparison)
        {
            if (inventory.Loading)
            {
                return;
            }

            inventory.Loading = true;

            var items = new List<Item>();
            for (var i = 0; i < inventory.Content.Count; i++)
            {
                if (inventory.IsIndexLocked(i))
                {
                    continue;
                }

                var item = inventory.Content[i];
                if (item == null)
                {
                    continue;
                }

                item.Detach();
                items.Add(item);
            }

            var sortedItems = new List<Item>();
            foreach (var itemsOfSameType in items.Where(static item => item != null).GroupBy(static item => item.TypeID))
            {
                if (itemsOfSameType.First().Stackable &&
                    TryMerge(itemsOfSameType, out var mergedItems))
                {
                    sortedItems.AddRange(mergedItems);
                }
                else
                {
                    sortedItems.AddRange(itemsOfSameType);
                }
            }

            sortedItems.Sort(comparison);

            foreach (var item in sortedItems)
            {
                inventory.AddItem(item);
            }

            inventory.Loading = false;
            InvokeInventorySorted(inventory);
        }

        private static bool TryMerge(IEnumerable<Item> itemsOfSameTypeID, out List<Item> result)
        {
            result = null!;

            var items = itemsOfSameTypeID.Where(static item => item != null).ToList();
            if (items.Count <= 0)
            {
                return false;
            }

            var typeID = items[0].TypeID;
            foreach (var item in items)
            {
                if (typeID != item.TypeID)
                {
                    Debug.LogError("尝试融合的Item具有不同的TypeID,已取消");
                    return false;
                }
            }

            if (!items[0].Stackable)
            {
                Debug.LogError("此类物品不可堆叠，已取消");
                return false;
            }

            result = new List<Item>();
            var stack = new Stack<Item>(items);
            Item? currentItem = null;

            while (stack.Count > 0)
            {
                currentItem ??= stack.Pop();

                if (stack.Count <= 0)
                {
                    result.Add(currentItem);
                    break;
                }

                currentItem.Detach();
                Item? incomingItem = null;
                while (currentItem.StackCount < currentItem.MaxStackCount && stack.Count > 0)
                {
                    incomingItem = stack.Pop();
                    incomingItem.Detach();
                    currentItem.Combine(incomingItem);
                }

                result.Add(currentItem);
                if (incomingItem != null && incomingItem.StackCount > 0)
                {
                    if (stack.Count <= 0)
                    {
                        result.Add(incomingItem);
                        break;
                    }

                    currentItem = incomingItem;
                }
                else
                {
                    currentItem = null;
                }
            }

            return true;
        }

        private static void InvokeInventorySorted(Inventory inventory)
        {
            var handler = OnInventorySortedField?.GetValue(inventory) as Action<Inventory>;
            handler?.Invoke(inventory);
        }

        private static int CompareItems(Item? left, Item? right, SortMode sortMode)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var result = sortMode switch
            {
                SortMode.Value => right.GetTotalRawValue().CompareTo(left.GetTotalRawValue()),
                SortMode.Weight => right.TotalWeight.CompareTo(left.TotalWeight),
                SortMode.ValuePerWeight => GetValuePerWeight(right).CompareTo(GetValuePerWeight(left)),
                _ => 0
            };

            if (result != 0)
            {
                return result;
            }

            result = right.Quality.CompareTo(left.Quality);
            if (result != 0)
            {
                return result;
            }

            return string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.CurrentCulture);
        }

        private static float GetValuePerWeight(Item item)
        {
            var weight = item.TotalWeight;
            if (weight <= 0f)
            {
                return item.GetTotalRawValue() > 0 ? float.PositiveInfinity : 0f;
            }

            return item.GetTotalRawValue() / weight;
        }

        private enum SortMode
        {
            Value,
            Weight,
            ValuePerWeight
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(InventoryDisplay), "Setup")]
        private static class InventoryDisplaySetupPatch
        {
            private static void Postfix(InventoryDisplay __instance)
            {
                EnsureButtons(__instance);
            }
        }
    }
}
