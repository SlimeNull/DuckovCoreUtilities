using EleCho.JsonRpc;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Sockets;
using System.Reflection;

namespace SlimeNull.DuckovInterop.HierarchyInspectorBridge
{
    internal sealed class Program
    {
        private static async Task Main(string[] args)
        {
            var apiClient = new DuckovApiClient();
            var tools = new HierarchyInspectorMcpTools(apiClient);
            var transport = new StdioServerTransport(HierarchyInspectorRpcEndpoint.ServerName);
            var server = McpServer.Create(transport, CreateServerOptions(tools));
            await server.RunAsync().ConfigureAwait(false);
        }

        private static McpServerOptions CreateServerOptions(HierarchyInspectorMcpTools tools)
        {
            return new McpServerOptions
            {
                ServerInfo = new Implementation
                {
                    Name = HierarchyInspectorRpcEndpoint.ServerName,
                    Title = "Duckov Interop",
                    Version = "1.0.0"
                },
                ToolCollection = DiscoverTools(tools),
                ServerInstructions = "Inspect and manipulate Unity objects through instance IDs and stored object GUIDs."
            };
        }

        private static McpServerPrimitiveCollection<McpServerTool> DiscoverTools(object target)
        {
            var collection = new McpServerPrimitiveCollection<McpServerTool>();
            var targetType = target.GetType();
            if (!targetType.IsDefined(typeof(McpServerToolTypeAttribute), inherit: true))
            {
                return collection;
            }

            foreach (var method in targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.IsDefined(typeof(McpServerToolAttribute), inherit: true)))
            {
                collection.Add(McpServerTool.Create(method, target));
            }

            return collection;
        }
    }

    public sealed class DuckovApiClient : IDisposable
    {
        private const string ConnectionError = "无法连接到 DuckovInterop, 游戏是否在运行且启用了 Mod?";

        private readonly object _sync = new object();
        private TcpClient? _tcpClient;
        private RpcClient<IHierarchyInspectorRpc>? _rpcClient;

        public ApiResult<T> Invoke<T>(Func<IHierarchyInspectorRpc, ApiResult<T>> action)
        {
            IHierarchyInspectorRpc remote;
            try
            {
                remote = EnsureConnected();
            }
            catch
            {
                return ApiResult<T>.Failure(ConnectionError);
            }

            try
            {
                return action(remote);
            }
            catch
            {
                ResetConnection();
                try
                {
                    remote = EnsureConnected();
                    return action(remote);
                }
                catch
                {
                    return ApiResult<T>.Failure(ConnectionError);
                }
            }
        }

        private IHierarchyInspectorRpc EnsureConnected()
        {
            lock (_sync)
            {
                if (_rpcClient != null && _tcpClient != null && IsConnected(_tcpClient))
                {
                    return _rpcClient.Remote;
                }

                ResetConnection();

                var tcpClient = new TcpClient();
                tcpClient.Connect(HierarchyInspectorRpcEndpoint.Host, HierarchyInspectorRpcEndpoint.Port);

                var rpcClient = new RpcClient<IHierarchyInspectorRpc>(tcpClient.GetStream());
                rpcClient.Start();

                _tcpClient = tcpClient;
                _rpcClient = rpcClient;
                return rpcClient.Remote;
            }
        }

        private static bool IsConnected(TcpClient client)
        {
            try
            {
                return client.Client != null && client.Connected && !(client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0);
            }
            catch
            {
                return false;
            }
        }

        private void ResetConnection()
        {
            lock (_sync)
            {
                try
                {
                    _rpcClient?.Dispose();
                }
                catch
                {
                }

                try
                {
                    _tcpClient?.Close();
                }
                catch
                {
                }

                _rpcClient = null;
                _tcpClient = null;
            }
        }

        public void Dispose()
        {
            ResetConnection();
        }
    }

    [McpServerToolType]
    public sealed class HierarchyInspectorMcpTools
    {
        private readonly DuckovApiClient _apiClient;

        public HierarchyInspectorMcpTools(DuckovApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [McpServerTool(Name = "get_hierarchy", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<HierarchyResponse>))]
        [Description("Return loaded Unity scene hierarchy trees with GameObject and component instance IDs.")]
        public ApiResult<HierarchyResponse> GetHierarchy()
        {
            return _apiClient.Invoke(api => api.GetHierarchy());
        }

        [McpServerTool(Name = "get_scene_snapshot", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<SceneSnapshot>))]
        [Description("Return a complete Unity scene snapshot including hierarchy, components, and Inspector-visible serialized fields.")]
        public ApiResult<SceneSnapshot> GetSceneSnapshot()
        {
            return _apiClient.Invoke(api => api.GetSceneSnapshot());
        }

        [McpServerTool(Name = "set_game_object_active", ReadOnly = false, Destructive = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<bool>))]
        [Description("Set a scene GameObject's activeSelf state by instance ID.")]
        public ApiResult<bool> SetGameObjectActive(
            [Description("GameObject instance ID.")] string gameObjectId,
            [Description("The new activeSelf state.")] bool active)
        {
            return _apiClient.Invoke(api => api.SetGameObjectActive(gameObjectId, active));
        }

        [McpServerTool(Name = "get_components", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<List<ComponentInfo>>))]
        [Description("Return all components attached to a GameObject by GameObject instance ID.")]
        public ApiResult<List<ComponentInfo>> GetComponents([Description("GameObject instance ID.")] string gameObjectId)
        {
            return _apiClient.Invoke(api => api.GetComponents(gameObjectId));
        }

        [McpServerTool(Name = "find_by_name", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<List<ObjectSearchResult>>))]
        [Description("Find scene GameObjects by name.")]
        public ApiResult<List<ObjectSearchResult>> FindByName(
            [Description("Full or partial GameObject name, case-insensitive.")] string name,
            [Description("Whether inactive GameObjects should be included.")] bool includeInactive)
        {
            return _apiClient.Invoke(api => api.FindByName(name, includeInactive));
        }

        [McpServerTool(Name = "find_by_type", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<List<ObjectSearchResult>>))]
        [Description("Find scene GameObjects or Components by type full name.")]
        public ApiResult<List<ObjectSearchResult>> FindByType(
            [Description("Type full name or simple type name.")] string typeName,
            [Description("Whether inactive GameObjects/components should be included.")] bool includeInactive)
        {
            return _apiClient.Invoke(api => api.FindByType(typeName, includeInactive));
        }

        [McpServerTool(Name = "get_value", ReadOnly = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<ValueInfo>))]
        [Description("Get a field/property/path value from a Unity instance ID or stored GUID.")]
        public ApiResult<ValueInfo> GetValue(
            [Description("Unity instance ID or stored object GUID.")] string objectId,
            [Description("Reflection path, such as Text or A.B[3].C.")] string path,
            [Description("Whether non-primitive results should be stored and returned with a GUID.")] bool storeResult)
        {
            return _apiClient.Invoke(api => api.GetValue(objectId, path, storeResult));
        }

        [McpServerTool(Name = "set_value", ReadOnly = false, Destructive = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<ValueInfo>))]
        [Description("Set a primitive field/property/path value on a Unity instance ID or stored GUID.")]
        public ApiResult<ValueInfo> SetValue(
            [Description("Unity instance ID or stored object GUID.")] string objectId,
            [Description("Reflection path, such as Text or A.B[3].C.")] string path,
            [Description("JSON encoded primitive value.")] string valueJson,
            [Description("Whether the assigned value should be stored and returned with a GUID when applicable.")] bool storeResult)
        {
            return _apiClient.Invoke(api => api.SetValue(objectId, path, valueJson, storeResult));
        }

        [McpServerTool(Name = "jint_evaluate", ReadOnly = false, Destructive = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<ValueInfo>))]
        [Description("Evaluate JavaScript code using Jint. JS object will be returned directly. Non-primitive CLR object can be stored and returned with a GUID.")]
        public ApiResult<ValueInfo> JintEvaluate(
            [Description("JavaScript code to evaluate.")] string script,
            [Description("Whether non-primitive CLR results should be stored and returned with a GUID.")] bool storeResult)
        {
            return _apiClient.Invoke(api => api.JintEvaluate(script, storeResult));
        }

        [McpServerTool(Name = "call_method", ReadOnly = false, Destructive = true, UseStructuredContent = true, OutputSchemaType = typeof(ApiResult<ValueInfo>))]
        [Description("Call an instance or static method. Pass arguments as a JSON array.")]
        public ApiResult<ValueInfo> CallMethod(
            [Description("Unity instance ID or stored object GUID. Use empty string for static method calls.")] string objectId,
            [Description("Method path, such as AddComponent<UnityEngine.MeshRenderer> or UnityEngine.Object.Destroy.")] string path,
            [Description("JSON encoded argument array.")] string argumentsJson,
            [Description("Whether non-primitive return values should be stored and returned with a GUID.")] bool storeResult)
        {
            return _apiClient.Invoke(api => api.CallMethod(objectId, path, argumentsJson, storeResult));
        }
    }
}
