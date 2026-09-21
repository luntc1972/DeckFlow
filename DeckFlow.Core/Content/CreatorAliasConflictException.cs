namespace DeckFlow.Core.Content;

#pragma warning disable CS1591

/// <summary>Thrown when an alias is already owned by another canonical creator slug.</summary>
public sealed class CreatorAliasConflictException : InvalidOperationException
{
    public CreatorAliasConflictException(string alias, string ownerSlug)
        : base($"Creator alias '{alias}' is already owned by '{ownerSlug}'.")
    {
        Alias = alias;
        OwnerSlug = ownerSlug;
    }

    public string Alias { get; }
    public string OwnerSlug { get; }
}
#pragma warning restore CS1591
