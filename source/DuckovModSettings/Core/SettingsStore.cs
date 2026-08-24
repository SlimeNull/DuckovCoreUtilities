using Duckov.Modding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SlimeNull.DuckovModSettings.Core
{
    internal static class SettingsStore
    {
        private const int CurrentVersion = 2;
        private const string FolderName = "DuckovModSettings";
        private const string FileName = "settings.json";
        private const string LegacyFolderName = "ModSetting";
        private const string LegacyFileName = "ModSetting.json";

        private static readonly Dictionary<string, Dictionary<string, JToken>> Values =
            new Dictionary<string, Dictionary<string, JToken>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, JToken>> LegacyValues =
            new Dictionary<string, Dictionary<string, JToken>>(StringComparer.Ordinal);

        private static bool _loaded;
        private static bool _dirty;
        private static float _dirtySince;

        public static string BuildModId(ModInfo info)
        {
            return $"name:{info.name};publishedFileId:{info.publishedFileId}";
        }

        public static bool TryGet(
            ModInfo info,
            string key,
            IEnumerable<string> aliases,
            Type valueType,
            out object? value)
        {
            EnsureLoaded();
            var candidates = new[] { key }.Concat(aliases ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (TryFindToken(Values, info, candidate, out var token) &&
                    SettingValueCodec.TryFromToken(token, valueType, out value))
                {
                    if (!string.Equals(candidate, key, StringComparison.Ordinal))
                    {
                        Set(info, key, value, valueType);
                    }
                    return true;
                }
            }

            foreach (var candidate in candidates)
            {
                if (TryFindToken(LegacyValues, info, candidate, out var token) &&
                    SettingValueCodec.TryFromToken(token, valueType, out value))
                {
                    Set(info, key, value, valueType);
                    return true;
                }
            }

            value = null;
            return false;
        }

        public static void Set(ModInfo info, string key, object? value, Type valueType)
        {
            EnsureLoaded();
            var modId = BuildModId(info);
            if (!Values.TryGetValue(modId, out var settings))
            {
                settings = new Dictionary<string, JToken>(StringComparer.Ordinal);
                Values.Add(modId, settings);
            }

            var token = SettingValueCodec.ToToken(value, valueType);
            if (settings.TryGetValue(key, out var previous) && JToken.DeepEquals(previous, token))
            {
                return;
            }

            settings[key] = token;
            MarkDirty();
        }

        public static void SaveIfDue(float now)
        {
            if (_dirty && now - _dirtySince >= 0.35f)
            {
                SaveNow();
            }
        }

        public static void SaveNow()
        {
            EnsureLoaded();
            if (!_dirty)
            {
                return;
            }

            var path = GetCurrentPath();
            var temporaryPath = path + ".tmp";
            var backupPath = path + ".bak";
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var mods = new JObject();
                foreach (var mod in Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    var settings = new JObject();
                    foreach (var setting in mod.Value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        settings[setting.Key] = setting.Value.DeepClone();
                    }
                    mods[mod.Key] = settings;
                }

                var root = new JObject
                {
                    ["version"] = CurrentVersion,
                    ["mods"] = mods,
                };

                File.WriteAllText(temporaryPath, root.ToString(Formatting.Indented));
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                if (File.Exists(path))
                {
                    File.Move(path, backupPath);
                }
                File.Move(temporaryPath, path);
                _dirty = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovModSettings] Failed to save settings: {ex}");
                TryDelete(temporaryPath);
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            LoadCurrent();
            LoadLegacy();
        }

        private static void LoadCurrent()
        {
            var path = GetCurrentPath();
            if (!TryLoadCurrentFile(path) && !TryLoadCurrentFile(path + ".bak"))
            {
                Values.Clear();
            }
        }

        private static bool TryLoadCurrentFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                if (root["mods"] is not JObject mods)
                {
                    return false;
                }

                Values.Clear();
                foreach (var modProperty in mods.Properties())
                {
                    if (modProperty.Value is not JObject settingObject)
                    {
                        continue;
                    }

                    var settings = new Dictionary<string, JToken>(StringComparer.Ordinal);
                    foreach (var settingProperty in settingObject.Properties())
                    {
                        settings[settingProperty.Name] = settingProperty.Value.DeepClone();
                    }
                    Values[modProperty.Name] = settings;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovModSettings] Could not read '{path}': {ex.Message}");
                return false;
            }
        }

        private static void LoadLegacy()
        {
            var path = Path.Combine(GetPersistentRoot(), LegacyFolderName, LegacyFileName);
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                if (root["configDatas"] is not JArray mods)
                {
                    return;
                }

                foreach (var modToken in mods.OfType<JObject>())
                {
                    var modId = modToken.Value<string>("modId");
                    if (string.IsNullOrWhiteSpace(modId) || modToken["allConfigDatas"] is not JArray settingsArray)
                    {
                        continue;
                    }

                    var settings = new Dictionary<string, JToken>(StringComparer.Ordinal);
                    foreach (var setting in settingsArray.OfType<JObject>())
                    {
                        var settingKey = setting.Value<string>("Key");
                        if (string.IsNullOrWhiteSpace(settingKey))
                        {
                            continue;
                        }

                        var settingValue = setting["Enable"] ?? setting["Value"] ?? setting["KeyCode"];
                        if (settingValue != null)
                        {
                            settings[settingKey] = settingValue.DeepClone();
                        }
                    }

                    LegacyValues[modId] = settings;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovModSettings] Could not import legacy settings: {ex.Message}");
            }
        }

        private static bool TryFindToken(
            Dictionary<string, Dictionary<string, JToken>> source,
            ModInfo info,
            string key,
            out JToken token)
        {
            var exactId = BuildModId(info);
            if (source.TryGetValue(exactId, out var exactSettings) && exactSettings.TryGetValue(key, out token!))
            {
                return true;
            }

            var prefix = $"name:{info.name};publishedFileId:";
            foreach (var candidate in source)
            {
                if (candidate.Key.StartsWith(prefix, StringComparison.Ordinal) && candidate.Value.TryGetValue(key, out token!))
                {
                    return true;
                }
            }

            token = null!;
            return false;
        }

        private static void MarkDirty()
        {
            if (!_dirty)
            {
                _dirtySince = Time.unscaledTime;
            }
            _dirty = true;
        }

        private static string GetCurrentPath()
        {
            return Path.Combine(GetPersistentRoot(), FolderName, FileName);
        }

        private static string GetPersistentRoot()
        {
            return string.IsNullOrWhiteSpace(Application.persistentDataPath)
                ? Directory.GetCurrentDirectory()
                : Application.persistentDataPath;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
