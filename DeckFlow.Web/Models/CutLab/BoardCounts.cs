namespace DeckFlow.Web.Models.CutLab;

/// <summary>Board-by-board card counts used for Cut Lab intake validation and display.</summary>
public sealed record BoardCounts
{
    /// <summary>Mainboard-only card quantity, excluding the commander.</summary>
    public int MainboardCount { get; init; }

    /// <summary>Sideboard card quantity.</summary>
    public int SideboardCount { get; init; }

    /// <summary>Considering or maybeboard card quantity.</summary>
    public int MaybeboardCount { get; init; }

    /// <summary>Builds the shared board-breakdown display string.</summary>
    /// <returns>The shared board breakdown.</returns>
    public string ToBreakdown() =>
        $"Main {MainboardCount} · Sideboard {SideboardCount} · Considering/Maybe {MaybeboardCount}";
}
