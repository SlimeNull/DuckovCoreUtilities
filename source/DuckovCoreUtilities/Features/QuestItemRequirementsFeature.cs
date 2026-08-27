using Duckov.Buildings;
using Duckov.PerkTrees;
using Duckov.Quests;
using Duckov.Quests.Tasks;
using Duckov.UI;
using Duckov.Utilities;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class QuestItemRequirementsFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(QuestItemRequirementsFeature);
        private static readonly HashSet<string> ExcludedPerkTrees = new HashSet<string>(StringComparer.Ordinal)
        {
            "Blueprint",
            "PerkTree_Farming"
        };
        private static readonly Dictionary<int, int> CachedStorageCounts = new Dictionary<int, int>();
        private static readonly FieldInfo SubmitRequiredAmountField = AccessTools.Field(typeof(SubmitItems), "requiredAmount");
        private static readonly FieldInfo SubmitCurrentAmountField = AccessTools.Field(typeof(SubmitItems), "submittedAmount");
        private static readonly FieldInfo UseItemTypeField = AccessTools.Field(typeof(QuestTask_UseItem), "itemTypeID");
        private static readonly FieldInfo UseRequiredAmountField = AccessTools.Field(typeof(QuestTask_UseItem), "requireAmount");
        private static readonly FieldInfo UseCurrentAmountField = AccessTools.Field(typeof(QuestTask_UseItem), "amount");

        private TextMeshProUGUI? _text;
        private ItemHoveringUI? _currentUi;
        private Item? _currentItem;
        private bool _detailsShown;

        public override string Name => "Quest item requirements";

        public bool ShowQuestRequirements { get; set; } = true;
        public bool ShowPerkRequirements { get; set; } = true;
        public bool ShowBuildingRequirements { get; set; } = true;

        public void RefreshCurrentDisplay()
        {
            if (_currentUi != null && _currentItem != null)
            {
                UpdateDisplay(_currentUi, _currentItem, _detailsShown);
            }
        }

        protected override void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItem;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
            Context.Harmony.PatchCategory(HarmonyCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            ItemHoveringUI.onSetupItem -= OnSetupItem;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
            _currentUi = null;
            _currentItem = null;

            if (_text != null)
            {
                UnityEngine.Object.Destroy(_text.gameObject);
                _text = null;
            }
        }

        public override void Tick()
        {
            if (_currentUi == null || _currentItem == null || _text == null || !_text.gameObject.activeSelf)
            {
                return;
            }

            var details = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (details != _detailsShown)
            {
                UpdateDisplay(_currentUi, _currentItem, details);
            }
        }

        public override void RefreshLocalization()
        {
            if (_currentUi != null && _currentItem != null)
            {
                UpdateDisplay(_currentUi, _currentItem, _detailsShown);
            }
        }

        private void OnSetupMeta(ItemHoveringUI _, ItemMetaData __)
        {
            _currentUi = null;
            _currentItem = null;
            SetVisible(false);
        }

        private void OnSetupItem(ItemHoveringUI ui, Item item)
        {
            _currentUi = ui;
            _currentItem = item;
            if (item == null)
            {
                SetVisible(false);
                return;
            }

            var details = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            UpdateDisplay(ui, item, details);
        }

        private void UpdateDisplay(ItemHoveringUI ui, Item item, bool showDetails)
        {
            _detailsShown = showDetails;
            var groups = CollectRequirements(item.TypeID);
            var required = groups.Sum(static group => group.Lines.Sum(static line => line.Amount));
            if (required <= 0)
            {
                SetVisible(false);
                return;
            }

            EnsureText(ui);
            var available = GetTotalItemAmount(item.TypeID);
            var color = available >= required ? "#58C779" : "#E06767";
            var builder = new StringBuilder();
            builder.Append(Localize("RequirementsTotal"));
            builder.Append(" <color=").Append(color).Append('>').Append(available).Append("</color> / ").Append(required);

            if (showDetails)
            {
                foreach (var group in groups)
                {
                    if (group.Lines.Count == 0)
                    {
                        continue;
                    }

                    builder.Append('\n').Append(Localize(group.LabelKey));
                    foreach (var line in group.Lines)
                    {
                        builder.Append("\n\t").Append(line.Amount).Append("  -  ").Append(line.Name);
                    }
                }
            }
            else
            {
                builder.Append("\n\t<color=#E4C86A><size=17>----- ")
                    .Append(Localize("RequirementsPressShift"))
                    .Append(" -----</size></color>");
            }

            _text!.text = builder.ToString();
            _text.gameObject.SetActive(true);
        }

        private List<RequirementGroup> CollectRequirements(int itemTypeId)
        {
            var result = new List<RequirementGroup>();
            if (ShowQuestRequirements)
            {
                result.Add(CollectDirectQuestRequirements(itemTypeId));
                result.Add(CollectSubmitQuestRequirements(itemTypeId));
                result.Add(CollectUseQuestRequirements(itemTypeId));
            }
            if (ShowPerkRequirements)
            {
                result.Add(CollectPerkRequirements(itemTypeId));
            }
            if (ShowBuildingRequirements)
            {
                result.Add(CollectBuildingRequirements(itemTypeId));
            }
            return result;
        }

        private static RequirementGroup CollectDirectQuestRequirements(int itemTypeId)
        {
            var group = new RequirementGroup("RequirementsQuestPrepare");
            foreach (var quest in GetUnfinishedQuests())
            {
                if (quest.RequiredItemID == itemTypeId && quest.RequiredItemCount > 0)
                {
                    group.Lines.Add(new RequirementLine(quest.RequiredItemCount, quest.DisplayName));
                }
            }
            return group;
        }

        private static RequirementGroup CollectSubmitQuestRequirements(int itemTypeId)
        {
            var group = new RequirementGroup("RequirementsQuestSubmit");
            foreach (var quest in GetUnfinishedQuests())
            {
                foreach (var task in quest.Tasks.OfType<SubmitItems>())
                {
                    if (task.ItemTypeID != itemTypeId || task.IsFinished())
                    {
                        continue;
                    }

                    var required = (int)SubmitRequiredAmountField.GetValue(task);
                    var submitted = (int)SubmitCurrentAmountField.GetValue(task);
                    var remaining = Mathf.Max(0, required - submitted);
                    if (remaining > 0)
                    {
                        group.Lines.Add(new RequirementLine(remaining, quest.DisplayName));
                    }
                }
            }
            return group;
        }

        private static RequirementGroup CollectUseQuestRequirements(int itemTypeId)
        {
            var group = new RequirementGroup("RequirementsQuestUse");
            foreach (var quest in GetUnfinishedQuests())
            {
                foreach (var task in quest.Tasks.OfType<QuestTask_UseItem>())
                {
                    if (task.IsFinished() || (int)UseItemTypeField.GetValue(task) != itemTypeId)
                    {
                        continue;
                    }

                    var required = (int)UseRequiredAmountField.GetValue(task);
                    var current = (int)UseCurrentAmountField.GetValue(task);
                    var remaining = Mathf.Max(0, required - current);
                    if (remaining > 0)
                    {
                        group.Lines.Add(new RequirementLine(remaining, quest.DisplayName));
                    }
                }
            }
            return group;
        }

        private static RequirementGroup CollectPerkRequirements(int itemTypeId)
        {
            var group = new RequirementGroup("RequirementsPerk");
            var manager = PerkTreeManager.Instance;
            if (manager == null || manager.perkTrees == null)
            {
                return group;
            }

            foreach (var tree in manager.perkTrees.Where(tree => tree != null && !ExcludedPerkTrees.Contains(tree.ID)))
            {
                foreach (var perk in tree.Perks.Where(static perk => perk != null && !perk.Unlocked && perk.Requirement?.cost.items != null))
                {
                    foreach (var cost in perk.Requirement.cost.items.Where(cost => cost.id == itemTypeId && cost.amount > 0))
                    {
                        group.Lines.Add(new RequirementLine(cost.amount, $"{tree.DisplayName}/{perk.DisplayName}"));
                    }
                }
            }
            return group;
        }

        private static RequirementGroup CollectBuildingRequirements(int itemTypeId)
        {
            var group = new RequirementGroup("RequirementsBuilding");
            var collection = BuildingDataCollection.Instance;
            if (collection == null)
            {
                return group;
            }

            foreach (var building in collection.Infos)
            {
                if (!building.Valid || building.CurrentAmount != 0 ||
                    IsTestingName(building.DisplayName) ||
                    (building.requireBuildings != null && building.requireBuildings.Contains("PetHouse")) ||
                    building.cost.items == null)
                {
                    continue;
                }

                foreach (var cost in building.cost.items.Where(cost => cost.id == itemTypeId && cost.amount > 0))
                {
                    group.Lines.Add(new RequirementLine(cost.amount, building.DisplayName));
                }
            }
            return group;
        }

        private static IEnumerable<Quest> GetUnfinishedQuests()
        {
            var manager = QuestManager.Instance;
            if (manager == null || GameplayDataSettings.QuestCollection == null)
            {
                return Enumerable.Empty<Quest>();
            }

            var historyIds = new HashSet<int>(manager.HistoryQuests.Where(static quest => quest != null).Select(static quest => quest.ID));
            var activeById = manager.ActiveQuests.Where(static quest => quest != null).ToDictionary(static quest => quest.ID);
            return GameplayDataSettings.QuestCollection
                .Where(quest => quest != null && !historyIds.Contains(quest.ID) && quest.RequireLevel < 999 && !IsTestingName(quest.DisplayName))
                .Select(quest => activeById.TryGetValue(quest.ID, out var active) ? active : quest)
                .ToList();
        }

        private static bool IsTestingName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.StartsWith("*", StringComparison.Ordinal) && name.EndsWith("*", StringComparison.Ordinal);
        }

        private void EnsureText(ItemHoveringUI ui)
        {
            if (_text == null)
            {
                _text = UnityEngine.Object.Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
            }

            _text.transform.SetParent(ui.LayoutParent, false);
            _text.transform.localScale = Vector3.one;
            _text.fontSize = 20f;
        }

        private void SetVisible(bool visible)
        {
            if (_text != null)
            {
                _text.gameObject.SetActive(visible);
            }
        }

        private static string Localize(string key)
        {
            return SettingsText.ResourceManager.GetString(key, SettingsText.Culture) ?? key;
        }

        private static void CacheStorageCounts()
        {
            CachedStorageCounts.Clear();
            CountInventory(PlayerStorage.Inventory, CachedStorageCounts);
        }

        private static long GetTotalItemAmount(int typeId)
        {
            var counts = new Dictionary<int, int>();
            var level = LevelManager.Instance;
            CountInventory(level?.MainCharacter?.CharacterItem?.Inventory, counts);
            CountSlots(level?.MainCharacter?.CharacterItem?.Slots, counts);
            CountInventory(level?.PetProxy?.Inventory, counts);

            if (PlayerStorage.Inventory != null)
            {
                CountInventory(PlayerStorage.Inventory, counts);
            }
            else
            {
                foreach (var pair in CachedStorageCounts)
                {
                    counts[pair.Key] = counts.TryGetValue(pair.Key, out var current) ? current + pair.Value : pair.Value;
                }
            }

            return counts.TryGetValue(typeId, out var count) ? count : 0;
        }

        private static void CountInventory(Inventory? inventory, Dictionary<int, int> counts)
        {
            if (inventory?.Content == null)
            {
                return;
            }
            CountItems(inventory.Content, counts);
        }

        private static void CountSlots(SlotCollection? slots, Dictionary<int, int> counts)
        {
            if (slots == null)
            {
                return;
            }

            var items = new List<Item>();
            foreach (var slot in slots)
            {
                if (slot?.Content != null)
                {
                    items.Add(slot.Content);
                }
            }
            CountItems(items, counts);
        }

        private static void CountItems(IEnumerable<Item> items, Dictionary<int, int> counts)
        {
            var stack = new Stack<Item>(items.Where(static item => item != null));
            while (stack.Count > 0)
            {
                var item = stack.Pop();
                counts[item.TypeID] = counts.TryGetValue(item.TypeID, out var current) ? current + item.StackCount : item.StackCount;
                if (item.Slots == null)
                {
                    continue;
                }

                foreach (var slot in item.Slots)
                {
                    if (slot?.Content != null)
                    {
                        stack.Push(slot.Content);
                    }
                }
            }
        }

        private sealed class RequirementGroup
        {
            public RequirementGroup(string labelKey)
            {
                LabelKey = labelKey;
            }

            public string LabelKey { get; }
            public List<RequirementLine> Lines { get; } = new List<RequirementLine>();
        }

        private readonly struct RequirementLine
        {
            public RequirementLine(long amount, string name)
            {
                Amount = amount;
                Name = name;
            }

            public long Amount { get; }
            public string Name { get; }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(PlayerStorage), "OnDestroy")]
        private static class PlayerStorageDestroyPatch
        {
            private static void Prefix()
            {
                CacheStorageCounts();
            }
        }
    }
}
