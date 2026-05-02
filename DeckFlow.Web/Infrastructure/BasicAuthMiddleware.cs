using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Infrastructure;

public sealed class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BasicAuthMiddleware> _logger;
    private readonly string _realm;
    private readonly IAdminBruteForceTrackerStore _store;

    public BasicAuthMiddleware(
        RequestDelegate next,
        ILogger<BasicAuthMiddleware> logger,
        string realm,
        IAdminBruteForceTrackerStore store)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(store);
        _next = next;
        _logger = logger;
        _realm = realm;
        _store = store;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // BUG-02 / Phase 5 — throttle gate BEFORE any auth parsing or env-var checks.
        // Partition key reads CF-Connecting-IP via the shared helper used by the
        // feedback-submit limiter — single source of truth.
        var partitionKey = Program.DeriveAdminPartitionKey(context);
        var nowForGate = DateTimeOffset.UtcNow;
        var (throttled, retryAfter) = await _store.IsThrottledAsync(partitionKey, nowForGate, context.RequestAborted);
        if (throttled)
        {
            var remoteIpHeader = context.Request.Headers["CF-Connecting-IP"].ToString();
            if (string.IsNullOrWhiteSpace(remoteIpHeader)) remoteIpHeader = "unknown";
            _logger.LogWarning(
                "Admin basic-auth throttled: {RemoteIp} retry after {RetryAfterSeconds}s",
                remoteIpHeader, retryAfter);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfter.ToString(CultureInfo.InvariantCulture);
            return;
        }

        var user = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_USER");
        var password = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            // Misconfigured admin — DO NOT count toward throttle (env-var path is operator
            // error, not a brute-force attempt). Phase 4-01 invariant preserved.
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Admin not configured.");
            return;
        }

        var header = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            await ChallengeAsync(context, partitionKey, "missing or non-Basic Authorization header");
            return;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring(6).Trim()));
        }
        catch (FormatException)
        {
            await ChallengeAsync(context, partitionKey, "malformed base64 in Authorization header");
            return;
        }

        var separator = decoded.IndexOf(':');
        if (separator <= 0)
        {
            await ChallengeAsync(context, partitionKey, "malformed credentials in Authorization header");
            return;
        }

        var suppliedUser = decoded.Substring(0, separator);
        var suppliedPass = decoded.Substring(separator + 1);

        if (!FixedTimeEquals(suppliedUser, user) || !FixedTimeEquals(suppliedPass, password))
        {
            await ChallengeAsync(context, partitionKey, "invalid credentials");
            return;
        }

        // Successful auth: fall through. DO NOT call RecordFailureAsync here — only
        // ChallengeAsync-emitted 401s count toward the throttle (Phase 4-01 invariant).
        await _next(context);
    }

    private async Task ChallengeAsync(HttpContext context, string partitionKey, string reason)
    {
        var remoteIp = context.Request.Headers["CF-Connecting-IP"].ToString();
        if (string.IsNullOrWhiteSpace(remoteIp)) remoteIp = "unknown";
        _logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", reason, remoteIp);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{_realm}\", charset=\"UTF-8\"";
        // BUG-02 / Phase 5 — count only Challenge-emitted 401s. Env-var-503 path bypasses
        // RecordFailureAsync above; successful-auth fall-through never reaches here.
        await _store.RecordFailureAsync(partitionKey, DateTimeOffset.UtcNow, context.RequestAborted);
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
