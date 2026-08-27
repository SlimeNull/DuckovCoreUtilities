using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using SlimeNull.Mods.Localization;

namespace SlimeNull.DuckovModSettings.Core
{
    internal static class LocalizedText
    {
        private static readonly Dictionary<Assembly, AssemblyResources> ResourcesByAssembly =
            new Dictionary<Assembly, AssemblyResources>();

        private static CultureInfo _culture = CultureInfo.CurrentUICulture;

        public static CultureInfo Culture => _culture;

        public static string Resolve(string? text, Assembly assembly)
        {
            if (string.IsNullOrEmpty(text) || text![0] != '@')
            {
                return text ?? string.Empty;
            }

            if (!ResourcesByAssembly.TryGetValue(assembly, out var resources))
            {
                resources = new AssemblyResources(assembly, _culture);
                ResourcesByAssembly.Add(assembly, resources);
            }

            return resources.Resolve(text);
        }

        public static void SetCulture(CultureInfo culture)
        {
            _culture = culture;
            foreach (var resources in ResourcesByAssembly.Values)
            {
                resources.SetCulture(culture);
            }
        }

        private sealed class AssemblyResources
        {
            private const BindingFlags StaticFlags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            private readonly Assembly _assembly;
            private readonly Type[] _types;
            private readonly Dictionary<Type, ResourceAccessor?> _accessors =
                new Dictionary<Type, ResourceAccessor?>();
            private readonly Dictionary<string, string> _resolved =
                new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly List<ResourceAccessor> _createdAccessors = new List<ResourceAccessor>();
            private ResourceAccessor? _first;
            private bool _firstResolved;
            private CultureInfo _culture;

            public AssemblyResources(Assembly assembly, CultureInfo culture)
            {
                _assembly = assembly;
                _culture = culture;
                _types = GetLoadableTypes(assembly);
            }

            public string Resolve(string expression)
            {
                if (_resolved.TryGetValue(expression, out var cached))
                {
                    return cached;
                }

                var token = expression.Substring(1);
                var separator = token.IndexOf('/');
                var typeName = separator >= 0 ? token.Substring(0, separator).Trim() : null;
                var key = separator >= 0 ? token.Substring(separator + 1).Trim() : token.Trim();
                if (key.Length == 0 || (separator >= 0 && string.IsNullOrEmpty(typeName)))
                {
                    _resolved[expression] = expression;
                    return expression;
                }

                var accessor = typeName == null ? GetFirstAccessor() : GetNamedAccessor(typeName);
                var result = accessor?.GetString(key, _culture);
                if (result == null)
                {
                    result = expression;
                }

                _resolved[expression] = result;
                return result;
            }

            public void SetCulture(CultureInfo culture)
            {
                _culture = culture;
                _resolved.Clear();
                foreach (var accessor in _createdAccessors)
                {
                    accessor.SetCulture(culture);
                }
            }

            private ResourceAccessor? GetFirstAccessor()
            {
                if (_firstResolved)
                {
                    return _first;
                }

                _firstResolved = true;
                foreach (var type in _types)
                {
                    if (!HasResourceManager(type))
                    {
                        continue;
                    }

                    _first = GetAccessor(type, allowConstruction: false);
                    if (_first != null)
                    {
                        return _first;
                    }
                }

                foreach (var resourceName in GetManifestResourceNames(_assembly))
                {
                    if (!resourceName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var baseName = resourceName.Substring(0, resourceName.Length - ".resources".Length);
                    _first = AddAccessor(new ResourceAccessor(
                        new ResourceManager(baseName, _assembly),
                        null,
                        _assembly));
                    return _first;
                }

                return null;
            }

            private ResourceAccessor? GetNamedAccessor(string typeName)
            {
                var type = _assembly.GetType(typeName, throwOnError: false, ignoreCase: false) ??
                    _types.FirstOrDefault(candidate => string.Equals(candidate.Name, typeName, StringComparison.Ordinal));
                return type == null ? null : GetAccessor(type, allowConstruction: true);
            }

            private ResourceAccessor? GetAccessor(Type type, bool allowConstruction)
            {
                if (_accessors.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                ResourceManager? manager = null;
                try
                {
                    var property = FindResourceManagerProperty(type);
                    manager = property?.GetValue(null, null) as ResourceManager;
                    if (manager == null && allowConstruction)
                    {
                        manager = new ResourceManager(type);
                    }
                }
                catch
                {
                    manager = null;
                }

                var accessor = manager == null
                    ? null
                    : AddAccessor(new ResourceAccessor(manager, type, type.Assembly));
                _accessors[type] = accessor;
                return accessor;
            }

            private ResourceAccessor AddAccessor(ResourceAccessor accessor)
            {
                accessor.SetCulture(_culture);
                _createdAccessors.Add(accessor);
                return accessor;
            }

            private static bool HasResourceManager(Type type)
            {
                try
                {
                    return FindResourceManagerProperty(type) != null;
                }
                catch
                {
                    return false;
                }
            }

            private static PropertyInfo? FindResourceManagerProperty(Type type)
            {
                var named = type.GetProperty("ResourceManager", StaticFlags);
                if (named != null && named.GetIndexParameters().Length == 0 &&
                    typeof(ResourceManager).IsAssignableFrom(named.PropertyType))
                {
                    return named;
                }

                return type.GetProperties(StaticFlags).FirstOrDefault(property =>
                    property.GetIndexParameters().Length == 0 &&
                    typeof(ResourceManager).IsAssignableFrom(property.PropertyType));
            }

            private static Type[] GetLoadableTypes(Assembly assembly)
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(type => type != null).Cast<Type>().ToArray();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            }

            private static string[] GetManifestResourceNames(Assembly assembly)
            {
                try
                {
                    return assembly.GetManifestResourceNames();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            private sealed class ResourceAccessor
            {
                private readonly ResourceManager _manager;
                private readonly Assembly _assembly;
                private readonly PropertyInfo? _staticCultureProperty;
                private readonly FieldInfo? _staticCultureField;
                private string? _appliedCultureName;

                public ResourceAccessor(ResourceManager manager, Type? resourceType, Assembly assembly)
                {
                    _manager = manager;
                    _assembly = assembly;
                    if (resourceType == null)
                    {
                        return;
                    }

                    _staticCultureProperty = FindCultureProperty(resourceType);
                    if (_staticCultureProperty == null)
                    {
                        _staticCultureField = FindCultureField(resourceType);
                    }
                }

                public string? GetString(string key, CultureInfo culture)
                {
                    try
                    {
                        SetCulture(culture);
                        return _manager.GetString(key, culture);
                    }
                    catch
                    {
                        return null;
                    }
                }

                public void SetCulture(CultureInfo culture)
                {
                    if (string.Equals(_appliedCultureName, culture.Name, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _appliedCultureName = culture.Name;
                    try
                    {
                        ModLanguage.PrepareResourceManager(_manager, _assembly, culture);
                        _staticCultureProperty?.SetValue(null, culture, null);
                        _staticCultureField?.SetValue(null, culture);
                    }
                    catch
                    {
                        // Resource lookup still receives the culture explicitly.
                    }
                }

                private static PropertyInfo? FindCultureProperty(Type type)
                {
                    foreach (var name in new[] { "Culture", "ResourceCulture" })
                    {
                        var property = type.GetProperty(name, StaticFlags);
                        if (property?.CanWrite == true && property.GetIndexParameters().Length == 0 &&
                            typeof(CultureInfo).IsAssignableFrom(property.PropertyType))
                        {
                            return property;
                        }
                    }
                    return null;
                }

                private static FieldInfo? FindCultureField(Type type)
                {
                    foreach (var name in new[] { "resourceCulture", "Culture", "ResourceCulture" })
                    {
                        var field = type.GetField(name, StaticFlags);
                        if (field != null && !field.IsInitOnly && typeof(CultureInfo).IsAssignableFrom(field.FieldType))
                        {
                            return field;
                        }
                    }
                    return null;
                }
            }
        }
    }
}
