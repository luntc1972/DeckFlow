namespace DeckFlow.Core.Loading;

/// <summary>
/// Describes a single deck-loading operation.
/// </summary>
/// <param name="Platform">Target deck platform to parse or import.</param>
/// <param name="InputKind">Input source type.</param>
/// <param name="InputValue">Raw text, URL, deck id, or file path depending on <paramref name="InputKind"/>.</param>
/// <param name="ExcludeMaybeboard">Whether Moxfield maybeboard entries should be filtered out.</param>
public sealed record DeckLoadRequest(
    DeckPlatform Platform,
    DeckInputKind InputKind,
    string InputValue,
    bool ExcludeMaybeboard = false);
