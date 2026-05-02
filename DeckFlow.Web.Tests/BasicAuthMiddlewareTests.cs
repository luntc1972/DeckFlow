using System.Text;
using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class BasicAuthMiddlewareTests
{
    private const string EnvUser = "FEEDBACK_ADMIN_USER";
    private const string EnvPass = "FEEDBACK_ADMIN_PASSWORD";

    [Fact]
    public async Task EnvVarsMissing_Returns503()
    {
        using var _ = EnvScope.Clear(EnvUser, EnvPass);
        var context = new DefaultHttpContext();
        var nextCalled = false;

        var middleware = new BasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin");
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task NoAuthHeader_Returns401_WithChallenge()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin");

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
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin");

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
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin");

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
        var middleware = new BasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();

        private EnvScope(params string[] keys)
        {
            foreach (var key in keys)
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
            }
        }

        public static EnvScope Clear(params string[] keys)
        {
            var scope = new EnvScope(keys);
            foreach (var key in keys) Environment.SetEnvironmentVariable(key, null);
            return scope;
        }

        public static EnvScope Set(string k1, string v1, string k2, string v2)
        {
            var scope = new EnvScope(k1, k2);
            Environment.SetEnvironmentVariable(k1, v1);
            Environment.SetEnvironmentVariable(k2, v2);
            return scope;
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
