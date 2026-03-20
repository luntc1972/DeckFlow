using System.ComponentModel.DataAnnotations;
using DeckSyncWorkbench.Core.Models;
using DeckSyncWorkbench.Web.Models;

namespace DeckSyncWorkbench.Web.Models.Api;

public sealed record DeckSyncApiRequest
{
    public SyncDirection Direction { get; init; } = SyncDirection.DeckSyncWorkbench;

    public MatchMode Mode { get; init; } = MatchMode.Loose;

    public CategorySyncMode CategorySyncMode { get; init; } = CategorySyncMode.TargetCategories;

    public DeckInputSource MoxfieldInputSource { get; init; } = DeckInputSource.PasteText;

    public string? MoxfieldText { get; init; }

    public string? MoxfieldUrl { get; init; }

    public DeckInputSource ArchidektInputSource { get; init; } = DeckInputSource.PasteText;

    public string? ArchidektText { get; init; }

    public string? ArchidektUrl { get; init; }

    public DeckDiffRequest ToDeckDiffRequest()
    {
        return new DeckDiffRequest
        {
            Direction = Direction,
            Mode = Mode,
            CategorySyncMode = CategorySyncMode,
            MoxfieldInputSource = MoxfieldInputSource,
            MoxfieldText = MoxfieldText ?? string.Empty,
            MoxfieldUrl = MoxfieldUrl ?? string.Empty,
            ArchidektInputSource = ArchidektInputSource,
            ArchidektText = ArchidektText ?? string.Empty,
            ArchidektUrl = ArchidektUrl ?? string.Empty,
        };
    }
}
