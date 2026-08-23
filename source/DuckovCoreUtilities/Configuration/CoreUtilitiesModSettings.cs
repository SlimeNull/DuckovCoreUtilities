using Duckov.Modding;
using ModSetting.Api;
using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Configuration
{
    internal sealed class CoreUtilitiesModSettings
    {
        private readonly SettingsBuilder _builder;
        private readonly FeatureHost _host;

        public CoreUtilitiesModSettings(ModInfo modInfo, FeatureHost host)
        {
            _builder = SettingsBuilder.Create(modInfo) ?? throw new InvalidOperationException("ModSetting is not available.");
            _host = host;
        }

        public void Configure(
            DisplayPriceFeature displayPrice,
            DisplayStorageCount displayStorageCount,
            DisplayQualityFeature displayQuality,
            LootboxOutlineFeature lootOutline,
            InventorySortButtonsFeature inventorySort,
            AutoCloseBackpackFeature autoCloseBackpack,
            AutoFadeHudWhenAimingFeature fadeHud,
            BulletCountCrosshairColorFeature crosshairColor,
            MuteAndPauseWhenUnfocusedFeature unfocused,
            LowHealthInnerShadowFeature lowHealthShadow,
            KillRecordFeature killRecord)
        {
            ConfigureDisplayPrice(displayPrice);
            ConfigureStorageCount(displayStorageCount);
            ConfigureFeatureOnly("DisplayQuality", "显示物品品质", displayQuality);
            ConfigureLootOutline(lootOutline);
            ConfigureFeatureOnly("InventorySort", "仓库排序按钮", inventorySort);
            ConfigureAutoCloseBackpack(autoCloseBackpack);
            ConfigureFadeHud(fadeHud);
            ConfigureCrosshairColor(crosshairColor);
            ConfigureUnfocused(unfocused);
            ConfigureLowHealthShadow(lowHealthShadow);
            ConfigureKillRecord(killRecord);
        }

        private void ConfigureDisplayPrice(DisplayPriceFeature feature)
        {
            const string prefix = "DisplayPrice";
            var mode = Load(prefix + ".Mode", feature.Mode.ToString());
            if (Enum.TryParse(mode, out DisplayPriceFeature.DisplayMode parsedMode))
            {
                feature.Mode = parsedMode;
            }

            AddEnabled(prefix, "显示物品价格", feature);
            _builder.AddDropdownList(prefix + ".Mode", "价格类型",
                new List<string> { nameof(DisplayPriceFeature.DisplayMode.SellPrice), nameof(DisplayPriceFeature.DisplayMode.RawPrice) },
                feature.Mode.ToString(), value =>
                {
                    if (Enum.TryParse(value, out DisplayPriceFeature.DisplayMode parsed))
                    {
                        feature.Mode = parsed;
                    }
                });
            AddGroup(prefix, "显示物品价格", prefix + ".Enabled", prefix + ".Mode");
        }

        private void ConfigureStorageCount(DisplayStorageCount feature)
        {
            const string prefix = "DisplayStorageCount";
            feature.DisplayItemCountInBackpack = Load(prefix + ".Backpack", feature.DisplayItemCountInBackpack);
            feature.DisplayItemCountInRepository = Load(prefix + ".Repository", feature.DisplayItemCountInRepository);
            AddEnabled(prefix, "显示库存数量", feature);
            _builder
                .AddToggle(prefix + ".Backpack", "显示背包内数量", feature.DisplayItemCountInBackpack, value => feature.DisplayItemCountInBackpack = value)
                .AddToggle(prefix + ".Repository", "显示仓库内数量", feature.DisplayItemCountInRepository, value => feature.DisplayItemCountInRepository = value);
            AddGroup(prefix, "显示库存数量", prefix + ".Enabled", prefix + ".Backpack", prefix + ".Repository");
        }

        private void ConfigureLootOutline(LootboxOutlineFeature feature)
        {
            const string prefix = "LootOutline";
            feature.EnableLootboxOutline = Load(prefix + ".Lootboxes", feature.EnableLootboxOutline);
            feature.EnableGroundItemOutline = Load(prefix + ".GroundItems", feature.EnableGroundItemOutline);
            feature.UseQualityColor = Load(prefix + ".QualityColor", feature.UseQualityColor);
            feature.LootboxBreathingEffect = Load(prefix + ".LootboxBreathing", feature.LootboxBreathingEffect);
            feature.GroundItemBreathingEffect = Load(prefix + ".GroundItemBreathing", feature.GroundItemBreathingEffect);
            feature.BreathingPeriod = Load(prefix + ".BreathingPeriod", feature.BreathingPeriod);
            feature.BreathingMinAlpha = Load(prefix + ".BreathingMinAlpha", feature.BreathingMinAlpha);

            AddEnabled(prefix, "战利品轮廓", feature);
            _builder
                .AddToggle(prefix + ".Lootboxes", "显示战利品箱轮廓", feature.EnableLootboxOutline, value => feature.EnableLootboxOutline = value)
                .AddToggle(prefix + ".GroundItems", "显示地面物品轮廓", feature.EnableGroundItemOutline, value => feature.EnableGroundItemOutline = value)
                .AddToggle(prefix + ".QualityColor", "使用物品品质颜色", feature.UseQualityColor, value => feature.UseQualityColor = value)
                .AddToggle(prefix + ".LootboxBreathing", "战利品箱呼吸效果", feature.LootboxBreathingEffect, value => feature.LootboxBreathingEffect = value)
                .AddToggle(prefix + ".GroundItemBreathing", "地面物品呼吸效果", feature.GroundItemBreathingEffect, value => feature.GroundItemBreathingEffect = value)
                .AddSlider(prefix + ".BreathingPeriod", "呼吸周期（秒）", feature.BreathingPeriod, new Vector2(0.1f, 5f), value => feature.BreathingPeriod = value, 2)
                .AddSlider(prefix + ".BreathingMinAlpha", "呼吸效果最低透明度", feature.BreathingMinAlpha, new Vector2(0f, 1f), value => feature.BreathingMinAlpha = value, 2);
            AddGroup(prefix, "战利品轮廓", prefix + ".Enabled", prefix + ".Lootboxes", prefix + ".GroundItems",
                prefix + ".QualityColor", prefix + ".LootboxBreathing", prefix + ".GroundItemBreathing",
                prefix + ".BreathingPeriod", prefix + ".BreathingMinAlpha");
        }

        private void ConfigureAutoCloseBackpack(AutoCloseBackpackFeature feature)
        {
            const string prefix = "AutoCloseBackpack";
            feature.WhenMove = Load(prefix + ".WhenMove", feature.WhenMove);
            feature.WhenHurt = Load(prefix + ".WhenHurt", feature.WhenHurt);
            AddEnabled(prefix, "自动关闭背包", feature);
            _builder
                .AddToggle(prefix + ".WhenMove", "移动时关闭", feature.WhenMove, value => feature.WhenMove = value)
                .AddToggle(prefix + ".WhenHurt", "受伤时关闭", feature.WhenHurt, value => feature.WhenHurt = value);
            AddGroup(prefix, "自动关闭背包", prefix + ".Enabled", prefix + ".WhenMove", prefix + ".WhenHurt");
        }

        private void ConfigureFadeHud(AutoFadeHudWhenAimingFeature feature)
        {
            const string prefix = "FadeHud";
            feature.TargetAlpha = Load(prefix + ".TargetAlpha", feature.TargetAlpha);
            feature.SmoothTime = Load(prefix + ".SmoothTime", feature.SmoothTime);
            AddEnabled(prefix, "瞄准时淡出 HUD", feature);
            _builder
                .AddSlider(prefix + ".TargetAlpha", "瞄准时 HUD 透明度", feature.TargetAlpha, new Vector2(0f, 1f), value => feature.TargetAlpha = value, 2)
                .AddSlider(prefix + ".SmoothTime", "淡入淡出时间（秒）", feature.SmoothTime, new Vector2(0.01f, 1f), value => feature.SmoothTime = value, 2);
            AddGroup(prefix, "瞄准时淡出 HUD", prefix + ".Enabled", prefix + ".TargetAlpha", prefix + ".SmoothTime");
        }

        private void ConfigureCrosshairColor(BulletCountCrosshairColorFeature feature)
        {
            const string prefix = "CrosshairColor";
            feature.WarnRatio = Load(prefix + ".WarnRatio", feature.WarnRatio);
            AddEnabled(prefix, "弹药量准星颜色", feature);
            _builder.AddSlider(prefix + ".WarnRatio", "低弹药警告比例", feature.WarnRatio, new Vector2(0f, 1f), value => feature.WarnRatio = value, 2);
            AddGroup(prefix, "弹药量准星颜色", prefix + ".Enabled", prefix + ".WarnRatio");
        }

        private void ConfigureUnfocused(MuteAndPauseWhenUnfocusedFeature feature)
        {
            const string prefix = "Unfocused";
            feature.MuteWhenUnfocused = Load(prefix + ".Mute", feature.MuteWhenUnfocused);
            feature.PauseWhenUnfocused = Load(prefix + ".Pause", feature.PauseWhenUnfocused);
            AddEnabled(prefix, "游戏失去焦点时", feature);
            _builder
                .AddToggle(prefix + ".Mute", "游戏失去焦点时静音", feature.MuteWhenUnfocused, value => feature.MuteWhenUnfocused = value)
                .AddToggle(prefix + ".Pause", "游戏失去焦点时暂停", feature.PauseWhenUnfocused, value => feature.PauseWhenUnfocused = value);
            AddGroup(prefix, "游戏失去焦点时", prefix + ".Enabled", prefix + ".Mute", prefix + ".Pause");
        }

        private void ConfigureLowHealthShadow(LowHealthInnerShadowFeature feature)
        {
            const string prefix = "LowHealthShadow";
            feature.ShadowDistance = Load(prefix + ".Distance", feature.ShadowDistance);
            feature.HealthThresholdUpper = Load(prefix + ".UpperThreshold", feature.HealthThresholdUpper);
            feature.HealthThresholdLower = Load(prefix + ".LowerThreshold", feature.HealthThresholdLower);
            AddEnabled(prefix, "低生命值屏幕阴影", feature);
            _builder
                .AddSlider(prefix + ".Distance", "阴影宽度", feature.ShadowDistance, new Vector2(0f, 400f), value => feature.ShadowDistance = value, 0, 4)
                .AddSlider(prefix + ".UpperThreshold", "开始显示的生命比例", feature.HealthThresholdUpper, new Vector2(0f, 1f), value => feature.HealthThresholdUpper = value, 2)
                .AddSlider(prefix + ".LowerThreshold", "达到最深效果的生命比例", feature.HealthThresholdLower, new Vector2(0f, 1f), value => feature.HealthThresholdLower = value, 2);
            AddGroup(prefix, "低生命值屏幕阴影", prefix + ".Enabled", prefix + ".Distance", prefix + ".UpperThreshold", prefix + ".LowerThreshold");
        }

        private void ConfigureKillRecord(KillRecordFeature feature)
        {
            const string prefix = "KillRecord";
            feature.RecordDuration = Load(prefix + ".Duration", feature.RecordDuration);
            feature.MaxRecordCount = Load(prefix + ".MaxCount", feature.MaxRecordCount);
            var recordFormat = Load(prefix + ".Format", feature.RecordFormat);
            if (IsValidRecordFormat(recordFormat))
            {
                feature.RecordFormat = recordFormat;
            }
            AddEnabled(prefix, "击杀记录", feature);
            _builder
                .AddSlider(prefix + ".Duration", "记录显示时间（秒）", feature.RecordDuration, new Vector2(1f, 30f), value => feature.RecordDuration = value, 1)
                .AddSlider(prefix + ".MaxCount", "最多显示条数", feature.MaxRecordCount, 1, 20, value => feature.MaxRecordCount = value)
                .AddInput(prefix + ".Format", "记录文本格式，{0} 代表目标名称", feature.RecordFormat, 80, value =>
                {
                    if (IsValidRecordFormat(value))
                    {
                        feature.RecordFormat = value;
                    }
                });
            AddGroup(prefix, "击杀记录", prefix + ".Enabled", prefix + ".Duration", prefix + ".MaxCount", prefix + ".Format");
        }

        private static bool IsValidRecordFormat(string value)
        {
            try
            {
                string.Format(value, "target");
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private void ConfigureFeatureOnly(string prefix, string description, FeatureBase feature)
        {
            AddEnabled(prefix, description, feature);
            AddGroup(prefix, description, prefix + ".Enabled");
        }

        private void AddEnabled(string prefix, string description, FeatureBase feature)
        {
            var enabled = Load(prefix + ".Enabled", true);
            _builder.AddToggle(prefix + ".Enabled", "启用" + description, enabled, value => _host.SetEnabled(feature, value));
            _host.SetEnabled(feature, enabled);
        }

        private void AddGroup(string key, string description, params string[] children)
        {
            _builder.AddGroup(key + ".Group", description, new List<string>(children), open: false);
        }

        private T Load<T>(string key, T fallback)
        {
            return _builder.GetSavedValue<T>(key, out var value) ? value : fallback;
        }
    }
}
