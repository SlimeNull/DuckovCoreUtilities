using Duckov;
using Duckov.UI;
using FMODUnity;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.IO;
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
        private AudioObject? _localAudioObject;

        private readonly string[] _localFilePaths = new string[QualityCount];
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

        public void ConfigureQuality(int quality, string? localFilePath, string? eventPath, float volume)
        {
            if (quality < 0 || quality >= QualityCount)
            {
                throw new ArgumentOutOfRangeException(nameof(quality));
            }

            _localFilePaths[quality] = localFilePath ?? string.Empty;
            _eventPaths[quality] = eventPath?.Trim() ?? string.Empty;
            _volumes[quality] = Mathf.Max(0f, volume);
        }

        protected override void OnEnable()
        {
            var audioObject = new GameObject("DCU_ItemSearchSoundPlayer");
            audioObject.transform.SetParent(Context.HostObject.transform, false);
            _localAudioObject = audioObject.AddComponent<AudioObject>();
            _active = this;
            Context.Harmony.PatchCategory(HarmonyCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            _active = null;
            if (_localAudioObject != null)
            {
                UnityEngine.Object.Destroy(_localAudioObject.gameObject);
                _localAudioObject = null;
            }
            _lastPlayedItem = null;
            _lastPlayedFrame = -1;
        }

        private void Play(Item item)
        {
            if (ReferenceEquals(_lastPlayedItem, item) && _lastPlayedFrame == Time.frameCount)
            {
                return;
            }

            _lastPlayedItem = item;
            _lastPlayedFrame = Time.frameCount;

            var quality = Mathf.Clamp(item.Quality, 0, QualityCount - 1);
            if (TryPlayLocal(_localFilePaths[quality], _volumes[quality]))
            {
                return;
            }

            PlayFmod(quality);
        }

        private bool TryPlayLocal(string path, float volume)
        {
            if (_localAudioObject == null || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return false;
            }

            if (!File.Exists(fullPath) || !IsSupportedAudioFile(fullPath))
            {
                return false;
            }

            try
            {
                var instance = _localAudioObject.PostCustomSFX(fullPath, doRelease: false);
                if (!instance.HasValue || !instance.Value.isValid())
                {
                    return false;
                }

                var eventInstance = instance.Value;
                eventInstance.setVolume(Mathf.Max(0f, volume));
                eventInstance.release();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CoreUtilities] Failed to play local item search sound '{fullPath}': {ex.Message}");
                return false;
            }
        }

        private static bool IsSupportedAudioFile(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".wav":
                case ".ogg":
                case ".mp3":
                case ".aif":
                case ".aiff":
                    return true;
                default:
                    return false;
            }
        }

        private void PlayFmod(int quality)
        {
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
