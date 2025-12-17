using System.Diagnostics;
using System.Security.Authentication;
using HttpClientTestServer.ConnectionState;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace HttpClientTestServer.Launcher;

public class InProcessTestServer : ITestServer
{
    private readonly ServerApplication _server;
    private readonly bool _listeningOnUnixDomainSocket;

    public int Port { get; }
    public bool IsSecure { get; }

    public string BaseUri => _listeningOnUnixDomainSocket
        ? "http://localhost"
        : $"{(IsSecure ? "https" : "http")}://localhost:{Port}";

    private InProcessTestServer(int port, TestServerListenMode listenMode, ILoggerProvider? loggerProvider, TestServerOptions? testServerOptions)
    {
        Port = port;
        IsSecure = listenMode is TestServerListenMode.SecureHttp1Only or
            TestServerListenMode.SecureHttp2Only or
            TestServerListenMode.SecureHttp1AndHttp2;

        _listeningOnUnixDomainSocket = testServerOptions?.UnixDomainSocketPath != null;
        _server = new ServerApplication([]);
        
        _server.ConfigureBuilder(builder =>
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                if (testServerOptions?.UnixDomainSocketPath is { } unixDomainSocketPath)
                {
                    options.ListenUnixSocket(unixDomainSocketPath, listenOptions =>
                    {
                        listenOptions.Protocols = listenMode switch
                        {
                            TestServerListenMode.InsecureHttp1Only => HttpProtocols.Http1,
                            TestServerListenMode.InsecureHttp2Only => HttpProtocols.Http2,
                            TestServerListenMode.SecureHttp1Only => HttpProtocols.Http1,
                            TestServerListenMode.SecureHttp2Only => HttpProtocols.Http2,
                            //TestServerListenMode.SecureHttp1AndHttp2 => HttpProtocols.Http1AndHttp2,
                            _ => throw new NotSupportedException(),
                        };
                        
                        listenOptions.UseConnectionState();
                    });
                    // hyperlocal uses the 'unix' scheme and passes the URI to hyper. As a result, the ':scheme' header in the request is set to 'unix'.
                    // By default, Kestrel does not accept non-HTTP schemes. To allow non-HTTP schemes, we need to set 'AllowAlternateSchemes' to true.
                    options.AllowAlternateSchemes = true;
                }
                else
                {
                    options.ListenLocalhost(port, listenOptions =>
                    {
                        listenOptions.Protocols = listenMode switch
                        {
                            TestServerListenMode.InsecureHttp1Only => HttpProtocols.Http1,
                            TestServerListenMode.InsecureHttp2Only => HttpProtocols.Http2,
                            TestServerListenMode.SecureHttp1Only => HttpProtocols.Http1,
                            TestServerListenMode.SecureHttp2Only => HttpProtocols.Http2,
                            TestServerListenMode.SecureHttp1AndHttp2 => HttpProtocols.Http1AndHttp2,
                            _ => throw new NotSupportedException(),
                        };

                        if (IsSecure)
                        {
                            listenOptions.UseHttps();
                        }

                        listenOptions.UseConnectionState();
                    });
                }
                
                if (testServerOptions?.SslProtocols is { } sslProtocols)
                {
                    options.ConfigureHttpsDefaults(options =>
                    {
                        options.SslProtocols = sslProtocols;
                    });
                }
            });
            if (loggerProvider is not null)
            {
                if (Debugger.IsAttached)
                {
                    builder.Logging.SetMinimumLevel(LogLevel.Trace);
                }
                builder.Logging.AddProvider(loggerProvider);
            }
        });
    }

    public Task StartAsync() => _server.StartAsync();

    public static async Task<ITestServer> LaunchAsync(TestServerListenMode listenMode, ILoggerProvider? loggerProvider = null, CancellationToken shutdownToken = default, TestServerOptions? options = null)
    {
        var port = TestServerHelper.GetUnusedEphemeralPort();
        var server = new InProcessTestServer(port, listenMode, loggerProvider, options);
        await server.StartAsync();

        shutdownToken.Register(() =>
        {
            server.Shutdown();
        });

        return server;
    }

    public void Shutdown()
    {
        _server.Shutdown();
    }

    public ValueTask DisposeAsync()
    {
        return _server.DisposeAsync();
    }
}

public record TestServerOptions(SslProtocols? SslProtocols, string? UnixDomainSocketPath);

public enum TestServerListenMode
{
    InsecureHttp1Only,
    InsecureHttp2Only,
    SecureHttp1Only,
    SecureHttp2Only,
    SecureHttp1AndHttp2,
}
