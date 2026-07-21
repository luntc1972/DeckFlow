namespace DeckFlow.Core.Exporting;

/// <summary>
/// Classifies whether a card's color identity fits within the commander's color identity.
/// </summary>
public static class CommanderIdentityCheck
{
    /// <summary>
    /// Returns whether the card identity is legal, illegal, or unverified for the given commander identity.
    /// </summary>
    public static CommanderIdentityCheckResult IsWithinCommanderIdentity(IReadOnlyList<string>? cardIdentity, IReadOnlySet<string> commanderIdentity)
    {
        ArgumentNullException.ThrowIfNull(commanderIdentity);

        if (cardIdentity is null)
        {
            return CommanderIdentityCheckResult.Unverified;
        }

        var normalizedCommanderIdentity = commanderIdentity
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => color.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var color in cardIdentity
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => color.Trim().ToUpperInvariant()))
        {
            if (!normalizedCommanderIdentity.Contains(color))
            {
                return CommanderIdentityCheckResult.Illegal;
            }
        }

        return CommanderIdentityCheckResult.Legal;
    }
}

/// <summary>
/// The outcome of checking a card against the commander's color identity.
/// </summary>
public enum CommanderIdentityCheckResult
{
    /// <summary>
    /// The card identity is fully within the commander's color identity.
    /// </summary>
    Legal,

    /// <summary>
    /// The card identity includes at least one color outside the commander's color identity.
    /// </summary>
    Illegal,

    /// <summary>
    /// The card identity could not be verified from the available data.
    /// </summary>
    Unverified,
}
