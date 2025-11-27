using HttpClientTestServer.ConnectionState;
using HttpClientTestServer.Endpoint;
using HttpClientTestServer.Services;
using HttpClientTestServer.SessionState;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Collections.Concurrent;
using System.CommandLine;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.Services.AddConnectionState();
builder.Services.AddSessionState();
builder.Services.AddGrpc();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

if (!TryConfigureFromCommandLine(args, builder))
{
    Environment.ExitCode = -1;
    return;
}

var app = builder.Build();

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

app.Run();


static bool TryConfigureFromCommandLine(string[] args, WebApplicationBuilder builder)
{
    var rootCommand = new RootCommand();
    var optionProtocolVersion = new Option<HttpProtocols?>("--protocol");
    var optionPort = new Option<int?>("--port", "-p");
    var optionSecure = new Option<bool>("--secure", "-s");
    rootCommand.Options.Add(optionProtocolVersion);
    rootCommand.Options.Add(optionPort);
    rootCommand.Options.Add(optionSecure);

    var result = rootCommand.Parse(args);
    if (result.Action is not null)
    {
        result.Invoke();
        return false;
    }

    var port = result.GetValue(optionPort);
    if (port is not null)
    {
#pragma warning disable ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
        var logger = builder.Logging.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
#pragma warning restore ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureHttpsDefaults(options =>
            {
                options.ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(AppContext.BaseDirectory, "Certificates", "localhost.pfx"), null);
            });
            options.ListenAnyIP(port.Value, listenOptions =>
            {
                var isSecure = result.GetValue(optionSecure);
                var protocols = result.GetValue(optionProtocolVersion) ?? (isSecure ? HttpProtocols.Http1AndHttp2 : HttpProtocols.Http1);
                logger.LogInformation("Configuring server on port {Port} (Secure: {Secure}, Protocol: {Protocol})", port.Value, isSecure, protocols);
                if (isSecure)
                {
                    listenOptions.UseHttps();
                }
                listenOptions.Protocols = protocols;
                listenOptions.UseConnectionState();
            });
        });
    }

    return true;
}
