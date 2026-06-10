using System.Text.RegularExpressions;

namespace DeckFlow.Web.Services;

/// <summary>
/// Neutralizes prompt-injection shaped transcript tokens before clip text is injected into analysis prompts.
/// </summary>
public static partial class ContentKbClipSanitizer
{
    private const string OverridePhraseReplacement = "[instruction-override phrase removed]";
    private const string CodeFenceReplacement = "[code fence removed]";
    private const string FenceDelimiterReplacement = "<>";
    private const string HeaderReplacementPrefix = "[section] ";

    /// <summary>
    /// Returns clip text with instruction-like transcript patterns neutralized for prompt injection safety.
    /// </summary>
    /// <param name="clipText">The untrusted clip text to sanitize.</param>
    /// <returns>The sanitized text, or an empty string when the input is <see langword="null"/> or empty.</returns>
    public static string Sanitize(string? clipText)
    {
        if (string.IsNullOrEmpty(clipText))
        {
            return string.Empty;
        }

        // Why: PITFALLS P7 and Phase 34 KBR-03 require transcript-derived prompt text to neutralize
        // role-confusion, override directives, and prompt-structure markdown without destroying meaning.
        var sanitized = RoleMarkerLineRegex().Replace(clipText, "${indent}");
        sanitized = OverridePhraseRegex().Replace(sanitized, OverridePhraseReplacement);
        sanitized = CodeFenceRegex().Replace(sanitized, CodeFenceReplacement);
        // Why: WR-01 defense-in-depth. Defang 3+ angle-bracket runs so transcript text cannot forge
        // or close the Expert Context structural fence with <<<...>>> tokens.
        sanitized = FenceDelimiterRunRegex().Replace(sanitized, FenceDelimiterReplacement);
        sanitized = AtxHeaderRegex().Replace(sanitized, "${indent}" + HeaderReplacementPrefix);

        return sanitized;
    }

    [GeneratedRegex(@"^(?<indent>\s*)(?:System|Assistant|User|AI)\s*:\s*", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoleMarkerLineRegex();

    [GeneratedRegex(@"\b(?:ignore|disregard|forget|override)\s+(?:all\s+)?(?:the\s+)?(?:previous|prior|above|earlier|preceding)\s+(?:instructions|guidelines|rules|prompts)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OverridePhraseRegex();

    [GeneratedRegex(@"```+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex(@"<{3,}|>{3,}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex FenceDelimiterRunRegex();

    [GeneratedRegex(@"^(?<indent>\s*)#{1,6}\s+", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AtxHeaderRegex();
}
