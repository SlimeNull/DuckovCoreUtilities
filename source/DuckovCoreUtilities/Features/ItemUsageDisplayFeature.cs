using Duckov.UI;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Features.Abstraction;
using SodaCraft.Localizations;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class ItemUsageDisplayFeature : ItemInfoDisplayFeature
    {
        private const string UsageLocalizationKey = "SlimeNull.DuckovCoreUtilities.ItemUsage.Available";

        public override string Name => "Item usage display";

        protected override void OnEnable()
        {
            base.OnEnable();

            LocalizationManager.OnSetLanguage += UpdateLocalization;
            UpdateLocalization(LocalizationManager.CurrentLanguage);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            LocalizationManager.OnSetLanguage -= UpdateLocalization;
            LocalizationManager.RemoveOverrideText(UsageLocalizationKey);
        }

        protected override bool ShouldDisplay(ItemHoveringUI uiInstance, Item item)
        {
            return item.UseDurability;
        }

        protected override string GetText(ItemHoveringUI uiInstance, Item item)
        {
            var label = LocalizationManager.GetPlainText(UsageLocalizationKey);
            return item.DurabilityLoss > 0.000001f
                ? $"{label}: {(int)item.Durability}/{(int)item.MaxDurability} ({(int)item.MaxDurabilityWithLoss})"
                : $"{label}: {(int)item.Durability}/{(int)item.MaxDurability}";
        }

        private static void UpdateLocalization(SystemLanguage language)
        {
            var chinese = language == SystemLanguage.Chinese ||
                language == SystemLanguage.ChineseSimplified ||
                language == SystemLanguage.ChineseTraditional;
            LocalizationManager.SetOverrideText(UsageLocalizationKey, chinese ? "可用" : "Uses");
        }
    }
}
