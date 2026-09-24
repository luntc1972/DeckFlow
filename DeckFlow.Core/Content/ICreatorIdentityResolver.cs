namespace DeckFlow.Core.Content;

#pragma warning disable CS1591

/// <summary>Resolves any known creator representation to its canonical identity.</summary>
public interface ICreatorIdentityResolver
{
    Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default);

    async Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(
        IReadOnlyCollection<string> representations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(representations);
        var resolved = new Dictionary<string, CreatorIdentity?>(StringComparer.OrdinalIgnoreCase);
        foreach (var representation in representations.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            resolved[representation] = await ResolveAsync(representation, cancellationToken).ConfigureAwait(false);
        }

        return resolved;
    }
}
#pragma warning restore CS1591
