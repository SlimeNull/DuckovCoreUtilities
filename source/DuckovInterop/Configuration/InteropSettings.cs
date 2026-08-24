using SlimeNull.DuckovInterop;
using System;
using System.Net;
using UnityEngine;
using UnityEngine.Serialization;

namespace SlimeNull.DuckovInterop.Configuration
{
    internal sealed class InteropSettings : MonoBehaviour
    {
        [Serializable]
        private sealed class ServerOptions
        {
            [InspectorName("启用服务")]
            [Tooltip("启用供场景检视器使用的 JSON RPC 服务")]
            [FormerlySerializedAs("Server.Enabled")]
            public bool Enabled = true;

            [InspectorName("监听地址")]
            [Tooltip("重新启用服务后生效")]
            [FormerlySerializedAs("Server.Host")]
            public string Host = HierarchyInspectorRpcEndpoint.Host;

            [InspectorName("监听端口")]
            [Tooltip("重新启用服务后生效")]
            [Range(1024, 65535)]
            [FormerlySerializedAs("Server.Port")]
            public int Port = HierarchyInspectorRpcEndpoint.Port;

            [InspectorName("诊断日志")]
            [Tooltip("将 RPC 请求和错误写入游戏日志")]
            [FormerlySerializedAs("Server.DiagnosticLogging")]
            public bool DiagnosticLogging = false;
        }

        [SerializeField]
        [InspectorName("JSON RPC 服务")]
        private ServerOptions server = new ServerOptions();

        private ModBehaviour? _owner;

        public bool ServerEnabled => server.Enabled;
        public bool DiagnosticLogging => server.DiagnosticLogging;
        public string ListenHost => server.Host;
        public int ListenPort => server.Port;

        public void Initialize(ModBehaviour owner)
        {
            _owner = owner;
            OnValidate();
        }

        private void OnValidate()
        {
            server.Host = NormalizeAddress(server.Host);
            server.Port = Mathf.Clamp(server.Port, 1024, 65535);
            _owner?.ApplySettings(this);
        }

        private void DuckovModSettingsUpdated()
        {
            OnValidate();
        }

        private static string NormalizeAddress(string? value)
        {
            value = value?.Trim();
            if (!string.IsNullOrEmpty(value) && IPAddress.TryParse(value, out _))
            {
                return value;
            }

            Debug.LogWarning($"[DuckovInterop] Invalid listen address '{value}', restored to {HierarchyInspectorRpcEndpoint.Host}.");
            return HierarchyInspectorRpcEndpoint.Host;
        }
    }
}
