using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Localization;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace SlimeNull.DuckovCoreUtilities.Configuration
{
    internal sealed class CoreUtilitiesModSettings : MonoBehaviour
    {
        [Serializable]
        private sealed class DisplayPriceOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("DisplayPrice.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/PriceType")]
            [FormerlySerializedAs("DisplayPrice.Mode")]
            public DisplayPriceFeature.DisplayMode Mode = DisplayPriceFeature.DisplayMode.SellPrice;
        }

        [Serializable]
        private sealed class BlackMarketPriceOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/DemandPriceBaseline")]
            public BlackMarketPriceComparisonFeature.DemandBaseline DemandBaseline =
                BlackMarketPriceComparisonFeature.DemandBaseline.MerchantSellback;
        }

        [Serializable]
        private sealed class StorageCountOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("DisplayStorageCount.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/ShowBackpackCount")]
            [FormerlySerializedAs("DisplayStorageCount.Backpack")]
            public bool Backpack = true;

            [InspectorName("@SettingsText/ShowRepositoryCount")]
            [FormerlySerializedAs("DisplayStorageCount.Repository")]
            public bool Repository = true;
        }

        [Serializable]
        private sealed class DisplayQualityOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("DisplayQuality.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/DisplayMode")]
            public DisplayQualityFeature.DecorateMode Mode = DisplayQualityFeature.DecorateMode.Border;

            [InspectorName("@SettingsText/Quality0Color")]
            public Color Quality0Color = new Color(1f, 1f, 1f, 0f);

            [InspectorName("@SettingsText/Quality1Color")]
            public Color Quality1Color = new Color(1f, 1f, 1f, 0f);

            [InspectorName("@SettingsText/Quality2Color")]
            public Color Quality2Color = new Color(0.6f, 0.9f, 0.6f, 0.24f);

            [InspectorName("@SettingsText/Quality3Color")]
            public Color Quality3Color = new Color(0.6f, 0.8f, 1f, 0.3f);

            [InspectorName("@SettingsText/Quality4Color")]
            public Color Quality4Color = new Color(1f, 0.5f, 1f, 0.4f);

            [InspectorName("@SettingsText/Quality5Color")]
            public Color Quality5Color = new Color(1f, 0.75f, 0.2f, 0.6f);

            [InspectorName("@SettingsText/Quality6Color")]
            public Color Quality6Color = new Color(1f, 0.3f, 0.3f, 0.4f);
        }

        [Serializable]
        private sealed class QualitySoundOptions
        {
            [InspectorName("@SettingsText/SoundEventPath")]
            public string EventPath;

            [InspectorName("@SettingsText/SoundVolume")]
            [Range(0f, 10f)]
            public float Volume;

            public QualitySoundOptions(string eventPath, float volume)
            {
                EventPath = eventPath;
                Volume = volume;
            }
        }

        [Serializable]
        private sealed class ItemSearchSoundOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/Quality0")]
            public QualitySoundOptions Quality0 = new QualitySoundOptions("event:/UI/level_up", 8f);

            [InspectorName("@SettingsText/Quality1")]
            public QualitySoundOptions Quality1 = new QualitySoundOptions("event:/UI/click", 1f);

            [InspectorName("@SettingsText/Quality2")]
            public QualitySoundOptions Quality2 = new QualitySoundOptions("event:/UI/click", 3f);

            [InspectorName("@SettingsText/Quality3")]
            public QualitySoundOptions Quality3 = new QualitySoundOptions("event:/UI/confirm", 3f);

            [InspectorName("@SettingsText/Quality4")]
            public QualitySoundOptions Quality4 = new QualitySoundOptions("event:/UI/ui_skill_up", 1f);

            [InspectorName("@SettingsText/Quality5")]
            public QualitySoundOptions Quality5 = new QualitySoundOptions("event:/UI/level_up", 2f);

            [InspectorName("@SettingsText/Quality6")]
            public QualitySoundOptions Quality6 = new QualitySoundOptions("event:/UI/level_up", 8f);
        }

        [Serializable]
        private sealed class LootOutlineOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("LootOutline.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/ShowLootboxOutline")]
            [FormerlySerializedAs("LootOutline.Lootboxes")]
            public bool Lootboxes = true;

            [InspectorName("@SettingsText/ShowGroundItemOutline")]
            [FormerlySerializedAs("LootOutline.GroundItems")]
            public bool GroundItems = true;

            [InspectorName("@SettingsText/UseQualityColor")]
            [FormerlySerializedAs("LootOutline.QualityColor")]
            public bool QualityColor = true;

            [InspectorName("@SettingsText/LootboxBreathing")]
            [FormerlySerializedAs("LootOutline.LootboxBreathing")]
            public bool LootboxBreathing = true;

            [InspectorName("@SettingsText/GroundItemBreathing")]
            [FormerlySerializedAs("LootOutline.GroundItemBreathing")]
            public bool GroundItemBreathing = true;

            [InspectorName("@SettingsText/BreathingPeriod")]
            [Tooltip("@SettingsText/BreathingPeriodTooltip")]
            [Range(0.1f, 5f)]
            [FormerlySerializedAs("LootOutline.BreathingPeriod")]
            public float BreathingPeriod = 1.5f;

            [InspectorName("@SettingsText/MinimumOpacity")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("LootOutline.BreathingMinAlpha")]
            public float BreathingMinAlpha = 0.35f;
        }

        [Serializable]
        private sealed class InventorySortOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("InventorySort.Enabled")]
            public bool Enabled = true;
        }

        [Serializable]
        private sealed class AutoCloseOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("AutoCloseBackpack.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/CloseWhileMoving")]
            [FormerlySerializedAs("AutoCloseBackpack.WhenMove")]
            public bool WhenMove = true;

            [InspectorName("@SettingsText/CloseWhenHurt")]
            [FormerlySerializedAs("AutoCloseBackpack.WhenHurt")]
            public bool WhenHurt = true;
        }

        [Serializable]
        private sealed class FadeHudOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("FadeHud.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/AimOpacity")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("FadeHud.TargetAlpha")]
            public float TargetAlpha = 0.3f;

            [InspectorName("@SettingsText/FadeDuration")]
            [Range(0.01f, 1f)]
            [FormerlySerializedAs("FadeHud.SmoothTime")]
            public float SmoothTime = 0.1f;
        }

        [Serializable]
        private sealed class CrosshairColorOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("CrosshairColor.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/WarningThreshold")]
            [Tooltip("@SettingsText/WarningThresholdTooltip")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("CrosshairColor.WarnRatio")]
            public float WarnRatio = 0.5f;

            [InspectorName("@SettingsText/FinalWarningColor")]
            [Tooltip("@SettingsText/FinalWarningColorTooltip")]
            [FormerlySerializedAs("CrosshairColor.FinalWarningColor")]
            public Color FinalWarningColor = Color.red;

            [InspectorName("@SettingsText/InitialWarningColor")]
            [Tooltip("@SettingsText/InitialWarningColorTooltip")]
            [FormerlySerializedAs("CrosshairColor.StartWarningColor")]
            public Color StartWarningColor = Color.yellow;
        }

        [Serializable]
        private sealed class UnfocusedOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("Unfocused.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/MuteWhenUnfocused")]
            [FormerlySerializedAs("Unfocused.Mute")]
            public bool Mute = true;

            [InspectorName("@SettingsText/PauseWhenUnfocused")]
            [FormerlySerializedAs("Unfocused.Pause")]
#if DEBUG
            public bool Pause = false;
#else
            public bool Pause = true;
#endif
        }

        [Serializable]
        private sealed class LowHealthShadowOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("LowHealthShadow.Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/ShadowColor")]
            [FormerlySerializedAs("LowHealthShadow.Color")]
            public Color Color = new Color(1f, 0f, 0f, 0.5f);

            [InspectorName("@SettingsText/ShadowWidth")]
            [Range(0f, 400f)]
            [FormerlySerializedAs("LowHealthShadow.Distance")]
            public float Distance = 150f;

            [InspectorName("@SettingsText/ShowThreshold")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("LowHealthShadow.UpperThreshold")]
            public float UpperThreshold = 0.6f;

            [InspectorName("@SettingsText/MaximumEffectThreshold")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("LowHealthShadow.LowerThreshold")]
            public float LowerThreshold = 0.2f;
        }

        [Serializable]
        private sealed class KillRecordOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("KillRecord.Enabled")]
            public bool Enabled = false;

            [InspectorName("@SettingsText/DisplayDuration")]
            [Range(1f, 30f)]
            [FormerlySerializedAs("KillRecord.Duration")]
            public float Duration = 5f;

            [InspectorName("@SettingsText/MaximumEntries")]
            [Range(1, 20)]
            [FormerlySerializedAs("KillRecord.MaxCount")]
            public int MaxCount = 5;

            [InspectorName("@SettingsText/TextFormat")]
            [Tooltip("@SettingsText/TextFormatTooltip")]
            [FormerlySerializedAs("KillRecord.Format")]
            public string Format = SettingsText.KillRecordDefaultFormat;
        }

        [Serializable]
        private sealed class MinimapOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            [FormerlySerializedAs("Minimap.Enabled")]
            public bool Enabled = false;

            [InspectorName("@SettingsText/DisplaySize")]
            [Range(100f, 600f)]
            [FormerlySerializedAs("Minimap.DisplaySize")]
            public float DisplaySize = 260f;

            [InspectorName("@SettingsText/Zoom")]
            [Range(MinimapFeature.MinimumZoom, MinimapFeature.MaximumZoom)]
            [FormerlySerializedAs("Minimap.Zoom")]
            public float Zoom = 1f;

            [InspectorName("@SettingsText/MapOrientation")]
            [FormerlySerializedAs("Minimap.Mode")]
            public MinimapFeature.OrientationMode Mode = MinimapFeature.OrientationMode.FixedAngle;

            [InspectorName("@SettingsText/ZoomOutKey")]
            [FormerlySerializedAs("Minimap.ZoomOutKey")]
            public Key ZoomOutKey = MinimapFeature.DefaultZoomOutKey;

            [InspectorName("@SettingsText/ZoomInKey")]
            [FormerlySerializedAs("Minimap.ZoomInKey")]
            public Key ZoomInKey = MinimapFeature.DefaultZoomInKey;

            [InspectorName("@SettingsText/Opacity")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("Minimap.Opacity")]
            public float Opacity = 0.7f;
        }

        [Serializable]
        private sealed class BossMapMarkerOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/PositionMode")]
            public BossMapMarkerFeature.TrackingMode Mode = BossMapMarkerFeature.TrackingMode.Static;

            [InspectorName("@SettingsText/ShowNames")]
            public bool ShowNames = true;

            [InspectorName("@SettingsText/MarkerColor")]
            public Color MarkerColor = new Color(1f, 0.3f, 0.3f, 1f);
        }

        [Serializable]
        private sealed class WakeTimeOptions
        {
            [InspectorName("@SettingsText/Hour")]
            [Range(0, 23)]
            public int Hour;

            [InspectorName("@SettingsText/Minute")]
            [Range(0, 59)]
            public int Minute;

            public WakeTimeOptions(int hour, int minute)
            {
                Hour = hour;
                Minute = minute;
            }
        }

        [Serializable]
        private sealed class QuickSleepOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/PresetTime1")]
            public WakeTimeOptions FirstTime = new WakeTimeOptions(6, 0);

            [InspectorName("@SettingsText/PresetTime2")]
            public WakeTimeOptions SecondTime = new WakeTimeOptions(22, 0);
        }

        [Serializable]
        private sealed class ItemUsageOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;
        }

        [Serializable]
        private sealed class GrenadeRadiusOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/ShowFuseProgress")]
            public bool ShowFuseProgress = true;

            [InspectorName("@SettingsText/ShowSmokeTimer")]
            public bool ShowSmokeTimer = true;

            [InspectorName("@SettingsText/GrenadeRadiusColor")]
            public Color RadiusColor = new Color(1f, 0.25f, 0.25f, 0.35f);

            [InspectorName("@SettingsText/GrenadeProgressColor")]
            public Color ProgressColor = new Color(1f, 0.9f, 0.6f, 0.5f);

            [InspectorName("@SettingsText/SmokeTimerColor")]
            public Color SmokeTimerColor = Color.white;
        }

        [Serializable]
        private sealed class RecordedItemIndicatorOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/IndicatorBackgroundColor")]
            public Color BackgroundColor = new Color(0.2f, 0.8f, 0.2f, 1f);

            [InspectorName("@SettingsText/IndicatorTextColor")]
            public Color TextColor = Color.white;
        }

        [Serializable]
        private sealed class QuestRequirementsOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/IncludeQuestRequirements")]
            public bool IncludeQuests = true;

            [InspectorName("@SettingsText/IncludePerkRequirements")]
            public bool IncludePerks = true;

            [InspectorName("@SettingsText/IncludeBuildingRequirements")]
            public bool IncludeBuildings = true;
        }

        [Serializable]
        private sealed class QuestFavoriteOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/FavoriteMarkerStyle")]
            public QuestFavoriteFeature.MarkerStyle MarkerStyle = QuestFavoriteFeature.MarkerStyle.Star;

            [InspectorName("@SettingsText/FavoriteMarkerColor")]
            public Color MarkerColor = new Color(1f, 0.78f, 0.12f, 1f);
        }

        [Header("@SettingsText/CategoryItemsAndEconomy")]
        [SerializeField, InspectorName("@SettingsText/FeatureDisplayPrice")]
        private DisplayPriceOptions displayPrice = new DisplayPriceOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureBlackMarketPrice")]
        private BlackMarketPriceOptions blackMarketPrice = new BlackMarketPriceOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureStorageCount")]
        private StorageCountOptions storageCount = new StorageCountOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureDisplayQuality")]
        private DisplayQualityOptions displayQuality = new DisplayQualityOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureItemUsage")]
        private ItemUsageOptions itemUsage = new ItemUsageOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureRecordedItemIndicator")]
        private RecordedItemIndicatorOptions recordedItemIndicator = new RecordedItemIndicatorOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureQuestRequirements")]
        private QuestRequirementsOptions questRequirements = new QuestRequirementsOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureItemSearchSound")]
        private ItemSearchSoundOptions itemSearchSound = new ItemSearchSoundOptions();

        [Header("@SettingsText/CategoryLootAndInventory")]
        [SerializeField, InspectorName("@SettingsText/FeatureLootOutline")]
        private LootOutlineOptions lootOutline = new LootOutlineOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureInventorySort")]
        private InventorySortOptions inventorySort = new InventorySortOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureAutoCloseBackpack")]
        private AutoCloseOptions autoCloseBackpack = new AutoCloseOptions();

        [Header("@SettingsText/CategoryCombatAndHud")]
        [SerializeField, InspectorName("@SettingsText/FeatureFadeHud")]
        private FadeHudOptions fadeHud = new FadeHudOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureCrosshairColor")]
        private CrosshairColorOptions crosshairColor = new CrosshairColorOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureLowHealthShadow")]
        private LowHealthShadowOptions lowHealthShadow = new LowHealthShadowOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureKillRecord")]
        private KillRecordOptions killRecord = new KillRecordOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureGrenadeRadius")]
        private GrenadeRadiusOptions grenadeRadius = new GrenadeRadiusOptions();

        [Header("@SettingsText/CategoryMapAndTime")]
        [SerializeField, InspectorName("@SettingsText/FeatureMinimap")]
        private MinimapOptions minimap = new MinimapOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureBossMapMarker")]
        private BossMapMarkerOptions bossMapMarker = new BossMapMarkerOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureQuickSleep")]
        private QuickSleepOptions quickSleep = new QuickSleepOptions();

        [SerializeField, InspectorName("@SettingsText/FeatureQuestFavorites")]
        private QuestFavoriteOptions questFavorites = new QuestFavoriteOptions();

        [Header("@SettingsText/CategoryWindowBehavior")]
        [SerializeField, InspectorName("@SettingsText/FeatureUnfocused")]
        private UnfocusedOptions unfocused = new UnfocusedOptions();

        private FeatureHost? _host;
        private DisplayPriceFeature? _displayPriceFeature;
        private BlackMarketPriceComparisonFeature? _blackMarketPriceFeature;
        private DisplayStorageCount? _storageCountFeature;
        private DisplayQualityFeature? _displayQualityFeature;
        private ItemSearchSoundFeature? _itemSearchSoundFeature;
        private LootboxOutlineFeature? _lootOutlineFeature;
        private InventorySortButtonsFeature? _inventorySortFeature;
        private AutoCloseBackpackFeature? _autoCloseFeature;
        private AutoFadeHudWhenAimingFeature? _fadeHudFeature;
        private BulletCountCrosshairColorFeature? _crosshairFeature;
        private MuteAndPauseWhenUnfocusedFeature? _unfocusedFeature;
        private LowHealthInnerShadowFeature? _lowHealthFeature;
        private KillRecordFeature? _killRecordFeature;
        private MinimapFeature? _minimapFeature;
        private BossMapMarkerFeature? _bossMapMarkerFeature;
        private QuickSleepFeature? _quickSleepFeature;
        private ItemUsageDisplayFeature? _itemUsageFeature;
        private GrenadeRadiusFeature? _grenadeRadiusFeature;
        private RecordedItemIndicatorFeature? _recordedItemIndicatorFeature;
        private QuestItemRequirementsFeature? _questRequirementsFeature;
        private QuestFavoriteFeature? _questFavoriteFeature;

        public void Initialize(
            FeatureHost host,
            DisplayPriceFeature displayPriceFeature,
            BlackMarketPriceComparisonFeature blackMarketPriceFeature,
            DisplayStorageCount storageCountFeature,
            DisplayQualityFeature displayQualityFeature,
            ItemSearchSoundFeature itemSearchSoundFeature,
            LootboxOutlineFeature lootOutlineFeature,
            InventorySortButtonsFeature inventorySortFeature,
            AutoCloseBackpackFeature autoCloseFeature,
            AutoFadeHudWhenAimingFeature fadeHudFeature,
            BulletCountCrosshairColorFeature crosshairFeature,
            MuteAndPauseWhenUnfocusedFeature unfocusedFeature,
            LowHealthInnerShadowFeature lowHealthFeature,
            KillRecordFeature killRecordFeature,
            MinimapFeature minimapFeature,
            BossMapMarkerFeature bossMapMarkerFeature,
            QuickSleepFeature quickSleepFeature,
            ItemUsageDisplayFeature itemUsageFeature,
            GrenadeRadiusFeature grenadeRadiusFeature,
            RecordedItemIndicatorFeature recordedItemIndicatorFeature,
            QuestItemRequirementsFeature questRequirementsFeature,
            QuestFavoriteFeature questFavoriteFeature)
        {
            _host = host;
            _displayPriceFeature = displayPriceFeature;
            _blackMarketPriceFeature = blackMarketPriceFeature;
            _storageCountFeature = storageCountFeature;
            _displayQualityFeature = displayQualityFeature;
            _itemSearchSoundFeature = itemSearchSoundFeature;
            _lootOutlineFeature = lootOutlineFeature;
            _inventorySortFeature = inventorySortFeature;
            _autoCloseFeature = autoCloseFeature;
            _fadeHudFeature = fadeHudFeature;
            _crosshairFeature = crosshairFeature;
            _unfocusedFeature = unfocusedFeature;
            _lowHealthFeature = lowHealthFeature;
            _killRecordFeature = killRecordFeature;
            _minimapFeature = minimapFeature;
            _bossMapMarkerFeature = bossMapMarkerFeature;
            _quickSleepFeature = quickSleepFeature;
            _itemUsageFeature = itemUsageFeature;
            _grenadeRadiusFeature = grenadeRadiusFeature;
            _recordedItemIndicatorFeature = recordedItemIndicatorFeature;
            _questRequirementsFeature = questRequirementsFeature;
            _questFavoriteFeature = questFavoriteFeature;
            _minimapFeature.ZoomChangedByInput += OnMinimapZoomChanged;
            OnValidate();
        }

        private void OnValidate()
        {
            ClampValues();
            if (_host == null)
            {
                return;
            }

            _displayPriceFeature!.Mode = displayPrice.Mode;
            _host.SetEnabled(_displayPriceFeature, displayPrice.Enabled);

            _blackMarketPriceFeature!.Baseline = blackMarketPrice.DemandBaseline;
            _host.SetEnabled(_blackMarketPriceFeature, blackMarketPrice.Enabled);

            _storageCountFeature!.DisplayItemCountInBackpack = storageCount.Backpack;
            _storageCountFeature.DisplayItemCountInRepository = storageCount.Repository;
            _host.SetEnabled(_storageCountFeature, storageCount.Enabled);

            _displayQualityFeature!.SetQualityColors(
                displayQuality.Quality0Color,
                displayQuality.Quality1Color,
                displayQuality.Quality2Color,
                displayQuality.Quality3Color,
                displayQuality.Quality4Color,
                displayQuality.Quality5Color,
                displayQuality.Quality6Color);
            _displayQualityFeature.Mode = displayQuality.Mode;
            _host.SetEnabled(_displayQualityFeature, displayQuality.Enabled);

            _itemSearchSoundFeature!.ConfigureQuality(0, itemSearchSound.Quality0.EventPath, itemSearchSound.Quality0.Volume);
            _itemSearchSoundFeature.ConfigureQuality(1, itemSearchSound.Quality1.EventPath, itemSearchSound.Quality1.Volume);
            _itemSearchSoundFeature.ConfigureQuality(2, itemSearchSound.Quality2.EventPath, itemSearchSound.Quality2.Volume);
            _itemSearchSoundFeature.ConfigureQuality(3, itemSearchSound.Quality3.EventPath, itemSearchSound.Quality3.Volume);
            _itemSearchSoundFeature.ConfigureQuality(4, itemSearchSound.Quality4.EventPath, itemSearchSound.Quality4.Volume);
            _itemSearchSoundFeature.ConfigureQuality(5, itemSearchSound.Quality5.EventPath, itemSearchSound.Quality5.Volume);
            _itemSearchSoundFeature.ConfigureQuality(6, itemSearchSound.Quality6.EventPath, itemSearchSound.Quality6.Volume);
            _host.SetEnabled(_itemSearchSoundFeature, itemSearchSound.Enabled);

            _lootOutlineFeature!.EnableLootboxOutline = lootOutline.Lootboxes;
            _lootOutlineFeature.EnableGroundItemOutline = lootOutline.GroundItems;
            _lootOutlineFeature.UseQualityColor = lootOutline.QualityColor;
            _lootOutlineFeature.LootboxBreathingEffect = lootOutline.LootboxBreathing;
            _lootOutlineFeature.GroundItemBreathingEffect = lootOutline.GroundItemBreathing;
            _lootOutlineFeature.BreathingPeriod = lootOutline.BreathingPeriod;
            _lootOutlineFeature.BreathingMinAlpha = lootOutline.BreathingMinAlpha;
            _host.SetEnabled(_lootOutlineFeature, lootOutline.Enabled);

            _host.SetEnabled(_inventorySortFeature!, inventorySort.Enabled);

            _autoCloseFeature!.WhenMove = autoCloseBackpack.WhenMove;
            _autoCloseFeature.WhenHurt = autoCloseBackpack.WhenHurt;
            _host.SetEnabled(_autoCloseFeature, autoCloseBackpack.Enabled);

            _fadeHudFeature!.TargetAlpha = fadeHud.TargetAlpha;
            _fadeHudFeature.SmoothTime = fadeHud.SmoothTime;
            _host.SetEnabled(_fadeHudFeature, fadeHud.Enabled);

            _crosshairFeature!.WarnRatio = crosshairColor.WarnRatio;
            _crosshairFeature.FinalWarningColor = crosshairColor.FinalWarningColor;
            _crosshairFeature.StartWarningColor = crosshairColor.StartWarningColor;
            _host.SetEnabled(_crosshairFeature, crosshairColor.Enabled);

            _unfocusedFeature!.MuteWhenUnfocused = unfocused.Mute;
            _unfocusedFeature.PauseWhenUnfocused = unfocused.Pause;
            _host.SetEnabled(_unfocusedFeature, unfocused.Enabled);

            _lowHealthFeature!.ShadowColor = lowHealthShadow.Color;
            _lowHealthFeature.ShadowDistance = lowHealthShadow.Distance;
            _lowHealthFeature.HealthThresholdUpper = lowHealthShadow.UpperThreshold;
            _lowHealthFeature.HealthThresholdLower = lowHealthShadow.LowerThreshold;
            _host.SetEnabled(_lowHealthFeature, lowHealthShadow.Enabled);

            _killRecordFeature!.RecordDuration = killRecord.Duration;
            _killRecordFeature.MaxRecordCount = killRecord.MaxCount;
            _killRecordFeature.RecordFormat = killRecord.Format;
            _host.SetEnabled(_killRecordFeature, killRecord.Enabled);

            _minimapFeature!.DisplaySize = minimap.DisplaySize;
            _minimapFeature.Zoom = minimap.Zoom;
            _minimapFeature.Mode = minimap.Mode;
            _minimapFeature.ZoomOutKey = minimap.ZoomOutKey;
            _minimapFeature.ZoomInKey = minimap.ZoomInKey;
            _minimapFeature.Opacity = minimap.Opacity;
            _host.SetEnabled(_minimapFeature, minimap.Enabled);

            _bossMapMarkerFeature!.Mode = bossMapMarker.Mode;
            _bossMapMarkerFeature.ShowNames = bossMapMarker.ShowNames;
            _bossMapMarkerFeature.MarkerColor = bossMapMarker.MarkerColor;
            _host.SetEnabled(_bossMapMarkerFeature, bossMapMarker.Enabled);

            _quickSleepFeature!.FirstHour = quickSleep.FirstTime.Hour;
            _quickSleepFeature.FirstMinute = quickSleep.FirstTime.Minute;
            _quickSleepFeature.SecondHour = quickSleep.SecondTime.Hour;
            _quickSleepFeature.SecondMinute = quickSleep.SecondTime.Minute;
            _host.SetEnabled(_quickSleepFeature, quickSleep.Enabled);

            _host.SetEnabled(_itemUsageFeature!, itemUsage.Enabled);

            _grenadeRadiusFeature!.RadiusColor = grenadeRadius.RadiusColor;
            _grenadeRadiusFeature.ProgressColor = grenadeRadius.ProgressColor;
            _grenadeRadiusFeature.SmokeTimerColor = grenadeRadius.SmokeTimerColor;
            _grenadeRadiusFeature.ShowFuseProgress = grenadeRadius.ShowFuseProgress;
            _grenadeRadiusFeature.ShowSmokeTimer = grenadeRadius.ShowSmokeTimer;
            _host.SetEnabled(_grenadeRadiusFeature, grenadeRadius.Enabled);
            if (_grenadeRadiusFeature.IsEnabled)
            {
                _grenadeRadiusFeature.RefreshExistingIndicators();
            }

            _recordedItemIndicatorFeature!.BackgroundColor = recordedItemIndicator.BackgroundColor;
            _recordedItemIndicatorFeature.TextColor = recordedItemIndicator.TextColor;
            _host.SetEnabled(_recordedItemIndicatorFeature, recordedItemIndicator.Enabled);
            if (_recordedItemIndicatorFeature.IsEnabled)
            {
                _recordedItemIndicatorFeature.RefreshExistingIndicators();
            }

            _questRequirementsFeature!.ShowQuestRequirements = questRequirements.IncludeQuests;
            _questRequirementsFeature.ShowPerkRequirements = questRequirements.IncludePerks;
            _questRequirementsFeature.ShowBuildingRequirements = questRequirements.IncludeBuildings;
            _host.SetEnabled(_questRequirementsFeature, questRequirements.Enabled);
            if (_questRequirementsFeature.IsEnabled)
            {
                _questRequirementsFeature.RefreshCurrentDisplay();
            }

            _questFavoriteFeature!.Style = questFavorites.MarkerStyle;
            _questFavoriteFeature.MarkerColor = questFavorites.MarkerColor;
            _host.SetEnabled(_questFavoriteFeature, questFavorites.Enabled);
            if (_questFavoriteFeature.IsEnabled)
            {
                _questFavoriteFeature.RefreshAppearanceAndSort();
            }
        }

        private void DuckovModSettingsUpdated()
        {
            OnValidate();
        }

        internal void RefreshLocalization(string previousDefaultFormat)
        {
            if (!string.Equals(killRecord.Format, previousDefaultFormat, StringComparison.Ordinal))
            {
                return;
            }

            killRecord.Format = SettingsText.KillRecordDefaultFormat;
            if (_killRecordFeature != null)
            {
                _killRecordFeature.RecordFormat = killRecord.Format;
            }
        }

        private void OnDestroy()
        {
            if (_minimapFeature != null)
            {
                _minimapFeature.ZoomChangedByInput -= OnMinimapZoomChanged;
            }
        }

        private void OnMinimapZoomChanged(float value)
        {
            minimap.Zoom = Mathf.Clamp(value, MinimapFeature.MinimumZoom, MinimapFeature.MaximumZoom);
        }

        private void ClampValues()
        {
            lootOutline.BreathingPeriod = Mathf.Clamp(lootOutline.BreathingPeriod, 0.1f, 5f);
            lootOutline.BreathingMinAlpha = Mathf.Clamp01(lootOutline.BreathingMinAlpha);
            fadeHud.TargetAlpha = Mathf.Clamp01(fadeHud.TargetAlpha);
            fadeHud.SmoothTime = Mathf.Clamp(fadeHud.SmoothTime, 0.01f, 1f);
            crosshairColor.WarnRatio = Mathf.Clamp01(crosshairColor.WarnRatio);
            crosshairColor.FinalWarningColor.a = 1f;
            crosshairColor.StartWarningColor.a = 1f;
            lowHealthShadow.Distance = Mathf.Clamp(lowHealthShadow.Distance, 0f, 400f);
            lowHealthShadow.UpperThreshold = Mathf.Clamp01(lowHealthShadow.UpperThreshold);
            lowHealthShadow.LowerThreshold = Mathf.Clamp01(lowHealthShadow.LowerThreshold);
            killRecord.Duration = Mathf.Clamp(killRecord.Duration, 1f, 30f);
            killRecord.MaxCount = Mathf.Clamp(killRecord.MaxCount, 1, 20);
            if (!IsValidRecordFormat(killRecord.Format))
            {
                killRecord.Format = SettingsText.KillRecordDefaultFormat;
            }
            minimap.DisplaySize = Mathf.Clamp(minimap.DisplaySize, 100f, 600f);
            minimap.Zoom = Mathf.Clamp(minimap.Zoom, MinimapFeature.MinimumZoom, MinimapFeature.MaximumZoom);
            minimap.Opacity = Mathf.Clamp01(minimap.Opacity);
            bossMapMarker.MarkerColor.r = Mathf.Clamp01(bossMapMarker.MarkerColor.r);
            bossMapMarker.MarkerColor.g = Mathf.Clamp01(bossMapMarker.MarkerColor.g);
            bossMapMarker.MarkerColor.b = Mathf.Clamp01(bossMapMarker.MarkerColor.b);
            bossMapMarker.MarkerColor.a = Mathf.Clamp01(bossMapMarker.MarkerColor.a);
            quickSleep.FirstTime.Hour = Mathf.Clamp(quickSleep.FirstTime.Hour, 0, 23);
            quickSleep.FirstTime.Minute = Mathf.Clamp(quickSleep.FirstTime.Minute, 0, 59);
            quickSleep.SecondTime.Hour = Mathf.Clamp(quickSleep.SecondTime.Hour, 0, 23);
            quickSleep.SecondTime.Minute = Mathf.Clamp(quickSleep.SecondTime.Minute, 0, 59);
            itemSearchSound.Quality0.Volume = Mathf.Clamp(itemSearchSound.Quality0.Volume, 0f, 10f);
            itemSearchSound.Quality1.Volume = Mathf.Clamp(itemSearchSound.Quality1.Volume, 0f, 10f);
            itemSearchSound.Quality2.Volume = Mathf.Clamp(itemSearchSound.Quality2.Volume, 0f, 10f);
            itemSearchSound.Quality3.Volume = Mathf.Clamp(itemSearchSound.Quality3.Volume, 0f, 10f);
            itemSearchSound.Quality4.Volume = Mathf.Clamp(itemSearchSound.Quality4.Volume, 0f, 10f);
            itemSearchSound.Quality5.Volume = Mathf.Clamp(itemSearchSound.Quality5.Volume, 0f, 10f);
            itemSearchSound.Quality6.Volume = Mathf.Clamp(itemSearchSound.Quality6.Volume, 0f, 10f);
        }

        private static bool IsValidRecordFormat(string? value)
        {
            try
            {
                string.Format(value ?? string.Empty, "target");
                return !string.IsNullOrEmpty(value);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
