using HttpClientTestServer.ConnectionState;
using HttpClientTestServer.Endpoint;
using HttpClientTestServer.Services;
using HttpClientTestServer.SessionState;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

namespace HttpClientTestServer;

public class ServerApplication : IAsyncDisposable
{
    private Task? _runningTask;
    private readonly WebApplicationBuilder builder;
    private readonly List<Action<WebApplicationBuilder>> _configureBuilders = new();
    private readonly List<Action<WebApplication>> _configureApps = new();
    private WebApplication? _application;
    private IHostApplicationLifetime? _appLifetime;
    private readonly TaskCompletionSource _waitForAppStarted = new();
    private readonly TaskCompletionSource _appStopped = new();

    public Task Stopped => _appStopped.Task;

    public ServerApplication(string[] args)
    {
        builder = WebApplication.CreateSlimBuilder(args);
    }

    private void ConfigureKestrelDefaults()
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureHttpsDefaults(options =>
            {
#if NET8_0
                options.ServerCertificate = new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "Certificates", "localhost.pfx"));
#else
                options.ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(AppContext.BaseDirectory, "Certificates", "localhost.pfx"), null);
#endif
            });
        });
    }

    private void ConfigureBuilderDefaults()
    {
        builder.Services.AddConnectionState();
        builder.Services.AddSessionState();
        builder.Services.AddGrpc();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, HttpClientTestServerJsonSerializerContext.Default);
        });
    }

    private void ConfigureAppDefaults()
    {
        var app = _application ?? throw new InvalidOperationException();

        // ConnectionId header
        app.Use((ctx, next) =>
        {
            ctx.Response.Headers["x-connection-id"] = ctx.Connection.Id;
            return next(ctx);
        });

        // SessionState
        app.UseMiddleware<SessionStateMiddleware>();
        app.MapSessionState();

        // ConnectionState
        app.MapConnectionState();

        // HTTP/1 and HTTP/2
        app.MapAppEndpoints();

        // gRPC
        app.MapGrpcService<GreeterService>();

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = "/_certs",
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "Certificates")),
            ServeUnknownFileTypes = true,
        });

        // Compression
        app.UseResponseCompression();
        app.MapGet("/compression", () => Results.Content(new string('A', 1024 * 64))); // 64KB
        app.MapGet("/accept-encodings", (HttpContext httpContext) => Results.Content(string.Join(",", httpContext.Request.Headers.AcceptEncoding.ToArray())));

    }

    public void ConfigureBuilder(Action<WebApplicationBuilder> configure)
    {
        _configureBuilders.Add(configure);
    }

    public async Task StartAsync()
    {
        ConfigureBuilderDefaults();
        ConfigureKestrelDefaults();

        foreach (var c in _configureBuilders)
        {
            c(builder);
        }

        _application = builder.Build();
        
        ConfigureAppDefaults();

        foreach (var c in _configureApps)
        {
            c(_application);
        }

        _runningTask = _application.RunAsync();
        if (_runningTask.IsFaulted)
        {
            await _runningTask;
        }

        _appLifetime = _application.Services.GetRequiredService<IHostApplicationLifetime>();
        _appLifetime.ApplicationStarted.Register(() => _waitForAppStarted.SetResult());
        _appLifetime.ApplicationStopped.Register(() => _appStopped.SetResult());

        await _waitForAppStarted.Task;
    }

    public void Shutdown()
    {
        if (_appLifetime is not null && _runningTask is not null)
        {
            _appLifetime.StopApplication();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_appLifetime is not null && _runningTask is not null)
        {
            _appLifetime.StopApplication();
            try
            {
                await _runningTask.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
            }
        }
    }
}