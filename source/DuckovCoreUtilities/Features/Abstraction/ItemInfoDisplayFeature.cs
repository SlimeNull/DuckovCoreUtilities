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
        private TextMeshProUGUI? _text;

        protected override void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
        }

        protected override void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;

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

        private void OnSetupItemHoveringUI(ItemHoveringUI uiInstance, Item item)
        {
            if (item == null || !ShouldDisplay(uiInstance, item))
            {
                SetVisible(false);
                return;
            }

            var textToDisplay = GetText(uiInstance, item);
            if (_text == null ||
                _text.gameObject == null)
            {
                _text = UnityEngine.Object.Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
            }

            _text.gameObject.SetActive(true);
            _text.transform.SetParent(uiInstance.LayoutParent, false);
            _text.transform.localScale = Vector3.one;
            _text.text = textToDisplay;
            _text.fontSize = 20f;
        }

        private void SetVisible(bool visible)
        {
            if (_text != null)
            {
                _text.gameObject.SetActive(visible);
            }
        }

        protected virtual bool ShouldDisplay(ItemHoveringUI uiInstance, Item item) => true;

        protected abstract string GetText(ItemHoveringUI uiInstance, Item item);
    }
}
