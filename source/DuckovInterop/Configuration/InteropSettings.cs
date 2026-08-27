using SlimeNull.DuckovInterop;
using System;
using System.Net;
using UnityEngine;

namespace SlimeNull.DuckovInterop.Configuration
{
    internal sealed class InteropSettings : MonoBehaviour
    {
        [Serializable]
        private sealed class ServerOptions
        {
            [InspectorName("@SettingsText/ServerEnabled")]
            [Tooltip("@SettingsText/ServerEnabledTooltip")]
            public bool Enabled = true;

            [InspectorName("@SettingsText/ListenAddress")]
            [Tooltip("@SettingsText/RestartServiceTooltip")]
            public string Host = HierarchyInspectorRpcEndpoint.Host;

            [InspectorName("@SettingsText/ListenPort")]
            [Tooltip("@SettingsText/RestartServiceTooltip")]
            [Range(1024, 65535)]
            public int Port = HierarchyInspectorRpcEndpoint.Port;

            [InspectorName("@SettingsText/DiagnosticLogging")]
            [Tooltip("@SettingsText/DiagnosticLoggingTooltip")]
            public bool DiagnosticLogging = false;
        }

        [SerializeField]
        [InspectorName("@SettingsText/ServerGroup")]
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
