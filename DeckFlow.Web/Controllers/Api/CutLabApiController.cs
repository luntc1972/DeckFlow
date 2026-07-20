using DeckFlow.Core.Manabase;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services.CutLab;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Api;

/// <summary>Exposes Cut Lab decision application through the JSON API.</summary>
[ApiController]
[Route("api/cut-lab")]
public sealed class CutLabApiController : ControllerBase
{
    private const string InvalidStateMessage = "Cut Lab state is invalid. Re-import the pool and try again.";

    private readonly ICutLabAnalysisContextBuilder _contextBuilder;
    private readonly ICutLabSimulationService _simulationService;
    private readonly ILogger<CutLabApiController> _logger;

    /// <summary>Creates the Cut Lab API controller.</summary>
    /// <param name="contextBuilder">Shared analysis-context builder reused by intake and decision flows.</param>
    /// <param name="simulationService">Simulation service used for proposal deltas.</param>
    /// <param name="logger">Logger used for non-fatal API warnings.</param>
    public CutLabApiController(
        ICutLabAnalysisContextBuilder contextBuilder,
        ICutLabSimulationService simulationService,
        ILogger<CutLabApiController> logger)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Applies one Cut Lab decision and returns the next proposal payload.</summary>
    /// <param name="request">Decision request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated state plus the next proposal surface.</returns>
    [HttpPost("decide")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [ProducesResponseType(typeof(CutLabDecideApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CutLabDecideApiResponse>> PostDecideAsync([FromBody] CutLabDecideApiRequest request, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
        }

        if (request is null)
        {
            return BadRequest(new { Message = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(request.CardName))
        {
            return BadRequest(new { Message = "Cut Lab state and card name are required." });
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            if (state.Pool.Count == 0)
            {
                return BadRequest(new { Message = InvalidStateMessage });
            }

            IReadOnlyList<string> commanderNames = GetCommanderNames(state);
            IReadOnlyDictionary<string, int> floorByRole = BuildFloorMap(state.RoleFloors);
            IReadOnlyList<CutLabPoolCard> fullPool = state.Pool;

            IReadOnlyList<CutLabPoolCard> beforeWorkingList = CutLabWorkingList.Derive(state.Pool, state.Decisions);
            string beforePoolKey = CutLabResolvedCardCache.ComputePoolKey(beforeWorkingList);
            CutLabAnalysisContext beforeContext = await _contextBuilder.BuildAsync(
                beforeWorkingList,
                state.Intent.PlayExperience,
                commanderNames,
                poolKey: beforePoolKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            (_, CutLabRoundPlan beforeRoundPlan) = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
                beforeWorkingList,
                beforeContext,
                floorByRole,
                state.Decisions);

            string roundKey = DetermineRoundKey(state, request, beforeRoundPlan);
            IReadOnlyList<CutLabDecideFloorWarningDto> floorWarnings = request.Decision == CutLabDecideAction.Accept
                ? BuildFloorWarnings(beforeWorkingList, beforeContext, floorByRole, request.CardName)
                : [];
            state = CutLabDecisionApplier.Apply(state, request.CardName, request.Decision, roundKey);

            IReadOnlyList<CutLabPoolCard> afterWorkingList = CutLabWorkingList.Derive(state.Pool, state.Decisions);
            string afterPoolKey = CutLabResolvedCardCache.ComputePoolKey(afterWorkingList);
            IReadOnlyList<ScryfallCardData>? afterPreResolvedCards = TryBuildAfterPreResolvedCards(
                fullPool,
                afterWorkingList,
                beforeContext.ResolvedCards);
            CutLabAnalysisContext afterContext = await _contextBuilder.BuildAsync(
                afterWorkingList,
                state.Intent.PlayExperience,
                commanderNames,
                afterPreResolvedCards,
                afterPoolKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            (CutLabStructuralFindingsResult afterFindings, CutLabRoundPlan roundPlan) = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
                afterWorkingList,
                afterContext,
                floorByRole,
                state.Decisions);

            CutLabProposalDeltas? proposalDeltas = null;
            if (roundPlan.NextProposal is not null)
            {
                proposalDeltas = await _simulationService.ComputeProposalDeltas(
                    afterWorkingList,
                    roundPlan.NextProposal.CardName,
                    state.Intent.PlayExperience,
                    poolKey: afterPoolKey,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            string serializedState = CutLabStateSerializer.Serialize(state);
            CutLabDecideApiResponse response = new()
            {
                CutLabStateJson = serializedState,
                NextProposal = BuildNextProposal(roundPlan, afterFindings),
                ProposalDeltas = proposalDeltas is null ? null : BuildProposalDeltas(proposalDeltas),
                FloorWarnings = floorWarnings,
                CardsRemaining = roundPlan.CardsRemainingToTarget,
                CutsMade = BuildCutsMade(state.Decisions),
            };

            return Ok(response);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(exception, "Cut Lab decide API request failed.");
            return BadRequest(new { Message = CutLabMessages.NoChangeMessage });
        }
    }

    private IReadOnlyList<ScryfallCardData>? TryBuildAfterPreResolvedCards(
        IReadOnlyList<CutLabPoolCard> fullPool,
        IReadOnlyList<CutLabPoolCard> afterWorkingList,
        IReadOnlyList<ScryfallCardData> beforeResolvedCards)
    {
        if (_contextBuilder.TrySeedDerivedPool(afterWorkingList, beforeResolvedCards, out IReadOnlyList<ScryfallCardData>? seededCards)
            && seededCards is not null)
        {
            return seededCards;
        }

        if (_contextBuilder.TryGetCachedResolvedCards(fullPool, out IReadOnlyList<ScryfallCardData>? fullPoolCards)
            && fullPoolCards is not null
            && _contextBuilder.TrySeedDerivedPool(afterWorkingList, fullPoolCards, out seededCards)
            && seededCards is not null)
        {
            return seededCards;
        }

        return BuildPartialResolvedSubset(afterWorkingList, fullPoolCards ?? beforeResolvedCards);
    }

    private static IReadOnlyList<string> GetCommanderNames(CutLabState state)
        => string.IsNullOrWhiteSpace(state.Commander) ? [] : [state.Commander];

    private static IReadOnlyDictionary<string, int> BuildFloorMap(IReadOnlyList<CutLabRoleFloor> roleFloors)
    {
        Dictionary<string, int> floors = new(StringComparer.OrdinalIgnoreCase);
        foreach (CutLabRoleFloor floor in roleFloors)
        {
            if (!string.IsNullOrWhiteSpace(floor.Role))
            {
                floors[floor.Role] = floor.Floor;
            }
        }

        return floors;
    }

    private static string DetermineRoundKey(CutLabState state, CutLabDecideApiRequest request, CutLabRoundPlan roundPlan)
    {
        if (request.Decision == CutLabDecideAction.Restore)
        {
            return CutLabDecisionApplier.LatestRoundForCard(state, request.CardName);
        }

        if (roundPlan.NextProposal is not null
            && string.Equals(roundPlan.NextProposal.CardName, request.CardName, StringComparison.OrdinalIgnoreCase))
        {
            return roundPlan.NextProposal.RoundKey;
        }

        return roundPlan.Queue
            .FirstOrDefault(item => string.Equals(item.CardName, request.CardName, StringComparison.OrdinalIgnoreCase))
            ?.RoundKey
            ?? CutLabDecisionApplier.LatestRoundForCard(state, request.CardName);
    }

    private static IReadOnlyList<CutLabDecideFloorWarningDto> BuildFloorWarnings(
        IReadOnlyList<CutLabPoolCard> workingList,
        CutLabAnalysisContext context,
        IReadOnlyDictionary<string, int> floorByRole,
        string cardName)
    {
        if (!context.RolesByCardName.TryGetValue(cardName, out IReadOnlyList<string>? roles))
        {
            return [];
        }

        int quantity = workingList.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 1;
        return CutLabFloorRules.Evaluate(context.RoleCounts, floorByRole, roles, cardName, quantity)
            .Select(warning => new CutLabDecideFloorWarningDto
            {
                Role = warning.Role,
                NewCount = warning.NewCount,
                Floor = warning.Floor,
                Message = warning.Message,
            })
            .ToArray();
    }

    private static IReadOnlyList<ScryfallCardData>? BuildPartialResolvedSubset(
        IReadOnlyList<CutLabPoolCard> targetPool,
        IReadOnlyList<ScryfallCardData> sourceCards)
    {
        IReadOnlyDictionary<string, ScryfallCardData> sourceByName = CutLabCardNames.ToLastWinsDictionary(
            sourceCards,
            card => card.Name,
            card => card);
        return targetPool
            .Select(card => sourceByName.TryGetValue(CutLabCardNames.Normalize(card.Name), out ScryfallCardData? resolvedCard) ? resolvedCard : null)
            .Where(card => card is not null)
            .Cast<ScryfallCardData>()
            .DistinctBy(card => CutLabCardNames.Normalize(card.Name))
            .ToArray();
    }

    private static CutLabDecideNextProposalDto BuildNextProposal(CutLabRoundPlan roundPlan, CutLabStructuralFindingsResult findings)
    {
        if (roundPlan.NextProposal is null)
        {
            return new CutLabDecideNextProposalDto
            {
                IsTerminal = true,
                IsAtTarget = roundPlan.CardsRemainingToTarget == 0,
                IsNothingToCut = roundPlan.CardsRemainingToTarget > 0,
            };
        }

        string[] chips = findings.Findings
            .Where(finding => finding.Evidence.Any(evidence => string.Equals(evidence.CardName, roundPlan.NextProposal.CardName, StringComparison.OrdinalIgnoreCase)))
            .Select(finding => finding.Heading)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CutLabDecideNextProposalDto
        {
            CardName = roundPlan.NextProposal.CardName,
            RoundKey = roundPlan.NextProposal.RoundKey,
            RoundLabel = roundPlan.NextProposal.RoundLabel,
            RoundBannerBody = CutLabCutRoundEngine.RoundBannerBodyFor(roundPlan.NextProposal.RoundKey),
            FindingCount = roundPlan.NextProposal.FindingCount,
            FindingChips = chips,
        };
    }

    private static CutLabDecideProposalDeltasDto BuildProposalDeltas(CutLabProposalDeltas proposalDeltas)
        => new()
        {
            CardName = proposalDeltas.CardName,
            ChangedFamilyCount = proposalDeltas.ChangedFamilyCount,
            Deltas = proposalDeltas.Deltas
                .Select(delta => new CutLabDecideMetricDeltaDto
                {
                    Kind = delta.Kind,
                    Label = delta.Label,
                    Before = delta.Before,
                    After = delta.After,
                    Delta = delta.Delta,
                    Unit = delta.Unit,
                    Direction = delta.Direction,
                    IsMeaningful = delta.IsMeaningful,
                })
                .ToArray(),
        };

    private static IReadOnlyList<CutLabDecideCutRecordDto> BuildCutsMade(IReadOnlyList<CutLabDecision> decisions)
        => decisions
            .Where(decision => decision.Kind == CutLabDecisionKind.Accepted)
            .OrderByDescending(decision => decision.Ordinal)
            .Select(decision => new CutLabDecideCutRecordDto
            {
                CardName = decision.CardName,
                RoundKey = decision.Round,
                RoundLabel = CutLabCutRoundEngine.LabelFor(decision.Round),
                Ordinal = decision.Ordinal,
            })
            .ToArray();
}
