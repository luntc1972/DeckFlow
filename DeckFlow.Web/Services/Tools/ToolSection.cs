namespace DeckFlow.Web.Services.Tools;

/// <summary>
/// Visible tools grouped by one navigation section.
/// </summary>
public sealed record ToolSection
{
    /// <summary>
    /// The navigation section.
    /// </summary>
    public required ToolNavSection Section { get; init; }

    /// <summary>
    /// Visible tools in registry order.
    /// </summary>
    public required IReadOnlyList<ToolDefinition> Tools { get; init; }
}
