namespace DeckFlow.Core.Manabase;

/// <summary>
/// Helpers for reading a Magic type line, which may carry two faces separated by <c>//</c>
/// (Adventures, modal/transforming DFCs, spell/land MDFCs). The FRONT face — the one you play from
/// hand — is what decides a card's permanent-ness for plan / ramp classification, so these helpers
/// are the single canonical place that split on <c>//</c> and read the front. (Note: some callers
/// deliberately test the WHOLE type line instead — e.g. repeatable-ramp credit, where any spell face
/// disqualifies — and do not use these.)
/// </summary>
public static class CardTypeLine
{
    /// <summary>The front face of a (possibly two-faced) type line — everything before <c>//</c>, trimmed.</summary>
    public static string FrontFace(string? typeLine)
        => (typeLine ?? string.Empty).Split("//")[0].Trim();

    /// <summary>
    /// True when the FRONT face is an Instant or Sorcery — the two non-permanent spell types. An
    /// Adventure creature (<c>Creature — Giant // Instant — Adventure</c>) is a permanent (creature
    /// front); a spell/land MDFC (<c>Instant // Land</c>) is not (instant front).
    /// </summary>
    public static bool IsNonPermanentFront(string? typeLine)
    {
        string front = FrontFace(typeLine);
        return front.Contains("Instant", StringComparison.OrdinalIgnoreCase)
            || front.Contains("Sorcery", StringComparison.OrdinalIgnoreCase);
    }
}
