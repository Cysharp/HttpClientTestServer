using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Connections;

namespace HttpClientTestServer.ConnectionState;

public static class ListenOptionsExtensions
{
    public static void UseConnectionState(this ListenOptions listenOptions)
    {
        var state = listenOptions.ApplicationServices.GetRequiredService<ConnectionStateService>();
        listenOptions.Use((next) => async (ctx) =>
        {
            try
            {
                state.Connected(ctx.ConnectionId);
                await next(ctx);
            }
            finally
            {
                state.Disconnected(ctx.ConnectionId);
            }
        });
    }
}