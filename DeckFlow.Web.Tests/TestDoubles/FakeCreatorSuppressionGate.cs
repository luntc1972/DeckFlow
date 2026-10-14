using DeckFlow.Core.Content;
using DeckFlow.Web.Services.CreatorStyle;

namespace DeckFlow.Web.Tests;

internal sealed class FakeCreatorSuppressionGate : ICreatorSuppressionGate
{
    private readonly ICreatorSuppressionStore _store;

    public FakeCreatorSuppressionGate(ICreatorSuppressionStore store) => _store = store;

    public async Task<bool> IsSuppressedAsync(string creatorSlug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);
        return new CreatorSuppressionMatcher(await _store.ListAsync(cancellationToken).ConfigureAwait(false)).IsSuppressed(creatorSlug);
    }

    public async Task<IReadOnlySet<string>> GetUnsuppressedAsync(IEnumerable<string> creatorSlugs, CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string slug in creatorSlugs)
        {
            if (!await IsSuppressedAsync(slug, cancellationToken).ConfigureAwait(false)) result.Add(slug);
        }

        return result;
    }
}
