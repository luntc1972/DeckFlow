namespace DeckFlow.Core.Content;

/// <summary>Consistent production view used to replace a local suppression replica.</summary>
public sealed record CreatorSuppressionSnapshot(long Revision, IReadOnlyList<CreatorSuppression> Rows);
