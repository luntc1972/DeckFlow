using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.Tools;

/// <summary>
/// Canonical metadata for one public DeckFlow tool.
/// </summary>
public sealed record ToolDefinition
{
    /// <summary>
    /// Stable machine key for the tool.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Friendly label shown in navigation and admin surfaces.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Canonical public route path.
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Navigation/home section the tool belongs to.
    /// </summary>
    public required ToolNavSection Section { get; init; }

    /// <summary>
    /// Feature-flag key controlling visibility for the tool.
    /// </summary>
    public required string FlagKey { get; init; }

    /// <summary>
    /// Indicates whether the tool is one of the core Analyze workflows.
    /// </summary>
    public required bool Core { get; init; }

    /// <summary>
    /// Home-tile title copy.
    /// </summary>
    public required string TileTitle { get; init; }

    /// <summary>
    /// Home-tile description copy.
    /// </summary>
    public required string TileDescription { get; init; }

    /// <summary>
    /// Help topic slug, or <see langword="null" /> when no help topic exists.
    /// </summary>
    public string? HelpSlug { get; init; }

    /// <summary>
    /// Tab used to mark the tool active in the nav strip.
    /// </summary>
    public required DeckPageTab Tab { get; init; }

    /// <summary>
    /// Indicates whether the tool is highlighted as a primary home tile.
    /// </summary>
    public required bool IsPrimaryTile { get; init; }

    /// <summary>
    /// Stable icon identifier for inline SVG lookup.
    /// </summary>
    public required string IconKey { get; init; }
}
