using System.Net;
using Microsoft.Extensions.Configuration;

namespace DeckFlow.Studio;

/// <summary>
/// Pure helper that classifies Kestrel bind URLs as loopback-only or non-loopback, and gathers
/// the effective set of bind URLs from <see cref="IConfiguration"/>.
/// Used at startup to prevent DeckFlow Studio — an unauthenticated prod-publish tool — from
/// ever binding to a routable address (H2).
/// </summary>
internal static class LoopbackBindGuard
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="url"/> binds exclusively to a loopback
    /// address (localhost, 127.0.0.0/8, or ::1); <see langword="false"/> for wildcard binds
    /// (0.0.0.0, ::, +, *, empty) or routable addresses.
    /// Fails closed: a null or whitespace URL is treated as non-loopback.
    /// </summary>
    /// <param name="url">A Kestrel bind URL such as "http://localhost:5271".</param>
    /// <returns><see langword="true"/> only for loopback-only bind addresses.</returns>
    internal static bool IsLoopbackBindUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Strip the scheme ("http://" or "https://").
        var schemeDelimiter = url.IndexOf("://", StringComparison.Ordinal);
        var afterScheme = schemeDelimiter >= 0 ? url[(schemeDelimiter + 3)..] : url;

        // Authority = everything before the first path separator '/'.
        var slashIndex = afterScheme.IndexOf('/');
        var authority = slashIndex >= 0 ? afterScheme[..slashIndex] : afterScheme;

        // Extract the host, handling IPv6 bracket notation vs. host:port.
        string host;
        if (authority.StartsWith("[", StringComparison.Ordinal))
        {
            // Bracketed IPv6: "[::1]:5271" → host = "::1".
            var closeIndex = authority.IndexOf(']');
            host = closeIndex > 0 ? authority[1..closeIndex] : authority[1..];
        }
        else
        {
            // host:port or bare host: "127.0.0.1:5271" → "127.0.0.1"; "localhost" → "localhost".
            var colonIndex = authority.IndexOf(':');
            host = colonIndex >= 0 ? authority[..colonIndex] : authority;
        }

        host = host.ToLowerInvariant();

        // Bare IPv6 without brackets (e.g. "::1"): authority starts with ':' and host comes out
        // empty because the first ':' is at index 0. Parse the authority directly so that
        // "::1" (loopback) returns true and "::" (unspecified / wildcard) returns false.
        if (host.Length == 0 && authority.Length > 0 && authority[0] == ':')
        {
            return IPAddress.TryParse(authority, out var bareIpv6) && IPAddress.IsLoopback(bareIpv6);
        }

        // Explicit non-loopback wildcard binds — fail closed.
        if (host is "" or "+" or "*" or "0.0.0.0" or "::" or "[::]")
        {
            return false;
        }

        if (host == "localhost")
        {
            return true;
        }

        // Numeric IP: covers 127.0.0.0/8 (loopback) and ::1.
        if (IPAddress.TryParse(host, out var ip))
        {
            return IPAddress.IsLoopback(ip);
        }

        // DNS hostname (e.g. "studio.example.com") — treat as non-loopback.
        return false;
    }

    /// <summary>
    /// Filters <paramref name="urls"/> to those that are NOT loopback-only binds.
    /// Returns an empty list when every URL is loopback-safe.
    /// </summary>
    /// <param name="urls">Enumeration of Kestrel bind URLs to inspect.</param>
    /// <returns>Non-loopback URLs, or an empty list if all are loopback.</returns>
    internal static IReadOnlyList<string> FindNonLoopbackBindings(IEnumerable<string> urls)
        => urls.Where(u => !IsLoopbackBindUrl(u)).ToList();

    /// <summary>
    /// Gathers the effective set of Kestrel bind URLs from <paramref name="configuration"/> in
    /// priority order: ASPNETCORE_URLS / --urls first, then Kestrel:Endpoints:*:Url, then the
    /// Studio default http://localhost:5271. Returns distinct, trimmed entries.
    /// </summary>
    /// <param name="configuration">Application configuration with environment variables and
    /// command-line args already merged in.</param>
    /// <returns>Distinct, non-empty bind URLs in effective priority order.</returns>
    internal static IReadOnlyList<string> GatherConfiguredBindUrls(IConfiguration configuration)
    {
        var collected = new List<string>();

        // 1. ASPNETCORE_URLS / --urls (semicolon-separated, highest priority).
        var urlsValue = configuration["urls"];
        var hasExplicitUrls = !string.IsNullOrWhiteSpace(urlsValue);
        if (hasExplicitUrls)
        {
            foreach (var part in urlsValue!.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    collected.Add(trimmed);
                }
            }
        }
        else
        {
            // 1b. ASPNETCORE_HTTP_PORTS / ASPNETCORE_HTTPS_PORTS (config keys "http_ports" /
            // "https_ports", semicolon- or comma-separated). Kestrel ignores these when "urls" is
            // set, so they only matter in the else-branch. When present they create WILDCARD binds
            // (http://*:port on all interfaces) — a non-loopback bind that would otherwise bypass
            // the guard (Codex HIGH). Model each as "http(s)://+:port" so IsLoopbackBindUrl
            // classifies it as non-loopback via the "+" wildcard host.
            foreach (var port in SplitPorts(configuration["http_ports"]))
            {
                collected.Add($"http://+:{port}");
            }

            foreach (var port in SplitPorts(configuration["https_ports"]))
            {
                collected.Add($"https://+:{port}");
            }
        }

        // 2. Kestrel:Endpoints:*:Url — appsettings.json sets Http:Url = http://localhost:5271.
        var endpointsSection = configuration.GetSection("Kestrel:Endpoints");
        foreach (var endpoint in endpointsSection.GetChildren())
        {
            var endpointUrl = endpoint["Url"];
            if (!string.IsNullOrWhiteSpace(endpointUrl))
            {
                collected.Add(endpointUrl.Trim());
            }
        }

        // 3. Fallback default when no explicit URL is configured.
        if (collected.Count == 0)
        {
            collected.Add("http://localhost:5271");
        }

        // Deduplicate while preserving insertion order.
        return collected.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Splits an ASPNETCORE_HTTP_PORTS / ASPNETCORE_HTTPS_PORTS value (semicolon- or
    /// comma-separated port list) into trimmed, non-empty port tokens. Returns empty when the
    /// value is null/whitespace.
    /// </summary>
    /// <param name="portsValue">Raw ports configuration value, e.g. "8080;8081".</param>
    /// <returns>Trimmed port tokens in order.</returns>
    private static IEnumerable<string> SplitPorts(string? portsValue)
    {
        if (string.IsNullOrWhiteSpace(portsValue))
        {
            yield break;
        }

        foreach (var part in portsValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                yield return trimmed;
            }
        }
    }
}
