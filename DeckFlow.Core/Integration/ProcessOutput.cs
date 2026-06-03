namespace DeckFlow.Core.Integration;

/// <summary>
/// Shared helpers for surfacing external-process output in error messages.
/// </summary>
internal static class ProcessOutput
{
    private const int ErrorTailLength = 800;

    /// <summary>
    /// Returns at most the last <see cref="ErrorTailLength"/> characters of captured stderr,
    /// keeping thrown error messages bounded.
    /// </summary>
    /// <param name="text">Captured process output.</param>
    /// <returns>The bounded tail of the text.</returns>
    internal static string Tail(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= ErrorTailLength)
        {
            return text;
        }

        return text[^ErrorTailLength..];
    }
}
