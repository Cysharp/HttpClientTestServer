using HttpClientTestServer;
using HttpClientTestServer.ConnectionState;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.CommandLine;
using System.Security.Authentication;

await using var server = new ServerApplication(args);
server.ConfigureBuilder(builder =>
{
    builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
});

if (!TryConfigureFromCommandLine(args, server))
{
    Environment.ExitCode = -1;
    return;
}

await server.StartAsync();
await server.Stopped;

static bool TryConfigureFromCommandLine(string[] args, ServerApplication server)
{
    var rootCommand = new RootCommand();
    var optionProtocolVersion = new Option<HttpProtocols?>("--protocol") { Description = "HTTP protocol version" };
    var optionPort = new Option<int?>("--port", "-p") { Description = "Port number" };
    var optionSecure = new Option<bool>("--secure", "-s") { Description = "Enable HTTPS" };
    var optionTlsVersion = new Option<SslProtocols?>("--tls") { Description = "TLS version (Default setting is None. None means the OS chooses the best protocol.)" };
    rootCommand.Options.Add(optionProtocolVersion);
    rootCommand.Options.Add(optionPort);
    rootCommand.Options.Add(optionSecure);
    rootCommand.Options.Add(optionTlsVersion);

    var result = rootCommand.Parse(args);
    if (result.Action is not null)
    {
        result.Invoke();
        return false;
    }

    var port = result.GetValue(optionPort);
    if (port is not null)
    {
        server.ConfigureBuilder(builder =>
        {
#pragma warning disable ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
            var logger = builder.Logging.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
#pragma warning restore ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(port.Value, listenOptions =>
                {
                    var isSecure = result.GetValue(optionSecure);
                    var protocols = result.GetValue(optionProtocolVersion) ?? (isSecure ? HttpProtocols.Http1AndHttp2 : HttpProtocols.Http1);
                    var sslProtocols = result.GetValue(optionTlsVersion) ?? SslProtocols.None;
                    logger.LogInformation("Configuring server on port {Port} (Secure: {Secure}, Protocol: {Protocol}, SslProtocols: {SslProtocols})", port.Value, isSecure, protocols, sslProtocols);
                    if (isSecure)
                    {
                        listenOptions.UseHttps(options =>
                        {
                            options.SslProtocols = sslProtocols;
                        });
                    }
                    listenOptions.Protocols = protocols;
                    listenOptions.UseConnectionState();
                });
            });
        });
    }

    return true;
}
