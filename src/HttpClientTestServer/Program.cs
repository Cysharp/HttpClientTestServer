using System.CommandLine;
using HttpClientTestServer.Endpoint;
using HttpClientTestServer.Services;
using HttpClientTestServer.SessionState;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

if (!TryConfigureFromCommandLine(args, builder))
{
    Environment.ExitCode = -1;
    return;
}

builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.Services.AddGrpc();
builder.Services.AddResponseCompression();

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
    if (result.Errors.Any())
    {
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"Error: {error.Message}");
        }
        return false;
    }

    var port = result.GetValue(optionPort);
    if (port is not null)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(port.Value, listenOptions =>
            {
                if (result.GetValue(optionSecure))
                {
                    listenOptions.UseHttps();
                    listenOptions.Protocols = result.GetValue(optionProtocolVersion) ?? HttpProtocols.Http1AndHttp2;
                }
                else
                {
                    listenOptions.Protocols = result.GetValue(optionProtocolVersion) ?? HttpProtocols.Http1;
                }
            });
        });
    }

    return true;
}
