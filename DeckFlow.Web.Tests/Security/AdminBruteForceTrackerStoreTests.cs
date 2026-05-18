using System.Net;
using System.Text;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests.Security;

/// <summary>
/// Integration tests for <see cref="AdminBruteForceTrackerStore"/> covering lockout tracking,
/// attempt recording, and automatic expiry against a temporary SQLite database.
/// </summary>
public sealed class AdminBruteForceTrackerStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AdminBruteForceTrackerStore _store;

    public AdminBruteForceTrackerStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"admin-throttle-test-{Guid.NewGuid():N}.db");
        _store = new AdminBruteForceTrackerStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* sqlite handle release timing */ }
        }
    }

    [Fact]
    public async Task RecordFailure_TenTimesUnderSameKey_EleventhCheckReturnsThrottled()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
            await _store.RecordFailureAsync("admin:10.0.0.1", now);
        var (throttled, retryAfter) = await _store.IsThrottledAsync("admin:10.0.0.1", now);
        Assert.True(throttled);
        Assert.InRange(retryAfter, 1, 900);
    }

    [Fact]
    public async Task IsThrottled_NinthFailure_StillNotThrottled()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 9; i++)
            await _store.RecordFailureAsync("admin:10.0.0.1", now);
        var (throttled, _) = await _store.IsThrottledAsync("admin:10.0.0.1", now);
        Assert.False(throttled);
    }

    [Fact]
    public async Task IsThrottled_DifferentKeys_DoNotInterfere()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
            await _store.RecordFailureAsync("admin:10.0.0.1", now);
        var (throttled, _) = await _store.IsThrottledAsync("admin:10.0.0.2", now);
        Assert.False(throttled);
    }

    [Fact]
    public async Task RecordFailure_AfterWindowExpiry_ResetsBucket()
    {
        var t0 = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
            await _store.RecordFailureAsync("admin:10.0.0.1", t0);
        var future = t0.AddMinutes(16);
        await _store.RecordFailureAsync("admin:10.0.0.1", future);
        var (throttled, _) = await _store.IsThrottledAsync("admin:10.0.0.1", future);
        Assert.False(throttled);
    }

    [Fact]
    public async Task IsThrottled_ReturnsRemainingSecondsInWindow()
    {
        var t0 = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
            await _store.RecordFailureAsync("admin:10.0.0.1", t0);
        var t0Plus5 = t0.AddMinutes(5);
        var (throttled, retryAfter) = await _store.IsThrottledAsync("admin:10.0.0.1", t0Plus5);
        Assert.True(throttled);
        Assert.InRange(retryAfter, 599, 601);
    }

    [Fact]
    public async Task ElevenFailedAuthsFromSameIp_TenthReturns401_EleventhReturns429()
    {
        using var _envUser = EnvScope.Set("FEEDBACK_ADMIN_USER", "admin");
        using var _envPass = EnvScope.Set("FEEDBACK_ADMIN_PASSWORD", "secret");

        var middleware = new BasicAuthMiddleware(
            _ => Task.CompletedTask,
            NullLogger<BasicAuthMiddleware>.Instance,
            "DeckFlow Admin",
            _store);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));

        int lastStatus = 0;
        string lastRetryAfter = string.Empty;
        for (var i = 0; i < 11; i++)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["CF-Connecting-IP"] = "10.20.30.40";
            ctx.Request.Headers["Authorization"] = $"Basic {encoded}";
            await middleware.InvokeAsync(ctx);
            lastStatus = ctx.Response.StatusCode;
            lastRetryAfter = ctx.Response.Headers["Retry-After"].ToString();
            if (i < 10) Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        }
        Assert.Equal(StatusCodes.Status429TooManyRequests, lastStatus);
        Assert.False(string.IsNullOrEmpty(lastRetryAfter));
    }

    [Fact]
    public async Task SuccessfulAuthDoesNotCountTowardThrottle()
    {
        using var _envUser = EnvScope.Set("FEEDBACK_ADMIN_USER", "admin");
        using var _envPass = EnvScope.Set("FEEDBACK_ADMIN_PASSWORD", "secret");

        var middleware = new BasicAuthMiddleware(
            _ => Task.CompletedTask,
            NullLogger<BasicAuthMiddleware>.Instance,
            "DeckFlow Admin",
            _store);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));

        for (var i = 0; i < 50; i++)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["CF-Connecting-IP"] = "10.20.30.40";
            ctx.Request.Headers["Authorization"] = $"Basic {encoded}";
            await middleware.InvokeAsync(ctx);
            Assert.NotEqual(StatusCodes.Status429TooManyRequests, ctx.Response.StatusCode);
        }

        var (throttled, _) = await _store.IsThrottledAsync("admin:10.20.30.40", DateTimeOffset.UtcNow);
        Assert.False(throttled);
    }

    [Fact]
    public async Task MissingCfConnectingIpHeader_FallsBackToUnknownBucket()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
            await _store.RecordFailureAsync("admin:unknown", now);
        var (throttled, _) = await _store.IsThrottledAsync("admin:unknown", now);
        Assert.True(throttled);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;
        private EnvScope(string name) { _name = name; _previous = Environment.GetEnvironmentVariable(name); }
        public static EnvScope Set(string name, string value)
        {
            var s = new EnvScope(name);
            Environment.SetEnvironmentVariable(name, value);
            return s;
        }
        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
