using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace DeckFlow.Web.Tests.Infrastructure;

public sealed class ClientAbortMiddlewareTests
{
    [Fact]
    public async Task AbortedRequestWithOperationCanceledException_Sets499WithoutThrowing()
    {
        var context = CreateContext(aborted: true);
        var middleware = new ClientAbortMiddleware(_ => throw new OperationCanceledException());

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task NonAbortedRequestWithOperationCanceledException_Rethrows()
    {
        var context = CreateContext(aborted: false);
        var exception = new OperationCanceledException();
        var middleware = new ClientAbortMiddleware(_ => throw exception);

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task AbortedRequestWithTaskCanceledException_Sets499()
    {
        var context = CreateContext(aborted: true);
        var middleware = new ClientAbortMiddleware(_ => throw new TaskCanceledException());

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task AbortedRequestWithOtherException_Rethrows()
    {
        var context = CreateContext(aborted: true);
        var exception = new InvalidOperationException("unexpected");
        var middleware = new ClientAbortMiddleware(_ => throw exception);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task NormalRequest_CallsNextOnceAndLeavesStatusUnchanged()
    {
        var context = CreateContext(aborted: false);
        var nextCalls = 0;
        var middleware = new ClientAbortMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(1, nextCalls);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task AbortedRequestWithStartedResponse_DoesNotChangeStatus()
    {
        var context = CreateContext(aborted: true);
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        var middleware = new ClientAbortMiddleware(_ => throw new OperationCanceledException());

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext(bool aborted)
    {
        var context = new DefaultHttpContext();
        if (aborted)
        {
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            context.RequestAborted = cancellation.Token;
        }

        return context;
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
