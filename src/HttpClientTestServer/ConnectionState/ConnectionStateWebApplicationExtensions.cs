namespace HttpClientTestServer.ConnectionState;

public static class ConnectionStateWebApplicationExtensions
{
    public static void MapConnectionState(this WebApplication app)
    {
        app.MapGet("/connection-state/active-connections", (HttpContext httpContext, ConnectionStateService state) =>
        {
            return Results.Content(string.Join(",", state.ActiveConnectionsById.Keys), "text/plain");
        });
        app.MapGet("/connection-state/current", (HttpContext httpContext, ConnectionStateService state) =>
        {
            return new ActiveConnectionsResponse(
                ActiveConnections: state.ActiveConnections,
                ActiveConnectionIds: state.ActiveConnectionsById.Keys.ToArray(),
                CurrentConnectionId: httpContext.Connection.Id
            );
        });
    }
}
public record ActiveConnectionsResponse(int ActiveConnections, string[] ActiveConnectionIds, string CurrentConnectionId);