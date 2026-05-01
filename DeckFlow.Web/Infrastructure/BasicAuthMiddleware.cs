using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Infrastructure;

public sealed class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BasicAuthMiddleware> _logger;
    private readonly string _realm;
    private readonly IAdminBruteForceTracker _tracker;

    public BasicAuthMiddleware(
        RequestDelegate next,
        ILogger<BasicAuthMiddleware> logger,
        string realm,
        IAdminBruteForceTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(tracker);
        _next = next;
        _logger = logger;
        _realm = realm;
        _tracker = tracker;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // BUG-02 / D-02 — throttle gate before any auth parsing.
        var partitionKey = "admin:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var now = DateTimeOffset.UtcNow;
        var (throttled, retryAfter) = _tracker.IsThrottled(partitionKey, now);
        if (throttled)
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogWarning(
                "Admin basic-auth throttled: {RemoteIp} retry after {RetryAfterSeconds}s",
                remoteIp, retryAfter);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }

        var user = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_USER");
        var password = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Admin not configured.");
            return;
        }

        var header = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(context, "missing or non-Basic Authorization header");
            return;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring(6).Trim()));
        }
        catch (FormatException)
        {
            Challenge(context, "malformed base64 in Authorization header");
            return;
        }

        var separator = decoded.IndexOf(':');
        if (separator <= 0)
        {
            Challenge(context, "malformed credentials in Authorization header");
            return;
        }

        var suppliedUser = decoded.Substring(0, separator);
        var suppliedPass = decoded.Substring(separator + 1);

        if (!FixedTimeEquals(suppliedUser, user) || !FixedTimeEquals(suppliedPass, password))
        {
            Challenge(context, "invalid credentials");
            return;
        }

        await _next(context);
    }

    private void Challenge(HttpContext context, string reason)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", reason, remoteIp);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{_realm}\", charset=\"UTF-8\"";
        // BUG-02 / D-01 — count only Challenge-emitted 401s (env-var 503 path bypasses this).
        var partitionKey = "admin:" + remoteIp;
        _tracker.RecordFailure(partitionKey, DateTimeOffset.UtcNow);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length)
        {
            var sink = new byte[ba.Length];
            CryptographicOperations.FixedTimeEquals(ba, sink);
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
