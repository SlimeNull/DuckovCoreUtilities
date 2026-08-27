using SlimeNull.DockovParty.Networking.Protocol;
using SlimeNull.DockovParty.Localization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlimeNull.DockovParty.Networking
{
    internal enum PartyRole
    {
        None,
        Host,
        Client,
    }

    internal sealed class PartySession : IDisposable
    {
        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(12);

        private readonly object _gate = new object();
        private CancellationTokenSource? _lifetimeSource;
        private IStreamListener? _listener;
        private StreamPeer? _peer;
        private Task? _acceptTask;
        private Task? _keepAliveTask;
        private long _lastReceiveTicks;
        private bool _disposed;

        public PartyRole Role { get; private set; }
        public bool HasPeer
        {
            get
            {
                lock (_gate)
                {
                    return _peer != null && !_peer.IsClosed;
                }
            }
        }

        public event Action<PartyRole>? Started;
        public event Action<string>? PeerConnected;
        public event Action<PartyMessage>? MessageReceived;
        public event Action<Exception?>? PeerClosed;
        public event Action<Exception>? Failed;

        public void StartHost(IStreamListener listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            lock (_gate)
            {
                EnsureCanStart();
                try
                {
                    listener.Listen();
                }
                catch
                {
                    listener.Dispose();
                    throw;
                }

                var lifetimeSource = new CancellationTokenSource();
                _lifetimeSource = lifetimeSource;
                _listener = listener;
                Role = PartyRole.Host;
                _acceptTask = Task.Run(() => AcceptLoopAsync(lifetimeSource.Token));
                _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(lifetimeSource.Token));
            }

            Started?.Invoke(PartyRole.Host);
        }

        public async Task StartClientAsync(
            IStreamConnector connector,
            CancellationToken cancellationToken = default)
        {
            if (connector == null)
            {
                throw new ArgumentNullException(nameof(connector));
            }

            CancellationTokenSource lifetimeSource;
            lock (_gate)
            {
                EnsureCanStart();
                _lifetimeSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                lifetimeSource = _lifetimeSource;
                Role = PartyRole.Client;
            }

            try
            {
                var connection = await connector.ConnectAsync(lifetimeSource.Token).ConfigureAwait(false);
                AttachPeer(connection);
                _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(lifetimeSource.Token));
                Started?.Invoke(PartyRole.Client);
            }
            catch
            {
                Stop();
                throw;
            }
        }

        public Task SendAsync(PartyMessage message, CancellationToken cancellationToken = default)
        {
            StreamPeer? peer;
            lock (_gate)
            {
                peer = _peer;
            }

            if (peer == null || peer.IsClosed)
            {
                throw new InvalidOperationException("No party peer is connected.");
            }

            return peer.SendAsync(message, cancellationToken);
        }

        public void Send(PartyMessage message)
        {
            Task task;
            try
            {
                task = SendAsync(message);
            }
            catch (Exception ex)
            {
                Failed?.Invoke(ex);
                return;
            }

            _ = task.ContinueWith(
                completed => Failed?.Invoke(completed.Exception?.GetBaseException() ??
                    new InvalidOperationException("Party message send failed.")),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        public void Stop()
        {
            CancellationTokenSource? source;
            IStreamListener? listener;
            StreamPeer? peer;
            lock (_gate)
            {
                source = _lifetimeSource;
                listener = _listener;
                peer = _peer;
                _lifetimeSource = null;
                _listener = null;
                _peer = null;
                _acceptTask = null;
                _keepAliveTask = null;
                Role = PartyRole.None;
            }

            try
            {
                source?.Cancel();
            }
            catch
            {
            }

            listener?.Dispose();
            peer?.Dispose();
            source?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    IStreamListener? listener;
                    lock (_gate)
                    {
                        listener = _listener;
                    }

                    if (listener == null)
                    {
                        return;
                    }

                    var connection = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                    if (HasPeer)
                    {
                        await RejectAdditionalPeerAsync(connection, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    AttachPeer(connection);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Failed?.Invoke(ex);
                    }

                    return;
                }
            }
        }

        private void AttachPeer(StreamConnection connection)
        {
            var peer = new StreamPeer(connection);
            peer.MessageReceived += OnPeerMessageReceived;
            peer.Closed += OnPeerClosed;

            lock (_gate)
            {
                if (_peer != null && !_peer.IsClosed)
                {
                    peer.Dispose();
                    throw new InvalidOperationException("A party peer is already connected.");
                }

                _peer = peer;
                Interlocked.Exchange(ref _lastReceiveTicks, DateTime.UtcNow.Ticks);
            }

            peer.Start();
            PeerConnected?.Invoke(peer.RemoteEndPoint);
        }

        private void OnPeerMessageReceived(PartyMessage message)
        {
            Interlocked.Exchange(ref _lastReceiveTicks, DateTime.UtcNow.Ticks);
            if (message is PingMessage ping)
            {
                Send(new PongMessage { Timestamp = ping.Timestamp });
                return;
            }

            if (message is PongMessage)
            {
                return;
            }

            MessageReceived?.Invoke(message);
        }

        private void OnPeerClosed(Exception? failure)
        {
            StreamPeer? oldPeer = null;
            var stopClient = false;
            lock (_gate)
            {
                if (_peer != null && _peer.IsClosed)
                {
                    oldPeer = _peer;
                    _peer = null;
                }

                stopClient = Role == PartyRole.Client;
            }

            oldPeer?.Dispose();
            PeerClosed?.Invoke(failure);
            if (stopClient)
            {
                Stop();
            }
        }

        private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(KeepAliveInterval, cancellationToken).ConfigureAwait(false);
                    if (!HasPeer)
                    {
                        continue;
                    }

                    var lastReceive = new DateTime(
                        Interlocked.Read(ref _lastReceiveTicks),
                        DateTimeKind.Utc);
                    if (DateTime.UtcNow - lastReceive > ConnectionTimeout)
                    {
                        StreamPeer? peer;
                        lock (_gate)
                        {
                            peer = _peer;
                        }

                        peer?.Dispose();
                        continue;
                    }

                    Send(new PingMessage { Timestamp = DateTime.UtcNow.Ticks });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Failed?.Invoke(ex);
                }
            }
        }

        private static async Task RejectAdditionalPeerAsync(
            StreamConnection connection,
            CancellationToken cancellationToken)
        {
            using (connection)
            {
                await PartyWireCodec.WriteAsync(
                    connection.Stream,
                    new ErrorMessage
                    {
                        Code = "party-full",
                        Description = SettingsText.PartyFull,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private void EnsureCanStart()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PartySession));
            }

            if (Role != PartyRole.None || _lifetimeSource != null)
            {
                throw new InvalidOperationException("A party session is already running.");
            }
        }
    }
}
