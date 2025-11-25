using Microsoft.AspNetCore.Http.Features;
using System.IO.Pipelines;
using System.Net;
using HttpClientTestServer.SessionState;

namespace HttpClientTestServer.Endpoint;

public static class AppEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder builder)
    {
        // HTTP/1 and HTTP/2
        builder.MapGet("/", () => Results.Content("__OK__"));

        builder.MapGet("/not-found", () => Results.Content("__Not_Found__", statusCode: 404));

        builder.MapGet("/response-headers", (HttpContext httpContext) =>
        {
            httpContext.Response.Headers["x-test"] = "foo";
            return Results.Content("__OK__");
        });

        builder.MapGet("/slow-response-headers", async (HttpContext httpContext) =>
        {
            using var _ = httpContext.RequestAborted.Register(() =>
            {
                httpContext.Features.GetRequiredFeature<SessionStateFeature>().Items["IsCanceled"] = true;
            });

            await Task.Delay(1000);
            httpContext.Response.Headers["x-test"] = "foo";

            return Results.Content("__OK__");
        });

        builder.MapGet("/ハロー", () => Results.Content("Konnichiwa"));

        builder.MapPost("/slow-upload", async (HttpContext ctx, PipeReader reader) =>
        {
            while (true)
            {
                await Task.Delay(1000);
                var readResult = await reader.ReadAsync();
                reader.AdvanceTo(readResult.Buffer.End);
                if (readResult.IsCompleted || readResult.IsCanceled) break;
            }
            return Results.Content("OK");
        });

        builder.MapPost("/post-echo", async (HttpContext httpContext, Stream bodyStream) =>
        {
            httpContext.Response.Headers["x-request-content-type"] = httpContext.Request.ContentType;

            return Results.Bytes(await bodyStream.ToArrayAsync(), "application/octet-stream");
        });

        builder.MapPost("/post-streaming", async (HttpContext httpContext, PipeReader reader) =>
        {
            // Send status code and response headers.
            httpContext.Response.Headers["x-header-1"] = "foo";
            httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
            await httpContext.Response.BodyWriter.FlushAsync();

            var readLen = 0L;
            while (true)
            {
                var readResult = await reader.ReadAsync();
                readLen += readResult.Buffer.Length;
                reader.AdvanceTo(readResult.Buffer.End);
                if (readResult.IsCompleted || readResult.IsCanceled) break;
            }

            await httpContext.Response.WriteAsync(readLen.ToString());

            return Results.Empty;
        });

        builder.MapPost("/post-response-trailers", (HttpContext httpContext) =>
        {
            httpContext.Response.AppendTrailer("x-trailer-1", "foo");
            httpContext.Response.AppendTrailer("x-trailer-2", "bar");

            return Results.Ok("__OK__");
        });

        builder.MapPost("/post-response-headers-immediately", async (HttpContext httpContext, PipeReader reader) =>
        {
            // Send status code and response headers.
            httpContext.Response.Headers["x-header-1"] = "foo";
            httpContext.Response.StatusCode = (int)HttpStatusCode.Accepted;
            await httpContext.Response.BodyWriter.FlushAsync();

            await Task.Delay(100000);
            await httpContext.Response.WriteAsync("__OK__");
            return Results.Empty;
        });

        builder.MapPost("/post-abort-while-reading", async (HttpContext httpContext, PipeReader reader) =>
        {
            var readResult = await reader.ReadAsync();
            reader.AdvanceTo(readResult.Buffer.End);
            httpContext.Abort();

            return Results.Empty;
        });

        builder.MapPost("/post-null", async (HttpContext httpContext, PipeReader reader) =>
        {
            while (!httpContext.RequestAborted.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync();
                reader.AdvanceTo(readResult.Buffer.End);
                if (readResult.IsCompleted || readResult.IsCanceled) break;
            }
            return Results.Empty;
        });

        builder.MapPost("/post-null-duplex", async (HttpContext httpContext, PipeReader reader) =>
        {
            // Send status code and response headers.
            httpContext.Response.Headers["x-header-1"] = "foo";
            httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
            await httpContext.Response.BodyWriter.FlushAsync();

            while (!httpContext.RequestAborted.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync();
                reader.AdvanceTo(readResult.Buffer.End);
                if (readResult.IsCompleted || readResult.IsCanceled) break;
            }
            return Results.Empty;
        });

        builder.MapPost("/post-never-read", async (HttpContext httpContext) =>
        {
            // Send status code and response headers.
            httpContext.Response.Headers["x-header-1"] = "foo";
            httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
            await httpContext.Response.BodyWriter.FlushAsync();

            while (!httpContext.RequestAborted.IsCancellationRequested)
            {
                await Task.Delay(100);
            }
            return Results.Empty;
        });

        builder.MapGet("/random", (int size) =>
        {
            var buffer = new byte[size];
            Random.Shared.NextBytes(buffer);
            return Results.Bytes(buffer, "application/octet-stream");
        });

        // HTTP/2
        builder.MapGet("/error-reset", (HttpContext httpContext) =>
        {
            // https://learn.microsoft.com/ja-jp/aspnet/core/fundamentals/servers/kestrel/http2?view=aspnetcore-7.0#reset-1
            var resetFeature = httpContext.Features.Get<IHttpResetFeature>();
            resetFeature!.Reset(errorCode: 2); // INTERNAL_ERROR
            return Results.Empty;
        });

        return builder;
    }
}
