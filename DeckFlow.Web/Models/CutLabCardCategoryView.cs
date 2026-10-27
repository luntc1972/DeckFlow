namespace DeckFlow.Web.Models;

/// <summary>Effective deck-builder categories displayed for a pool card.</summary>
public sealed record CutLabCardCategoryView
{
    /// <summary>The effective primary category, or null for an uncategorized card.</summary>
    public string? Primary { get; init; }

    /// <summary>Additional effective category tags in imported order.</summary>
    public IReadOnlyList<string> AlsoTagged { get; init; } = [];

    /// <summary>Whether the card has an explicit category override.</summary>
    public bool IsEdited { get; init; }
}
