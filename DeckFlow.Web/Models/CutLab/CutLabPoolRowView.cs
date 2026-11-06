using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Models.CutLab;

/// <summary>A pool card with separate display and persisted ordering metadata.</summary>
public sealed record CutLabPoolRowView
{
    /// <summary>The original pool card.</summary>
    public required CutLabPoolCard Card { get; init; }

    /// <summary>The card's position in the persisted pool.</summary>
    public int PoolIndex { get; init; }

    /// <summary>The card's alphabetical display position without commander pinning.</summary>
    public int AlphabeticalIndex { get; init; }
}
