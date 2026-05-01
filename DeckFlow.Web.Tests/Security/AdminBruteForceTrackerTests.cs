using System;
using System.Net;
using System.Text;
using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests.Security;

/// <summary>
/// Unit + integration coverage for the BUG-02 admin brute-force throttle.
/// Pure-tracker tests assert the in-memory bucket semantics; integration tests
/// drive BasicAuthMiddleware end-to-end with a real AdminBruteForceTracker.
/// </summary>
public sealed class AdminBruteForceTrackerTests
{
    private const string EnvUser = "FEEDBACK_ADMIN_USER";
    private const string EnvPass = "FEEDBACK_ADMIN_PASSWORD";

    [Fact]
    public void RecordFailure_TenTimesUnderSameKey_EleventhCheckReturnsThrottled()
    {
        var tracker = new AdminBruteForceTracker();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", now);
        var (throttled, retryAfter) = tracker.IsThrottled("admin:10.0.0.1", now);
        Assert.True(throttled);
        Assert.InRange(retryAfter, 1, 900);
    }

    [Fact]
    public void IsThrottled_NinthFailure_StillNotThrottled()
    {
        var tracker = new AdminBruteForceTracker();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 9; i++) tracker.RecordFailure("admin:10.0.0.1", now);
        var (throttled, _) = tracker.IsThrottled("admin:10.0.0.1", now);
        Assert.False(throttled);
    }

    [Fact]
    public void IsThrottled_DifferentKeys_DoNotInterfere()
    {
        var tracker = new AdminBruteForceTracker();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", now);
        var (throttled, _) = tracker.IsThrottled("admin:10.0.0.2", now);
        Assert.False(throttled);
    }

    [Fact]
    public void RecordFailure_AfterWindowExpiry_ResetsBucket()
    {
        var tracker = new AdminBruteForceTracker();
        var t0 = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", t0);
        var future = t0.AddMinutes(16);
        tracker.RecordFailure("admin:10.0.0.1", future);
        var (throttled, _) = tracker.IsThrottled("admin:10.0.0.1", future);
        Assert.False(throttled);
    }

    [Fact]
    public void IsThrottled_ReturnsRemainingSecondsInWindow()
    {
        var tracker = new AdminBruteForceTracker();
        var t0 = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", t0);
        var t0Plus5 = t0.AddMinutes(5);
        var (throttled, retryAfter) = tracker.IsThrottled("admin:10.0.0.1", t0Plus5);
        Assert.True(throttled);
        Assert.InRange(retryAfter, 599, 601);
    }

    [Fact]
    public async System.Threading.Tasks.Task ElevenFailedAuthsFromSameIp_TenthReturns401_EleventhReturns429()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var tracker = new AdminBruteForceTracker();
        var middleware = new BasicAuthMiddleware(
            _ => System.Threading.Tasks.Task.CompletedTask,
            NullLogger<BasicAuthMiddleware>.Instance,
            "DeckFlow Admin",
            tracker);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));

        int lastStatus = 0;
        string lastRetryAfter = string.Empty;
        string lastWwwAuthenticate = string.Empty;
        for (var i = 0; i < 11; i++)
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
            ctx.Request.Headers["Authorization"] = $"Basic {encoded}";
            await middleware.InvokeAsync(ctx);
            lastStatus = ctx.Response.StatusCode;
            lastRetryAfter = ctx.Response.Headers["Retry-After"].ToString();
            lastWwwAuthenticate = ctx.Response.Headers["WWW-Authenticate"].ToString();
            if (i < 10) Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        }
        Assert.Equal(StatusCodes.Status429TooManyRequests, lastStatus);
        Assert.NotEmpty(lastRetryAfter);
        Assert.Empty(lastWwwAuthenticate);
    }

    [Fact]
    public async System.Threading.Tasks.Task SuccessfulAuthDoesNotCountTowardThrottle()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var tracker = new AdminBruteForceTracker();
        var middleware = new BasicAuthMiddleware(
            _ => System.Threading.Tasks.Task.CompletedTask,
            NullLogger<BasicAuthMiddleware>.Instance,
            "DeckFlow Admin",
            tracker);
        var wrong = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));
        var right = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));

        int finalStatus = 0;
        for (var i = 0; i < 11; i++)
        {
            var token = (i == 9) ? right : wrong;
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.41");
            ctx.Request.Headers["Authorization"] = $"Basic {token}";
            await middleware.InvokeAsync(ctx);
            finalStatus = ctx.Response.StatusCode;
        }
        Assert.Equal(StatusCodes.Status401Unauthorized, finalStatus);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly (string Key, string? Original)[] _entries;

        private EnvScope((string, string?)[] entries)
        {
            _entries = entries;
        }

        public static EnvScope Set(params string[] kvp)
        {
            if (kvp.Length % 2 != 0) throw new ArgumentException("expected key,value pairs", nameof(kvp));
            var entries = new (string, string?)[kvp.Length / 2];
            for (var i = 0; i < entries.Length; i++)
            {
                var k = kvp[2 * i];
                var v = kvp[2 * i + 1];
                entries[i] = (k, Environment.GetEnvironmentVariable(k));
                Environment.SetEnvironmentVariable(k, v);
            }
            return new EnvScope(entries);
        }

        public void Dispose()
        {
            foreach (var (k, original) in _entries)
            {
                Environment.SetEnvironmentVariable(k, original);
            }
        }
    }
}
