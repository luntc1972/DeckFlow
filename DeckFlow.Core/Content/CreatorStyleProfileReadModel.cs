using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Dapper materialization target for creator style-profile read queries.
/// </summary>
public sealed class CreatorStyleProfileReadModel
{
    /// <summary>
    /// Gets the creator slug.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the platform identifier.
    /// </summary>
    public required string Platform { get; init; }

    /// <summary>
    /// Gets the minimum deck count used for the profile.
    /// </summary>
    public int MinDecks { get; init; }

    /// <summary>
    /// Gets a value indicating whether the profile sample is insufficient.
    /// </summary>
    public bool InsufficientSample { get; init; }

    /// <summary>
    /// Gets the serialized stated-rules section payload, if any.
    /// </summary>
    public string? StatedRulesJson { get; init; }

    /// <summary>
    /// Gets the serialized measured-metrics section payload, if any.
    /// </summary>
    public string? MeasuredMetricsJson { get; init; }

    /// <summary>
    /// Gets the serialized fused-targets section payload, if any.
    /// </summary>
    public string? FusedTargetsJson { get; init; }

    /// <summary>
    /// Gets the profile update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; init; }
}

/// <summary>
/// Shared SELECT column list for creator style-profile read queries.
/// </summary>
public static class CreatorStyleProfileReadColumns
{
    /// <summary>
    /// Gets the canonical read SELECT column list in stable order.
    /// </summary>
    public const string SelectList = "slug, platform, min_decks, insufficient_sample, stated_rules_json, measured_metrics_json, fused_targets_json, updated_utc";
}

/// <summary>
/// Maps read-model rows into creator style profiles.
/// </summary>
public static class CreatorStyleProfileMapper
{
    /// <summary>
    /// Maps a materialized read-model row into a <see cref="CreatorStyleProfile"/>.
    /// </summary>
    /// <param name="row">The materialized row.</param>
    /// <returns>The mapped creator style profile.</returns>
    public static CreatorStyleProfile ToProfile(CreatorStyleProfileReadModel row)
        => new()
        {
            Slug = row.Slug,
            Platform = row.Platform,
            MinDecks = row.MinDecks,
            InsufficientSample = row.InsufficientSample,
            StatedRules = CreatorStyleProfileSections.DeserializeSection<StatedRule>(row.StatedRulesJson),
            MeasuredMetrics = CreatorStyleProfileSections.DeserializeSection<MeasuredMetric>(row.MeasuredMetricsJson),
            FusedTargets = CreatorStyleProfileSections.DeserializeSection<FusedTarget>(row.FusedTargetsJson),
            UpdatedUtc = row.UpdatedUtc
        };
}
