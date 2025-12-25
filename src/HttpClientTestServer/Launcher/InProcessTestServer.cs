using HttpClientTestServer.ConnectionState;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Diagnostics;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace HttpClientTestServer.Launcher;

public class InProcessTestServer : ITestServer
{
    private readonly ServerApplication _server;
    private readonly bool _listeningOnUnixDomainSocket;

    public int Port { get; }
    public bool IsSecure { get; }
    public Task Stopped => _server.Stopped;

    public string BaseUri => _listeningOnUnixDomainSocket
        ? "http://localhost"
        : $"{(IsSecure ? "https" : "http")}://localhost:{Port}";

    private InProcessTestServer(TestServerOptions testServerOptions, ILoggerProvider? loggerProvider)
    {
        Port = testServerOptions.Port ?? TestServerHelper.GetUnusedEphemeralPort();
        IsSecure = testServerOptions.IsSecure;

        _listeningOnUnixDomainSocket = testServerOptions.UnixDomainSocketPath != null;
        _server = new ServerApplication([]);

        var protocols = testServerOptions.HttpProtocols;
        var sslProtocols = testServerOptions.SslProtocols ?? SslProtocols.Tls13;

        _server.ConfigureBuilder(builder =>
        {
#pragma warning disable ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
            var logger = builder.Logging.Services.BuildServiceProvider().GetRequiredService<ILogger<InProcessTestServer>>();
#pragma warning restore ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'

            builder.WebHost.ConfigureKestrel(options =>
            {
                if (testServerOptions.UnixDomainSocketPath is { } unixDomainSocketPath)
                {
                    logger.LogInformation("Configuring server on Unix Domain Socket ({Path}) (Protocol: {Protocol})", unixDomainSocketPath, protocols);

                    options.ListenUnixSocket(unixDomainSocketPath, listenOptions =>
                    {
                        listenOptions.Protocols = protocols;
                        listenOptions.UseConnectionState();
                    });
                    // hyperlocal uses the 'unix' scheme and passes the URI to hyper. As a result, the ':scheme' header in the request is set to 'unix'.
                    // By default, Kestrel does not accept non-HTTP schemes. To allow non-HTTP schemes, we need to set 'AllowAlternateSchemes' to true.
                    options.AllowAlternateSchemes = true;
                }
                else
                {
                    logger.LogInformation("Configuring server on port {Port} (Secure: {Secure}, Protocol: {Protocol}, SslProtocols: {SslProtocols}, EnableClientCertificateValidation: {EnableClientCertificateValidation})",
                        Port, IsSecure, protocols, sslProtocols, testServerOptions.EnableClientCertificateValidation);
                    if (testServerOptions.LocalhostOnly)
                    {
                        options.ListenLocalhost(Port, ConfigureHttpListenOptions);
                    }
                    else
                    {
                        options.ListenAnyIP(Port, ConfigureHttpListenOptions);
                    }
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

        void ConfigureHttpListenOptions(ListenOptions listenOptions)
        {
            listenOptions.Protocols = protocols;

            if (IsSecure)
            {
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.SslProtocols = sslProtocols;
#if NET8_0
                    httpsOptions.ServerCertificate = new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "Certificates", "localhost.pfx"));
#else
                    httpsOptions.ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(AppContext.BaseDirectory, "Certificates", "localhost.pfx"), null);
#endif
                    if (testServerOptions.EnableClientCertificateValidation)
                    {
                        httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                        httpsOptions.ClientCertificateValidation = (certificate2, chain, policyError) =>
                        {
                            return certificate2.Subject == "CN=client.example.com";
                        };
                    }
                });
            }

            listenOptions.UseConnectionState();
        }
    }

    public Task StartAsync() => _server.StartAsync();

    public static async Task<ITestServer> LaunchAsync(TestServerOptions options, ILoggerProvider? loggerProvider = null, CancellationToken shutdownToken = default)
    {
        var server = new InProcessTestServer(options, loggerProvider);
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

public enum TestServerListenMode
{
    InsecureHttp1Only,
    InsecureHttp2Only,
    SecureHttp1Only,
    SecureHttp2Only,
    SecureHttp1AndHttp2,
}
