namespace DeckFlow.Web.Services;

/// <summary>
/// Produces commander-name lookup candidates for committed baseline snapshots.
/// </summary>
public static class CommanderBaselineKeys
{
    /// <summary>
    /// Produces the commander-name candidate keys used for committed baseline lookups.
    /// </summary>
    /// <param name="commanderNames">The deck's commander names.</param>
    /// <returns>The ordered candidate keys to try against a baseline snapshot.</returns>
    // Baseline keys are either a single commander name or a partner pair joined by " / " in an
    // unspecified order; try the solo name, then both partner orders. An MDFC commander name
    // (containing " // ") is a single card = a single key, so it is never split. The Phase 2
    // corpus has zero partner-pair keys and 50 DFC keys in full "A // B" form, so partner and
    // Background decks resolve nothing here and correctly fall back to the bracket floor.
    public static IEnumerable<string> Candidates(IReadOnlyList<string> commanderNames)
    {
        if (commanderNames.Count == 1)
        {
            yield return commanderNames[0];
        }
        else if (commanderNames.Count == 2)
        {
            yield return $"{commanderNames[0]} / {commanderNames[1]}";
            yield return $"{commanderNames[1]} / {commanderNames[0]}";
        }
    }
}
