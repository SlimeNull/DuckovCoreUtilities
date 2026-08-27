using Duckov.UI;
using FMODUnity;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class ItemSearchSoundFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(ItemSearchSoundFeature);
        private const int QualityCount = 7;

        private static ItemSearchSoundFeature? _active;

        private Item? _lastPlayedItem;
        private int _lastPlayedFrame = -1;

        private readonly string[] _eventPaths =
        {
            "event:/UI/level_up",
            "event:/UI/click",
            "event:/UI/click",
            "event:/UI/confirm",
            "event:/UI/ui_skill_up",
            "event:/UI/level_up",
            "event:/UI/level_up",
        };

        private readonly float[] _volumes = { 8f, 1f, 3f, 3f, 1f, 2f, 8f };

        public override string Name => "Item search sounds";

        public void ConfigureQuality(int quality, string? eventPath, float volume)
        {
            if (quality < 0 || quality >= QualityCount)
            {
                throw new ArgumentOutOfRangeException(nameof(quality));
            }

            _eventPaths[quality] = eventPath?.Trim() ?? string.Empty;
            _volumes[quality] = Mathf.Max(0f, volume);
        }

        protected override void OnEnable()
        {
            _active = this;
            Context.Harmony.PatchCategory(HarmonyCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            _active = null;
        }

        private void Play(Item item)
        {
            if (ReferenceEquals(_lastPlayedItem, item) && _lastPlayedFrame == Time.frameCount)
            {
                return;
            }

            _lastPlayedItem = item;
            _lastPlayedFrame = Time.frameCount;

            var quality = item.Quality;
            quality = Mathf.Clamp(quality, 0, QualityCount - 1);
            var eventPath = _eventPaths[quality];
            if (string.IsNullOrWhiteSpace(eventPath))
            {
                return;
            }

            try
            {
                var instance = RuntimeManager.CreateInstance(eventPath);
                if (!instance.isValid())
                {
                    Debug.LogWarning($"[CoreUtilities] Invalid item search sound event: {eventPath}");
                    return;
                }

                instance.setVolume(_volumes[quality]);
                instance.start();
                instance.release();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CoreUtilities] Failed to play item search sound '{eventPath}': {ex.Message}");
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(ItemDisplay), "OnTargetInspectionStateChanged")]
        private static class ItemDisplayInspectionPatch
        {
            private static void Postfix(ItemDisplay __instance, Item item)
            {
                if (_active is { } feature &&
                    item != null &&
                    item == __instance.Target &&
                    item.Inspected &&
                    item.Inspecting)
                {
                    feature.Play(item);
                }
            }
        }
    }
}
