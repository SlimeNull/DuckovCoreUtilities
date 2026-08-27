using SlimeNull.DuckovCoreUtilities.Configuration;
using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Localization;
using SlimeNull.Mods.Localization;
using SodaCraft.Localizations;
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

            var language = LocalizationManager.Initialized
                ? LocalizationManager.CurrentLanguage
                : Application.systemLanguage;
            var culture = ModLanguage.GetCulture(language);
            ModLanguage.PrepareResourceManager(SettingsText.ResourceManager, typeof(SettingsText).Assembly, culture);
            SettingsText.Culture = culture;

            _features = new FeatureHost(gameObject);
            var displayPrice = new DisplayPriceFeature();
            var blackMarketPrice = new BlackMarketPriceComparisonFeature();
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
            var bossMapMarker = new BossMapMarkerFeature();
            var quickSleep = new QuickSleepFeature();
            var itemUsage = new ItemUsageDisplayFeature();

            _features.Register(displayPrice);
            _features.Register(blackMarketPrice);
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
            _features.Register(bossMapMarker);
            _features.Register(quickSleep);
            _features.Register(itemUsage);

            _settings = gameObject.GetComponent<CoreUtilitiesModSettings>() ?? gameObject.AddComponent<CoreUtilitiesModSettings>();
            _settings.Initialize(
                _features,
                displayPrice,
                blackMarketPrice,
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
                minimap,
                bossMapMarker,
                quickSleep,
                itemUsage);

            LocalizationManager.OnSetLanguage += OnLanguageChanged;
            Debug.Log("loaded DCU");
        }

        protected override void OnBeforeDeactivate()
        {
            Debug.Log("deactivating DCU");
            LocalizationManager.OnSetLanguage -= OnLanguageChanged;
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

        private void OnLanguageChanged(SystemLanguage language)
        {
            var previousDefaultFormat = SettingsText.KillRecordDefaultFormat;
            var culture = ModLanguage.GetCulture(language);
            ModLanguage.PrepareResourceManager(SettingsText.ResourceManager, typeof(SettingsText).Assembly, culture);
            SettingsText.Culture = culture;
            _settings?.RefreshLocalization(previousDefaultFormat);
            _features?.RefreshLocalization();
        }
    }
}
