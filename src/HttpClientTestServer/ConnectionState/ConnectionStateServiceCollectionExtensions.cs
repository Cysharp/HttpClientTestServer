namespace HttpClientTestServer.ConnectionState;

public static class ConnectionStateServiceCollectionExtensions
{
    public static IServiceCollection AddConnectionState(this IServiceCollection services)
    {
        services.AddSingleton<ConnectionStateService>();
        return services;
    }
}