using System.Collections.Concurrent;

namespace HttpClientTestServer.SessionState;

public class SessionStateMiddleware
{
    public const string SessionStateKey = "SessionState";
    public const string SessionStateHeaderKey = "x-test-session-id";

    private readonly RequestDelegate _next;

    public SessionStateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(SessionStateHeaderKey, out var headerValues))
        {
            var sessionStates = context.RequestServices.GetRequiredKeyedService<ConcurrentDictionary<string, ConcurrentDictionary<string, object>>>(SessionStateKey);
            var sessionStateItems = sessionStates.GetOrAdd(headerValues.ToString(), _ => new ConcurrentDictionary<string, object>());
            context.Features.Set(new SessionStateFeature(sessionStateItems));
        }
        else
        {
            context.Features.Set(new SessionStateFeature(new ConcurrentDictionary<string, object>()));
        }
        await _next(context);
    }
}
