using Duckov.UI;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Features.Abstraction;
using SlimeNull.DuckovCoreUtilities.Localization;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class ItemUsageDisplayFeature : ItemInfoDisplayFeature
    {
        public override string Name => "Item usage display";

        protected override bool ShouldDisplay(ItemHoveringUI uiInstance, Item item)
        {
            return item.UseDurability;
        }

        protected override string GetText(ItemHoveringUI uiInstance, Item item)
        {
            var label = SettingsText.ItemUsageAvailable;
            return item.DurabilityLoss > 0.000001f
                ? $"{label}: {(int)item.Durability}/{(int)item.MaxDurability} ({(int)item.MaxDurabilityWithLoss})"
                : $"{label}: {(int)item.Durability}/{(int)item.MaxDurability}";
        }

    }
}
