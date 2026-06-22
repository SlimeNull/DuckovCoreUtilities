using Duckov.UI;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
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
            ClearMinWidth(sortButton.gameObject);
            AddSortButton(inventoryDisplay, sortButton, SortMode.Value, "＄", 1);
            AddSortButton(inventoryDisplay, sortButton, SortMode.Weight, "W", 2);
            AddSortButton(inventoryDisplay, sortButton, SortMode.ValuePerWeight, "R", 3);
        }

        private static bool ShouldAddButtons(InventoryDisplay inventoryDisplay)
        {
            var playerInventory = LevelManager.Instance?.MainCharacter?.CharacterItem?.Inventory;
            return inventoryDisplay != null &&
                inventoryDisplay.Target != null &&
                inventoryDisplay.Target == playerInventory &&
                inventoryDisplay.Editable &&
                inventoryDisplay.ShowSortButton;
        }

        private static void ClearMinWidth(GameObject gameObject)
        {
            var layoutElement = gameObject.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minWidth = 0f;
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
            }
        }

        private static void AddSortButton(InventoryDisplay inventoryDisplay, Button sortButton, SortMode sortMode, string label, int offset)
        {
            var buttonName = ButtonPrefix + sortMode;
            var parent = sortButton.transform.parent;
            var existingTransform = parent.Find(buttonName);
            var button = existingTransform == null
                ? Object.Instantiate(sortButton, parent)
                : existingTransform.GetComponent<Button>();

            if (button == null)
            {
                return;
            }

            button.name = buttonName;
            button.transform.SetSiblingIndex(sortButton.transform.GetSiblingIndex() + offset);
            button.gameObject.SetActive(sortButton.gameObject.activeSelf);

            ModifyText(button.gameObject, label);
            ClearMinWidth(button.gameObject);

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

            inventoryDisplay.Target.Sort((left, right) => CompareItems(left, right, sortMode));
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
