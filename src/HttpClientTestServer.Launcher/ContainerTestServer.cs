using System.Net.Sockets;
using System.Security.Authentication;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HttpClientTestServer.Launcher;

public class ContainerTestServer : ITestServer
{
    private const string DockerImage = "ghcr.io/cysharp/httpclienttestserver:main";

    private readonly IContainer _container;
    private readonly Task _appTask;
    private readonly Task _waitForServerStartedTask;
    private readonly ILogger _logger;
    private readonly bool _listeningOnUnixDomainSocket;

    public int Port { get; }
    public bool IsSecure { get; }

    public string BaseUri => $"{(IsSecure ? "https" : "http")}://localhost:{Port}";

    private ContainerTestServer(int port, TestServerListenMode listenMode, ILoggerProvider? loggerProvider, TestServerOptions? options, CancellationToken cancellationToken)
    {
        Port = port;
        IsSecure = listenMode is TestServerListenMode.SecureHttp1Only or
            TestServerListenMode.SecureHttp2Only or
            TestServerListenMode.SecureHttp1AndHttp2;

        _listeningOnUnixDomainSocket = options?.UnixDomainSocketPath != null;
        var protocols = listenMode switch
        {
            TestServerListenMode.InsecureHttp1Only => HttpProtocols.Http1,
            TestServerListenMode.InsecureHttp2Only => HttpProtocols.Http2,
            TestServerListenMode.SecureHttp1Only => HttpProtocols.Http1,
            TestServerListenMode.SecureHttp2Only => HttpProtocols.Http2,
            TestServerListenMode.SecureHttp1AndHttp2 => HttpProtocols.Http1AndHttp2,
            _ => throw new NotSupportedException(),
        };
        var sslProtocols = options?.SslProtocols ?? SslProtocols.Tls13;

        // Workaround Docker API version mismatch (client newer than daemon).
        // If DOCKER_API_VERSION not already set, pin to daemon max (1.43) to allow negotiation.
        if (Environment.GetEnvironmentVariable("DOCKER_API_VERSION") is null)
        {
            Environment.SetEnvironmentVariable("DOCKER_API_VERSION", "1.43");
        }

        _logger = (loggerProvider ?? NullLoggerProvider.Instance).CreateLogger(nameof(ContainerTestServer));

        _logger.LogInformation($"[{DateTime.Now}][{GetType().Name}] Server starting... (port={port}; protocol={protocols}; secure={IsSecure})");
        _container = new ContainerBuilder()
            .WithImage(DockerImage)
            .WithCommand(new[]
                {
                    "--port", "80",
                    "--protocol", protocols.ToString(),
                    "--tls", sslProtocols.ToString()
                }
                .Concat(IsSecure ? ["--secure"] : [])
                .Concat(_listeningOnUnixDomainSocket ? ["--uds", options?.UnixDomainSocketPath ?? ""] : [])
                .ToArray()
            )
            .WithPortBinding(port, 80)
            .Build();
        _appTask = _container.StartAsync(cancellationToken);

        _waitForServerStartedTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = new TcpClient();
                try
                {
                    await tcpClient.ConnectAsync("localhost", port, cancellationToken);
                    break;
                }
                catch
                {
                }

                await Task.Delay(16);
            }
        });
    }

    public static async Task<ITestServer> LaunchAsync(TestServerListenMode listenMode, ILoggerProvider? loggerProvider = null, CancellationToken shutdownToken = default, TestServerOptions? options = default)
    {
        var port = TestServerHelper.GetUnusedEphemeralPort();
        var server = new ContainerTestServer(port, listenMode, loggerProvider, options, shutdownToken);
        await server._waitForServerStartedTask.WaitAsync(shutdownToken);

        shutdownToken.Register(() =>
        {
            server.Shutdown();
        });

        return server;
    }

    public void Shutdown()
    {
        if (_container.State == TestcontainersStates.Running)
        {
            _container.StopAsync().GetAwaiter().GetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container.State == TestcontainersStates.Running)
        {
            await _container.DisposeAsync();
        }
        try
        {
            await _appTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch (TimeoutException)
        {
        }
    }
}
