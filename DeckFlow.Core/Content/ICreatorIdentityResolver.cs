namespace DeckFlow.Core.Content;

#pragma warning disable CS1591

/// <summary>Resolves any known creator representation to its canonical identity.</summary>
public interface ICreatorIdentityResolver
{
    Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default);
}
#pragma warning restore CS1591
