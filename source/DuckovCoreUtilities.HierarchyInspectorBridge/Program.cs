using EleCho.JsonRpc;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlimeNull.DuckovCoreUtilities.HierarchyInspector;
using System;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;

namespace SlimeNull.DuckovCoreUtilities.HierarchyInspectorBridge
{
    internal sealed class Program
    {
        private const string PipeName = "SlimeNull.DuckovCoreUtilities.HierachyInspector";

        private static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await pipe.ConnectAsync().ConfigureAwait(false);

                using (var rpcClient = new RpcClient<IHierarchyInspectorRpc>(pipe))
                {
                    rpcClient.DisposeBaseStream = false;

                    var hier = rpcClient.Remote.Test("abaaba");

                    var tools = new HierarchyInspectorMcpTools(rpcClient.Remote);
                    var transport = new StdioServerTransport(PipeName);
                    var server = McpServer.Create(transport, CreateServerOptions(tools));
                    await server.RunAsync().ConfigureAwait(false);
                }
            }
        }

        private static McpServerOptions CreateServerOptions(HierarchyInspectorMcpTools tools)
        {
            var toolCollection = new McpServerPrimitiveCollection<McpServerTool>
            {
                CreateTool(tools, nameof(HierarchyInspectorMcpTools.GetHierarchy), "get_hierarchy", "Return loaded Unity scene hierarchy trees with GameObject and component instance IDs.", true),
                CreateTool(tools, nameof(HierarchyInspectorMcpTools.FindByName), "find_by_name", "Find scene GameObjects by name.", true),
                CreateTool(tools, nameof(HierarchyInspectorMcpTools.FindByType), "find_by_type", "Find scene GameObjects or Components by type full name.", true),
                CreateTool(tools, nameof(HierarchyInspectorMcpTools.GetValue), "get_value", "Get a field/property/path value from a Unity instance ID or stored GUID.", true),
                CreateTool(tools, nameof(HierarchyInspectorMcpTools.SetValue), "set_value", "Set a primitive field/property/path value on a Unity instance ID or stored GUID.", false),
                CreateTool(tools, nameof(HierarchyInspectorMcpTools.CallMethod), "call_method", "Call an instance or static method. Pass arguments as JSON array.", false)
            };

            return new McpServerOptions
            {
                ServerInfo = new Implementation
                {
                    Name = PipeName,
                    Title = "Duckov Hierarchy Inspector",
                    Version = "1.0.0"
                },
                ToolCollection = toolCollection,
                ServerInstructions = "Inspect and manipulate Unity hierarchy objects through instance IDs and stored object GUIDs."
            };
        }

        private static McpServerTool CreateTool(object target, string methodName, string toolName, string description, bool readOnly)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            return McpServerTool.Create(method, target, new McpServerToolCreateOptions
            {
                Name = toolName,
                Description = description,
                ReadOnly = readOnly
            });
        }
    }

    public sealed class HierarchyInspectorMcpTools
    {
        private readonly IHierarchyInspectorRpc _rpc;

        public HierarchyInspectorMcpTools(IHierarchyInspectorRpc rpc)
        {
            _rpc = rpc;
        }

        public string GetHierarchy()
        {
            return _rpc.GetHierarchy();
        }

        public string FindByName(string name, bool includeInactive)
        {
            return _rpc.FindByName(name, includeInactive);
        }

        public string FindByType(string typeName, bool includeInactive)
        {
            return _rpc.FindByType(typeName, includeInactive);
        }

        public string GetValue(string objectId, string path, bool storeResult)
        {
            return _rpc.GetValue(objectId, path, storeResult);
        }

        public string SetValue(string objectId, string path, string valueJson, bool storeResult)
        {
            return _rpc.SetValue(objectId, path, valueJson, storeResult);
        }

        public string CallMethod(string objectId, string path, string argumentsJson, bool storeResult)
        {
            return _rpc.CallMethod(objectId, path, argumentsJson, storeResult);
        }
    }
}
