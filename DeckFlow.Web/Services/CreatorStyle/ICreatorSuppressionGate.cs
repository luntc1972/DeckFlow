namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>Determines suppression after resolving every known representation of a creator.</summary>
public interface ICreatorSuppressionGate
{
    /// <summary>Returns whether the supplied creator representation is suppressed.</summary>
    Task<bool> IsSuppressedAsync(string creatorSlug, CancellationToken cancellationToken = default);

    /// <summary>Returns creator slugs that are not suppressed from one suppression snapshot.</summary>
    Task<IReadOnlySet<string>> GetUnsuppressedAsync(IEnumerable<string> creatorSlugs, CancellationToken cancellationToken = default);
}
