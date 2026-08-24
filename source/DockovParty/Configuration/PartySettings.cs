using ModSetting.Api;
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace SlimeNull.DockovParty.Configuration
{
    internal sealed class PartySettings
    {
        public string ListenAddress { get; private set; } = "0.0.0.0";
        public string JoinAddress { get; private set; } = "127.0.0.1";
        public int Port { get; private set; } = 37622;
        public string PlayerName { get; private set; } = "玩家";
        public int StateRate { get; private set; } = 15;
        public float InterpolationDelay { get; private set; } = 0.1f;
        public bool DiagnosticLogging { get; private set; }

        public void Configure(Duckov.Modding.ModInfo modInfo)
        {
            var builder = SettingsBuilder.Create(modInfo) ??
                throw new InvalidOperationException("ModSetting is not available.");

            ListenAddress = Load(builder, "Network.ListenAddress", ListenAddress);
            JoinAddress = Load(builder, "Network.JoinAddress", JoinAddress);
            Port = Mathf.Clamp(Load(builder, "Network.Port", Port), 1024, 65535);
            PlayerName = NormalizePlayerName(Load(builder, "Identity.PlayerName", PlayerName));
            StateRate = Mathf.Clamp(Load(builder, "Network.StateRate", StateRate), 5, 30);
            InterpolationDelay = Mathf.Clamp(Load(builder, "Network.InterpolationDelay", InterpolationDelay), 0.03f, 0.3f);
            DiagnosticLogging = Load(builder, "Network.DiagnosticLogging", DiagnosticLogging);

            builder
                .AddInput("Identity.PlayerName", "联机昵称", PlayerName, 24,
                    value => PlayerName = NormalizePlayerName(value))
                .AddGroup("Identity.Group", "玩家", new List<string>
                {
                    "Identity.PlayerName",
                })
                .AddInput("Network.ListenAddress", "服主监听地址（下次开服生效）", ListenAddress, 45, value =>
                {
                    if (IPAddress.TryParse(value, out _))
                    {
                        ListenAddress = value;
                    }
                    else
                    {
                        Debug.LogError($"[DockovParty] 无效监听地址: {value}");
                    }
                })
                .AddInput("Network.JoinAddress", "加入游戏地址", JoinAddress, 255, value =>
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        JoinAddress = value.Trim();
                    }
                })
                .AddSlider("Network.Port", "端口", Port, 1024, 65535,
                    value => Port = value, 5)
                .AddSlider("Network.StateRate", "状态同步频率", StateRate, 5, 30,
                    value => StateRate = value)
                .AddSlider("Network.InterpolationDelay", "插值延迟（秒）", InterpolationDelay,
                    new Vector2(0.03f, 0.3f), value => InterpolationDelay = value, 2)
                .AddToggle("Network.DiagnosticLogging", "输出联机诊断日志", DiagnosticLogging,
                    value => DiagnosticLogging = value)
                .AddGroup("Network.Group", "网络", new List<string>
                {
                    "Network.ListenAddress",
                    "Network.JoinAddress",
                    "Network.Port",
                    "Network.StateRate",
                    "Network.InterpolationDelay",
                    "Network.DiagnosticLogging",
                });
        }

        private static string NormalizePlayerName(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "玩家" : value.Trim();
        }

        private static T Load<T>(SettingsBuilder builder, string key, T fallback)
        {
            return builder.GetSavedValue<T>(key, out var value) ? value : fallback;
        }
    }
}
