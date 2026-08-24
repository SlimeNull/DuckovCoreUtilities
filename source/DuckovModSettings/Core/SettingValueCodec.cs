using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SlimeNull.DuckovModSettings.Core
{
    internal static class SettingValueCodec
    {
        public static bool IsIntegral(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(byte) || type == typeof(sbyte) ||
                type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) ||
                type == typeof(long) || type == typeof(ulong);
        }

        public static bool IsFloatingPoint(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        public static bool IsSupportedLeaf(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(bool) || type == typeof(string) || type == typeof(char) ||
                IsIntegral(type) || IsFloatingPoint(type) || type.IsEnum ||
                type == typeof(Color) || type == typeof(Color32);
        }

        public static SettingNodeKind GetKind(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(bool))
            {
                return SettingNodeKind.Boolean;
            }
            if (IsIntegral(type))
            {
                return SettingNodeKind.Integer;
            }
            if (IsFloatingPoint(type))
            {
                return SettingNodeKind.FloatingPoint;
            }
            if (type == typeof(string) || type == typeof(char))
            {
                return SettingNodeKind.String;
            }
            if (type == typeof(Color) || type == typeof(Color32))
            {
                return SettingNodeKind.Color;
            }
            if (type.IsEnum && (type.FullName == "UnityEngine.KeyCode" || type.FullName == "UnityEngine.InputSystem.Key"))
            {
                return SettingNodeKind.Key;
            }
            return SettingNodeKind.Enum;
        }

        public static bool TryConvert(object? value, Type targetType, out object? converted)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            var effectiveType = nullableType ?? targetType;
            if (value == null)
            {
                converted = null;
                return nullableType != null || !targetType.IsValueType;
            }

            if (value is JToken token)
            {
                return TryFromToken(token, targetType, out converted);
            }

            if (targetType.IsInstanceOfType(value) || effectiveType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            try
            {
                if (effectiveType.IsEnum)
                {
                    if (value is string enumName)
                    {
                        return TryParseEnum(effectiveType, enumName, out converted);
                    }
                    converted = Enum.ToObject(effectiveType, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    return true;
                }

                if (effectiveType == typeof(Color) && value is Color32 color32)
                {
                    converted = (Color)color32;
                    return true;
                }
                if (effectiveType == typeof(Color32) && value is Color color)
                {
                    converted = (Color32)color;
                    return true;
                }
                if (effectiveType == typeof(char))
                {
                    var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (text.Length == 0)
                    {
                        converted = null;
                        return false;
                    }
                    converted = text[0];
                    return true;
                }

                converted = Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }

        public static JToken ToToken(object? value, Type type)
        {
            if (value == null)
            {
                return JValue.CreateNull();
            }

            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum)
            {
                return new JValue(value.ToString());
            }
            if (type == typeof(Color))
            {
                var color = (Color)value;
                return new JObject
                {
                    ["r"] = color.r,
                    ["g"] = color.g,
                    ["b"] = color.b,
                    ["a"] = color.a,
                };
            }
            if (type == typeof(Color32))
            {
                var color = (Color32)value;
                return new JObject
                {
                    ["r"] = color.r,
                    ["g"] = color.g,
                    ["b"] = color.b,
                    ["a"] = color.a,
                };
            }
            if (type == typeof(char))
            {
                return new JValue(value.ToString());
            }

            return JToken.FromObject(value);
        }

        public static bool TryFromToken(JToken token, Type targetType, out object? value)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            var effectiveType = nullableType ?? targetType;
            if (token.Type == JTokenType.Null)
            {
                value = null;
                return nullableType != null || !targetType.IsValueType;
            }

            try
            {
                if (effectiveType.IsEnum)
                {
                    if (token.Type == JTokenType.String)
                    {
                        return TryParseEnum(effectiveType, token.Value<string>() ?? string.Empty, out value);
                    }
                    value = Enum.ToObject(effectiveType, token.Value<long>());
                    return true;
                }
                if (effectiveType == typeof(Color))
                {
                    value = ReadColor(token);
                    return true;
                }
                if (effectiveType == typeof(Color32))
                {
                    value = (Color32)ReadColor(token);
                    return true;
                }
                if (effectiveType == typeof(char))
                {
                    var text = token.Value<string>() ?? string.Empty;
                    if (text.Length == 0)
                    {
                        value = null;
                        return false;
                    }
                    value = text[0];
                    return true;
                }

                value = token.ToObject(effectiveType);
                return value != null || !effectiveType.IsValueType;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public static bool ValuesEqual(object? left, object? right, Type type)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left == null || right == null)
            {
                return false;
            }

            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(float))
            {
                return Mathf.Approximately((float)left, (float)right);
            }
            if (type == typeof(double))
            {
                return Math.Abs((double)left - (double)right) <= 0.000001d;
            }
            if (type == typeof(Color))
            {
                var a = (Color)left;
                var b = (Color)right;
                return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) &&
                    Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
            }
            return left.Equals(right);
        }

        public static object? CloneValue(object? value, Type type)
        {
            // Every supported leaf is either immutable or a value type.
            return value;
        }

        public static string[] GetEnumNames(Type enumType)
        {
            return Enum.GetNames(Nullable.GetUnderlyingType(enumType) ?? enumType);
        }

        public static string[] GetEnumDisplayNames(Type enumType)
        {
            enumType = Nullable.GetUnderlyingType(enumType) ?? enumType;
            return GetEnumNames(enumType).Select(name =>
            {
                var field = enumType.GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    return name;
                }

                try
                {
                    var attribute = CustomAttributeData.GetCustomAttributes(field)
                        .FirstOrDefault(item => item.AttributeType.FullName == "UnityEngine.InspectorNameAttribute");
                    return attribute?.ConstructorArguments.FirstOrDefault().Value as string ?? NameUtility.NicifyMemberName(name);
                }
                catch
                {
                    return NameUtility.NicifyMemberName(name);
                }
            }).ToArray();
        }

        private static bool TryParseEnum(Type enumType, string text, out object? value)
        {
            try
            {
                value = Enum.Parse(enumType, text, ignoreCase: true);
                return true;
            }
            catch
            {
            }

            var names = GetEnumNames(enumType);
            var displayNames = GetEnumDisplayNames(enumType);
            for (var i = 0; i < names.Length; i++)
            {
                if (string.Equals(displayNames[i], text, StringComparison.CurrentCultureIgnoreCase))
                {
                    value = Enum.Parse(enumType, names[i], ignoreCase: false);
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static Color ReadColor(JToken token)
        {
            if (token.Type == JTokenType.String && ColorUtility.TryParseHtmlString(token.Value<string>(), out var htmlColor))
            {
                return htmlColor;
            }

            return new Color(
                token["r"]?.Value<float>() ?? 0f,
                token["g"]?.Value<float>() ?? 0f,
                token["b"]?.Value<float>() ?? 0f,
                token["a"]?.Value<float>() ?? 1f);
        }
    }
}
