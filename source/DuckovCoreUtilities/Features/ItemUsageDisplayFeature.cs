using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class ItemUsageDisplayFeature : FeatureBase
    {
        private const string UsageLocalizationKey = "SlimeNull.DuckovCoreUtilities.ItemUsage.Available";
        private TextMeshProUGUI? _text;

        public override string Name => "Item usage display";

        protected override void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItem;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
            LocalizationManager.OnSetLanguage += UpdateLocalization;
            UpdateLocalization(LocalizationManager.CurrentLanguage);
        }

        protected override void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItem;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
            LocalizationManager.OnSetLanguage -= UpdateLocalization;
            LocalizationManager.RemoveOverrideText(UsageLocalizationKey);
            if (_text != null)
            {
                UnityEngine.Object.Destroy(_text.gameObject);
                _text = null;
            }
        }

        private void OnSetupMeta(ItemHoveringUI _, ItemMetaData __)
        {
            SetVisible(false);
        }

        private void OnSetupItem(ItemHoveringUI ui, Item item)
        {
            if (item == null || !item.UseDurability)
            {
                SetVisible(false);
                return;
            }

            if (_text == null)
            {
                _text = UnityEngine.Object.Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
            }

            _text.gameObject.SetActive(true);
            _text.transform.SetParent(ui.LayoutParent, false);
            _text.transform.localScale = Vector3.one;
            var label = LocalizationManager.GetPlainText(UsageLocalizationKey);
            _text.text = item.DurabilityLoss > 0.000001f
                ? $"{label}: {(int)item.Durability}/{(int)item.MaxDurability} ({(int)item.MaxDurabilityWithLoss})"
                : $"{label}: {(int)item.Durability}/{(int)item.MaxDurability}";
            _text.fontSize = 20f;
        }

        private void SetVisible(bool visible)
        {
            if (_text != null)
            {
                _text.gameObject.SetActive(visible);
            }
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
