using System;
using System.Security.Authentication;

namespace HttpClientTestServer.Launcher
{
    public class TestServerOptions
    {
        public TestServerOptions(ListenHttpProtocols httpProtocols, bool isSecure)
        {
            HttpProtocols = httpProtocols;
            IsSecure = isSecure;
        }

        public ListenHttpProtocols HttpProtocols { get; set; }
        public bool IsSecure { get; set; }
        public SslProtocols? SslProtocols { get; set; }
        public string? UnixDomainSocketPath { get; set; }
        public int? Port { get; set; }
        public bool LocalhostOnly { get; set; } = true;
        public bool EnableClientCertificateValidation { get; set; } = false;

        public static TestServerOptions CreateFromListenMode(TestServerListenMode listenMode)
        {
            var (httpProtocols, isSecure) = listenMode switch
            {
                TestServerListenMode.InsecureHttp1Only => (ListenHttpProtocols.Http1, false),
                TestServerListenMode.InsecureHttp2Only => (ListenHttpProtocols.Http2, false),
                TestServerListenMode.SecureHttp1Only => (ListenHttpProtocols.Http1, true),
                TestServerListenMode.SecureHttp2Only => (ListenHttpProtocols.Http2, true),
                TestServerListenMode.SecureHttp1AndHttp2 => (ListenHttpProtocols.Http1AndHttp2, true),
                _ => throw new NotSupportedException(),
            };

            return new TestServerOptions(httpProtocols, isSecure);
        }
    }

    public enum TestServerListenMode
    {
        InsecureHttp1Only,
        InsecureHttp2Only,
        SecureHttp1Only,
        SecureHttp2Only,
        SecureHttp1AndHttp2,
    }
}

