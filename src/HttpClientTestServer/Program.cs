using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;
using HttpClientTestServer;
using HttpClientTestServer.Launcher;

if (!TryConfigureFromCommandLine(args, out var serverOptions))
{
    Environment.ExitCode = -1;
    return;
}

await using var server = await InProcessTestServer.LaunchAsync(serverOptions);
await server.Stopped;

static bool TryConfigureFromCommandLine(string[] args, [NotNullWhen(true)] out TestServerOptions? serverOptions)
{
    var rootCommand = new RootCommand();
    var optionProtocolVersion = new Option<ListenHttpProtocols?>("--protocol") { Description = "HTTP protocol version" };
    var optionPort = new Option<int?>("--port", "-p") { Description = "Port number" };
    var optionSecure = new Option<bool>("--secure", "-s") { Description = "Enable HTTPS" };
    var optionTlsVersion = new Option<SslProtocols?>("--tls") { Description = "TLS version (Default setting is None. None means the OS chooses the best protocol.)" };
    var optionUnixDomainSocket = new Option<string?>("--uds") { Description = "Unix Domain Socket path" };
    var optionEnableClientCertificateValidation = new Option<bool?>("--enable-client-cert-validation") { Description = "Enable client certificate validation" };
    rootCommand.Options.Add(optionProtocolVersion);
    rootCommand.Options.Add(optionPort);
    rootCommand.Options.Add(optionSecure);
    rootCommand.Options.Add(optionTlsVersion);
    rootCommand.Options.Add(optionUnixDomainSocket);
    rootCommand.Options.Add(optionEnableClientCertificateValidation);

    var result = rootCommand.Parse(args);
    if (result.Action is not null)
    {
        result.Invoke();
        serverOptions = null;
        return false;
    }

    var udsPath = result.GetValue(optionUnixDomainSocket);
    var port = result.GetValue(optionPort) ?? 8080;
    var isSecure = result.GetValue(optionSecure);
    var protocols = result.GetValue(optionProtocolVersion) ?? (isSecure ? ListenHttpProtocols.Http1AndHttp2 : ListenHttpProtocols.Http1);
    var sslProtocols = result.GetValue(optionTlsVersion);
    var enableClientCertificateValidation = result.GetValue(optionEnableClientCertificateValidation) ?? false;

    serverOptions = new TestServerOptions(protocols, isSecure)
    {
        Port = port,
        IsSecure = isSecure,
        SslProtocols = sslProtocols,
        UnixDomainSocketPath = udsPath,
        LocalhostOnly = false,
        EnableClientCertificateValidation = enableClientCertificateValidation,
    };
    return true;
}
