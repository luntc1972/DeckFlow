using Microsoft.AspNetCore.Http;

namespace DeckFlow.Web.Security;

/// <summary>
/// Validates that browser-originated unsafe requests target DeckFlow from the same origin.
/// </summary>
public static class SameOriginRequestValidator
{
    private const string ForbiddenMessage = "This endpoint only accepts same-origin browser requests.";

    /// <summary>
    /// Determines whether the current request should be accepted based on its Origin or Referer headers.
    /// </summary>
    /// <param name="request">Incoming HTTP request.</param>
    /// <returns><see langword="true"/> when the request is same-origin or lacks browser origin metadata; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TryParseOrigin(request.Headers.Origin, out var origin))
        {
            return UriMatchesRequestOrigin(origin, request);
        }

        if (TryParseOrigin(request.Headers.Referer, out var referer))
        {
            return UriMatchesRequestOrigin(referer, request);
        }

        // Allow non-browser callers and same-origin requests where the browser omitted both headers.
        return true;
    }

    /// <summary>
    /// Returns the standard forbidden message used when same-origin validation fails.
    /// </summary>
    /// <returns>User-facing validation message.</returns>
    public static string GetForbiddenMessage()
        => ForbiddenMessage;

    /// <summary>
    /// Parses an Origin or Referer header into an absolute URI.
    /// </summary>
    /// <param name="headerValue">Header value to parse.</param>
    /// <param name="uri">Parsed absolute URI when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseOrigin(string? headerValue, out Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(headerValue)
            && Uri.TryCreate(headerValue, UriKind.Absolute, out uri!))
        {
            return true;
        }

        uri = null!;
        return false;
    }

    /// <summary>
    /// Compares an absolute Origin/Referer URI to the active request's scheme, host, and port.
    /// </summary>
    /// <param name="origin">Origin or Referer URI.</param>
    /// <param name="request">Incoming HTTP request.</param>
    /// <returns><see langword="true"/> when the URI matches the request origin.</returns>
    private static bool UriMatchesRequestOrigin(Uri origin, HttpRequest request)
    {
        var requestHost = request.Host.Host ?? string.Empty;

        // Phase 7.1 plan 02 (CAT-FIX-01): honor X-Forwarded-Proto when origin is https.
        //
        // Render's reverse proxy correctly sets X-Forwarded-Proto=https, but ASP.NET Core's
        // default ForwardedHeadersOptions only honors forwarded headers from KnownProxies /
        // KnownNetworks (localhost ranges). Render's proxy IPs are outside that range, so
        // UseForwardedHeaders ignores the header and request.Scheme stays "http". Without
        // this branch, every legitimate same-origin browser POST fails the scheme leg
        // (https vs http) AND the port leg (443 vs scheme-derived 80) and is rejected.
        //
        // We do NOT modify Program.cs middleware ordering — Phase 4/5 invariant requires
        // UseForwardedHeaders to run before HTTPS redirect / security headers / this
        // validator, and re-tuning KnownProxies/KnownNetworks for Render's IP ranges is
        // out of scope here. Promoting the scheme inside the validator is a narrowly
        // scoped fix that leaves the broader middleware contract untouched.
        //
        // Trust model: an attacker on a different origin cannot forge X-Forwarded-Proto
        // without already controlling the network path between the client and the server
        // — the same trust model ASP.NET Core's HTTPS redirection middleware already
        // relies on. Honoring this header here does not weaken the CSRF gate; the
        // host-leg comparison still rejects cross-origin requests outright.
        //
        // Scheme and the scheme-derived default port are coupled: if we promote scheme
        // to "https" we must also recompute requestPort from the promoted scheme,
        // otherwise the port leg still rejects (origin 443 vs request 80).
        var requestScheme = request.Scheme;
        if (string.Equals(origin.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(requestScheme, "https", StringComparison.OrdinalIgnoreCase)
            && (request.IsHttps
                || string.Equals(request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase)))
        {
            requestScheme = "https";
        }

        var requestPort = request.Host.Port
            ?? (string.Equals(requestScheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);
        var originPort = origin.IsDefaultPort
            ? (string.Equals(origin.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : origin.Port;

        return string.Equals(origin.Scheme, requestScheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(origin.Host, requestHost, StringComparison.OrdinalIgnoreCase)
            && originPort == requestPort;
    }
}
