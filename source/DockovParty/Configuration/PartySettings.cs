using System;
using System.Net;
using UnityEngine;
using UnityEngine.Serialization;

namespace SlimeNull.DockovParty.Configuration
{
    internal sealed class PartySettings : MonoBehaviour
    {
        [Serializable]
        private sealed class IdentityOptions
        {
            [InspectorName("联机昵称")]
            [Tooltip("其他玩家在联机游戏中看到的名称")]
            [FormerlySerializedAs("Identity.PlayerName")]
            public string PlayerName = "玩家";
        }

        [Serializable]
        private sealed class NetworkOptions
        {
            [InspectorName("服主监听地址")]
            [Tooltip("下次开服时生效")]
            [FormerlySerializedAs("Network.ListenAddress")]
            public string ListenAddress = "0.0.0.0";

            [InspectorName("加入游戏地址")]
            [FormerlySerializedAs("Network.JoinAddress")]
            public string JoinAddress = "127.0.0.1";

            [InspectorName("端口")]
            [Range(1024, 65535)]
            [FormerlySerializedAs("Network.Port")]
            public int Port = 37622;

            [InspectorName("状态同步频率")]
            [Tooltip("每秒发送的玩家状态数量")]
            [Range(5, 30)]
            [FormerlySerializedAs("Network.StateRate")]
            public int StateRate = 15;

            [InspectorName("插值延迟")]
            [Tooltip("远程玩家移动的插值缓冲时间（秒）")]
            [Range(0.03f, 0.3f)]
            [FormerlySerializedAs("Network.InterpolationDelay")]
            public float InterpolationDelay = 0.1f;

            [InspectorName("诊断日志")]
            [Tooltip("将联机协议收发信息写入游戏日志")]
            [FormerlySerializedAs("Network.DiagnosticLogging")]
            public bool DiagnosticLogging = false;
        }

        [SerializeField]
        [InspectorName("玩家")]
        private IdentityOptions identity = new IdentityOptions();

        [SerializeField]
        [InspectorName("网络")]
        private NetworkOptions network = new NetworkOptions();

        public string ListenAddress => network.ListenAddress;
        public string JoinAddress => network.JoinAddress;
        public int Port => network.Port;
        public string PlayerName => identity.PlayerName;
        public int StateRate => network.StateRate;
        public float InterpolationDelay => network.InterpolationDelay;
        public bool DiagnosticLogging => network.DiagnosticLogging;

        private void OnValidate()
        {
            identity.PlayerName = NormalizePlayerName(identity.PlayerName);
            network.ListenAddress = NormalizeListenAddress(network.ListenAddress);
            network.JoinAddress = string.IsNullOrWhiteSpace(network.JoinAddress)
                ? "127.0.0.1"
                : network.JoinAddress.Trim();
            network.Port = Mathf.Clamp(network.Port, 1024, 65535);
            network.StateRate = Mathf.Clamp(network.StateRate, 5, 30);
            network.InterpolationDelay = Mathf.Clamp(network.InterpolationDelay, 0.03f, 0.3f);
        }

        private void DuckovModSettingsUpdated()
        {
            OnValidate();
        }

        private static string NormalizePlayerName(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "玩家" : value.Trim();
        }

        private static string NormalizeListenAddress(string? value)
        {
            value = value?.Trim();
            if (!string.IsNullOrEmpty(value) && IPAddress.TryParse(value, out _))
            {
                return value;
            }

            Debug.LogWarning($"[DockovParty] 无效监听地址 '{value}'，已恢复为 0.0.0.0。");
            return "0.0.0.0";
        }
    }
}
