using System;
using Newtonsoft.Json;

namespace EleCho.JsonRpc
{
    internal abstract record class RpcPackage
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc => "2.0";
    }

    [JsonConverter(typeof(Utils.JsonUtils.RpcPackageIdConverter))]
    internal record struct RpcPackageId
    {
        private RpcPackageId(object value)
        {
            Value = value;
        }

        public object Value { get; }

        public static RpcPackageId? CreateOrNull(object? id)
        {
            if (id == null)
                return null;

            return Create(id);
        }

        public static RpcPackageId Create(object id)
        {
            if (id is string strId)
                return Create(strId);
            else if (id is int intId)
                return Create(intId);
            else if (id is long longId)
                return Create(checked((int)longId));
            else
                throw new ArgumentException("Invalid type of id", nameof(id));
        }

        public static RpcPackageId Create(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Empty value", nameof(id));

            return new RpcPackageId(id);
        }

        public static RpcPackageId Create(int id)
        {
            return new RpcPackageId(id);
        }
    }

    internal record class RpcRequest : RpcPackage
    {
        [JsonConstructor]
        public RpcRequest(string method, object?[]? args, string? signature, RpcPackageId? id)
        {
            Method = method;
            Args = args;
            Signature = signature;
            Id = id;
        }

        [JsonProperty("method")]
        public string Method { get; }

        [JsonProperty("params")]
        public object?[]? Args { get; }

        [JsonProperty("signature")]
        public string? Signature { get; }

        [JsonProperty("id")]
        public RpcPackageId? Id { get; }
    }

    internal record class RpcResponse : RpcPackage
    {
        [JsonConstructor]
        public RpcResponse(object? result, object?[]? refResults, RpcPackageId id)
        {
            Result = result;
            RefResults = refResults;
            Id = id;
        }

        [JsonProperty("result")]
        public object? Result { get; }

        [JsonProperty("ref_results")]
        public object?[]? RefResults { get; }

        [JsonProperty("id")]
        public RpcPackageId Id { get; }
    }

    internal record class RpcErrorResponse : RpcPackage
    {
        [JsonConstructor]
        public RpcErrorResponse(RpcError error, RpcPackageId id)
        {
            Error = error;
            Id = id;
        }

        [JsonProperty("error")]
        public RpcError Error { get; }

        [JsonProperty("id")]
        public RpcPackageId Id { get; }
    }

    internal struct RpcError
    {
        [JsonConstructor]
        public RpcError(int code, string message, object? data)
        {
            Code = code;
            Message = message;
            Data = data;
        }

        public RpcError(RpcErrorCode code, string message, object? data) :
            this((int)code, message, data)
        { }

        [JsonProperty("code")]
        public int Code { get; }

        [JsonProperty("message")]
        public string Message { get; }

        [JsonProperty("data")]
        public object? Data { get; }

        [JsonIgnore]
        public bool IsParseError =>
            Code == (int)RpcErrorCode.ParseError;

        [JsonIgnore]
        public bool IsInvalidRequest =>
            Code == (int)RpcErrorCode.InvalidRequest;

        [JsonIgnore]
        public bool IsMethodNotFound =>
            Code == (int)RpcErrorCode.MethodNotFound;

        [JsonIgnore]
        public bool IsInvalidParams =>
            Code == (int)RpcErrorCode.InvalidParams;

        [JsonIgnore]
        public bool IsInternalError =>
            Code == (int)RpcErrorCode.InternalError;

        [JsonIgnore]
        public bool IsServerError =>
            Code <= (int)RpcErrorCode.ServerErrorUpBound &&
            Code >= (int)RpcErrorCode.ServerErrorDownBound;
    }

    internal enum RpcErrorCode
    {
        ParseError           = -32700,
        InvalidRequest       = -32600,
        MethodNotFound       = -32601,
        InvalidParams        = -32602,
        InternalError        = -32603,
        ServerErrorUpBound   = -32000,
        ServerErrorDownBound = -32099
    }
}
