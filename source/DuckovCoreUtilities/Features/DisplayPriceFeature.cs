using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Features.Abstraction;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class DisplayPriceFeature : ItemInfoDisplayFeature
    {
        public enum DisplayMode
        {
            SellPrice,
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
