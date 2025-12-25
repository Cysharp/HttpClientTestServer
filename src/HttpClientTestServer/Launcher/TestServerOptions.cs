using System.Security.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace HttpClientTestServer.Launcher;

public record TestServerOptions(HttpProtocols HttpProtocols, bool IsSecure)
{
    public SslProtocols? SslProtocols { get; init; }
    public string? UnixDomainSocketPath { get; init; }
    public int? Port { get; init; }
    public bool LocalhostOnly { get; init; } = true;
    public bool EnableClientCertificateValidation { get; init; } = false;

    public static TestServerOptions CreateFromListenMode(TestServerListenMode listenMode)
    {
        var (httpProtocols, isSecure) = listenMode switch
        {
            TestServerListenMode.InsecureHttp1Only => (HttpProtocols.Http1, false),
            TestServerListenMode.InsecureHttp2Only => (HttpProtocols.Http2, false),
            TestServerListenMode.SecureHttp1Only => (HttpProtocols.Http1, true),
            TestServerListenMode.SecureHttp2Only => (HttpProtocols.Http2, true),
            TestServerListenMode.SecureHttp1AndHttp2 => (HttpProtocols.Http1AndHttp2, true),
            _ => throw new NotSupportedException(),
        };

        return new TestServerOptions(httpProtocols, isSecure);
    }
}