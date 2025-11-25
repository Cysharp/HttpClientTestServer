using System.Collections.Concurrent;

namespace HttpClientTestServer.SessionState;

public static class SessionStateEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSessionState(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/session-state", (HttpContext ctx, string id, string key) =>
        {
            var sessionStates = ctx.RequestServices.GetRequiredKeyedService<ConcurrentDictionary<string, ConcurrentDictionary<string, object>>>(SessionStateMiddleware.SessionStateHeaderKey);
            if (sessionStates.TryGetValue(id, out var items))
            {
                if (items.TryGetValue(key, out var value))
                {
                    return Results.Content(value.ToString());
                }
                return Results.Content(string.Empty);
            }

            return Results.NotFound();
        });

        return builder;
    }
}