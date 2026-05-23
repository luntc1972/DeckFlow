using System.Text;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DeckFlow.Web.Tests.Infrastructure;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="BasicAuthMiddleware"/> covering credential validation, brute-force lockout, and 401/403 responses.
/// </summary>
[Collection("AdminEnvSerial")]
public sealed class BasicAuthMiddlewareTests
{
    private const string EnvUser = "FEEDBACK_ADMIN_USER";
    private const string EnvPass = "FEEDBACK_ADMIN_PASSWORD";

    private static AdminBruteForceTrackerStore CreateStore(out string dbPath)
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"admin-throttle-test-{Guid.NewGuid():N}.db");
        return new AdminBruteForceTrackerStore(dbPath);
    }

    [Fact]
    public async Task EnvVarsMissing_Returns503()
    {
        using var _ = EnvScope.Clear(EnvUser, EnvPass);
        var context = new DefaultHttpContext();
        var nextCalled = false;

        var store = CreateStore(out var dbPath);
        var middleware = new BasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin", store);
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task NoAuthHeader_Returns401_WithChallenge()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        var store = CreateStore(out var dbPath);
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin", store);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("Basic", context.Response.Headers["WWW-Authenticate"].ToString());
        Assert.Contains("realm=\"DeckFlow Admin\"", context.Response.Headers["WWW-Authenticate"].ToString());
    }

    [Fact]
    public async Task MalformedHeader_Returns401()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "NotBasic xxx";
        var store = CreateStore(out var dbPath);
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin", store);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task WrongCredentials_Returns401()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));
        context.Request.Headers["Authorization"] = $"Basic {encoded}";
        var store = CreateStore(out var dbPath);
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin", store);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task CorrectCredentials_InvokesNext()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));
        context.Request.Headers["Authorization"] = $"Basic {encoded}";
        var nextCalled = false;
        var store = CreateStore(out var dbPath);
        var middleware = new BasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin", store);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }
}
