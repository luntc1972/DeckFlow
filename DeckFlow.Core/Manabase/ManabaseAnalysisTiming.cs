namespace DeckFlow.Core.Manabase;

/// <summary>Reports elapsed time for a measured sub-stage of a manabase analysis.</summary>
public sealed record ManabaseAnalysisTiming(string Name, long ElapsedMilliseconds);
