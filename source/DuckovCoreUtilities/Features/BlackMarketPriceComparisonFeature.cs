using Duckov.BlackMarkets;
using Duckov.BlackMarkets.UI;
using Duckov.Economy;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class BlackMarketPriceComparisonFeature : FeatureBase
    {
        public enum DemandBaseline
        {
            [InspectorName("@SettingsText/MerchantSellback")]
            MerchantSellback,

            [InspectorName("@SettingsText/ItemBasePrice")]
            ItemBasePrice,
        }

        private const string HarmonyCategory = nameof(BlackMarketPriceComparisonFeature);
        private const string DisplayName = "DCU_BlackMarketPriceComparison";
        private const float DisplayHeight = 20f;
        private static readonly FieldInfo? BatchCountField =
            AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "batchCount");

        private readonly HashSet<PriceComparisonDisplay> _displays = new HashSet<PriceComparisonDisplay>();
        private DemandBaseline _baseline = DemandBaseline.MerchantSellback;
        private StockShop? _stockShop;
        private int _stockShopLookupFrame = -1;

        private static BlackMarketPriceComparisonFeature? Current { get; set; }

        public override string Name => "Black market price comparison";

        public DemandBaseline Baseline
        {
            get => _baseline;
            set
            {
                if (_baseline == value)
                {
                    return;
                }

                _baseline = value;
                RefreshDisplays();
            }
        }

        protected override void OnEnable()
        {
            Current = this;
            LocalizationManager.OnSetLanguage += OnLanguageChanged;
            Context.Harmony.PatchCategory(HarmonyCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            LocalizationManager.OnSetLanguage -= OnLanguageChanged;
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            foreach (var display in _displays)
            {
                if (display != null)
                {
                    UnityEngine.Object.Destroy(display.gameObject);
                }
            }

            _displays.Clear();
            _stockShop = null;
            _stockShopLookupFrame = -1;
        }

        private void OnLanguageChanged(SystemLanguage _)
        {
            RefreshDisplays();
        }

        private void SetupDemand(DemandPanel_Entry entry, BlackMarket.DemandSupplyEntry target)
        {
            if (entry == null || target == null)
            {
                return;
            }

            var anchor = FindPriceText(
                entry.transform,
                "Content/DealButton/Graphics/Cost/Money/MoneyText",
                target.TotalPrice);
            SetupDisplay(anchor, target, isDemand: true);
        }

        private void SetupSupply(SupplyPanel_Entry entry, BlackMarket.DemandSupplyEntry target)
        {
            if (entry == null || target == null)
            {
                return;
            }

            var anchor = FindPriceText(entry.transform, "Content/DealButton/Graphics/Cost/Money/MoneyText", target.TotalPrice);
            SetupDisplay(anchor, target, isDemand: false);
        }

        private void SetupDisplay(
            TextMeshProUGUI? anchor,
            BlackMarket.DemandSupplyEntry target,
            bool isDemand)
        {
            if (anchor == null || target == null)
            {
                return;
            }

            if (!TryGetDisplayLayout(
                anchor,
                out var layoutParent,
                out var priceContainer,
                out var moneyRow))
            {
                return;
            }

            var displayTransform = layoutParent.Find(DisplayName) ??
                priceContainer.Find(DisplayName) ??
                moneyRow.Find(DisplayName) ??
                anchor.transform.Find(DisplayName);
            var display = displayTransform != null
                ? displayTransform.GetComponent<PriceComparisonDisplay>()
                : null;
            if (display == null)
            {
                display = CreateDisplay(anchor);
            }
            else
            {
                ConfigureDisplayLayout(display, anchor, layoutParent, priceContainer);
            }

            display.Target = target;
            display.IsDemand = isDemand;
            _displays.Add(display);
            RefreshDisplay(display);
        }

        private static PriceComparisonDisplay CreateDisplay(TextMeshProUGUI anchor)
        {
            var gameObject = new GameObject(
                DisplayName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement),
                typeof(PriceComparisonDisplay));
            gameObject.layer = anchor.gameObject.layer;

            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = anchor.font;
            text.fontSharedMaterial = anchor.fontSharedMaterial;
            text.fontStyle = anchor.fontStyle;
            text.alignment = anchor.alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = Mathf.Max(12f, anchor.fontSize * 0.72f);

            var display = gameObject.GetComponent<PriceComparisonDisplay>();
            display.Text = text;
            if (TryGetDisplayLayout(
                anchor,
                out var layoutParent,
                out var priceContainer,
                out _))
            {
                ConfigureDisplayLayout(display, anchor, layoutParent, priceContainer);
            }
            return display;
        }

        private static void ConfigureDisplayLayout(
            PriceComparisonDisplay display,
            TextMeshProUGUI anchor,
            Transform layoutParent,
            Transform priceContainer)
        {
            var displayTransform = display.transform;
            if (displayTransform.parent != layoutParent)
            {
                displayTransform.SetParent(layoutParent, false);
            }

            displayTransform.SetSiblingIndex(priceContainer.GetSiblingIndex() + 1);

            var layout = display.GetComponent<LayoutElement>() ?? display.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = DisplayHeight;
            layout.preferredHeight = DisplayHeight;
            layout.flexibleHeight = 0f;
            layout.minWidth = -1f;
            layout.preferredWidth = -1f;
            layout.flexibleWidth = 1f;
            layout.ignoreLayout = false;
            display.Text.alignment = anchor.alignment;

            if (layoutParent is RectTransform layoutParentRect)
            {
                LayoutRebuilder.MarkLayoutForRebuild(layoutParentRect);
            }
        }

        private static bool TryGetDisplayLayout(
            TextMeshProUGUI anchor,
            out Transform layoutParent,
            out Transform priceContainer,
            out Transform moneyRow)
        {
            moneyRow = anchor.transform.parent;
            priceContainer = moneyRow != null ? moneyRow.parent : null!;
            layoutParent = priceContainer != null ? priceContainer.parent : null!;
            return moneyRow != null &&
                priceContainer != null &&
                layoutParent != null &&
                layoutParent.GetComponent<VerticalLayoutGroup>() != null;
        }

        private static TextMeshProUGUI? FindPriceText(Transform root, string path, int expectedPrice)
        {
            var exact = root.Find(path)?.GetComponent<TextMeshProUGUI>();
            if (exact != null)
            {
                return exact;
            }

            var expected = expectedPrice.ToString();
            foreach (var candidate in root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true))
            {
                if (candidate != null && string.Equals(candidate.text, expected, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RefreshDisplays()
        {
            _displays.RemoveWhere(static display => display == null);
            foreach (var display in _displays)
            {
                RefreshDisplay(display);
            }
        }

        private void RefreshDisplay(PriceComparisonDisplay display)
        {
            if (display == null || display.Text == null || display.Target == null)
            {
                return;
            }

            var target = display.Target;
            var marketPrice = target.TotalPrice;
            var basePrice = GetItemBasePrice(target);
            var comparisonPrice = display.IsDemand && Baseline == DemandBaseline.MerchantSellback
                ? GetMerchantSellbackPrice(target.ItemID, basePrice)
                : basePrice;

            display.Text.text = comparisonPrice > 0
                ? BuildComparisonText(
                    marketPrice,
                    comparisonPrice,
                    Mathf.Max(1, target.Remaining),
                    display.IsDemand)
                : string.Empty;
        }

        private static int GetItemBasePrice(BlackMarket.DemandSupplyEntry target)
        {
            var metadata = ItemAssetsCollection.GetMetaData(target.ItemID);
            var batchCount = 1;
            try
            {
                if (BatchCountField?.GetValue(target) is int value)
                {
                    batchCount = Mathf.Max(1, value);
                }
            }
            catch
            {
                batchCount = 1;
            }

            return Mathf.FloorToInt(
                (float)metadata.priceEach * metadata.defaultStackCount * batchCount);
        }

        private int GetMerchantSellbackPrice(int itemTypeId, int basePrice)
        {
            var stockShop = GetStockShop();
            var factor = stockShop != null ? stockShop.sellFactor : 0.5f;
            if (stockShop?.overrideSellingPrice != null)
            {
                var priceOverride = stockShop.overrideSellingPrice.Find(
                    candidate => candidate != null && candidate.typeID == itemTypeId);
                if (priceOverride != null)
                {
                    factor = priceOverride.factor;
                }
            }

            return Mathf.FloorToInt(basePrice * factor);
        }

        private StockShop? GetStockShop()
        {
            if (_stockShop != null)
            {
                return _stockShop;
            }

            if (_stockShopLookupFrame == Time.frameCount)
            {
                return null;
            }

            _stockShopLookupFrame = Time.frameCount;

            var shops = UnityEngine.Object.FindObjectsOfType<StockShop>();
            if (shops.Length > 0)
            {
                _stockShop = Array.Find(shops, static shop => shop != null && shop.isActiveAndEnabled)
                    ?? shops[0];
            }

            return _stockShop;
        }

        private static string BuildComparisonText(
            int marketPrice,
            int comparisonPrice,
            int quantity,
            bool isDemand)
        {
            var difference = marketPrice - comparisonPrice;
            var percentage = Mathf.RoundToInt(((float)marketPrice / comparisonPrice - 1f) * 100f);
            var isChinese = IsChinese(LocalizationManager.CurrentLanguage);
            var perTransaction = FormatDifference(percentage, difference, isDemand, isChinese);
            if (quantity <= 1)
            {
                return perTransaction;
            }

            var totalDifference = difference * quantity;
            var total = FormatAbsoluteDifference(totalDifference, isDemand, isChinese);
            return isChinese
                ? $"{perTransaction} / 总计(×{quantity}): {total}"
                : $"{perTransaction} / Total (x{quantity}): {total}";
        }

        private static string FormatDifference(int percentage, int difference, bool isDemand, bool isChinese)
        {
            if (difference == 0)
            {
                return isChinese
                    ? "<color=#F2D36B>价格持平</color>"
                    : "<color=#F2D36B>Same price</color>";
            }

            var favorable = isDemand ? difference > 0 : difference < 0;
            var color = favorable ? "#72D58A" : "#FF7770";
            var sign = percentage > 0 ? "+" : string.Empty;
            var absolute = FormatAbsoluteDifference(difference, isDemand, isChinese);
            return $"<color={color}>{sign}{percentage}% ({absolute})</color>";
        }

        private static string FormatAbsoluteDifference(int difference, bool isDemand, bool isChinese)
        {
            if (difference == 0)
            {
                return isChinese ? "持平" : "same";
            }

            if (isChinese)
            {
                if (isDemand)
                {
                    return difference > 0 ? $"多卖 {difference}" : $"少卖 {-difference}";
                }

                return difference > 0 ? $"贵 {difference}" : $"便宜 {-difference}";
            }

            if (isDemand)
            {
                return difference > 0 ? $"gain {difference}" : $"lose {-difference}";
            }

            return difference > 0 ? $"costs {difference} more" : $"save {-difference}";
        }

        private static bool IsChinese(SystemLanguage language)
        {
            return language == SystemLanguage.Chinese ||
                language == SystemLanguage.ChineseSimplified ||
                language == SystemLanguage.ChineseTraditional;
        }

        private sealed class PriceComparisonDisplay : MonoBehaviour
        {
            public TextMeshProUGUI Text = null!;
            public BlackMarket.DemandSupplyEntry? Target;
            public bool IsDemand;
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(DemandPanel_Entry), "Setup")]
        private static class DemandSetupPatch
        {
            private static void Postfix(DemandPanel_Entry __instance, BlackMarket.DemandSupplyEntry target)
            {
                try
                {
                    Current?.SetupDemand(__instance, target);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DuckovCoreUtilities] Black-market demand comparison failed: {ex}");
                }
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(SupplyPanel_Entry), "Setup")]
        private static class SupplySetupPatch
        {
            private static void Postfix(SupplyPanel_Entry __instance, BlackMarket.DemandSupplyEntry target)
            {
                try
                {
                    Current?.SetupSupply(__instance, target);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DuckovCoreUtilities] Black-market supply comparison failed: {ex}");
                }
            }
        }
    }
}
