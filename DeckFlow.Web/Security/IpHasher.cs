using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace DeckFlow.Web.Security;

/// <summary>
/// Shared helper for IP address hashing across DeckFlow services (FeedbackStore,
/// RequestMetricsStore, analytics middleware).
/// Centralises three concerns:
/// <list type="bullet">
/// <item><description>Cloudflare-priority IP extraction (CF-Connecting-IP &gt; X-Forwarded-For first hop &gt; RemoteIpAddress) per Phase 5 BUG-02 / Phase 7.1 CAT-FIX-01.</description></item>
/// <item><description>SHA-256 + salt hashing that produces irreversible hashes (SC #3 PII gate).</description></item>
/// <item><description>Salt resolution: <c>FEEDBACK_IP_SALT</c> env var first, then <c>feedback_meta</c> table row, then auto-generated.</description></item>
/// </list>
/// </summary>
public static class IpHasher
{
    /// <summary>
    /// Derives the client IP from <paramref name="context"/> using Cloudflare-priority
    /// resolution and returns its SHA-256 hex hash, or <c>null</c> when no IP can be
    /// determined.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    /// <item><description><c>CF-Connecting-IP</c> request header (Cloudflare-injected; single trusted value).</description></item>
    /// <item><description>First hop of the <c>X-Forwarded-For</c> request header (comma-split, trimmed).</description></item>
    /// <item><description><see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/> on the connection.</description></item>
    /// </list>
    /// Returns <c>null</c> if all three sources are null or whitespace.
    /// </remarks>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="salt">Salt string appended before hashing (see <see cref="ResolveSaltAsync"/>).</param>
    /// <returns>Uppercase hex SHA-256 hash of the resolved IP, or <c>null</c>.</returns>
    public static string? HashRequestIp(HttpContext context, string salt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(salt);

        // (a) CF-Connecting-IP — Cloudflare sets this to the originating client IP;
        //     cannot be spoofed past Cloudflare's edge when Render Inbound IP Rules
        //     restrict origin traffic to Cloudflare CIDRs.
        var cfIp = context.Request.Headers["CF-Connecting-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(cfIp))
        {
            return Hash(cfIp.Trim(), salt);
        }

        // (b) X-Forwarded-For first hop — only used as a fallback when not behind CF.
        var xff = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(xff))
        {
            var firstHop = xff.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstHop))
            {
                return Hash(firstHop, salt);
            }
        }

        // (c) Direct TCP remote address — last resort.
        var remote = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remote))
        {
            return Hash(remote, salt);
        }

        return null;
    }

    /// <summary>
    /// Computes <c>SHA-256(ip + "|" + salt)</c> and returns the result as an uppercase
    /// hex string. Returns an empty string when <paramref name="ip"/> is null or
    /// whitespace.
    /// </summary>
    /// <param name="ip">Raw IP address string to hash.</param>
    /// <param name="salt">Salt appended to the IP before hashing.</param>
    /// <returns>Uppercase hex SHA-256 digest, or <see cref="string.Empty"/> when <paramref name="ip"/> is blank.</returns>
    public static string Hash(string? ip, string salt)
    {
        ArgumentNullException.ThrowIfNull(salt);

        if (string.IsNullOrWhiteSpace(ip))
        {
            return string.Empty;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip + "|" + salt));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Resolves the IP salt by checking (in order):
    /// <list type="number">
    /// <item><description>The <c>FEEDBACK_IP_SALT</c> environment variable.</description></item>
    /// <item><description>A <c>SELECT value FROM feedback_meta WHERE key = 'ip_salt'</c> query against <paramref name="connection"/>.</description></item>
    /// <item><description>A freshly generated 32-byte random hex string, which is persisted to <c>feedback_meta</c> so subsequent calls return the same value.</description></item>
    /// </list>
    /// Requires that <c>feedback_meta</c> already exists (created by <c>FeedbackStore.EnsureSchemaAsync</c>).
    /// </summary>
    /// <param name="connection">Open database connection against which <c>feedback_meta</c> is queried.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved or newly generated salt string.</returns>
    public static async Task<string> ResolveSaltAsync(DbConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var envSalt = Environment.GetEnvironmentVariable("FEEDBACK_IP_SALT");
        if (!string.IsNullOrWhiteSpace(envSalt))
        {
            return envSalt;
        }

        await using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT value FROM feedback_meta WHERE key = 'ip_salt'";
            var existing = await select.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (existing is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }

        var bytes = RandomNumberGenerator.GetBytes(32);
        var generated = Convert.ToHexString(bytes);
        await using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO feedback_meta (key, value) VALUES ('ip_salt', @value)";
        var param = insert.CreateParameter();
        param.ParameterName = "@value";
        param.Value = generated;
        insert.Parameters.Add(param);
        await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return generated;
    }
}
