namespace DeckFlow.Web.Services.Tools;

/// <summary>
/// Exposes the canonical list of public DeckFlow tools.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets the full tool list in stable registry order.
    /// </summary>
    IReadOnlyList<ToolDefinition> All { get; }
}
