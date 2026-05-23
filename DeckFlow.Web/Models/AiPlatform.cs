namespace DeckFlow.Web.Models;

/// <summary>
/// Single source of truth for the set of AI platforms DeckFlow supports.
/// Adding a new platform requires one entry in <see cref="All"/>.
/// </summary>
public sealed record AiPlatform(string Key, string DisplayName, string Description)
{
    /// <summary>ChatGPT — OpenAI's GPT-family models with markdown-headed prompts.</summary>
    public static readonly AiPlatform ChatGpt = new(
        Key: "ChatGPT",
        DisplayName: "ChatGPT",
        Description: "OpenAI's GPT-family models — markdown-headed prompts with fenced JSON output.");

    /// <summary>Claude — Anthropic's models with XML-tagged prompts.</summary>
    public static readonly AiPlatform Claude = new(
        Key: "Claude",
        DisplayName: "Claude",
        Description: "Anthropic's Claude models — XML-tagged prompts with <result>-wrapped output.");

    /// <summary>Gemini — Google's models with markdown persona-scaffold prompts.</summary>
    public static readonly AiPlatform Gemini = new(
        Key: "Gemini",
        DisplayName: "Gemini",
        Description: "Google's Gemini models — markdown prompts with persona scaffold and schema-strictness language.");

    /// <summary>
    /// All recognised platforms in display order. Adding a new entry here
    /// is the single source of truth for the application's AI surface.
    /// </summary>
    public static readonly IReadOnlyList<AiPlatform> All = [ChatGpt, Claude, Gemini];

    /// <summary>
    /// Default platform when input is null, empty, or out-of-set. Keeps
    /// existing zero-config behaviour stable across all three request models.
    /// </summary>
    public static AiPlatform Default => ChatGpt;

    /// <summary>
    /// Test-only seam: returns the production <see cref="All"/> list with one
    /// extra platform appended. Used by AiPlatformExtensionTests to prove that
    /// adding a 4th platform requires no edits to switch expressions, request
    /// model setters, the Razor partial, or RequestContextParser (Phase 15 SC5).
    /// </summary>
    internal static IReadOnlyList<AiPlatform> AllForTesting(AiPlatform extra) =>
        [..All, extra];

    /// <summary>
    /// Normalises a string from a form-post or zip request-context entry to a known platform.
    /// Out-of-set values fall back to <see cref="Default"/> (ChatGPT).
    /// Comparison is case-sensitive (Ordinal) to match the Phase 10 setter hardening contract.
    /// </summary>
    /// <param name="key">The raw platform string to normalise; null or empty returns Default.</param>
    /// <returns>The matching <see cref="AiPlatform"/>, or <see cref="Default"/> when unrecognised.</returns>
    public static AiPlatform Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Default;
        }

        foreach (var platform in All)
        {
            if (string.Equals(platform.Key, key, StringComparison.Ordinal))
            {
                return platform;
            }
        }

        return Default;
    }

    /// <summary>Returns <see cref="Key"/> as the string representation of this platform.</summary>
    public override string ToString() => Key;
}
