using DeckFlow.Core.Content;

namespace DeckFlow.Web.Tests;

internal sealed class FakeCreatorIdentityResolver : ICreatorIdentityResolver
{
    public Dictionary<string, CreatorIdentity> Identities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ThrowOn { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
    {
        if (string.Equals(anyRepresentation, ThrowOn, StringComparison.OrdinalIgnoreCase))
        {
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            throw new CreatorAliasConflictException(anyRepresentation, "first, second");
        }

        return Task.FromResult(Identities.GetValueOrDefault(anyRepresentation));
    }
}
