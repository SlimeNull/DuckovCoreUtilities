using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Localization;
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SlimeNull.DuckovCoreUtilities.Configuration
{
    internal sealed class CoreUtilitiesModSettings : MonoBehaviour
    {
        [Serializable]
        private sealed class DisplayPriceOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/PriceType")]
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
            public bool Enabled = true;

            [InspectorName("@SettingsText/ShowBackpackCount")]
            public bool Backpack = true;

            [InspectorName("@SettingsText/ShowRepositoryCount")]
            public bool Repository = true;
        }

        [Serializable]
        private sealed class DisplayQualityOptions
        {
            [InspectorName("@SettingsText/Enabled")]
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
            [InspectorName("@SettingsText/LocalSoundFile")]
            [Description("Audio Files|*.wav;*.ogg;*.mp3;*.aif;*.aiff")]
            public string LocalFilePath = string.Empty;

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
            public bool Enabled = true;

            [InspectorName("@SettingsText/ShowLootboxOutline")]
            public bool Lootboxes = true;

            [InspectorName("@SettingsText/ShowGroundItemOutline")]
            public bool GroundItems = true;

            [InspectorName("@SettingsText/UseQualityColor")]
            public bool QualityColor = true;

            [InspectorName("@SettingsText/LootboxBreathing")]
            public bool LootboxBreathing = true;

            [InspectorName("@SettingsText/GroundItemBreathing")]
            public bool GroundItemBreathing = true;

            [InspectorName("@SettingsText/BreathingPeriod")]
            [Tooltip("@SettingsText/BreathingPeriodTooltip")]
            [Range(0.1f, 5f)]
            public float BreathingPeriod = 1.5f;

            [InspectorName("@SettingsText/MinimumOpacity")]
            [Range(0f, 1f)]
            public float BreathingMinAlpha = 0.35f;
        }

        [Serializable]
        private sealed class InventorySortOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;
        }

        [Serializable]
        private sealed class AutoCloseOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/CloseWhileMoving")]
            public bool WhenMove = true;

            [InspectorName("@SettingsText/CloseWhenHurt")]
            public bool WhenHurt = true;
        }

        [Serializable]
        private sealed class FadeHudOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/AimOpacity")]
            [Range(0f, 1f)]
            public float TargetAlpha = 0.3f;

            [InspectorName("@SettingsText/FadeDuration")]
            [Range(0.01f, 1f)]
            public float SmoothTime = 0.1f;
        }

        [Serializable]
        private sealed class CrosshairColorOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/WarningThreshold")]
            [Tooltip("@SettingsText/WarningThresholdTooltip")]
            [Range(0f, 1f)]
            public float WarnRatio = 0.5f;

            [InspectorName("@SettingsText/FinalWarningColor")]
            [Tooltip("@SettingsText/FinalWarningColorTooltip")]
            public Color FinalWarningColor = Color.red;

            [InspectorName("@SettingsText/InitialWarningColor")]
            [Tooltip("@SettingsText/InitialWarningColorTooltip")]
            public Color StartWarningColor = Color.yellow;
        }

        [Serializable]
        private sealed class UnfocusedOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/MuteWhenUnfocused")]
            public bool Mute = true;

            [InspectorName("@SettingsText/PauseWhenUnfocused")]
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
            public bool Enabled = true;

            [InspectorName("@SettingsText/ShadowColor")]
            public Color Color = new Color(1f, 0f, 0f, 0.5f);

            [InspectorName("@SettingsText/ShadowWidth")]
            [Range(0f, 400f)]
            public float Distance = 150f;

            [InspectorName("@SettingsText/ShowThreshold")]
            [Range(0f, 1f)]
            public float UpperThreshold = 0.6f;

            [InspectorName("@SettingsText/MaximumEffectThreshold")]
            [Range(0f, 1f)]
            public float LowerThreshold = 0.2f;
        }

        [Serializable]
        private sealed class KillRecordOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = false;

            [InspectorName("@SettingsText/DisplayDuration")]
            [Range(1f, 30f)]
            public float Duration = 5f;

            [InspectorName("@SettingsText/MaximumEntries")]
            [Range(1, 20)]
            public int MaxCount = 5;

            [InspectorName("@SettingsText/TextFormat")]
            [Tooltip("@SettingsText/TextFormatTooltip")]
            public string Format = SettingsText.KillRecordDefaultFormat;
        }

        [Serializable]
        private sealed class MinimapOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = false;

            [InspectorName("@SettingsText/DisplaySize")]
            [Range(100f, 600f)]
            public float DisplaySize = 260f;

            [InspectorName("@SettingsText/Zoom")]
            [Range(MinimapFeature.MinimumZoom, MinimapFeature.MaximumZoom)]
            public float Zoom = 1f;

            [InspectorName("@SettingsText/MapOrientation")]
            public MinimapFeature.OrientationMode Mode = MinimapFeature.OrientationMode.FixedAngle;

            [InspectorName("@SettingsText/ZoomOutKey")]
            public Key ZoomOutKey = MinimapFeature.DefaultZoomOutKey;

            [InspectorName("@SettingsText/ZoomInKey")]
            public Key ZoomInKey = MinimapFeature.DefaultZoomInKey;

            [InspectorName("@SettingsText/Opacity")]
            [Range(0f, 1f)]
            public float Opacity = 0.7f;
        }

        [Serializable]
        private sealed class BossMapMarkerOptions
        {
            [InspectorName("@SettingsText/Enabled")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/PositionMode")]
            public BossMapMarkerFeature.TrackingMode Mode = BossMapMarkerFeature.TrackingMode.Dynamic;

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

        [Serializable]
        private sealed class ItemsAndEconomyOptions
        {
            [InspectorName("@SettingsText/FeatureDisplayPrice")]
            public DisplayPriceOptions DisplayPrice = new DisplayPriceOptions();

            [InspectorName("@SettingsText/FeatureBlackMarketPrice")]
            public BlackMarketPriceOptions BlackMarketPrice = new BlackMarketPriceOptions();

            [InspectorName("@SettingsText/FeatureStorageCount")]
            public StorageCountOptions StorageCount = new StorageCountOptions();

            [InspectorName("@SettingsText/FeatureDisplayQuality")]
            public DisplayQualityOptions DisplayQuality = new DisplayQualityOptions();

            [InspectorName("@SettingsText/FeatureItemSearchSound")]
            public ItemSearchSoundOptions ItemSearchSound = new ItemSearchSoundOptions();

            [InspectorName("@SettingsText/FeatureItemUsage")]
            public ItemUsageOptions ItemUsage = new ItemUsageOptions();

            [InspectorName("@SettingsText/FeatureRecordedItemIndicator")]
            public RecordedItemIndicatorOptions RecordedItemIndicator = new RecordedItemIndicatorOptions();

            [InspectorName("@SettingsText/FeatureQuestRequirements")]
            public QuestRequirementsOptions QuestRequirements = new QuestRequirementsOptions();
        }

        [Serializable]
        private sealed class LootAndInventoryOptions
        {
            [InspectorName("@SettingsText/FeatureLootOutline")]
            public LootOutlineOptions LootOutline = new LootOutlineOptions();

            [InspectorName("@SettingsText/FeatureInventorySort")]
            public InventorySortOptions InventorySort = new InventorySortOptions();

            [InspectorName("@SettingsText/FeatureAutoCloseBackpack")]
            public AutoCloseOptions AutoCloseBackpack = new AutoCloseOptions();
        }

        [Serializable]
        private sealed class CombatAndHudOptions
        {
            [InspectorName("@SettingsText/FeatureFadeHud")]
            public FadeHudOptions FadeHud = new FadeHudOptions();

            [InspectorName("@SettingsText/FeatureCrosshairColor")]
            public CrosshairColorOptions CrosshairColor = new CrosshairColorOptions();

            [InspectorName("@SettingsText/FeatureLowHealthShadow")]
            public LowHealthShadowOptions LowHealthShadow = new LowHealthShadowOptions();

            [InspectorName("@SettingsText/FeatureKillRecord")]
            public KillRecordOptions KillRecord = new KillRecordOptions();

            [InspectorName("@SettingsText/FeatureGrenadeRadius")]
            public GrenadeRadiusOptions GrenadeRadius = new GrenadeRadiusOptions();
        }

        [Serializable]
        private sealed class MapAndTimeOptions
        {
            [InspectorName("@SettingsText/FeatureMinimap")]
            public MinimapOptions Minimap = new MinimapOptions();

            [InspectorName("@SettingsText/FeatureBossMapMarker")]
            public BossMapMarkerOptions BossMapMarker = new BossMapMarkerOptions();

            [InspectorName("@SettingsText/FeatureQuickSleep")]
            public QuickSleepOptions QuickSleep = new QuickSleepOptions();

            [InspectorName("@SettingsText/FeatureQuestFavorites")]
            public QuestFavoriteOptions QuestFavorites = new QuestFavoriteOptions();
        }

        [Serializable]
        private sealed class WindowBehaviorOptions
        {
            [InspectorName("@SettingsText/FeatureUnfocused")]
            public UnfocusedOptions Unfocused = new UnfocusedOptions();
        }

        [SerializeField, InspectorName("@SettingsText/CategoryItemsAndEconomy")]
        private ItemsAndEconomyOptions itemsAndEconomy = new ItemsAndEconomyOptions();

        [SerializeField, InspectorName("@SettingsText/CategoryLootAndInventory")]
        private LootAndInventoryOptions lootAndInventory = new LootAndInventoryOptions();

        [SerializeField, InspectorName("@SettingsText/CategoryCombatAndHud")]
        private CombatAndHudOptions combatAndHud = new CombatAndHudOptions();

        [SerializeField, InspectorName("@SettingsText/CategoryMapAndTime")]
        private MapAndTimeOptions mapAndTime = new MapAndTimeOptions();

        [SerializeField, InspectorName("@SettingsText/CategoryWindowBehavior")]
        private WindowBehaviorOptions windowBehavior = new WindowBehaviorOptions();

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

            _displayPriceFeature!.Mode = itemsAndEconomy.DisplayPrice.Mode;
            _host.SetEnabled(_displayPriceFeature, itemsAndEconomy.DisplayPrice.Enabled);

            _blackMarketPriceFeature!.Baseline = itemsAndEconomy.BlackMarketPrice.DemandBaseline;
            _host.SetEnabled(_blackMarketPriceFeature, itemsAndEconomy.BlackMarketPrice.Enabled);

            _storageCountFeature!.DisplayItemCountInBackpack = itemsAndEconomy.StorageCount.Backpack;
            _storageCountFeature.DisplayItemCountInRepository = itemsAndEconomy.StorageCount.Repository;
            _host.SetEnabled(_storageCountFeature, itemsAndEconomy.StorageCount.Enabled);

            _displayQualityFeature!.SetQualityColors(
                itemsAndEconomy.DisplayQuality.Quality0Color,
                itemsAndEconomy.DisplayQuality.Quality1Color,
                itemsAndEconomy.DisplayQuality.Quality2Color,
                itemsAndEconomy.DisplayQuality.Quality3Color,
                itemsAndEconomy.DisplayQuality.Quality4Color,
                itemsAndEconomy.DisplayQuality.Quality5Color,
                itemsAndEconomy.DisplayQuality.Quality6Color);
            _displayQualityFeature.Mode = itemsAndEconomy.DisplayQuality.Mode;
            _host.SetEnabled(_displayQualityFeature, itemsAndEconomy.DisplayQuality.Enabled);

            ConfigureItemSearchSoundQuality(0, itemsAndEconomy.ItemSearchSound.Quality0);
            ConfigureItemSearchSoundQuality(1, itemsAndEconomy.ItemSearchSound.Quality1);
            ConfigureItemSearchSoundQuality(2, itemsAndEconomy.ItemSearchSound.Quality2);
            ConfigureItemSearchSoundQuality(3, itemsAndEconomy.ItemSearchSound.Quality3);
            ConfigureItemSearchSoundQuality(4, itemsAndEconomy.ItemSearchSound.Quality4);
            ConfigureItemSearchSoundQuality(5, itemsAndEconomy.ItemSearchSound.Quality5);
            ConfigureItemSearchSoundQuality(6, itemsAndEconomy.ItemSearchSound.Quality6);
            _host.SetEnabled(_itemSearchSoundFeature!, itemsAndEconomy.ItemSearchSound.Enabled);

            _lootOutlineFeature!.EnableLootboxOutline = lootAndInventory.LootOutline.Lootboxes;
            _lootOutlineFeature.EnableGroundItemOutline = lootAndInventory.LootOutline.GroundItems;
            _lootOutlineFeature.UseQualityColor = lootAndInventory.LootOutline.QualityColor;
            _lootOutlineFeature.LootboxBreathingEffect = lootAndInventory.LootOutline.LootboxBreathing;
            _lootOutlineFeature.GroundItemBreathingEffect = lootAndInventory.LootOutline.GroundItemBreathing;
            _lootOutlineFeature.BreathingPeriod = lootAndInventory.LootOutline.BreathingPeriod;
            _lootOutlineFeature.BreathingMinAlpha = lootAndInventory.LootOutline.BreathingMinAlpha;
            _host.SetEnabled(_lootOutlineFeature, lootAndInventory.LootOutline.Enabled);

            _host.SetEnabled(_inventorySortFeature!, lootAndInventory.InventorySort.Enabled);

            _autoCloseFeature!.WhenMove = lootAndInventory.AutoCloseBackpack.WhenMove;
            _autoCloseFeature.WhenHurt = lootAndInventory.AutoCloseBackpack.WhenHurt;
            _host.SetEnabled(_autoCloseFeature, lootAndInventory.AutoCloseBackpack.Enabled);

            _fadeHudFeature!.TargetAlpha = combatAndHud.FadeHud.TargetAlpha;
            _fadeHudFeature.SmoothTime = combatAndHud.FadeHud.SmoothTime;
            _host.SetEnabled(_fadeHudFeature, combatAndHud.FadeHud.Enabled);

            _crosshairFeature!.WarnRatio = combatAndHud.CrosshairColor.WarnRatio;
            _crosshairFeature.FinalWarningColor = combatAndHud.CrosshairColor.FinalWarningColor;
            _crosshairFeature.StartWarningColor = combatAndHud.CrosshairColor.StartWarningColor;
            _host.SetEnabled(_crosshairFeature, combatAndHud.CrosshairColor.Enabled);

            _unfocusedFeature!.MuteWhenUnfocused = windowBehavior.Unfocused.Mute;
            _unfocusedFeature.PauseWhenUnfocused = windowBehavior.Unfocused.Pause;
            _host.SetEnabled(_unfocusedFeature, windowBehavior.Unfocused.Enabled);

            _lowHealthFeature!.ShadowColor = combatAndHud.LowHealthShadow.Color;
            _lowHealthFeature.ShadowDistance = combatAndHud.LowHealthShadow.Distance;
            _lowHealthFeature.HealthThresholdUpper = combatAndHud.LowHealthShadow.UpperThreshold;
            _lowHealthFeature.HealthThresholdLower = combatAndHud.LowHealthShadow.LowerThreshold;
            _host.SetEnabled(_lowHealthFeature, combatAndHud.LowHealthShadow.Enabled);

            _killRecordFeature!.RecordDuration = combatAndHud.KillRecord.Duration;
            _killRecordFeature.MaxRecordCount = combatAndHud.KillRecord.MaxCount;
            _killRecordFeature.RecordFormat = combatAndHud.KillRecord.Format;
            _host.SetEnabled(_killRecordFeature, combatAndHud.KillRecord.Enabled);

            _minimapFeature!.DisplaySize = mapAndTime.Minimap.DisplaySize;
            _minimapFeature.Zoom = mapAndTime.Minimap.Zoom;
            _minimapFeature.Mode = mapAndTime.Minimap.Mode;
            _minimapFeature.ZoomOutKey = mapAndTime.Minimap.ZoomOutKey;
            _minimapFeature.ZoomInKey = mapAndTime.Minimap.ZoomInKey;
            _minimapFeature.Opacity = mapAndTime.Minimap.Opacity;
            _host.SetEnabled(_minimapFeature, mapAndTime.Minimap.Enabled);

            _bossMapMarkerFeature!.Mode = mapAndTime.BossMapMarker.Mode;
            _bossMapMarkerFeature.ShowNames = mapAndTime.BossMapMarker.ShowNames;
            _bossMapMarkerFeature.MarkerColor = mapAndTime.BossMapMarker.MarkerColor;
            _host.SetEnabled(_bossMapMarkerFeature, mapAndTime.BossMapMarker.Enabled);

            _quickSleepFeature!.FirstHour = mapAndTime.QuickSleep.FirstTime.Hour;
            _quickSleepFeature.FirstMinute = mapAndTime.QuickSleep.FirstTime.Minute;
            _quickSleepFeature.SecondHour = mapAndTime.QuickSleep.SecondTime.Hour;
            _quickSleepFeature.SecondMinute = mapAndTime.QuickSleep.SecondTime.Minute;
            _host.SetEnabled(_quickSleepFeature, mapAndTime.QuickSleep.Enabled);

            _host.SetEnabled(_itemUsageFeature!, itemsAndEconomy.ItemUsage.Enabled);

            _grenadeRadiusFeature!.RadiusColor = combatAndHud.GrenadeRadius.RadiusColor;
            _grenadeRadiusFeature.ProgressColor = combatAndHud.GrenadeRadius.ProgressColor;
            _grenadeRadiusFeature.SmokeTimerColor = combatAndHud.GrenadeRadius.SmokeTimerColor;
            _grenadeRadiusFeature.ShowFuseProgress = combatAndHud.GrenadeRadius.ShowFuseProgress;
            _grenadeRadiusFeature.ShowSmokeTimer = combatAndHud.GrenadeRadius.ShowSmokeTimer;
            _host.SetEnabled(_grenadeRadiusFeature, combatAndHud.GrenadeRadius.Enabled);
            if (_grenadeRadiusFeature.IsEnabled)
            {
                _grenadeRadiusFeature.RefreshExistingIndicators();
            }

            _recordedItemIndicatorFeature!.BackgroundColor = itemsAndEconomy.RecordedItemIndicator.BackgroundColor;
            _recordedItemIndicatorFeature.TextColor = itemsAndEconomy.RecordedItemIndicator.TextColor;
            _host.SetEnabled(_recordedItemIndicatorFeature, itemsAndEconomy.RecordedItemIndicator.Enabled);
            if (_recordedItemIndicatorFeature.IsEnabled)
            {
                _recordedItemIndicatorFeature.RefreshExistingIndicators();
            }

            _questRequirementsFeature!.ShowQuestRequirements = itemsAndEconomy.QuestRequirements.IncludeQuests;
            _questRequirementsFeature.ShowPerkRequirements = itemsAndEconomy.QuestRequirements.IncludePerks;
            _questRequirementsFeature.ShowBuildingRequirements = itemsAndEconomy.QuestRequirements.IncludeBuildings;
            _host.SetEnabled(_questRequirementsFeature, itemsAndEconomy.QuestRequirements.Enabled);
            if (_questRequirementsFeature.IsEnabled)
            {
                _questRequirementsFeature.RefreshCurrentDisplay();
            }

            _questFavoriteFeature!.Style = mapAndTime.QuestFavorites.MarkerStyle;
            _questFavoriteFeature.MarkerColor = mapAndTime.QuestFavorites.MarkerColor;
            _host.SetEnabled(_questFavoriteFeature, mapAndTime.QuestFavorites.Enabled);
            if (_questFavoriteFeature.IsEnabled)
            {
                _questFavoriteFeature.RefreshAppearanceAndSort();
            }
        }

        private void DuckovModSettingsUpdated()
        {
            OnValidate();
        }

        private void ConfigureItemSearchSoundQuality(int quality, QualitySoundOptions options)
        {
            _itemSearchSoundFeature!.ConfigureQuality(
                quality,
                options.LocalFilePath,
                options.EventPath,
                options.Volume);
        }

        internal void RefreshLocalization(string previousDefaultFormat)
        {
            if (!string.Equals(combatAndHud.KillRecord.Format, previousDefaultFormat, StringComparison.Ordinal))
            {
                return;
            }

            combatAndHud.KillRecord.Format = SettingsText.KillRecordDefaultFormat;
            if (_killRecordFeature != null)
            {
                _killRecordFeature.RecordFormat = combatAndHud.KillRecord.Format;
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
            mapAndTime.Minimap.Zoom = Mathf.Clamp(value, MinimapFeature.MinimumZoom, MinimapFeature.MaximumZoom);
        }

        private void ClampValues()
        {
            lootAndInventory.LootOutline.BreathingPeriod =
                Mathf.Clamp(lootAndInventory.LootOutline.BreathingPeriod, 0.1f, 5f);
            lootAndInventory.LootOutline.BreathingMinAlpha =
                Mathf.Clamp01(lootAndInventory.LootOutline.BreathingMinAlpha);
            combatAndHud.FadeHud.TargetAlpha = Mathf.Clamp01(combatAndHud.FadeHud.TargetAlpha);
            combatAndHud.FadeHud.SmoothTime = Mathf.Clamp(combatAndHud.FadeHud.SmoothTime, 0.01f, 1f);
            combatAndHud.CrosshairColor.WarnRatio = Mathf.Clamp01(combatAndHud.CrosshairColor.WarnRatio);
            combatAndHud.CrosshairColor.FinalWarningColor.a = 1f;
            combatAndHud.CrosshairColor.StartWarningColor.a = 1f;
            combatAndHud.LowHealthShadow.Distance = Mathf.Clamp(combatAndHud.LowHealthShadow.Distance, 0f, 400f);
            combatAndHud.LowHealthShadow.UpperThreshold =
                Mathf.Clamp01(combatAndHud.LowHealthShadow.UpperThreshold);
            combatAndHud.LowHealthShadow.LowerThreshold =
                Mathf.Clamp01(combatAndHud.LowHealthShadow.LowerThreshold);
            combatAndHud.KillRecord.Duration = Mathf.Clamp(combatAndHud.KillRecord.Duration, 1f, 30f);
            combatAndHud.KillRecord.MaxCount = Mathf.Clamp(combatAndHud.KillRecord.MaxCount, 1, 20);
            if (!IsValidRecordFormat(combatAndHud.KillRecord.Format))
            {
                combatAndHud.KillRecord.Format = SettingsText.KillRecordDefaultFormat;
            }
            mapAndTime.Minimap.DisplaySize = Mathf.Clamp(mapAndTime.Minimap.DisplaySize, 100f, 600f);
            mapAndTime.Minimap.Zoom = Mathf.Clamp(
                mapAndTime.Minimap.Zoom,
                MinimapFeature.MinimumZoom,
                MinimapFeature.MaximumZoom);
            mapAndTime.Minimap.Opacity = Mathf.Clamp01(mapAndTime.Minimap.Opacity);
            mapAndTime.BossMapMarker.MarkerColor.r = Mathf.Clamp01(mapAndTime.BossMapMarker.MarkerColor.r);
            mapAndTime.BossMapMarker.MarkerColor.g = Mathf.Clamp01(mapAndTime.BossMapMarker.MarkerColor.g);
            mapAndTime.BossMapMarker.MarkerColor.b = Mathf.Clamp01(mapAndTime.BossMapMarker.MarkerColor.b);
            mapAndTime.BossMapMarker.MarkerColor.a = Mathf.Clamp01(mapAndTime.BossMapMarker.MarkerColor.a);
            mapAndTime.QuickSleep.FirstTime.Hour = Mathf.Clamp(mapAndTime.QuickSleep.FirstTime.Hour, 0, 23);
            mapAndTime.QuickSleep.FirstTime.Minute = Mathf.Clamp(mapAndTime.QuickSleep.FirstTime.Minute, 0, 59);
            mapAndTime.QuickSleep.SecondTime.Hour = Mathf.Clamp(mapAndTime.QuickSleep.SecondTime.Hour, 0, 23);
            mapAndTime.QuickSleep.SecondTime.Minute = Mathf.Clamp(mapAndTime.QuickSleep.SecondTime.Minute, 0, 59);
            itemsAndEconomy.ItemSearchSound.Quality0.Volume =
                Mathf.Clamp(itemsAndEconomy.ItemSearchSound.Quality0.Volume, 0f, 10f);
            itemsAndEconomy.ItemSearchSound.Quality1.Volume =
                Mathf.Clamp(itemsAndEconomy.ItemSearchSound.Quality1.Volume, 0f, 10f);
            itemsAndEconomy.ItemSearchSound.Quality2.Volume =
                Mathf.Clamp(itemsAndEconomy.ItemSearchSound.Quality2.Volume, 0f, 10f);
            itemsAndEconomy.ItemSearchSound.Quality3.Volume =
                Mathf.Clamp(itemsAndEconomy.ItemSearchSound.Quality3.Volume, 0f, 10f);
            itemsAndEconomy.ItemSearchSound.Quality4.Volume =
                Mathf.Clamp(itemsAndEconomy.ItemSearchSound.Quality4.Volume, 0f, 10f);
            itemsAndEconomy.ItemSearchSound.Quality5.Volume =
                Mathf.Clamp(itemsAndEconomy.ItemSearchSound.Quality5.Volume, 0f, 10f);
            itemsAndEconomy.ItemSearchSound.Quality6.Volume =
                Mathf.Clamp(itemsAndEconomy.ItemSearchSound.Quality6.Volume, 0f, 10f);
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
