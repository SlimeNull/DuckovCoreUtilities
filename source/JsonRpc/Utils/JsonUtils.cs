using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace EleCho.JsonRpc.Utils
{
    internal static class JsonUtils
    {
        public static JsonSerializerSettings Settings { get; } = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                RpcPackageConverter.Instance,
                RpcPackageIdConverter.Instance,
            }
        };

        public static JsonSerializer Serializer { get; } = JsonSerializer.Create(Settings);

        public class RpcPackageConverter : JsonConverter<RpcPackage>
        {
            public static RpcPackageConverter Instance { get; } = new RpcPackageConverter();

            public override bool CanWrite => false;

            public override RpcPackage? ReadJson(JsonReader reader, Type objectType, RpcPackage? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var token = JToken.Load(reader);
                if (token is not JObject obj)
                    return null;

                if (obj["method"] != null)
                    return new RpcRequest(
                        obj.Value<string>("method") ?? string.Empty,
                        ReadObjectArray(obj["params"]),
                        obj.Value<string>("signature"),
                        ReadNullableId(obj["id"], serializer));

                if (obj["error"] is JObject errorObj)
                    return new RpcErrorResponse(
                        new RpcError(
                            errorObj.Value<int>("code"),
                            errorObj.Value<string>("message") ?? string.Empty,
                            errorObj["data"]),
                        ReadRequiredId(obj["id"], serializer));

                return new RpcResponse(
                    obj["result"],
                    ReadObjectArray(obj["ref_results"]),
                    ReadRequiredId(obj["id"], serializer));
            }

            public override void WriteJson(JsonWriter writer, RpcPackage? value, JsonSerializer serializer)
            {
                throw new NotSupportedException($"{nameof(RpcPackageConverter)} is only used for reading polymorphic RPC packages.");
            }

            private static object?[]? ReadObjectArray(JToken? token)
            {
                if (token == null || token.Type == JTokenType.Null)
                    return null;

                if (token is not JArray array)
                    throw new JsonSerializationException("Expected JSON array.");

                var result = new object?[array.Count];
                for (var i = 0; i < array.Count; i++)
                    result[i] = array[i];

                return result;
            }

            private static RpcPackageId? ReadNullableId(JToken? token, JsonSerializer serializer)
            {
                if (token == null || token.Type == JTokenType.Null)
                    return null;

                return ReadRequiredId(token, serializer);
            }

            private static RpcPackageId ReadRequiredId(JToken? token, JsonSerializer serializer)
            {
                if (token == null || token.Type == JTokenType.Null)
                    throw new JsonSerializationException("JSON-RPC package id is required.");

                return token.ToObject<RpcPackageId>(serializer);
            }
        }

        public class RpcPackageIdConverter : JsonConverter<RpcPackageId>
        {
            public static RpcPackageIdConverter Instance { get; } = new RpcPackageIdConverter();

            public override RpcPackageId ReadJson(JsonReader reader, Type objectType, RpcPackageId existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                    return RpcPackageId.Create((string)reader.Value!);
                if (reader.TokenType == JsonToken.Integer)
                    return RpcPackageId.Create(Convert.ToInt32(reader.Value));

                throw new JsonSerializationException("Invalid JSON-RPC id token.");
            }

            public override void WriteJson(JsonWriter writer, RpcPackageId value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value.Value);
            }
        }
    }
}
