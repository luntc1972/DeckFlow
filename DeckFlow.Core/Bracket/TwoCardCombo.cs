namespace DeckFlow.Core.Bracket;

/// <summary>
/// A two-card combination found in a deck that can produce a winning or game-deciding outcome.
/// This is a Core-local record used by <c>BracketClassifier</c>. The Web orchestrator
/// (plan 76-04) maps <c>SpellbookCombo</c> to this type before calling the classifier,
/// keeping <c>DeckFlow.Core</c> free of any <c>DeckFlow.Web</c> reference.
/// </summary>
/// <param name="CardNames">The names of the two cards forming the combo.</param>
/// <param name="Results">Outcome descriptions (e.g., "Infinite mana", "Win the game").</param>
public sealed record TwoCardCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results);
