using System.Net;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Api;

/// <summary>
/// Development-only JSON endpoint that builds the deck-analysis prompt headlessly, mirroring the
/// <c>/deck-analysis</c> page pipeline. Exists to support prompt A/B testing and automated quality
/// checks without driving the Razor UI. Returns 404 outside the Development environment so it never
/// widens the production surface.
/// </summary>
[ApiController]
[Route("api/analysis-prompt")]
public sealed class AnalysisPromptApiController : ControllerBase
{
    // Why: the analysis prompt is only built at WorkflowStep 2 (DeckAnalysisPacketService).
    private const int AnalysisPacketWorkflowStep = 2;

    // Default question set when the caller supplies none, so the endpoint produces a
    // representative full prompt out of the box. Ids come from AnalysisQuestionCatalog.
    private static readonly string[] DefaultAnalysisQuestions =
    [
        "bracket-assessment",
        "strengths-weaknesses",
        "primary-win-condition",
        "interaction-count",
        "faster-competitive",
        "resilience-to-wipes",
    ];

    private readonly IDeckAnalysisPacketService _deckAnalysisPacketService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AnalysisPromptApiController> _logger;

    /// <summary>
    /// Creates the development-only analysis-prompt API controller.
    /// </summary>
    /// <param name="deckAnalysisPacketService">Service that loads the deck and builds the analysis prompt.</param>
    /// <param name="environment">Host environment used to gate the endpoint to Development.</param>
    /// <param name="logger">Logger for validation/upstream warnings.</param>
    public AnalysisPromptApiController(
        IDeckAnalysisPacketService deckAnalysisPacketService,
        IWebHostEnvironment environment,
        ILogger<AnalysisPromptApiController> logger)
    {
        ArgumentNullException.ThrowIfNull(deckAnalysisPacketService);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _deckAnalysisPacketService = deckAnalysisPacketService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Builds the deck-analysis prompt for the supplied deck and returns it as JSON.
    /// </summary>
    /// <param name="request">Headless analysis-prompt request.</param>
    /// <param name="cancellationToken">Cancellation token for the build.</param>
    [HttpPost]
    [ProducesResponseType(typeof(AnalysisPromptApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisPromptApiResponse>> PostAsync(
        [FromBody] AnalysisPromptApiRequest request,
        CancellationToken cancellationToken)
    {
        // Why: dev-only harness. Never expose prompt-building as an unauthenticated prod route.
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        // Same-origin guard (mirrors the JSON API controllers): allows header-less server tooling
        // (curl/CLI) but blocks cross-origin browser pages from triggering deck imports + upstream work.
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
        }

        if (request is null)
        {
            return BadRequest(new { Message = "Request body is required." });
        }

        var hasUrl = !string.IsNullOrWhiteSpace(request.DeckUrl);
        var hasText = !string.IsNullOrWhiteSpace(request.DeckText);
        if (!hasUrl && !hasText)
        {
            return BadRequest(new { Message = "Provide deckUrl or deckText." });
        }

        var questions = request.SelectedAnalysisQuestions is { Count: > 0 } supplied
            ? supplied.ToList()
            : DefaultAnalysisQuestions.ToList();

        var deckRequest = new DeckAnalysisRequest
        {
            WorkflowStep = AnalysisPacketWorkflowStep,
            DeckInputSource = hasUrl ? DeckInputSource.PublicUrl : DeckInputSource.PasteText,
            DeckUrl = request.DeckUrl ?? string.Empty,
            DeckText = request.DeckText ?? string.Empty,
            Format = string.IsNullOrWhiteSpace(request.Format) ? "Commander" : request.Format,
            DeckName = request.DeckName ?? string.Empty,
            TargetCommanderBracket = request.TargetCommanderBracket ?? string.Empty,
            TargetAiPlatform = string.IsNullOrWhiteSpace(request.TargetAiPlatform) ? "ChatGPT" : request.TargetAiPlatform,
            SelectedAnalysisQuestions = questions,
            IncludeCandidateReferencesInAnalysis = request.IncludeCandidateReferencesInAnalysis,
        };

        try
        {
            var result = await _deckAnalysisPacketService.BuildAsync(deckRequest, cancellationToken).ConfigureAwait(false);
            var promptText = result.AnalysisPromptText ?? string.Empty;
            return Ok(new AnalysisPromptApiResponse(
                result.SuggestedChatTitle ?? string.Empty,
                promptText,
                result.ReferenceText ?? string.Empty,
                result.DeckProfileSchemaJson ?? string.Empty,
                result.SetUpgradePromptText ?? string.Empty,
                result.InputSummary ?? string.Empty,
                result.ImportWarning,
                promptText.Length));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Analysis-prompt API request failed validation.");
            return BadRequest(new { Message = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Analysis-prompt API request hit an upstream dependency.");
            return BadRequest(new { Message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception) });
        }
    }
}
