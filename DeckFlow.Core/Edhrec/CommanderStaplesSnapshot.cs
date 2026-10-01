namespace DeckFlow.Core.Edhrec;

/// <summary>Bundled EDHREC-derived staple cards grouped by solo commander.</summary>
public sealed record CommanderStaplesSnapshot(
    int SchemaVersion,
    string GeneratedUtc,
    string Source,
    int MinDecks,
    double MinInclusion,
    IReadOnlyList<CommanderStaplesCommander> Commanders);

/// <summary>Staple cards and EDHREC deck count for one commander.</summary>
public sealed record CommanderStaplesCommander(
    string Name,
    int DeckCount,
    IReadOnlyList<string> Staples);
