using HarmonyLib;
using SlimeNull.DuckovCustomDeath.Configuration;
using SlimeNull.DuckovCustomDeath.Gameplay;
using SlimeNull.DuckovCustomDeath.Localization;
using SlimeNull.Mods.Localization;
using SodaCraft.Localizations;
using System;
using UnityEngine;

namespace SlimeNull.DuckovCustomDeath
{
    public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string HarmonyId = "SlimeNull.DuckovCustomDeath";

        private Harmony? _harmony;
        private CustomDeathSettings? _settings;

        protected override void OnAfterSetup()
        {
            if (_harmony != null)
            {
                return;
            }

            try
            {
                ApplyLanguage(LocalizationManager.Initialized
                    ? LocalizationManager.CurrentLanguage
                    : Application.systemLanguage);
                LocalizationManager.OnSetLanguage += OnLanguageChanged;

                _settings = gameObject.GetComponent<CustomDeathSettings>() ??
                    gameObject.AddComponent<CustomDeathSettings>();

                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(ModBehaviour).Assembly);
                Debug.Log("[DuckovCustomDeath] 模组已加载");
            }
            catch (Exception ex)
            {
                LocalizationManager.OnSetLanguage -= OnLanguageChanged;
                _harmony?.UnpatchAll(HarmonyId);
                if (_settings != null)
                {
                    Destroy(_settings);
                }

                _settings = null;
                _harmony = null;
                CustomDeathOptions.Reset();
                Debug.LogError($"[DuckovCustomDeath] 初始化失败: {ex}");
            }
        }

        protected override void OnBeforeDeactivate()
        {
            LocalizationManager.OnSetLanguage -= OnLanguageChanged;
            DeathInventoryController.RestorePending();
            _harmony?.UnpatchAll(HarmonyId);
            if (_settings != null)
            {
                Destroy(_settings);
            }

            _settings = null;
            _harmony = null;
            CustomDeathOptions.Reset();
        }

        private static void ApplyLanguage(SystemLanguage language)
        {
            var culture = ModLanguage.GetCulture(language);
            ModLanguage.PrepareResourceManager(SettingsText.ResourceManager, typeof(SettingsText).Assembly, culture);
            SettingsText.Culture = culture;
        }

        private void OnLanguageChanged(SystemLanguage language)
        {
            ApplyLanguage(language);
        }
    }
}
