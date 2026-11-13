using System.Text.RegularExpressions;

namespace DeckFlow.Core.Analysis;

/// <summary>Shared operations on Oracle rules text.</summary>
internal static class OracleText
{
    private static readonly Regex ReminderTextRegex = new(@"\([^)]*\)", RegexOptions.Compiled);

    /// <summary>Remove parenthesized reminder text from Oracle rules text.</summary>
    public static string StripReminderText(string text)
        // Most Oracle text has no parentheses, so avoid a regex pass on the per-card hot path.
        => text.IndexOf('(', StringComparison.Ordinal) < 0 ? text : ReminderTextRegex.Replace(text, string.Empty);
}
