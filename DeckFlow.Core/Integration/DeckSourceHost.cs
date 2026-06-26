namespace DeckFlow.Core.Integration;

/// <summary>
/// Determines whether a URI belongs to a trusted deck-source host.
/// Uses exact domain or approved-subdomain matching — never substring.
/// Rejects look-alikes such as moxfield.com.evil.tld, evilmoxfield.com, and
/// moxfield.com@evil.tld (where Uri.Host resolves to the actual authority, evil.tld).
/// </summary>
public static class DeckSourceHost
{
    private const string MoxfieldApex = "moxfield.com";
    private const string ArchidektApex = "archidekt.com";

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="uri"/> targets moxfield.com or an
    /// approved moxfield.com subdomain (e.g. www.moxfield.com, api.moxfield.com).
    /// Rejects look-alikes: moxfield.com.evil.tld, evilmoxfield.com, moxfield.com@evil.tld,
    /// and trailing-dot FQDN moxfield.com.
    /// </summary>
    /// <param name="uri">Absolute URI whose <see cref="Uri.Host"/> is tested.</param>
    /// <returns><see langword="true"/> when the host is moxfield.com or a subdomain thereof.</returns>
    public static bool IsMoxfield(Uri uri)
    {
        return IsApprovedHost(uri.Host, MoxfieldApex);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="uri"/> targets archidekt.com or an
    /// approved archidekt.com subdomain (e.g. www.archidekt.com).
    /// Rejects look-alikes: archidekt.com.evil.tld, evilarchidekt.com,
    /// archidekt.com@evil.tld, and trailing-dot FQDN archidekt.com.
    /// </summary>
    /// <param name="uri">Absolute URI whose <see cref="Uri.Host"/> is tested.</param>
    /// <returns><see langword="true"/> when the host is archidekt.com or a subdomain thereof.</returns>
    public static bool IsArchidekt(Uri uri)
    {
        return IsApprovedHost(uri.Host, ArchidektApex);
    }

    // Uri.Host is already lowercased by .NET (RFC 3986 normalization).
    // OrdinalIgnoreCase is explicit to guard against future regressions even though
    // it is redundant in practice — domain names are ASCII.
    // Do NOT call TrimEnd('.') before comparing: trimming the trailing dot on a
    // trailing-dot FQDN (e.g. "moxfield.com.") would produce the apex string and
    // incorrectly accept confusable hostnames. See RESEARCH Pitfall 1.
    private static bool IsApprovedHost(string host, string apex)
    {
        return string.Equals(host, apex, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + apex, StringComparison.OrdinalIgnoreCase);
    }
}
