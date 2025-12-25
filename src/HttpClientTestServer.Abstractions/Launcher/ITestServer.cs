using System;
using System.Threading.Tasks;

namespace HttpClientTestServer.Launcher
{
    public interface ITestServer : IAsyncDisposable
    {
        string BaseUri { get; }
        Task Stopped { get; }
    }
}

