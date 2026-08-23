using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EleCho.JsonRpc.Utils
{
    internal static class JsonUtils
    {
        public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString |
                             JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters =
            {
                RpcPackageConverter.Instance,
                RpcPackageIdConverter.Instance,
            }
        };

        public static string SerializePackage(RpcPackage package)
        {
            return JsonSerializer.Serialize(package, package.GetType(), Options);
        }

        public static object? ConvertToType(object? value, Type targetType)
        {
            return value is JsonElement element
                ? element.Deserialize(targetType, Options)
                : value;
        }

        public class RpcPackageConverter : JsonConverter<RpcPackage>
        {
            public static RpcPackageConverter Instance { get; } = new RpcPackageConverter();

            public override RpcPackage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                if (root.TryGetProperty("method", out JsonElement methodElement))
                    return new RpcRequest(
                        ReadString(methodElement) ?? string.Empty,
                        ReadObjectArray(root, "params"),
                        ReadOptionalString(root, "signature"),
                        ReadNullableId(root, "id", options));

                if (root.TryGetProperty("error", out JsonElement errorElement) &&
                    errorElement.ValueKind == JsonValueKind.Object)
                    return new RpcErrorResponse(
                        new RpcError(
                            ReadInt32(errorElement, "code"),
                            ReadOptionalString(errorElement, "message") ?? string.Empty,
                            ReadObject(errorElement, "data")),
                        ReadRequiredId(root, "id", options));

                return new RpcResponse(
                    ReadObject(root, "result"),
                    ReadObjectArray(root, "ref_results"),
                    ReadRequiredId(root, "id", options));
            }

            public override void Write(Utf8JsonWriter writer, RpcPackage value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }

            private static object? ReadObject(JsonElement parent, string propertyName)
            {
                if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
                    element.ValueKind == JsonValueKind.Null)
                    return null;

                return element.Clone();
            }

            private static object?[]? ReadObjectArray(JsonElement parent, string propertyName)
            {
                if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
                    element.ValueKind == JsonValueKind.Null)
                    return null;

                if (element.ValueKind != JsonValueKind.Array)
                    throw new JsonException("Expected JSON array.");

                var result = new object?[element.GetArrayLength()];
                var index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                    result[index++] = item.Clone();

                return result;
            }

            private static string? ReadOptionalString(JsonElement parent, string propertyName)
            {
                return parent.TryGetProperty(propertyName, out JsonElement element)
                    ? ReadString(element)
                    : null;
            }

            private static string? ReadString(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Null)
                    return null;
                if (element.ValueKind != JsonValueKind.String)
                    throw new JsonException("Expected JSON string.");

                return element.GetString();
            }

            private static int ReadInt32(JsonElement parent, string propertyName)
            {
                if (!parent.TryGetProperty(propertyName, out JsonElement element))
                    return 0;
                if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
                    throw new JsonException("Expected 32-bit JSON integer.");

                return value;
            }

            private static RpcPackageId? ReadNullableId(JsonElement parent, string propertyName, JsonSerializerOptions options)
            {
                if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
                    element.ValueKind == JsonValueKind.Null)
                    return null;

                return ReadRequiredId(element, options);
            }

            private static RpcPackageId ReadRequiredId(JsonElement parent, string propertyName, JsonSerializerOptions options)
            {
                if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
                    element.ValueKind == JsonValueKind.Null)
                    throw new JsonException("JSON-RPC package id is required.");

                return ReadRequiredId(element, options);
            }

            private static RpcPackageId ReadRequiredId(JsonElement element, JsonSerializerOptions options)
            {
                return element.Deserialize<RpcPackageId>(options);
            }
        }

        public class RpcPackageIdConverter : JsonConverter<RpcPackageId>
        {
            public static RpcPackageIdConverter Instance { get; } = new RpcPackageIdConverter();

            public override RpcPackageId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                    return RpcPackageId.Create(reader.GetString()!);
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int id))
                    return RpcPackageId.Create(id);

                throw new JsonException("Invalid JSON-RPC id token.");
            }

            public override void Write(Utf8JsonWriter writer, RpcPackageId value, JsonSerializerOptions options)
            {
                if (value.Value is string stringId)
                    writer.WriteStringValue(stringId);
                else if (value.Value is int integerId)
                    writer.WriteNumberValue(integerId);
                else
                    throw new JsonException("Invalid JSON-RPC id value.");
            }
        }
    }
}
