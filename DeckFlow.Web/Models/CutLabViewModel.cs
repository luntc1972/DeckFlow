using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Models;

/// <summary>View model for the Cut Lab page.</summary>
public sealed record CutLabViewModel
{
    /// <summary>The active deck tool tab.</summary>
    public DeckPageTab ActiveTab { get; init; }

    /// <summary>The current request values to re-render into the form.</summary>
    public CutLabRequest Request { get; init; } = new();

    /// <summary>User-facing error message for hard failures.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Non-commander pool count returned by the service.</summary>
    public int CardCount { get; init; }

    /// <summary>Commander banned-card names present in the current pool.</summary>
    public IReadOnlyList<string> BannedCardsPresent { get; init; } = [];

    /// <summary>True when the current pool has no banned cards.</summary>
    public bool IsLegal { get; init; }

    /// <summary>True when the user must choose a commander manually.</summary>
    public bool CommanderSelectionRequired { get; init; }

    /// <summary>Commander-eligible choices to show when manual selection is required.</summary>
    public IReadOnlyList<string> CommanderChoices { get; init; } = [];

    /// <summary>Non-blocking warnings returned by the page service.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>True when a resolved state is available to render.</summary>
    public bool HasResult { get; init; }

    /// <summary>Serialized hidden-field working-session JSON.</summary>
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Resolved pool cards for the current working session.</summary>
    public IReadOnlyList<CutLabPoolCard> Pool { get; init; } = [];

    /// <summary>Resolved lock packages for the current working session.</summary>
    public IReadOnlyList<CutLabPackage> Packages { get; init; } = [];

    /// <summary>Display copy for the current pool and lock count.</summary>
    public string PoolStatusText { get; init; } = string.Empty;

    /// <summary>Builds the page model from the request and service result.</summary>
    /// <param name="request">Current request values.</param>
    /// <param name="result">Processed Cut Lab result.</param>
    public static CutLabViewModel From(CutLabRequest request, CutLabProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        IReadOnlyList<CutLabPoolCard> pool = result.State?.Pool ?? [];
        return new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            Request = request,
            ErrorMessage = result.ErrorMessage,
            CardCount = result.CardCount,
            BannedCardsPresent = result.BannedCardsPresent,
            IsLegal = result.IsLegal,
            CommanderSelectionRequired = result.CommanderSelectionRequired,
            CommanderChoices = result.CommanderChoices,
            Warnings = result.Warnings,
            HasResult = result.HasResult,
            CutLabStateJson = result.SerializedStateJson ?? request.CutLabStateJson,
            Pool = pool,
            Packages = result.State?.Packages ?? [],
            PoolStatusText = BuildPoolStatusText(result.CardCount, pool),
        };
    }

    private static string BuildPoolStatusText(int cardCount, IReadOnlyList<CutLabPoolCard> pool)
    {
        if (cardCount <= 0 || pool.Count == 0)
        {
            return string.Empty;
        }

        var lockedCount = pool.Count(card => card.IsLocked);
        return $"{cardCount} cards in pool · {lockedCount} locked";
    }
}
