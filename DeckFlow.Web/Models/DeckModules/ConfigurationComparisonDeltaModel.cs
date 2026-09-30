namespace DeckFlow.Web.Models.DeckModules;

/// <summary>Numeric, list-shaped differences between analyzed Deck Modules configurations.</summary>
public sealed record ConfigurationComparisonDeltaModel
{
    /// <summary>The configuration every other column is compared against; its deltas are always null.</summary>
    public required ConfigurationComparisonColumn Reference { get; init; }

    /// <summary>One column per configuration, in comparison order, with deltas relative to <see cref="Reference"/>.</summary>
    public IReadOnlyList<ConfigurationComparisonColumn> Columns { get; init; } = [];

    /// <summary>Color-source rows, each holding one value per column.</summary>
    public IReadOnlyList<ConfigurationColorSourceDeltaRow> ColorRows { get; init; } = [];

    /// <summary>Interaction rows keyed by module kind, each holding one value per column.</summary>
    public IReadOnlyList<ConfigurationInteractionDeltaRow> InteractionRows { get; init; } = [];
}

/// <summary>Metrics for one configuration, optionally compared with the reference configuration.</summary>
public sealed record ConfigurationComparisonColumn
{
    /// <summary>Configuration identifier; null when the configuration has no analysis.</summary>
    public string? ConfigurationId { get; init; }
    /// <summary>Display name of the configuration.</summary>
    public string? ConfigurationName { get; init; }
    /// <summary>Whether an analysis result exists for this configuration.</summary>
    public required bool IsAnalyzed { get; init; }
    /// <summary>Whether the analysis covered only the core cards rather than the full deck.</summary>
    public required bool IsCoreOnly { get; init; }
    /// <summary>Overall mana-base health label from the analysis.</summary>
    public string? Health { get; init; }
    /// <summary>Number of lands in the configuration.</summary>
    public int? LandCount { get; init; }
    /// <summary>Recommended land count for the configuration.</summary>
    public double? TargetLandCount { get; init; }
    /// <summary>Difference between the actual and recommended land count.</summary>
    public double? LandTargetDelta { get; init; }
    /// <summary>Number of ramp sources in the configuration.</summary>
    public int? RampSourceCount { get; init; }
    /// <summary>Number of cards flagged as hard to cast.</summary>
    public int? HardToCastCount { get; init; }
    /// <summary>Land count minus the reference land count; null for the reference column.</summary>
    public int? LandCountDelta { get; init; }
    /// <summary>Ramp source count minus the reference count; null for the reference column.</summary>
    public int? RampSourceCountDelta { get; init; }
    /// <summary>Hard-to-cast count minus the reference count; null for the reference column.</summary>
    public int? HardToCastCountDelta { get; init; }
    /// <summary>Whether the land count differs from the reference configuration.</summary>
    public required bool HasLandCountChange { get; init; }
}

/// <summary>Color-source values aligned across configurations.</summary>
public sealed record ConfigurationColorSourceDeltaRow
{
    /// <summary>Color key used to align values across configurations.</summary>
    public required string Color { get; init; }
    /// <summary>Human-readable color label.</summary>
    public required string DisplayColor { get; init; }
    /// <summary>One value per comparison column, in column order.</summary>
    public IReadOnlyList<ConfigurationColorSourceDeltaValue> Values { get; init; } = [];
}

/// <summary>One configuration's color-source value and delta.</summary>
public sealed record ConfigurationColorSourceDeltaValue
{
    /// <summary>Configuration this value belongs to.</summary>
    public string? ConfigurationId { get; init; }
    /// <summary>Number of sources producing this color.</summary>
    public double? ActualSources { get; init; }
    /// <summary>Number of sources the analysis requires for this color.</summary>
    public int? RequiredSources { get; init; }
    /// <summary>Actual sources minus the reference value; null for the reference or when either side is missing.</summary>
    public double? ActualSourcesDelta { get; init; }
    /// <summary>Whether this configuration's analysis reported the color at all.</summary>
    public required bool IsPresent { get; init; }
}

/// <summary>Interaction values aligned by configuration module kind.</summary>
public sealed record ConfigurationInteractionDeltaRow
{
    /// <summary>Module kind used to align values across configurations.</summary>
    public required Services.Modular.ConfigurationModuleKind ModuleKind { get; init; }
    /// <summary>Display name of the module.</summary>
    public required string ModuleName { get; init; }
    /// <summary>One value per comparison column, in column order.</summary>
    public IReadOnlyList<ConfigurationInteractionDeltaValue> Values { get; init; } = [];
}

/// <summary>One configuration's interaction value and delta.</summary>
public sealed record ConfigurationInteractionDeltaValue
{
    /// <summary>Configuration this value belongs to.</summary>
    public string? ConfigurationId { get; init; }
    /// <summary>Number of interactions found for the module.</summary>
    public int? InteractionCount { get; init; }
    /// <summary>Interaction count minus the reference value; null for the reference or when either side is missing.</summary>
    public int? Delta { get; init; }
    /// <summary>Whether this configuration's analysis reported the module at all.</summary>
    public required bool IsPresent { get; init; }
}
