using HttpClientTestServer.Endpoint;
using HttpClientTestServer.Services;
using HttpClientTestServer.SessionState;

var builder = WebApplication.CreateBuilder(args);

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
