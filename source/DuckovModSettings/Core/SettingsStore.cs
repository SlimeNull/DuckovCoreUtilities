using Duckov.Modding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DuckovModBehaviour = Duckov.Modding.ModBehaviour;

namespace SlimeNull.DuckovModSettings.Core
{
    internal static class SettingsStore
    {
        private const int CurrentVersion = 2;
        private const string FolderName = "DuckovModSettings";
        private const string FileName = "settings.json";

        private static readonly Dictionary<string, Dictionary<string, JToken>> Values =
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
            Type valueType,
            out object? value)
        {
            EnsureLoaded();
            if (TryFindToken(Values, info, key, out var token) &&
                SettingValueCodec.TryFromToken(token, valueType, out value))
            {
                return true;
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

        public static void ApplyPersistedValues(DuckovModBehaviour root)
        {
            EnsureLoaded();
            if (root == null || !TryGetModValues(Values, root.info, out var settings))
            {
                return;
            }

            var assembly = root.GetType().Assembly;
            MonoBehaviour[] components;
            try
            {
                components = root.GetComponents<MonoBehaviour>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovModSettings] Could not restore settings for '{root.info.name}': {ex.Message}");
                return;
            }

            var mod = new ModSettingsModel(root);
            foreach (var component in components)
            {
                if (component == null || component.GetType().Assembly != assembly)
                {
                    continue;
                }

                var componentName = component.GetType().FullName ?? component.GetType().Name;
                var prefix = componentName + ".";
                var changed = false;
                foreach (var pair in settings)
                {
                    if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal) ||
                        !TryResolvePath(component.GetType(), pair.Key.Substring(prefix.Length), out var path, out var valueType) ||
                        !SettingValueCodec.IsSupportedLeaf(valueType) ||
                        !SettingValueCodec.TryFromToken(pair.Value, valueType, out var value) ||
                        !path.TryGetValue(component, out var current) ||
                        SettingValueCodec.ValuesEqual(current, value, valueType))
                    {
                        continue;
                    }

                    changed |= path.TrySetValue(component, value);
                }

                if (changed)
                {
                    new ComponentSettingsModel(mod, component).InvokeOnValidate();
                }
            }
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

        private static bool TryGetModValues(
            Dictionary<string, Dictionary<string, JToken>> source,
            ModInfo info,
            out Dictionary<string, JToken> settings)
        {
            var exactId = BuildModId(info);
            if (source.TryGetValue(exactId, out settings!))
            {
                return true;
            }

            var prefix = $"name:{info.name};publishedFileId:";
            foreach (var candidate in source)
            {
                if (candidate.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    settings = candidate.Value;
                    return true;
                }
            }

            settings = null!;
            return false;
        }

        private static bool TryResolvePath(
            Type rootType,
            string memberPath,
            out ReflectionPath path,
            out Type valueType)
        {
            var steps = new List<ReflectionStep>();
            var currentType = rootType;
            foreach (var memberName in memberPath.Split('.'))
            {
                var member = FindMember(currentType, memberName);
                if (member == null)
                {
                    path = null!;
                    valueType = null!;
                    return false;
                }

                steps.Add(new ReflectionStep(member));
                currentType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
            }

            if (steps.Count == 0)
            {
                path = null!;
                valueType = null!;
                return false;
            }

            path = new ReflectionPath(steps);
            valueType = currentType;
            return true;
        }

        private static MemberInfo? FindMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type? current = type;
                current != null && current != typeof(object) && current != typeof(MonoBehaviour) &&
                current != typeof(Behaviour) && current != typeof(Component) &&
                current != typeof(UnityEngine.Object) && current != typeof(DuckovModBehaviour);
                current = current.BaseType)
            {
                var field = current.GetField(name, flags);
                if (field != null && !field.IsStatic && !field.IsInitOnly &&
                    !field.IsDefined(typeof(HideInInspector), inherit: true) &&
                    (field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: true) ||
                        field.IsDefined(typeof(SerializeReference), inherit: true)))
                {
                    return field;
                }

                var property = current.GetProperty(name, flags);
                if (property?.GetIndexParameters().Length == 0 &&
                    !property.IsDefined(typeof(HideInInspector), inherit: true) &&
                    property.GetGetMethod(nonPublic: false) is MethodInfo getter &&
                    property.GetSetMethod(nonPublic: false) is MethodInfo setter &&
                    !getter.IsStatic && !setter.IsStatic)
                {
                    return property;
                }
            }

            return null;
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
