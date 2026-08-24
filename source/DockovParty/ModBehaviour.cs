using HarmonyLib;
using SlimeNull.DockovParty.Configuration;
using SlimeNull.DockovParty.Game;
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
                _settings = gameObject.GetComponent<PartySettings>() ?? gameObject.AddComponent<PartySettings>();

                _runtime = gameObject.AddComponent<PartyRuntime>();
                _runtime.Initialize(_settings);

                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(ModBehaviour).Assembly);
                Debug.Log("[DockovParty] 模组已加载");
            }
            catch (Exception ex)
            {
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
    }
}
