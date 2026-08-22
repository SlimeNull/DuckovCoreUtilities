using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using TMPro;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features.Abstraction
{
    public abstract class ItemInfoDisplayFeature : FeatureBase
    {
        private TextMeshProUGUI? _text = null;

        protected override void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
        }

        protected override void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
        }

        private void OnSetupMeta(ItemHoveringUI uI, ItemMetaData data)
        {
            _text?.gameObject.SetActive(false);
        }

        private void OnSetupItemHoveringUI(ItemHoveringUI uiInstance, Item item)
        {
            if (item == null)
            {
                _text?.gameObject?.SetActive(false);
                return;
            }

            var textToDisplay = GetText(uiInstance, item);
            if (_text == null ||
                _text.gameObject == null)
            {
                _text = UnityEngine.Object.Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
            }

            _text.gameObject.SetActive(true);
            _text.transform.SetParent(uiInstance.LayoutParent);
            _text.transform.localScale = Vector3.one;
            _text.text = textToDisplay;
            _text.fontSize = 20f;
        }


        protected abstract string GetText(ItemHoveringUI uiInstance, Item item);
    }
}
