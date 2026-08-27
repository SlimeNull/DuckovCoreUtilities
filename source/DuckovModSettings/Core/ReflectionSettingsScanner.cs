using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DuckovModBehaviour = Duckov.Modding.ModBehaviour;

namespace SlimeNull.DuckovModSettings.Core
{
    internal static class ReflectionSettingsScanner
    {
        private const int MaximumDepth = 8;
        private const string SerializeFieldAttribute = "UnityEngine.SerializeField";
        private const string SerializeReferenceAttribute = "UnityEngine.SerializeReference";
        private const string HideInInspectorAttribute = "UnityEngine.HideInInspector";
        private const string HeaderAttribute = "UnityEngine.HeaderAttribute";
        private const string TooltipAttribute = "UnityEngine.TooltipAttribute";
        private const string RangeAttribute = "UnityEngine.RangeAttribute";
        private const string TextAreaAttribute = "UnityEngine.TextAreaAttribute";
        private const string InspectorNameAttribute = "UnityEngine.InspectorNameAttribute";
        private const string DescriptionAttribute = "System.ComponentModel.DescriptionAttribute";

        public static ModSettingsModel? Scan(DuckovModBehaviour root)
        {
            if (root == null)
            {
                return null;
            }

            var mod = new ModSettingsModel(root);
            var assembly = root.GetType().Assembly;
            MonoBehaviour[] components;
            try
            {
                components = root.GetComponents<MonoBehaviour>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovModSettings] Could not enumerate components for '{root.GetType().FullName}': {ex.Message}");
                return null;
            }

            foreach (var component in components)
            {
                if (component == null || component.GetType().Assembly != assembly)
                {
                    continue;
                }

                var componentModel = ScanComponent(mod, component);
                if (componentModel.Nodes.Count > 0)
                {
                    mod.Add(componentModel);
                }
            }

            return mod.Components.Count > 0 ? mod : null;
        }

        private static ComponentSettingsModel ScanComponent(ModSettingsModel mod, MonoBehaviour component)
        {
            var model = new ComponentSettingsModel(mod, component);
            var rootPath = new ReflectionPath(Array.Empty<ReflectionStep>());
            var typeStack = new HashSet<Type>();
            BuildMembers(model, model.Add, component.GetType(), rootPath, string.Empty, 0, typeStack);
            return model;
        }

        private static void BuildMembers(
            ComponentSettingsModel component,
            Action<SettingNode> add,
            Type objectType,
            ReflectionPath parentPath,
            string parentMemberPath,
            int depth,
            HashSet<Type> typeStack)
        {
            if (depth > MaximumDepth || !typeStack.Add(objectType))
            {
                return;
            }

            try
            {
                foreach (var member in GetCandidateMembers(objectType))
                {
                    var memberType = GetMemberType(member);
                    var path = parentPath.Append(member);
                    var memberPath = string.IsNullOrEmpty(parentMemberPath)
                        ? member.Name
                        : parentMemberPath + "." + member.Name;

                    if (!path.TryGetValue(component.Target, out var currentValue))
                    {
                        continue;
                    }
                    var displayName = GetStringAttribute(member, InspectorNameAttribute) ?? NameUtility.NicifyMemberName(member.Name);
                    var tooltip = GetStringAttribute(member, TooltipAttribute) ?? string.Empty;
                    var header = GetStringAttribute(member, HeaderAttribute);
                    var fileFilter = GetStringAttribute(member, DescriptionAttribute);
                    var range = GetRange(member);
                    var textArea = GetTextArea(member);

                    if (SettingValueCodec.IsSupportedLeaf(memberType))
                    {
                        add(new SettingNode(
                            component,
                            SettingValueCodec.GetKind(memberType),
                            memberPath,
                            displayName,
                            tooltip,
                            header,
                            memberType,
                            path,
                            currentValue,
                            fileFilter,
                            range,
                            textArea));
                        continue;
                    }

                    var containerType = currentValue != null &&
                        (memberType.IsAbstract || memberType.IsInterface || HasAttribute(member, SerializeReferenceAttribute))
                        ? currentValue.GetType()
                        : memberType;
                    if (currentValue == null || !IsSerializableContainer(containerType))
                    {
                        continue;
                    }

                    var group = new SettingNode(
                        component,
                        SettingNodeKind.Group,
                        memberPath,
                        displayName,
                        tooltip,
                        header,
                        memberType,
                        path,
                        currentValue,
                        fileFilter,
                        range: null,
                        textArea: null);
                    BuildMembers(component, group.Add, containerType, path, memberPath, depth + 1, typeStack);
                    if (group.Children.Count > 0)
                    {
                        add(group);
                    }
                }
            }
            finally
            {
                typeStack.Remove(objectType);
            }
        }

        private static IEnumerable<MemberInfo> GetCandidateMembers(Type type)
        {
            var hierarchy = new Stack<Type>();
            for (Type? current = type;
                current != null && current != typeof(MonoBehaviour) && current != typeof(Behaviour) &&
                current != typeof(Component) && current != typeof(UnityEngine.Object) &&
                current != typeof(DuckovModBehaviour);
                current = current.BaseType)
            {
                hierarchy.Push(current);
            }

            while (hierarchy.Count > 0)
            {
                var current = hierarchy.Pop();
                var members = new List<MemberInfo>();
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly || HasAttribute(field, HideInInspectorAttribute))
                    {
                        continue;
                    }
                    if (field.IsPublic || HasAttribute(field, SerializeFieldAttribute) || HasAttribute(field, SerializeReferenceAttribute))
                    {
                        members.Add(field);
                    }
                }

                foreach (var property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (property.GetIndexParameters().Length != 0 || HasAttribute(property, HideInInspectorAttribute))
                    {
                        continue;
                    }
                    var getter = property.GetGetMethod(nonPublic: false);
                    var setter = property.GetSetMethod(nonPublic: false);
                    if (getter != null && setter != null && !getter.IsStatic && !setter.IsStatic)
                    {
                        members.Add(property);
                    }
                }

                foreach (var member in members.OrderBy(item => item.MetadataToken))
                {
                    yield return member;
                }
            }
        }

        private static Type GetMemberType(MemberInfo member)
        {
            return member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
        }

        private static bool IsSerializableContainer(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsPointer ||
                type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type) ||
                typeof(Delegate).IsAssignableFrom(type) || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            return type.IsDefined(typeof(SerializableAttribute), inherit: false);
        }

        private static SettingRange? GetRange(MemberInfo member)
        {
            var attribute = GetAttribute(member, RangeAttribute);
            if (attribute == null || attribute.ConstructorArguments.Count < 2)
            {
                return null;
            }

            try
            {
                return new SettingRange(
                    Convert.ToSingle(attribute.ConstructorArguments[0].Value),
                    Convert.ToSingle(attribute.ConstructorArguments[1].Value));
            }
            catch
            {
                return null;
            }
        }

        private static TextAreaOptions? GetTextArea(MemberInfo member)
        {
            var attribute = GetAttribute(member, TextAreaAttribute);
            if (attribute == null)
            {
                return null;
            }

            try
            {
                var minimum = attribute.ConstructorArguments.Count > 0
                    ? Convert.ToInt32(attribute.ConstructorArguments[0].Value)
                    : 3;
                var maximum = attribute.ConstructorArguments.Count > 1
                    ? Convert.ToInt32(attribute.ConstructorArguments[1].Value)
                    : minimum;
                return new TextAreaOptions(minimum, maximum);
            }
            catch
            {
                return new TextAreaOptions(3, 3);
            }
        }

        private static string? GetStringAttribute(MemberInfo member, string fullName)
        {
            return GetStringAttributes(member, fullName).FirstOrDefault();
        }

        private static IEnumerable<string> GetStringAttributes(MemberInfo member, string fullName)
        {
            foreach (var attribute in GetAttributes(member))
            {
                if (attribute.AttributeType.FullName == fullName && attribute.ConstructorArguments.Count > 0 &&
                    attribute.ConstructorArguments[0].Value is string value)
                {
                    yield return value;
                }
            }
        }

        private static bool HasAttribute(MemberInfo member, string fullName)
        {
            return GetAttribute(member, fullName) != null;
        }

        private static CustomAttributeData? GetAttribute(MemberInfo member, string fullName)
        {
            return GetAttributes(member).FirstOrDefault(attribute => attribute.AttributeType.FullName == fullName);
        }

        private static IReadOnlyList<CustomAttributeData> GetAttributes(MemberInfo member)
        {
            try
            {
                return CustomAttributeData.GetCustomAttributes(member).ToArray();
            }
            catch
            {
                return Array.Empty<CustomAttributeData>();
            }
        }
    }
}
