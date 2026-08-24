using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
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
            [InspectorName("启用")]
            [FormerlySerializedAs("DisplayPrice.Enabled")]
            public bool Enabled = true;

            [InspectorName("价格类型")]
            [FormerlySerializedAs("DisplayPrice.Mode")]
            public DisplayPriceFeature.DisplayMode Mode = DisplayPriceFeature.DisplayMode.SellPrice;
        }

        [Serializable]
        private sealed class StorageCountOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("DisplayStorageCount.Enabled")]
            public bool Enabled = true;

            [InspectorName("显示背包内数量")]
            [FormerlySerializedAs("DisplayStorageCount.Backpack")]
            public bool Backpack = true;

            [InspectorName("显示仓库内数量")]
            [FormerlySerializedAs("DisplayStorageCount.Repository")]
            public bool Repository = true;
        }

        [Serializable]
        private sealed class DisplayQualityOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("DisplayQuality.Enabled")]
            public bool Enabled = true;

            [InspectorName("显示方式")]
            public DisplayQualityFeature.DecorateMode Mode = DisplayQualityFeature.DecorateMode.Border;
        }

        [Serializable]
        private sealed class LootOutlineOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("LootOutline.Enabled")]
            public bool Enabled = true;

            [InspectorName("显示战利品箱轮廓")]
            [FormerlySerializedAs("LootOutline.Lootboxes")]
            public bool Lootboxes = true;

            [InspectorName("显示地面物品轮廓")]
            [FormerlySerializedAs("LootOutline.GroundItems")]
            public bool GroundItems = true;

            [InspectorName("使用物品品质颜色")]
            [FormerlySerializedAs("LootOutline.QualityColor")]
            public bool QualityColor = true;

            [InspectorName("战利品箱呼吸效果")]
            [FormerlySerializedAs("LootOutline.LootboxBreathing")]
            public bool LootboxBreathing = true;

            [InspectorName("地面物品呼吸效果")]
            [FormerlySerializedAs("LootOutline.GroundItemBreathing")]
            public bool GroundItemBreathing = true;

            [InspectorName("呼吸周期")]
            [Tooltip("轮廓完成一次明暗变化所需的秒数")]
            [Range(0.1f, 5f)]
            [FormerlySerializedAs("LootOutline.BreathingPeriod")]
            public float BreathingPeriod = 1.5f;

            [InspectorName("最低透明度")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("LootOutline.BreathingMinAlpha")]
            public float BreathingMinAlpha = 0.35f;
        }

        [Serializable]
        private sealed class InventorySortOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("InventorySort.Enabled")]
            public bool Enabled = true;
        }

        [Serializable]
        private sealed class AutoCloseOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("AutoCloseBackpack.Enabled")]
            public bool Enabled = true;

            [InspectorName("移动时关闭")]
            [FormerlySerializedAs("AutoCloseBackpack.WhenMove")]
            public bool WhenMove = true;

            [InspectorName("受伤时关闭")]
            [FormerlySerializedAs("AutoCloseBackpack.WhenHurt")]
            public bool WhenHurt = true;
        }

        [Serializable]
        private sealed class FadeHudOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("FadeHud.Enabled")]
            public bool Enabled = true;

            [InspectorName("瞄准时透明度")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("FadeHud.TargetAlpha")]
            public float TargetAlpha = 0.3f;

            [InspectorName("淡入淡出时间")]
            [Range(0.01f, 1f)]
            [FormerlySerializedAs("FadeHud.SmoothTime")]
            public float SmoothTime = 0.1f;
        }

        [Serializable]
        private sealed class CrosshairColorOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("CrosshairColor.Enabled")]
            public bool Enabled = true;

            [InspectorName("开始警告比例")]
            [Tooltip("弹匣剩余比例低于此值时，准星开始变色")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("CrosshairColor.WarnRatio")]
            public float WarnRatio = 0.5f;

            [InspectorName("最终警告颜色")]
            [Tooltip("弹匣耗尽时的准星颜色")]
            [FormerlySerializedAs("CrosshairColor.FinalWarningColor")]
            public Color FinalWarningColor = Color.red;

            [InspectorName("开始警告颜色")]
            [Tooltip("刚进入低弹药警告区间时的准星颜色")]
            [FormerlySerializedAs("CrosshairColor.StartWarningColor")]
            public Color StartWarningColor = Color.yellow;
        }

        [Serializable]
        private sealed class UnfocusedOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("Unfocused.Enabled")]
            public bool Enabled = true;

            [InspectorName("失去焦点时静音")]
            [FormerlySerializedAs("Unfocused.Mute")]
            public bool Mute = true;

            [InspectorName("失去焦点时暂停")]
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
            [InspectorName("启用")]
            [FormerlySerializedAs("LowHealthShadow.Enabled")]
            public bool Enabled = true;

            [InspectorName("阴影颜色")]
            [FormerlySerializedAs("LowHealthShadow.Color")]
            public Color Color = new Color(1f, 0f, 0f, 0.5f);

            [InspectorName("阴影宽度")]
            [Range(0f, 400f)]
            [FormerlySerializedAs("LowHealthShadow.Distance")]
            public float Distance = 150f;

            [InspectorName("开始显示比例")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("LowHealthShadow.UpperThreshold")]
            public float UpperThreshold = 0.6f;

            [InspectorName("最深效果比例")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("LowHealthShadow.LowerThreshold")]
            public float LowerThreshold = 0.2f;
        }

        [Serializable]
        private sealed class KillRecordOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("KillRecord.Enabled")]
            public bool Enabled = false;

            [InspectorName("显示时间")]
            [Range(1f, 30f)]
            [FormerlySerializedAs("KillRecord.Duration")]
            public float Duration = 5f;

            [InspectorName("最多显示条数")]
            [Range(1, 20)]
            [FormerlySerializedAs("KillRecord.MaxCount")]
            public int MaxCount = 5;

            [InspectorName("文本格式")]
            [Tooltip("{0} 会替换为目标名称")]
            [FormerlySerializedAs("KillRecord.Format")]
            public string Format = "击杀 {0}";
        }

        [Serializable]
        private sealed class MinimapOptions
        {
            [InspectorName("启用")]
            [FormerlySerializedAs("Minimap.Enabled")]
            public bool Enabled = false;

            [InspectorName("显示尺寸")]
            [Range(100f, 600f)]
            [FormerlySerializedAs("Minimap.DisplaySize")]
            public float DisplaySize = 260f;

            [InspectorName("缩放系数")]
            [Range(MinimapFeature.MinimumZoom, MinimapFeature.MaximumZoom)]
            [FormerlySerializedAs("Minimap.Zoom")]
            public float Zoom = 1f;

            [InspectorName("地图方向")]
            [FormerlySerializedAs("Minimap.Mode")]
            public MinimapFeature.OrientationMode Mode = MinimapFeature.OrientationMode.FixedAngle;

            [InspectorName("缩小按键")]
            [FormerlySerializedAs("Minimap.ZoomOutKey")]
            public Key ZoomOutKey = MinimapFeature.DefaultZoomOutKey;

            [InspectorName("放大按键")]
            [FormerlySerializedAs("Minimap.ZoomInKey")]
            public Key ZoomInKey = MinimapFeature.DefaultZoomInKey;

            [InspectorName("不透明度")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("Minimap.Opacity")]
            public float Opacity = 0.7f;
        }

        [SerializeField, InspectorName("显示物品价格")]
        private DisplayPriceOptions displayPrice = new DisplayPriceOptions();

        [SerializeField, InspectorName("显示库存数量")]
        private StorageCountOptions storageCount = new StorageCountOptions();

        [SerializeField, InspectorName("显示物品品质")]
        private DisplayQualityOptions displayQuality = new DisplayQualityOptions();

        [SerializeField, InspectorName("战利品轮廓")]
        private LootOutlineOptions lootOutline = new LootOutlineOptions();

        [SerializeField, InspectorName("仓库排序按钮")]
        private InventorySortOptions inventorySort = new InventorySortOptions();

        [SerializeField, InspectorName("自动关闭背包")]
        private AutoCloseOptions autoCloseBackpack = new AutoCloseOptions();

        [SerializeField, InspectorName("瞄准时淡出 HUD")]
        private FadeHudOptions fadeHud = new FadeHudOptions();

        [SerializeField, InspectorName("弹药量准星颜色")]
        private CrosshairColorOptions crosshairColor = new CrosshairColorOptions();

        [SerializeField, InspectorName("游戏失去焦点时")]
        private UnfocusedOptions unfocused = new UnfocusedOptions();

        [SerializeField, InspectorName("低生命值屏幕阴影")]
        private LowHealthShadowOptions lowHealthShadow = new LowHealthShadowOptions();

        [SerializeField, InspectorName("击杀记录")]
        private KillRecordOptions killRecord = new KillRecordOptions();

        [SerializeField, InspectorName("小地图")]
        private MinimapOptions minimap = new MinimapOptions();

        private FeatureHost? _host;
        private DisplayPriceFeature? _displayPriceFeature;
        private DisplayStorageCount? _storageCountFeature;
        private DisplayQualityFeature? _displayQualityFeature;
        private LootboxOutlineFeature? _lootOutlineFeature;
        private InventorySortButtonsFeature? _inventorySortFeature;
        private AutoCloseBackpackFeature? _autoCloseFeature;
        private AutoFadeHudWhenAimingFeature? _fadeHudFeature;
        private BulletCountCrosshairColorFeature? _crosshairFeature;
        private MuteAndPauseWhenUnfocusedFeature? _unfocusedFeature;
        private LowHealthInnerShadowFeature? _lowHealthFeature;
        private KillRecordFeature? _killRecordFeature;
        private MinimapFeature? _minimapFeature;

        public void Initialize(
            FeatureHost host,
            DisplayPriceFeature displayPriceFeature,
            DisplayStorageCount storageCountFeature,
            DisplayQualityFeature displayQualityFeature,
            LootboxOutlineFeature lootOutlineFeature,
            InventorySortButtonsFeature inventorySortFeature,
            AutoCloseBackpackFeature autoCloseFeature,
            AutoFadeHudWhenAimingFeature fadeHudFeature,
            BulletCountCrosshairColorFeature crosshairFeature,
            MuteAndPauseWhenUnfocusedFeature unfocusedFeature,
            LowHealthInnerShadowFeature lowHealthFeature,
            KillRecordFeature killRecordFeature,
            MinimapFeature minimapFeature)
        {
            _host = host;
            _displayPriceFeature = displayPriceFeature;
            _storageCountFeature = storageCountFeature;
            _displayQualityFeature = displayQualityFeature;
            _lootOutlineFeature = lootOutlineFeature;
            _inventorySortFeature = inventorySortFeature;
            _autoCloseFeature = autoCloseFeature;
            _fadeHudFeature = fadeHudFeature;
            _crosshairFeature = crosshairFeature;
            _unfocusedFeature = unfocusedFeature;
            _lowHealthFeature = lowHealthFeature;
            _killRecordFeature = killRecordFeature;
            _minimapFeature = minimapFeature;
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

            _storageCountFeature!.DisplayItemCountInBackpack = storageCount.Backpack;
            _storageCountFeature.DisplayItemCountInRepository = storageCount.Repository;
            _host.SetEnabled(_storageCountFeature, storageCount.Enabled);

            _displayQualityFeature!.Mode = displayQuality.Mode;
            _host.SetEnabled(_displayQualityFeature, displayQuality.Enabled);

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
        }

        private void DuckovModSettingsUpdated()
        {
            OnValidate();
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
                killRecord.Format = "击杀 {0}";
            }
            minimap.DisplaySize = Mathf.Clamp(minimap.DisplaySize, 100f, 600f);
            minimap.Zoom = Mathf.Clamp(minimap.Zoom, MinimapFeature.MinimumZoom, MinimapFeature.MaximumZoom);
            minimap.Opacity = Mathf.Clamp01(minimap.Opacity);
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
