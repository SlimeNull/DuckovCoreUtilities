using EleCho.JsonRpc;
using SlimeNull.DuckovInterop;
using System.Net.Sockets;

namespace DockovInterop.HierarchyInspector;

internal sealed class DuckovRpcConnection : IDisposable
{
    private readonly object _sync = new();
    private TcpClient? _tcpClient;
    private RpcClient<IHierarchyInspectorRpc>? _rpcClient;

    public ApiResult<T> Invoke<T>(Func<IHierarchyInspectorRpc, ApiResult<T>> action)
    {
        try
        {
            return action(EnsureConnected());
        }
        catch
        {
            ResetConnection();
            try
            {
                return action(EnsureConnected());
            }
            catch (Exception ex)
            {
                return ApiResult<T>.Failure("Unable to connect to DuckovInterop. Make sure the game is running and the mod is enabled. " + ex.Message);
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
            var tcpClient = new TcpClient { NoDelay = true };
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
            return client.Connected && !(client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0);
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
            try { _rpcClient?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            _rpcClient = null;
            _tcpClient = null;
        }
    }

    public void Dispose() => ResetConnection();
}
