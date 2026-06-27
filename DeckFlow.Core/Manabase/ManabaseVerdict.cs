namespace DeckFlow.Core.Manabase;

/// <summary>
/// Deterministic plain-language reading of a <see cref="ManabaseReport"/> for UI, prompt, and text surfaces.
/// </summary>
public sealed record ManabaseVerdict
{
    /// <summary>True when the verdict surfaces actionable issue lines.</summary>
    public required bool HasIssues { get; init; }

    /// <summary>Ordered actionable issue lines, empty when <see cref="HasIssues"/> is false.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>Specific why-it's-fine reason, empty when <see cref="HasIssues"/> is true.</summary>
    public required string NoIssueReason { get; init; }

    /// <summary>Display heading for the verdict block.</summary>
    public required string Headline { get; init; }
}
