namespace HttpClientTestServer.Launcher;

public interface ITestServer : IAsyncDisposable
{
    string BaseUri { get; }
}
