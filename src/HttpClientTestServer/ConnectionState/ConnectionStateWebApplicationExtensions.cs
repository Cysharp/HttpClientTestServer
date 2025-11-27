namespace HttpClientTestServer.ConnectionState;

public static class ConnectionStateWebApplicationExtensions
{
    public static void MapConnectionState(this WebApplication app)
    {
        app.MapGet("/connection-state/active-connections", (ConnectionStateService state) =>
        {
            return $"""
                    activeConnections = {state.ActiveConnections}
                    activeConnectionIds = {string.Join(",", state.ActiveConnectionsById.Keys.ToArray())}
                    """;
        });
    }
}