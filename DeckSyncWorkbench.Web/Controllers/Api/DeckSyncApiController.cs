using System.Linq;
using DeckSyncWorkbench.Core.Exporting;
using DeckSyncWorkbench.Core.Models;
using DeckSyncWorkbench.Core.Parsing;
using DeckSyncWorkbench.Core.Reporting;
using DeckSyncWorkbench.Web.Models.Api;
using DeckSyncWorkbench.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckSyncWorkbench.Web.Controllers.Api;

[ApiController]
[Route("api/deck")]
public sealed class DeckSyncApiController : ControllerBase
{
    private readonly IDeckSyncService _deckSyncService;
    private readonly ILogger<DeckSyncApiController> _logger;

    public DeckSyncApiController(IDeckSyncService deckSyncService, ILogger<DeckSyncApiController> logger)
    {
        _deckSyncService = deckSyncService;
        _logger = logger;
    }

    [HttpPost("diff")]
    public async Task<ActionResult<DeckSyncApiResponse>> PostDiffAsync([FromBody] DeckSyncApiRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { Message = "Request body is required." });
        }

        try
        {
            var deckRequest = request.ToDeckDiffRequest();
            var syncResult = await _deckSyncService.CompareDecksAsync(deckRequest, cancellationToken).ConfigureAwait(false);
            var sourceSystem = DeckSyncSupport.GetSourceSystem(deckRequest.Direction);
            var targetSystem = DeckSyncSupport.GetTargetSystem(deckRequest.Direction);
            var diff = syncResult.Diff;
            var sourceEntries = DeckSyncSupport.GetSourceEntries(deckRequest.Direction, syncResult.LoadedDecks);
            var targetEntries = DeckSyncSupport.GetTargetEntries(deckRequest.Direction, syncResult.LoadedDecks);

            var response = new DeckSyncApiResponse(
                ReconciliationReporter.ToText(diff, sourceSystem, targetSystem),
                DeltaExporter.ToText(diff.ToAdd.ToList(), targetSystem),
                FullImportExporter.ToText(sourceEntries, targetEntries, deckRequest.Mode, targetSystem, diff.PrintingConflicts, deckRequest.CategorySyncMode),
                sourceSystem,
                targetSystem,
                new DeckSyncApiDiffSummary(
                    diff.ToAdd.Count,
                    diff.CountMismatch.Count,
                    diff.OnlyInArchidekt.Count,
                    diff.PrintingConflicts.Count),
                diff.PrintingConflicts.Select(conflict => new PrintingConflictDto(
                    conflict.CardName,
                    conflict.ArchidektVersion.SetCode ?? string.Empty,
                    conflict.ArchidektVersion.CollectorNumber ?? string.Empty,
                    conflict.ArchidektVersion.Category,
                    conflict.MoxfieldVersion.SetCode,
                    conflict.MoxfieldVersion.CollectorNumber,
                    conflict.Resolution.ToString())).ToList());

            return Ok(response);
        }
        catch (Exception exception) when (exception is DeckParseException or InvalidOperationException or HttpRequestException)
        {
            _logger.LogWarning(exception, "Deck sync API request failed.");
            return BadRequest(new { Message = exception.Message });
        }
    }
}
