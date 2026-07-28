namespace DeckFlow.Core.Research;

/// <summary>
/// Tallies already-classified EDHREC role keys into target-role counts.
/// </summary>
public static class EdhrecRoleTally
{
    // Why: this lives in Core so the EDHREC quantity rule is enforced by tests instead of by eye.
    // EDHREC decklists carry real basic-land quantities while the Postgres path is singleton, and
    // a += 1 regression here would silently undercount lands by roughly a dozen cards per average
    // deck while leaving every other role correct.
    /// <summary>
    /// Seeds each target role at zero and adds each classified card quantity to matching emitted roles.
    /// </summary>
    /// <param name="targetRoles">The role keys that must exist in the output, even when unused.</param>
    /// <param name="classifiedCards">The already-classified roles and decklist quantity for each card.</param>
    /// <returns>A per-role count map containing only the requested target roles.</returns>
    public static IReadOnlyDictionary<string, int> TallyRoleCounts(
        IReadOnlyCollection<string> targetRoles,
        IEnumerable<(IReadOnlyList<string> Roles, int Quantity)> classifiedCards)
    {
        ArgumentNullException.ThrowIfNull(targetRoles);
        ArgumentNullException.ThrowIfNull(classifiedCards);

        Dictionary<string, int> roleCounts = targetRoles.ToDictionary(role => role, _ => 0, StringComparer.Ordinal);

        foreach ((IReadOnlyList<string> roles, int quantity) in classifiedCards)
        {
            foreach (string role in roles)
            {
                if (roleCounts.ContainsKey(role))
                {
                    roleCounts[role] += quantity;
                }
            }
        }

        return roleCounts;
    }
}
