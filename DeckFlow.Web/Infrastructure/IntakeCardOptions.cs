namespace DeckFlow.Web.Infrastructure;

/// <summary>Options controlling the intake card wrapper and its result summary.</summary>
/// <param name="HasResult">Whether the card contains a submitted deck result.</param>
/// <param name="ResultLabel">The commander or deck label shown in the summary.</param>
/// <param name="ChangeLabel">The action label shown in the summary.</param>
public sealed record IntakeCardOptions(bool HasResult, string? ResultLabel, string ChangeLabel = "Edit");
