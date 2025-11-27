using System.Collections.Concurrent;

namespace HttpClientTestServer.ConnectionState;

public class ConnectionStateService
{
    private int _activeConnections;

    public ConcurrentDictionary<string, bool> ActiveConnectionsById { get; } = new();
    public int ActiveConnections => _activeConnections;

    public void Connected(string connectionId)
    {
        if (ActiveConnectionsById.TryAdd(connectionId, true))
        {
            Interlocked.Increment(ref _activeConnections);
        }
    }

    public void Disconnected(string connectionId)
    {
        if (ActiveConnectionsById.TryRemove(connectionId, out _))
        {
            Interlocked.Decrement(ref _activeConnections);
        }
    }
}