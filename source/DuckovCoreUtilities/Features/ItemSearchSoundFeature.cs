using Duckov.UI;
using FMODUnity;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class ItemSearchSoundFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(ItemSearchSoundFeature);
        private const int QualityCount = 7;
        private const string LocalSoundBusPath = "bus:/Master/SFX";

        private sealed class LocalPlayback
        {
            public int Quality { get; }
            public FMOD.Channel Channel { get; }

            public LocalPlayback(int quality, FMOD.Channel channel)
            {
                Quality = quality;
                Channel = channel;
            }
        }

        private static ItemSearchSoundFeature? _active;

        private Item? _lastPlayedItem;
        private int _lastPlayedFrame = -1;

        private readonly string[] _localFilePaths = new string[QualityCount];
        private readonly string[] _loadedLocalFilePaths = new string[QualityCount];
        private readonly FMOD.Sound[] _localSounds = new FMOD.Sound[QualityCount];
        private readonly List<LocalPlayback> _localPlaybacks = new List<LocalPlayback>();
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

            var nextLocalFilePath = localFilePath ?? string.Empty;
            if (!string.Equals(_localFilePaths[quality], nextLocalFilePath, StringComparison.Ordinal))
            {
                ReleaseLocalSound(quality);
                _localFilePaths[quality] = nextLocalFilePath;
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
            ReleaseAllLocalSounds();
            _lastPlayedItem = null;
            _lastPlayedFrame = -1;
        }

        public override void Tick()
        {
            for (var index = _localPlaybacks.Count - 1; index >= 0; index--)
            {
                var channel = _localPlaybacks[index].Channel;
                var result = channel.isPlaying(out var isPlaying);
                if (result != FMOD.RESULT.OK || !isPlaying)
                {
                    _localPlaybacks.RemoveAt(index);
                }
            }
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
            if (TryPlayLocal(quality, _localFilePaths[quality], _volumes[quality]))
            {
                return;
            }

            PlayFmod(quality);
        }

        private bool TryPlayLocal(int quality, string path, float volume)
        {
            if (string.IsNullOrWhiteSpace(path))
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
                if (!TryGetLocalSound(quality, fullPath, out var sound))
                {
                    return false;
                }

                var bus = RuntimeManager.GetBus(LocalSoundBusPath);
                var result = bus.getChannelGroup(out var channelGroup);
                if (!CheckFmodResult(result, "get the SFX channel group", fullPath))
                {
                    return false;
                }

                var coreSystem = RuntimeManager.CoreSystem;
                result = coreSystem.playSound(sound, channelGroup, paused: true, out var channel);
                if (!CheckFmodResult(result, "create a playback channel", fullPath))
                {
                    return false;
                }

                result = channel.setVolume(Mathf.Max(0f, volume));
                if (!CheckFmodResult(result, "set playback volume", fullPath))
                {
                    channel.stop();
                    return false;
                }

                result = channel.setPaused(paused: false);
                if (!CheckFmodResult(result, "start playback", fullPath))
                {
                    channel.stop();
                    return false;
                }

                result = channel.isPlaying(out var isPlaying);
                if (!CheckFmodResult(result, "verify playback", fullPath) || !isPlaying)
                {
                    channel.stop();
                    Debug.LogWarning($"[CoreUtilities] FMOD did not start local item search sound '{fullPath}'.");
                    return false;
                }

                _localPlaybacks.Add(new LocalPlayback(quality, channel));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CoreUtilities] Failed to play local item search sound '{fullPath}': {ex.Message}");
                return false;
            }
        }

        private bool TryGetLocalSound(int quality, string fullPath, out FMOD.Sound sound)
        {
            sound = _localSounds[quality];
            if (sound.hasHandle() &&
                string.Equals(_loadedLocalFilePaths[quality], fullPath, StringComparison.Ordinal))
            {
                return true;
            }

            ReleaseLocalSound(quality);

            var coreSystem = RuntimeManager.CoreSystem;
            var result = coreSystem.createSound(
                fullPath,
                FMOD.MODE.LOOP_OFF | FMOD.MODE._2D,
                out sound);
            if (!CheckFmodResult(result, "load the audio file", fullPath) || !sound.hasHandle())
            {
                sound = default;
                return false;
            }

            _localSounds[quality] = sound;
            _loadedLocalFilePaths[quality] = fullPath;
            return true;
        }

        private void ReleaseLocalSound(int quality)
        {
            var sound = _localSounds[quality];
            if (!sound.hasHandle())
            {
                _loadedLocalFilePaths[quality] = string.Empty;
                return;
            }

            for (var index = _localPlaybacks.Count - 1; index >= 0; index--)
            {
                if (_localPlaybacks[index].Quality != quality)
                {
                    continue;
                }

                _localPlaybacks[index].Channel.stop();
                _localPlaybacks.RemoveAt(index);
            }

            var result = sound.release();
            if (result != FMOD.RESULT.OK && result != FMOD.RESULT.ERR_INVALID_HANDLE)
            {
                Debug.LogWarning(
                    $"[CoreUtilities] Failed to release local item search sound " +
                    $"'{_loadedLocalFilePaths[quality]}': {result} ({FMOD.Error.String(result)})");
            }

            _localSounds[quality] = default;
            _loadedLocalFilePaths[quality] = string.Empty;
        }

        private void ReleaseAllLocalSounds()
        {
            for (var quality = 0; quality < QualityCount; quality++)
            {
                ReleaseLocalSound(quality);
            }

            _localPlaybacks.Clear();
        }

        private static bool CheckFmodResult(FMOD.RESULT result, string operation, string fullPath)
        {
            if (result == FMOD.RESULT.OK)
            {
                return true;
            }

            Debug.LogWarning(
                $"[CoreUtilities] Failed to {operation} for local item search sound '{fullPath}': " +
                $"{result} ({FMOD.Error.String(result)})");
            return false;
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
