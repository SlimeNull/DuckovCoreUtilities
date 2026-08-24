using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SlimeNull.DockovParty.Networking
{
    public sealed class StreamConnection : IDisposable
    {
        private readonly IDisposable? _owner;

        public StreamConnection(Stream stream, string remoteEndPoint, IDisposable? owner = null)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            RemoteEndPoint = remoteEndPoint ?? string.Empty;
            _owner = owner;
        }

        public Stream Stream { get; }
        public string RemoteEndPoint { get; }

        public void Dispose()
        {
            try
            {
                Stream.Dispose();
            }
            finally
            {
                _owner?.Dispose();
            }
        }
    }

    public interface IStreamListener : IDisposable
    {
        void Listen();
        Task<StreamConnection> AcceptAsync(CancellationToken cancellationToken);
    }

    public interface IStreamConnector
    {
        Task<StreamConnection> ConnectAsync(CancellationToken cancellationToken);
    }

    public sealed class TcpStreamListener : IStreamListener
    {
        private readonly TcpListener _listener;
        private bool _started;

        public TcpStreamListener(IPAddress address, int port)
        {
            _listener = new TcpListener(address ?? throw new ArgumentNullException(nameof(address)), port);
        }

        public IPEndPoint? LocalEndPoint => _started ? _listener.LocalEndpoint as IPEndPoint : null;

        public void Listen()
        {
            if (_started)
            {
                return;
            }

            _listener.Start(2);
            _started = true;
        }

        public async Task<StreamConnection> AcceptAsync(CancellationToken cancellationToken)
        {
            if (!_started)
            {
                throw new InvalidOperationException("Listen must be called before AcceptAsync.");
            }

            using (cancellationToken.Register(Stop))
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                Configure(client);
                return new StreamConnection(
                    client.GetStream(),
                    client.Client.RemoteEndPoint?.ToString() ?? string.Empty,
                    client);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
        }

        internal static void Configure(TcpClient client)
        {
            client.NoDelay = true;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            client.ReceiveBufferSize = 256 * 1024;
            client.SendBufferSize = 256 * 1024;
        }
    }

    public sealed class TcpStreamConnector : IStreamConnector
    {
        private readonly string _host;
        private readonly int _port;

        public TcpStreamConnector(string host, int port)
        {
            _host = string.IsNullOrWhiteSpace(host) ?
                throw new ArgumentException("Host is required.", nameof(host)) : host.Trim();
            _port = port;
        }

        public async Task<StreamConnection> ConnectAsync(CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            using (cancellationToken.Register(client.Dispose))
            {
                try
                {
                    await client.ConnectAsync(_host, _port).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    TcpStreamListener.Configure(client);
                    return new StreamConnection(
                        client.GetStream(),
                        client.Client.RemoteEndPoint?.ToString() ?? $"{_host}:{_port}",
                        client);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            }
        }
    }
}
