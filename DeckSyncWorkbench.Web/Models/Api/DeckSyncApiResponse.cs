using System.Collections.Generic;
using DeckSyncWorkbench.Core.Models;

namespace DeckSyncWorkbench.Web.Models.Api;

public sealed record DeckSyncApiResponse(
    string ReportText,
    string DeltaText,
    string FullImportText,
    string SourceSystem,
    string TargetSystem,
    DeckSyncApiDiffSummary Summary,
    IReadOnlyList<PrintingConflictDto> PrintingConflicts);

public sealed record DeckSyncApiDiffSummary(
    int AddCount,
    int CountMismatchCount,
    int OnlyInTargetCount,
    int PrintingConflictCount);

public sealed record PrintingConflictDto(
    string CardName,
    string ArchidektSetCode,
    string ArchidektCollectorNumber,
    string? ArchidektCategory,
    string? MoxfieldSetCode,
    string? MoxfieldCollectorNumber,
    string? SuggestedResolution);
