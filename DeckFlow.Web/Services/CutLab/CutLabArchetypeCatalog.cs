using System.Diagnostics.CodeAnalysis;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Fixed catalog of supported Cut Lab deck archetypes.</summary>
public static class CutLabArchetypeCatalog
{
    /// <summary>Supported archetypes in UI display order.</summary>
    public static IReadOnlyList<CutLabArchetypeEntry> Entries { get; } =
    [
        new("turbo-combo", "Turbo combo", "Wins in the first few turns with fast mana, tutors and a compact combo.", ["combo"], new CutLabGoalSettings { CommanderByTurn = 2, EngineByTurn = 2, RepresentativeLineByTurn = 3 }),
        new("midrange-combo", "Midrange combo", "Plays a normal game, then wins with a protected combo.", ["combo"], new CutLabGoalSettings { CommanderByTurn = 3, EngineByTurn = 3, RepresentativeLineByTurn = 5 }),
        new("stax", "Stax", "Taxes and locks opponents while the deck operates around it.", ["stax"], new CutLabGoalSettings { CommanderByTurn = 2, EngineByTurn = 3, RepresentativeLineByTurn = 6 }),
        new("control", "Control", "Answers threats and wins late on card advantage.", ["control"], new CutLabGoalSettings { CommanderByTurn = 4, EngineByTurn = 3, RepresentativeLineByTurn = 7 }),
        new("aggro-voltron", "Aggro / Voltron", "Wins through combat, often commander damage.", ["voltron", "combat"], new CutLabGoalSettings { CommanderByTurn = 2, EngineByTurn = 3, RepresentativeLineByTurn = 5 }),
        new("value-engine", "Value / Engine", "Grinds with card-advantage engines.", [], new CutLabGoalSettings { CommanderByTurn = 3, EngineByTurn = 3, RepresentativeLineByTurn = 6 }),
        new("spellslinger-storm", "Spellslinger / Storm", "Chains instants and sorceries into payoffs or storm.", ["spellslinger"], new CutLabGoalSettings { CommanderByTurn = 3, EngineByTurn = 3, RepresentativeLineByTurn = 5 }),
    ];

    /// <summary>Looks up an archetype by its exact stable slug.</summary>
    /// <param name="slug">Slug to resolve.</param>
    /// <param name="entry">Resolved catalog entry when found.</param>
    /// <returns><see langword="true"/> when the slug is known.</returns>
    public static bool TryGetBySlug(string? slug, [NotNullWhen(true)] out CutLabArchetypeEntry? entry)
    {
        entry = Entries.FirstOrDefault(candidate => string.Equals(candidate.Slug, slug, StringComparison.Ordinal));
        return entry is not null;
    }

    /// <summary>Returns a known exact slug, or <see langword="null"/>.</summary>
    public static string? NormalizeSlug(string? slug)
    {
        return TryGetBySlug(slug, out var entry) ? entry.Slug : null;
    }

    /// <summary>Combines manual strategy selections with an archetype's presets.</summary>
    /// <param name="profile">Profile holding manual selections and optional archetype.</param>
    /// <returns>Manual strategies first, followed by non-duplicate archetype presets.</returns>
    public static IReadOnlyList<string> EffectiveStrategies(CutLabPlanProfile? profile)
    {
        if (profile is null)
        {
            return [];
        }

        var effective = profile.GenericStrategies.ToList();
        var seen = new HashSet<string>(effective, StringComparer.OrdinalIgnoreCase);
        if (!TryGetBySlug(profile.Archetype, out var archetype))
        {
            return effective;
        }

        foreach (string strategy in archetype.PresetStrategies)
        {
            if (seen.Add(strategy))
            {
                effective.Add(strategy);
            }
        }

        return effective;
    }
}
