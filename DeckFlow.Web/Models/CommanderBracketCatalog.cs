namespace DeckFlow.Web.Models;

/// <summary>Selectable Commander bracket option used to describe expected deck power and pace.</summary>
/// <param name="Value">Stable bracket value posted by forms and APIs.</param>
/// <param name="Label">Human-readable bracket label shown in the UI.</param>
/// <param name="Summary">Short description of the bracket's expected deck shape.</param>
/// <param name="TurnsExpectation">Expected turn range for wins or losses in the bracket.</param>
public sealed record CommanderBracketOption(
    string Value,
    string Label,
    string Summary,
    string TurnsExpectation);

/// <summary>Provides the Commander bracket options used by deck analysis prompts.</summary>
public static class CommanderBracketCatalog
{
    /// <summary>Ordered Commander bracket options from casual exhibition through cEDH.</summary>
    public static IReadOnlyList<CommanderBracketOption> Options { get; } =
    [
        new(
            "Exhibition",
            "Bracket 1: Exhibition",
            "Prioritize theme, unusual ideas, flexible legality, and showcase gameplay over optimization.",
            "Expect to play at least nine turns before you win or lose."),
        new(
            "Core",
            "Bracket 2: Core",
            "Unoptimized and straightforward decks with incremental, disruptable wins and low-pressure gameplay.",
            "Expect to play at least eight turns before you win or lose."),
        new(
            "Upgraded",
            "Bracket 3: Upgraded",
            "Strong synergy, high card quality, meaningful interaction, and explosive but earned wins.",
            "Expect to play at least six turns before you win or lose."),
        new(
            "Optimized",
            "Bracket 4: Optimized",
            "Fast, lethal, efficient decks with strong game changers, fast mana, tutors, and explosive play.",
            "Expect to play at least four turns before you win or lose."),
        new(
            "cEDH",
            "Bracket 5: cEDH",
            "Metagame-tuned competitive Commander decks built for maximum efficiency and consistency.",
            "Games can end on any turn.")
    ];

    /// <summary>Finds a Commander bracket option by its posted value.</summary>
    /// <param name="value">Bracket value to match, ignoring case.</param>
    /// <returns>The matching bracket option, or null when the value is unknown.</returns>
    public static CommanderBracketOption? Find(string? value)
    {
        return Options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns whether the posted bracket value resolves to the canonical cEDH bracket.</summary>
    /// <param name="bracketValue">Bracket value to evaluate.</param>
    /// <returns>True when the value resolves to cEDH; otherwise false.</returns>
    public static bool IsCedh(string? bracketValue)
    {
        return string.Equals(Find(bracketValue)?.Value, "cEDH", StringComparison.OrdinalIgnoreCase);
    }
}
