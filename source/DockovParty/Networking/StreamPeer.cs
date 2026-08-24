using SlimeNull.DockovParty.Networking.Protocol;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SlimeNull.DockovParty.Networking
{
    internal sealed class StreamPeer : IDisposable
    {
        private readonly StreamConnection _connection;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _stopSource = new CancellationTokenSource();
        private int _closed;
        private Task? _receiveTask;

        public StreamPeer(StreamConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public string RemoteEndPoint => _connection.RemoteEndPoint;
        public bool IsClosed => Volatile.Read(ref _closed) != 0;

        public event Action<PartyMessage>? MessageReceived;
        public event Action<Exception?>? Closed;

        public void Start()
        {
            if (_receiveTask != null)
            {
                throw new InvalidOperationException("The peer receive loop is already running.");
            }

            _receiveTask = Task.Run(ReceiveLoopAsync);
        }

        public async Task SendAsync(PartyMessage message, CancellationToken cancellationToken = default)
        {
            if (IsClosed)
            {
                throw new ObjectDisposedException(nameof(StreamPeer));
            }

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _stopSource.Token))
            {
                await _writeLock.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    await PartyWireCodec.WriteAsync(
                        _connection.Stream,
                        message,
                        linked.Token).ConfigureAwait(false);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
        }

        public void Dispose()
        {
            Close(null);
        }

        private async Task ReceiveLoopAsync()
        {
            Exception? failure = null;
            try
            {
                while (!_stopSource.IsCancellationRequested)
                {
                    var message = await PartyWireCodec.ReadAsync(
                        _connection.Stream,
                        _stopSource.Token).ConfigureAwait(false);
                    if (message == null)
                    {
                        break;
                    }

                    MessageReceived?.Invoke(message);
                }
            }
            catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
            {
            }
            catch (IOException ex)
            {
                failure = ex;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                Close(failure);
            }
        }

        private void Close(Exception? failure)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }

            try
            {
                _stopSource.Cancel();
            }
            catch
            {
            }

            _connection.Dispose();
            Closed?.Invoke(failure);
        }
    }
}
