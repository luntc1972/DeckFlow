using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace DeckFlow.Web.Tests.Infrastructure;

public sealed class ClientAbortMiddlewareTests
{
    [Theory]
    [MemberData(nameof(ClientAbortExceptions))]
    public async Task AbortedRequestWithCancellationException_Sets499WithoutThrowing(OperationCanceledException exception)
    {
        var context = CreateContext(aborted: true);
        var middleware = new ClientAbortMiddleware(_ => throw exception);

        await middleware.InvokeAsync(context);

        Assert.Equal(ClientAbortMiddleware.ClientClosedRequestStatusCode, context.Response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ExceptionPropagationCase))]
    public async Task ExceptionalRequest_RethrowsOriginalException(bool aborted, Exception exception)
    {
        var context = CreateContext(aborted);
        var middleware = new ClientAbortMiddleware(_ => throw exception);

        var actual = await Assert.ThrowsAnyAsync<Exception>(() => middleware.InvokeAsync(context));

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

    [Fact]
    public async Task AbortedRequest_DisablesStatusCodePages()
    {
        var context = CreateContext(aborted: true);
        var feature = new StatusCodePagesFeature();
        context.Features.Set<IStatusCodePagesFeature>(feature);
        var middleware = new ClientAbortMiddleware(_ => throw new OperationCanceledException());

        await middleware.InvokeAsync(context);

        Assert.False(feature.Enabled);
    }

    private static DefaultHttpContext CreateContext(bool aborted)
    {
        var context = new DefaultHttpContext();
        if (aborted)
        {
            context.RequestAborted = new CancellationToken(canceled: true);
        }

        return context;
    }

    public static IEnumerable<object[]> ClientAbortExceptions()
    {
        yield return new object[] { new OperationCanceledException() };
        yield return new object[] { new TaskCanceledException() };
    }

    public static IEnumerable<object[]> ExceptionPropagationCase()
    {
        yield return new object[] { false, new OperationCanceledException() };
        yield return new object[] { true, new InvalidOperationException("unexpected") };
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
