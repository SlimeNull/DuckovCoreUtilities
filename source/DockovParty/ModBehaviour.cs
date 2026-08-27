using HarmonyLib;
using SlimeNull.DockovParty.Configuration;
using SlimeNull.DockovParty.Game;
using SlimeNull.DockovParty.Localization;
using SlimeNull.Mods.Localization;
using SodaCraft.Localizations;
using System;
using UnityEngine;

namespace SlimeNull.DockovParty
{
    public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string HarmonyId = "SlimeNull.DockovParty";
        private Harmony? _harmony;
        private PartyRuntime? _runtime;
        private PartySettings? _settings;

        protected override void OnAfterSetup()
        {
            try
            {
                var language = LocalizationManager.Initialized
                    ? LocalizationManager.CurrentLanguage
                    : Application.systemLanguage;
                SettingsText.Culture = ModLanguage.GetCulture(language);
                LocalizationManager.OnSetLanguage += OnLanguageChanged;
                _settings = gameObject.GetComponent<PartySettings>() ?? gameObject.AddComponent<PartySettings>();

                _runtime = gameObject.AddComponent<PartyRuntime>();
                _runtime.Initialize(_settings);

                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(ModBehaviour).Assembly);
                Debug.Log("[DockovParty] 模组已加载");
            }
            catch (Exception ex)
            {
                LocalizationManager.OnSetLanguage -= OnLanguageChanged;
                Debug.LogError($"[DockovParty] 初始化失败: {ex}");
                _harmony?.UnpatchAll(HarmonyId);
                if (_runtime != null)
                {
                    Destroy(_runtime);
                }

                if (_settings != null)
                {
                    Destroy(_settings);
                }

                _runtime = null;
                _settings = null;
                _harmony = null;
            }
        }

        protected override void OnBeforeDeactivate()
        {
            LocalizationManager.OnSetLanguage -= OnLanguageChanged;
            _runtime?.StopSession();
            _harmony?.UnpatchAll(HarmonyId);
            if (_runtime != null)
            {
                Destroy(_runtime);
            }

            if (_settings != null)
            {
                Destroy(_settings);
            }

            _runtime = null;
            _settings = null;
            _harmony = null;
        }

        private void OnLanguageChanged(SystemLanguage language)
        {
            SettingsText.Culture = ModLanguage.GetCulture(language);
            _runtime?.RefreshLocalization();
        }
    }
}
