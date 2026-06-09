namespace DeckFlow.Web.Models;

/// <summary>
/// Represents a single selectable primer section.
/// </summary>
/// <param name="Id">Stable section identifier posted by the workflow form.</param>
/// <param name="Number">Section number shown in the UI.</param>
/// <param name="Title">Display title shown to the user.</param>
/// <param name="HelpText">Explains what good AI output for this section looks like.</param>
/// <param name="Group">Group this section belongs to.</param>
/// <param name="BracketGate">Null = available in all brackets; "cedh-only" = bracket 5 only; "casual-only" = brackets 1-4 only.</param>
public sealed record PrimerSectionEntry(
    string Id,
    int Number,
    string Title,
    string HelpText,
    string Group,
    string? BracketGate = null);

/// <summary>
/// Groups related primer sections under a shared collapsible heading.
/// </summary>
/// <param name="Id">Stable group identifier.</param>
/// <param name="Label">Display label for the group.</param>
/// <param name="Sections">Sections included in the group.</param>
public sealed record PrimerSectionGroup(
    string Id,
    string Label,
    IReadOnlyList<PrimerSectionEntry> Sections);

/// <summary>
/// Provides the 31 primer sections, 5 collapsible groups, and bracket-preset helpers.
/// </summary>
public static class PrimerSectionCatalog
{
    /// <summary>
    /// Gets the ordered primer section groups shown on the deck-primer workflow page.
    /// </summary>
    public static IReadOnlyList<PrimerSectionGroup> Groups { get; } =
    [
        new(
            "identity",
            "Identity",
            [
                new("commander-identity", 1, "Commander Identity", "Summarize who the commander is, what the deck is trying to express, and the core identity the primer should reinforce.", "Identity"),
                new("color-pie-constraints", 2, "Color Pie Constraints", "Explain which effects the deck naturally excels at or lacks because of its color identity so the pilot understands real boundaries.", "Identity"),
                new("archetype-and-table-role", 3, "Archetype and Table Role", "Describe the archetype, seat role, and how the deck should present itself to the table in a typical pod.", "Identity"),
                new("win-conditions-overview", 4, "Win Conditions Overview", "List the primary and secondary ways the deck actually closes games, with enough detail that a pilot knows what success looks like.", "Identity"),
                new("card-rationale-core-inclusions", 5, "Core Inclusion Rationale", "Call out the most important includes and explain why each matters to the deck's identity instead of just naming staples.", "Identity"),
                new("signature-cards-and-keepers", 6, "Signature Cards and Keepers", "Identify the cards that define the deck and should usually survive cut discussions unless the entire strategy changes.", "Identity"),
                new("flex-slots-and-tunable-packages", 7, "Flex Slots and Tunable Packages", "Point out configurable slots or packages so the primer can separate sacred cows from cards that change with preference or meta.", "Identity")
            ]),
        new(
            "combos",
            "Combos",
            [
                new("verified-combos", 8, "Verified Combos", "Present grounded combo lines clearly, including what pieces matter, what the line does, and when it is worth pursuing.", "Combos"),
                new("near-combos", 9, "Near-Combos", "Highlight one-card-away lines or close assemblies that help the pilot recognize meaningful upgrade or tutor opportunities.", "Combos"),
                new("speculative-synergies", 10, "Speculative Synergies", "Separate plausible but unverified interactions from known combos so the AI contributes ideas without overstating certainty.", "Combos"),
                new("combo-prioritization", 11, "Combo Prioritization", "Rank combo routes by practicality, speed, resiliency, or table context so the pilot knows which lines deserve the most attention.", "Combos"),
                new("tutor-targets-and-assembly", 12, "Tutor Targets and Assembly", "Explain what to tutor for and how to assemble the deck's best lines without assuming every tutor goes for the same card.", "Combos"),
                new("combo-fail-states-and-backups", 13, "Combo Fail States and Backups", "Describe what to do when combo attempts are disrupted and how the deck pivots into backup wins or value plans.", "Combos")
            ]),
        new(
            "gameplay",
            "Gameplay",
            [
                new("game-plan-by-phase", 14, "Game Plan by Phase", "Break down early, mid, and late-game priorities so the pilot knows how the deck should progress across a real game.", "Gameplay"),
                new("engine-and-resource-loops", 15, "Engine and Resource Loops", "Explain the engines, draw patterns, mana loops, or recurring value packages that keep the deck functioning over time.", "Gameplay"),
                new("mulligan-principles", 16, "Mulligan Principles", "Describe what a strong opening hand looks like, what is too risky, and which resources matter most before the first draw step.", "Gameplay"),
                new("opening-sequences", 17, "Opening Sequences", "Give sample sequencing heuristics for the first turns so the pilot can convert a keepable hand into a stable start.", "Gameplay"),
                new("role-count-grounding", 18, "Role Count Grounding", "Ground the primer in role counts like ramp, draw, tutors, and interaction so recommendations stay tied to the deck's actual composition.", "Gameplay"),
                new("sequencing-and-pivot-lines", 19, "Sequencing and Pivot Lines", "Explain how to pivot between proactive and reactive lines when the table or draw steps force a change in role.", "Gameplay"),
                new("commander-deployment-timing", 20, "Commander Deployment Timing", "Clarify when to commit the commander, when to hold it, and what board states should change that decision.", "Gameplay"),
                new("recovery-and-rebuild-plan", 21, "Recovery and Rebuild Plan", "Show how the deck stabilizes after wipes, tax effects, or failed pushes so the pilot has a plan after setbacks.", "Gameplay")
            ]),
        new(
            "matchups",
            "Matchups",
            [
                new("matchup-archetype-plan", 22, "Matchup Archetype Plan", "Outline how the deck approaches common opposing archetypes and what strategic posture changes in each matchup.", "Matchups"),
                new("threat-assessment-priorities", 23, "Threat Assessment Priorities", "Explain which permanents, commanders, or game states deserve the most respect so the primer sharpens table reads.", "Matchups"),
                new("cedh-meta-macro-matchups", 24, "cEDH Meta Macro Matchups", "Map the deck into fast-combo, midrange, and stax-heavy cEDH pods with concrete expectations about speed and positioning.", "Matchups", "cedh-only"),
                new("stack-wars-and-fast-mana", 25, "Stack Wars and Fast Mana", "Explain how to navigate free interaction, mulligan aggression, and fast-mana races that specifically matter in cEDH games.", "Matchups", "cedh-only"),
                new("battlecruiser-politics-and-social-pacing", 26, "Battlecruiser Politics and Social Pacing", "Address threat signaling, pacing, and political considerations that matter more in slower casual pods than in cEDH.", "Matchups", "casual-only")
            ]),
        new(
            "maintenance",
            "Maintenance",
            [
                new("budget-cut-ladder", 27, "Budget Cut Ladder", "Recommend sensible cuts or substitutions when budget matters, prioritizing replacements that preserve the deck's core identity.", "Maintenance"),
                new("upgrade-paths", 28, "Upgrade Paths", "Lay out concrete next upgrades so the primer doubles as a roadmap for future iterations instead of a static snapshot.", "Maintenance"),
                new("meta-shift-adjustments", 29, "Meta Shift Adjustments", "Explain what to change when the local meta speeds up, slows down, or leans harder into specific strategies.", "Maintenance"),
                new("version-history-and-change-log", 30, "Version History and Change Log", "Suggest how to document meaningful changes between versions so future updates retain context and intent.", "Maintenance"),
                new("pilot-reminders-and-misplays", 31, "Pilot Reminders and Misplays", "Capture recurring mistakes, heuristics, and reminders that help the deck improve through repeated play.", "Maintenance")
            ])
    ];

    /// <summary>
    /// Gets the flattened ordered list of every selectable primer section across all groups.
    /// </summary>
    public static IReadOnlyList<PrimerSectionEntry> AllSections { get; } = Groups
        .SelectMany(group => group.Sections)
        .ToList();

    /// <summary>
    /// Gets the section IDs available only for bracket 5 cEDH primers.
    /// </summary>
    public static IReadOnlySet<string> CedhOnlySectionIds { get; } = AllSections
        .Where(section => string.Equals(section.BracketGate, "cedh-only", StringComparison.OrdinalIgnoreCase))
        .Select(section => section.Id)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the section IDs available only for brackets 1-4 primers.
    /// </summary>
    public static IReadOnlySet<string> CasualOnlySectionIds { get; } = AllSections
        .Where(section => string.Equals(section.BracketGate, "casual-only", StringComparison.OrdinalIgnoreCase))
        .Select(section => section.Id)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the default selected primer-section IDs for the supplied bracket.
    /// </summary>
    /// <param name="bracketValue">Commander bracket value posted by the UI.</param>
    /// <returns>An ordered list of section IDs to pre-select for the bracket, or an empty list when unknown.</returns>
    public static IReadOnlyList<string> GetPresetForBracket(string bracketValue)
    {
        var bracket = CommanderBracketCatalog.Find(bracketValue);
        if (bracket is null)
        {
            return [];
        }

        return bracket.Value switch
        {
            "Exhibition" or "Core" => BuildPreset(
                bracket.Value,
                "commander-identity",
                "color-pie-constraints",
                "archetype-and-table-role",
                "win-conditions-overview",
                "card-rationale-core-inclusions",
                "signature-cards-and-keepers",
                "flex-slots-and-tunable-packages",
                "game-plan-by-phase",
                "mulligan-principles",
                "budget-cut-ladder"),
            "Upgraded" => BuildPreset(
                bracket.Value,
                "commander-identity",
                "color-pie-constraints",
                "archetype-and-table-role",
                "win-conditions-overview",
                "card-rationale-core-inclusions",
                "signature-cards-and-keepers",
                "flex-slots-and-tunable-packages",
                "game-plan-by-phase",
                "engine-and-resource-loops",
                "mulligan-principles",
                "matchup-archetype-plan",
                "threat-assessment-priorities"),
            "Optimized" => BuildPreset(
                bracket.Value,
                "commander-identity",
                "color-pie-constraints",
                "archetype-and-table-role",
                "win-conditions-overview",
                "card-rationale-core-inclusions",
                "signature-cards-and-keepers",
                "flex-slots-and-tunable-packages",
                "verified-combos",
                "near-combos",
                "game-plan-by-phase",
                "engine-and-resource-loops",
                "mulligan-principles",
                "opening-sequences",
                "role-count-grounding",
                "sequencing-and-pivot-lines",
                "commander-deployment-timing",
                "recovery-and-rebuild-plan",
                "matchup-archetype-plan",
                "threat-assessment-priorities",
                "battlecruiser-politics-and-social-pacing"),
            "cEDH" => AllSections
                .Where(section => !CasualOnlySectionIds.Contains(section.Id))
                .Select(section => section.Id)
                .ToList(),
            _ => []
        };
    }

    /// <summary>
    /// Normalizes raw selected section IDs by trimming, validating, deduplicating, preserving catalog order, and removing bracket-gated entries.
    /// </summary>
    /// <param name="selections">Raw selected section IDs.</param>
    /// <param name="bracketValue">Commander bracket value posted by the UI.</param>
    /// <returns>A validated ordered list of section IDs allowed for the active bracket.</returns>
    public static IReadOnlyList<string> NormalizeSelections(IEnumerable<string>? selections, string bracketValue)
    {
        var allowed = (selections ?? Array.Empty<string>())
            .Where(selection => !string.IsNullOrWhiteSpace(selection))
            .Select(selection => selection.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return AllSections
            .Where(section => allowed.Contains(section.Id))
            .Where(section => IsSectionAvailableForBracket(section, bracketValue))
            .Select(section => section.Id)
            .ToList();
    }

    private static IReadOnlyList<string> BuildPreset(string bracketValue, params string[] sectionIds)
    {
        return NormalizeSelections(sectionIds, bracketValue);
    }

    private static bool IsSectionAvailableForBracket(PrimerSectionEntry section, string bracketValue)
    {
        var isCedh = string.Equals(
            CommanderBracketCatalog.Find(bracketValue)?.Value,
            "cEDH",
            StringComparison.OrdinalIgnoreCase);

        if (CedhOnlySectionIds.Contains(section.Id))
        {
            return isCedh;
        }

        if (CasualOnlySectionIds.Contains(section.Id))
        {
            return !isCedh;
        }

        return true;
    }
}
