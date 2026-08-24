using SlimeNull.DuckovCoreUtilities.Configuration;
using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private FeatureHost? _features;
        private CoreUtilitiesModSettings? _settings;

        protected override void OnAfterSetup()
        {
            if (_features != null)
            {
                return;
            }

            Debug.Log("loading DCU");

            _features = new FeatureHost(gameObject);
            var displayPrice = new DisplayPriceFeature();
            var displayStorageCount = new DisplayStorageCount();
            var displayQuality = new DisplayQualityFeature();
            var lootOutline = new LootboxOutlineFeature();
            var inventorySort = new InventorySortButtonsFeature();
            var autoCloseBackpack = new AutoCloseBackpackFeature();
            var fadeHud = new AutoFadeHudWhenAimingFeature();
            var crosshairColor = new BulletCountCrosshairColorFeature();
            var unfocused = new MuteAndPauseWhenUnfocusedFeature()
            {
                // 调试时失去焦点时不暂停游戏
#if DEBUG
                PauseWhenUnfocused = false,
#endif
            };
            var lowHealthShadow = new LowHealthInnerShadowFeature();
            var killRecord = new KillRecordFeature();
            var minimap = new MinimapFeature();

            _features.Register(displayPrice);
            _features.Register(displayStorageCount);
            _features.Register(displayQuality);
            _features.Register(lootOutline);
            _features.Register(inventorySort);
            _features.Register(autoCloseBackpack);
            _features.Register(fadeHud);
            _features.Register(crosshairColor);
            _features.Register(unfocused);
            _features.Register(lowHealthShadow);
            _features.Register(killRecord);
            _features.Register(minimap);

            _settings = gameObject.GetComponent<CoreUtilitiesModSettings>() ?? gameObject.AddComponent<CoreUtilitiesModSettings>();
            _settings.Initialize(
                _features,
                displayPrice,
                displayStorageCount,
                displayQuality,
                lootOutline,
                inventorySort,
                autoCloseBackpack,
                fadeHud,
                crosshairColor,
                unfocused,
                lowHealthShadow,
                killRecord,
                minimap);

            Debug.Log("loaded DCU");
        }

        protected override void OnBeforeDeactivate()
        {
            Debug.Log("deactivating DCU");
            if (_settings != null)
            {
                Destroy(_settings);
                _settings = null;
            }
            _features?.DisableAll();
            _features = null;
        }

        private void Update()
        {
            _features?.Tick();
        }

        private void OnGUI()
        {
            _features?.OnGUI();
        }
    }
}
