namespace DeckFlow.Web.Configuration;

/// <summary>
/// Toggles for AI-platform target options surfaced in the ChatGPT/Claude/Gemini selector.
/// Bound from environment variables in <c>Program.cs</c>. ChatGPT and Claude are always
/// available; Gemini is gated behind <c>DECKFLOW_GEMINI_ENABLED</c> because the full packet
/// frequently exceeds Gemini's paste limit, truncating instructions and producing degraded
/// output. Default off.
/// </summary>
public sealed class AiPlatformOptions
{
    /// <summary>
    /// When true, render the Gemini radio option in the AI selector. When false (default),
    /// the option is hidden. Server-side prompt builders still accept "Gemini" if posted
    /// directly (UI-hide only, per resume decision 2026-05-13).
    /// </summary>
    public bool GeminiEnabled { get; set; }
}
