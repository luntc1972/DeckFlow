namespace DeckFlow.Web.Services.Tools;

/// <summary>
/// Formats user-facing AI platform lists so UI copy matches the enabled selectors.
/// </summary>
public static class AiPlatformCopy
{
    /// <summary>
    /// Returns a natural-language platform list for prose copy.
    /// </summary>
    /// <param name="geminiEnabled">Whether Gemini is available in the UI selector.</param>
    /// <returns><c>ChatGPT, Claude, or Gemini</c> when Gemini is enabled; otherwise <c>ChatGPT or Claude</c>.</returns>
    public static string PlatformList(bool geminiEnabled)
        => geminiEnabled ? "ChatGPT, Claude, or Gemini" : "ChatGPT or Claude";

    /// <summary>
    /// Returns a slash-separated platform list for compact UI copy.
    /// </summary>
    /// <param name="geminiEnabled">Whether Gemini is available in the UI selector.</param>
    /// <returns><c>ChatGPT / Claude / Gemini</c> when Gemini is enabled; otherwise <c>ChatGPT / Claude</c>.</returns>
    public static string PlatformSlashList(bool geminiEnabled)
        => geminiEnabled ? "ChatGPT / Claude / Gemini" : "ChatGPT / Claude";
}
