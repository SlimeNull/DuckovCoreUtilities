using Duckov.UI;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Features.Abstraction;
using System;
using System.ComponentModel;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    [Description("Display item price in item hovering UI.")]
    internal sealed class DisplayPriceFeature : ItemInfoDisplayFeature
    {
        public enum DisplayMode
        {
            [UnityEngine.InspectorName("@SettingsText/PriceSell")]
            SellPrice,
            [UnityEngine.InspectorName("@SettingsText/PriceBase")]
            RawPrice,
        }

        public override string Name => "Display item price";

        public DisplayMode Mode { get; set; } = DisplayMode.SellPrice;

        protected override string GetText(ItemHoveringUI uiInstance, Item item)
        {
            var price = Mode switch
            {
                DisplayMode.SellPrice => item.GetTotalRawValue() / 2,
                DisplayMode.RawPrice => item.GetTotalRawValue(),
                _ => throw new ArgumentOutOfRangeException()
            };

            return $"${price}";
        }
    }
}
