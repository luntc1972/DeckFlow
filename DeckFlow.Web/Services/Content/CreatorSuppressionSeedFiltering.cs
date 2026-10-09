using DeckFlow.Core.Content;

namespace DeckFlow.Web.Services;

/// <summary>Filters seed creator keys while isolating conflicting aliases.</summary>
internal static class CreatorSuppressionSeedFiltering
{
    /// <summary>Returns unsuppressed keys, retrying individually when a batch has a conflicting alias.</summary>
    public static async Task<HashSet<string>> GetUnsuppressedWithConflictFallbackAsync(
        ICreatorSuppressionFilter filter,
        IReadOnlyList<string> keys,
        Action<string> onConflict,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await filter.GetUnsuppressedAsync(keys, cancellationToken).ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (CreatorAliasConflictException)
        {
            // Why: one conflicting alias must not abort the whole seed.
            var unsuppressed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                try
                {
                    if ((await filter.GetUnsuppressedAsync([key], cancellationToken).ConfigureAwait(false)).Count > 0)
                    {
                        unsuppressed.Add(key);
                    }
                }
                catch (CreatorAliasConflictException)
                {
                    onConflict(key);
                }
            }

            return unsuppressed;
        }
    }
}
