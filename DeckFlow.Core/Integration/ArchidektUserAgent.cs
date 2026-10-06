using System.Reflection;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Builds the honest User-Agent value used for Archidekt requests.
/// </summary>
internal static class ArchidektUserAgent
{
    /// <summary>
    /// Gets the contact URL included in the User-Agent value.
    /// </summary>
    internal const string ContactUrl = "https://www.deckflow.gg";

    /// <summary>
    /// Gets the process-wide Archidekt User-Agent value.
    /// </summary>
    internal static string Value { get; } = CreateValue();

    /// <summary>
    /// Formats an honest User-Agent from available assembly version information.
    /// </summary>
    internal static string Format(string? informationalVersion, Version? assemblyVersion)
    {
        var version = informationalVersion?.Split('+')[0].Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            version = assemblyVersion?.ToString();
        }

        return $"DeckFlow/{(string.IsNullOrWhiteSpace(version) ? "unknown" : version)} (+{ContactUrl})";
    }

    private static string CreateValue()
    {
        // DeckFlow.Core has no <Version>, so the entry assembly carries the deployed version.
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ArchidektUserAgent).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return Format(informationalVersion, assembly.GetName().Version);
    }
}
