using Grpc.Core;
using Microsoft.AspNetCore.Http.Features;

namespace HttpClientTestServer.Services;

public class GreeterService : Greeter.GreeterBase
{
#pragma warning disable CS1998
    public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return new HelloReply { Message = $"Hello {request.Name}" };
    }

    public override async Task<HelloReply> SayHelloSlow(HelloRequest request, ServerCallContext context)
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        return new HelloReply { Message = $"Hello {request.Name}" };
    }

    public override async Task<HelloReply> SayHelloNever(HelloRequest request, ServerCallContext context)
    {
        await Task.Delay(-1);
        throw new NotImplementedException();
    }

    public override async Task SayHelloDuplex(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {request.Name}" });
        }
    }

    public override async Task SayHelloDuplexCompleteRandomly(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(new HelloReply { Message = request.Name });
            if (Random.Shared.Next(0, 9) == 0)
            {
                return;
            }
        }
    }

    public override async Task SayHelloDuplexAbortRandomly(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(new HelloReply { Message = request.Name });
            if (Random.Shared.Next(0, 9) == 0)
            {
                context.GetHttpContext().Abort();
                return;
            }
        }
    }

    public override async Task<ResetReply> ResetByServer(ResetRequest request, ServerCallContext context)
    {
        context.GetHttpContext().Features.GetRequiredFeature<IHttpResetFeature>().Reset(errorCode: request.ErrorCode);
        return new ResetReply { };
    }

    public override async Task EchoDuplex(IAsyncStreamReader<EchoRequest> requestStream, IServerStreamWriter<EchoReply> responseStream, ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(new EchoReply() { Message = request.Message });
        }
    }
#pragma warning restore CS1998
}