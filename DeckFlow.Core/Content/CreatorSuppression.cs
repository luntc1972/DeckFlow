namespace DeckFlow.Core.Content;

#pragma warning disable CS1591

/// <summary>Persisted creator suppression request.</summary>
public sealed record CreatorSuppression
{
    public required string Slug { get; init; }
    public required IReadOnlyList<string> Aliases { get; init; }
    public required string Reason { get; init; }
    public required DateTimeOffset RequestedUtc { get; init; }
    public string? Note { get; init; }
}
#pragma warning restore CS1591
