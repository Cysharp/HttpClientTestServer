using System.Collections.Concurrent;

namespace HttpClientTestServer.SessionState;

public static class SessionStateServiceCollectionExtensions
{
    public static IServiceCollection AddSessionState(this IServiceCollection services)  
    {
        services.AddKeyedSingleton(SessionStateMiddleware.SessionStateKey, new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>());
        return services;
    }
}
