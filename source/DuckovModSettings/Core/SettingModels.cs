using Duckov.Modding;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using DuckovModBehaviour = Duckov.Modding.ModBehaviour;

namespace SlimeNull.DuckovModSettings.Core
{
    internal enum SettingNodeKind
    {
        Group,
        Boolean,
        Integer,
        FloatingPoint,
        String,
        Enum,
        Key,
        Color,
    }

    internal enum SettingChangeOrigin
    {
        Load,
        User,
        Reset,
        External,
    }

    internal sealed class SettingRange
    {
        public SettingRange(float minimum, float maximum)
        {
            Minimum = Mathf.Min(minimum, maximum);
            Maximum = Mathf.Max(minimum, maximum);
        }

        public float Minimum { get; }
        public float Maximum { get; }
    }

    internal sealed class TextAreaOptions
    {
        public TextAreaOptions(int minimumLines, int maximumLines)
        {
            MinimumLines = Math.Max(1, minimumLines);
            MaximumLines = Math.Max(MinimumLines, maximumLines);
        }

        public int MinimumLines { get; }
        public int MaximumLines { get; }
    }

    internal sealed class ModSettingsModel
    {
        private readonly List<ComponentSettingsModel> _components = new List<ComponentSettingsModel>();

        public ModSettingsModel(DuckovModBehaviour root)
        {
            Root = root;
            Info = root.info;
            Id = SettingsStore.BuildModId(Info);
        }

        public DuckovModBehaviour Root { get; }
        public ModInfo Info { get; }
        public string Id { get; }
        public string DisplayName => string.IsNullOrWhiteSpace(Info.displayName) ? Info.name : Info.displayName;
        public IReadOnlyList<ComponentSettingsModel> Components => _components;

        public void Add(ComponentSettingsModel component)
        {
            _components.Add(component);
        }
    }

    internal sealed class ComponentSettingsModel
    {
        private readonly List<SettingNode> _nodes = new List<SettingNode>();

        public ComponentSettingsModel(ModSettingsModel mod, MonoBehaviour target)
        {
            Mod = mod;
            Target = target;
            ComponentKey = target.GetType().FullName ?? target.GetType().Name;
            DisplayName = NameUtility.NicifyTypeName(target.GetType().Name);
        }

        public ModSettingsModel Mod { get; }
        public MonoBehaviour Target { get; }
        public string ComponentKey { get; }
        public string DisplayName { get; }
        public IReadOnlyList<SettingNode> Nodes => _nodes;

        public IEnumerable<SettingNode> Leaves => _nodes.SelectMany(node => node.SelfAndDescendants()).Where(node => node.IsValue);

        public void Add(SettingNode node)
        {
            _nodes.Add(node);
        }

        public void InvokeOnValidate()
        {
            if (Target == null)
            {
                return;
            }

            try
            {
                for (Type? type = Target.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                {
                    var method = type.GetMethod(
                        "OnValidate",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        binder: null,
                        types: Type.EmptyTypes,
                        modifiers: null);
                    if (method != null)
                    {
                        method.Invoke(Target, null);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovModSettings] OnValidate failed for '{Target.GetType().FullName}': {ex.InnerException ?? ex}");
            }
        }
    }

    internal sealed class SettingNode
    {
        private readonly List<SettingNode> _children = new List<SettingNode>();
        private object? _lastObservedValue;

        public SettingNode(
            ComponentSettingsModel owner,
            SettingNodeKind kind,
            string memberPath,
            string displayName,
            string tooltip,
            string? header,
            Type valueType,
            ReflectionPath? accessPath,
            object? defaultValue,
            IEnumerable<string>? formerKeys,
            SettingRange? range,
            TextAreaOptions? textArea)
        {
            Owner = owner;
            Kind = kind;
            MemberPath = memberPath;
            DisplayName = displayName;
            Tooltip = tooltip;
            Header = header;
            ValueType = valueType;
            AccessPath = accessPath;
            DefaultValue = SettingValueCodec.CloneValue(defaultValue, valueType);
            FormerKeys = (formerKeys ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            Range = range;
            TextArea = textArea;
            StoreKey = owner.ComponentKey + "." + memberPath;
            _lastObservedValue = SettingValueCodec.CloneValue(defaultValue, valueType);
        }

        public ComponentSettingsModel Owner { get; }
        public SettingNodeKind Kind { get; }
        public string MemberPath { get; }
        public string StoreKey { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public string? Header { get; }
        public Type ValueType { get; }
        public ReflectionPath? AccessPath { get; }
        public object? DefaultValue { get; }
        public IReadOnlyList<string> FormerKeys { get; }
        public SettingRange? Range { get; }
        public TextAreaOptions? TextArea { get; }
        public IReadOnlyList<SettingNode> Children => _children;
        public bool IsValue => Kind != SettingNodeKind.Group && AccessPath != null;

        public event Action<SettingNode, SettingChangeOrigin>? ValueChanged;

        public void Add(SettingNode node)
        {
            _children.Add(node);
        }

        public IEnumerable<SettingNode> SelfAndDescendants()
        {
            yield return this;
            foreach (var child in _children)
            {
                foreach (var descendant in child.SelfAndDescendants())
                {
                    yield return descendant;
                }
            }
        }

        public object? GetValue()
        {
            return AccessPath != null && AccessPath.TryGetValue(Owner.Target, out var value) ? value : null;
        }

        public bool TrySetValue(object? value, SettingChangeOrigin origin)
        {
            if (!IsValue || AccessPath == null ||
                !SettingValueCodec.TryConvert(value, ValueType, out var converted))
            {
                return false;
            }

            converted = Coerce(converted);
            var current = GetValue();
            if (SettingValueCodec.ValuesEqual(current, converted, ValueType))
            {
                ObserveCurrentValue();
                return true;
            }

            if (!AccessPath.TrySetValue(Owner.Target, converted))
            {
                return false;
            }

            _lastObservedValue = SettingValueCodec.CloneValue(converted, ValueType);
            ValueChanged?.Invoke(this, origin);
            return true;
        }

        public bool Reset()
        {
            return IsValue && TrySetValue(DefaultValue, SettingChangeOrigin.Reset);
        }

        public bool TryObserveExternalChange()
        {
            if (!IsValue)
            {
                return false;
            }

            var current = GetValue();
            if (SettingValueCodec.ValuesEqual(current, _lastObservedValue, ValueType))
            {
                return false;
            }

            _lastObservedValue = SettingValueCodec.CloneValue(current, ValueType);
            ValueChanged?.Invoke(this, SettingChangeOrigin.External);
            return true;
        }

        public void ObserveCurrentValue()
        {
            _lastObservedValue = SettingValueCodec.CloneValue(GetValue(), ValueType);
        }

        private object? Coerce(object? value)
        {
            if (value == null || Range == null)
            {
                return value;
            }

            if (SettingValueCodec.IsIntegral(ValueType))
            {
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                number = Math.Max(Range.Minimum, Math.Min(Range.Maximum, number));
                return Convert.ChangeType(Math.Round(number), Nullable.GetUnderlyingType(ValueType) ?? ValueType, CultureInfo.InvariantCulture);
            }

            if (SettingValueCodec.IsFloatingPoint(ValueType))
            {
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                number = Math.Max(Range.Minimum, Math.Min(Range.Maximum, number));
                return Convert.ChangeType(number, Nullable.GetUnderlyingType(ValueType) ?? ValueType, CultureInfo.InvariantCulture);
            }

            return value;
        }
    }

    internal sealed class ReflectionPath
    {
        private readonly ReflectionStep[] _steps;

        public ReflectionPath(IEnumerable<ReflectionStep> steps)
        {
            _steps = steps.ToArray();
        }

        public ReflectionPath Append(MemberInfo member)
        {
            return new ReflectionPath(_steps.Concat(new[] { new ReflectionStep(member) }));
        }

        public bool TryGetValue(object root, out object? value)
        {
            value = root;
            try
            {
                foreach (var step in _steps)
                {
                    if (value == null)
                    {
                        return false;
                    }
                    value = step.GetValue(value);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovModSettings] Could not read reflected setting: {ex.InnerException?.Message ?? ex.Message}");
                value = null;
                return false;
            }
        }

        public bool TrySetValue(object root, object? value)
        {
            if (_steps.Length == 0)
            {
                return false;
            }

            try
            {
                return TrySetRecursive(root, 0, value, out _);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovModSettings] Could not write reflected setting: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
        }

        private bool TrySetRecursive(object owner, int index, object? value, out object updatedOwner)
        {
            var step = _steps[index];
            if (index == _steps.Length - 1)
            {
                step.SetValue(owner, value);
                updatedOwner = owner;
                return true;
            }

            var child = step.GetValue(owner);
            if (child == null || !TrySetRecursive(child, index + 1, value, out var updatedChild))
            {
                updatedOwner = owner;
                return false;
            }

            // Always assign the child back. This is required for boxed value types and is harmless for classes.
            step.SetValue(owner, updatedChild);
            updatedOwner = owner;
            return true;
        }
    }

    internal sealed class ReflectionStep
    {
        private readonly FieldInfo? _field;
        private readonly PropertyInfo? _property;

        public ReflectionStep(MemberInfo member)
        {
            _field = member as FieldInfo;
            _property = member as PropertyInfo;
            if (_field == null && _property == null)
            {
                throw new ArgumentException("Only fields and properties are supported.", nameof(member));
            }
        }

        public object? GetValue(object owner)
        {
            return _field != null ? _field.GetValue(owner) : _property!.GetValue(owner, null);
        }

        public void SetValue(object owner, object? value)
        {
            if (_field != null)
            {
                _field.SetValue(owner, value);
            }
            else
            {
                _property!.SetValue(owner, value, null);
            }
        }
    }

    internal static class NameUtility
    {
        public static string NicifyTypeName(string value)
        {
            if (value.EndsWith("Settings", StringComparison.Ordinal) && value.Length > "Settings".Length)
            {
                value = value.Substring(0, value.Length - "Settings".Length);
            }
            else if (value.EndsWith("Behaviour", StringComparison.Ordinal) && value.Length > "Behaviour".Length)
            {
                value = value.Substring(0, value.Length - "Behaviour".Length);
            }
            return NicifyMemberName(value);
        }

        public static string NicifyMemberName(string value)
        {
            value = value.TrimStart('_').Replace('_', ' ');
            if (value.Length == 0)
            {
                return value;
            }

            var result = new StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var current = value[i];
                if (i > 0 && char.IsUpper(current) &&
                    value[i - 1] != ' ' &&
                    (!char.IsUpper(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                {
                    result.Append(' ');
                }
                result.Append(current);
            }

            if (result.Length > 0)
            {
                result[0] = char.ToUpperInvariant(result[0]);
            }
            return result.ToString();
        }
    }
}
