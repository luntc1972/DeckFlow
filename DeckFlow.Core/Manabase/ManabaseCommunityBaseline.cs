using System.Text.Json.Serialization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// The bundled community-baseline snapshot (deserialized from Data/manabase-baseline/latest.json).
/// Increment 1 carries per-bracket land means only; ramp/draw and per-commander rows arrive later.
/// </summary>
public sealed record ManabaseBaselineSnapshot
{
    /// <summary>Canonical provenance label for commander rows sourced from the EDHREC averages dump.</summary>
    public const string EdhrecAveragesSource = "edhrec-averages";

    /// <summary>Schema version of the data file (currently 1).</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    /// <summary>ISO-8601 UTC timestamp the file was generated.</summary>
    [JsonPropertyName("generatedUtc")]
    public string? GeneratedUtc { get; init; }

    /// <summary>Provenance label for the numbers (e.g. "edhrec-pilot-aggregate").</summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>Per-bracket rows (B2-B5 in Increment 1).</summary>
    [JsonPropertyName("brackets")]
    public IReadOnlyList<ManabaseBracketBaseline> Brackets { get; init; } = Array.Empty<ManabaseBracketBaseline>();

    /// <summary>Provenance label for the commanders block (e.g. "edhrec-averages"). Absent pre-Increment-2.</summary>
    [JsonPropertyName("commandersSource")]
    public string? CommandersSource { get; init; }

    /// <summary>Per-commander rows from the EDHREC averages dump (Increment 2; empty pre-Increment-2).</summary>
    [JsonPropertyName("commanders")]
    public IReadOnlyList<ManabaseCommanderBaseline> Commanders { get; init; } = Array.Empty<ManabaseCommanderBaseline>();
}

/// <summary>One per-bracket community baseline cell: the average land count real decks run at that bracket.</summary>
public sealed record ManabaseBracketBaseline
{
    /// <summary>Power bracket (2-5; Exhibition/B1 is unsupported).</summary>
    [JsonPropertyName("bracket")]
    public int Bracket { get; init; }

    /// <summary>Average land count across the sample.</summary>
    [JsonPropertyName("avgLands")]
    public double AvgLands { get; init; }

    /// <summary>Number of decks behind the average (display + trust).</summary>
    [JsonPropertyName("deckCount")]
    public int DeckCount { get; init; }

    /// <summary>
    /// Optional per-row provenance. Absent in Increment 1 (the file carries one snapshot-level
    /// <see cref="ManabaseBaselineSnapshot.Source"/>); the provider backfills this from the snapshot
    /// source when the row omits it. Increment 2 may set it per row (corpus vs edhrec).
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>Optional caveat note (e.g. thin/adjusted sample).</summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }
}

/// <summary>One per-commander (or partner-pair) community baseline cell from the EDHREC averages dump.</summary>
public sealed record ManabaseCommanderBaseline
{
    /// <summary>Primary commander name as published by EDHREC.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Partner commander name, when the row is a pair.</summary>
    [JsonPropertyName("partnerName")]
    public string? PartnerName { get; init; }

    /// <summary>Average land count across the sample (integer-rounded upstream).</summary>
    [JsonPropertyName("avgLands")]
    public required double AvgLands { get; init; }

    /// <summary>Number of EDHREC decks behind the average.</summary>
    [JsonPropertyName("deckCount")]
    public required int DeckCount { get; init; }
}

/// <summary>How the effective bracket for a result was chosen (drives the UI "auto-detected" hint in 1b).</summary>
public enum ManabaseBracketSource
{
    /// <summary>Auto-classified from the deck (Increment 1b).</summary>
    Auto,

    /// <summary>Explicitly chosen by the user via the bracket selector.</summary>
    Override,

    /// <summary>Derived from the analysis mode because no classification/override was available.</summary>
    Fallback,
}

/// <summary>
/// The resolved community-baseline block attached to a manabase result: the bracket used, its
/// bundled land average + sample size + provenance, and how the bracket was chosen. Present only
/// when the feature flag is on and a baseline row exists for the bracket.
/// </summary>
public sealed record ManabaseCommunityBaseline
{
    /// <summary>Maps a bracket number to the UI display name used for community baselines.</summary>
    public static string BracketName(int bracket) => bracket switch
    {
        2 => "Core",
        3 => "Upgraded",
        4 => "Optimized",
        5 => "cEDH",
        _ => $"B{bracket}"
    };

    /// <summary>The bracket (2-5) this baseline is for.</summary>
    public required int Bracket { get; init; }

    /// <summary>Average land count real decks run at this bracket.</summary>
    public required double AvgLands { get; init; }

    /// <summary>Sample size behind <see cref="AvgLands"/>.</summary>
    public required int DeckCount { get; init; }

    /// <summary>Provenance label from the data file.</summary>
    public required string? Source { get; init; }

    /// <summary>How the bracket was chosen.</summary>
    public required ManabaseBracketSource BracketSource { get; init; }

    /// <summary>Where the displayed land value came from (commander cell, blend, or bracket-global).</summary>
    public required ManabaseBaselineSource ValueSource { get; init; }

    /// <summary>Display name(s) for the commander cell when it contributed (e.g. "The Ur-Dragon"); null for Global.</summary>
    public string? CommanderDisplayName { get; init; }
}
