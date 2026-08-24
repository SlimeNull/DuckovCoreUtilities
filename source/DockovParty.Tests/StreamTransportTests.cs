using SlimeNull.DockovParty.Networking;
using SlimeNull.DockovParty.Networking.Protocol;
using System.Net;
using Xunit;

namespace SlimeNull.DockovParty.Tests;

public sealed class StreamTransportTests
{
    [Fact]
    public async Task Tcp_listener_and_connector_keep_one_stream_open_for_multiple_frames()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpStreamListener(IPAddress.Loopback, 0);
        listener.Listen();
        var endpoint = Assert.IsType<IPEndPoint>(listener.LocalEndPoint);

        var acceptTask = listener.AcceptAsync(timeout.Token);
        var connector = new TcpStreamConnector(IPAddress.Loopback.ToString(), endpoint.Port);
        using var client = await connector.ConnectAsync(timeout.Token);
        using var server = await acceptTask;

        for (var i = 0; i < 4; i++)
        {
            await PartyWireCodec.WriteAsync(
                client.Stream,
                new PingMessage { Timestamp = i },
                timeout.Token);
            var ping = Assert.IsType<PingMessage>(
                await PartyWireCodec.ReadAsync(server.Stream, timeout.Token));
            Assert.Equal(i, ping.Timestamp);

            await PartyWireCodec.WriteAsync(
                server.Stream,
                new NoticeMessage { Text = $"reply-{i}" },
                timeout.Token);
            var reply = Assert.IsType<NoticeMessage>(
                await PartyWireCodec.ReadAsync(client.Stream, timeout.Token));
            Assert.Equal($"reply-{i}", reply.Text);
        }

        Assert.False(client.Stream is null);
        Assert.False(server.Stream is null);
    }

    [Fact]
    public async Task Accept_requires_listen_to_be_called_first()
    {
        using var listener = new TcpStreamListener(IPAddress.Loopback, 0);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => listener.AcceptAsync(CancellationToken.None));
    }
}
