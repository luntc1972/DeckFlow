using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds a server-authored Cut Lab live UI patch from authoritative session state.</summary>
public interface ICutLabUiPatchBuilder
{
    /// <summary>Projects the provided Cut Lab session state into the live UI patch contract.</summary>
    /// <param name="state">Current authoritative Cut Lab state.</param>
    /// <param name="playExperience">Resolved play-experience label for the current state.</param>
    /// <param name="commanderNames">Resolved commander names for the current state.</param>
    /// <param name="floorByRole">Resolved floor map keyed by stable role key.</param>
    /// <param name="preResolvedCards">Optional resolved card payloads that can seed analysis.</param>
    /// <param name="poolKey">Optional precomputed pool key for the derived working list.</param>
    /// <param name="floorWarnings">Optional current-proposal floor warnings that should be preserved as-is.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server-authored live UI patch for the provided state.</returns>
    Task<CutLabUiPatchDto> BuildAsync(
        CutLabState state,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        IReadOnlyDictionary<string, int> floorByRole,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        IReadOnlyList<CutLabDecideFloorWarningDto>? floorWarnings = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Default Cut Lab live-patch projection service.</summary>
public sealed class CutLabUiPatchBuilder : ICutLabUiPatchBuilder
{
    private static readonly IReadOnlyDictionary<string, string> RoleDisplayLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lands"] = "Lands",
            ["ramp"] = "Ramp",
            ["draw"] = "Card draw",
            ["interaction"] = "Interaction",
            ["protection"] = "Protection",
            ["engines"] = "Engines",
            ["payoffs"] = "Payoffs",
            ["wincons"] = "Win conditions",
        };

    private readonly ICutLabAnalysisContextBuilder _analysisContextBuilder;
    private readonly ICutLabSimulationService _simulationService;

    /// <summary>Creates the Cut Lab live-patch builder.</summary>
    /// <param name="analysisContextBuilder">Shared Cut Lab analysis-context builder.</param>
    /// <param name="simulationService">Shared Cut Lab simulation service.</param>
    public CutLabUiPatchBuilder(
        ICutLabAnalysisContextBuilder analysisContextBuilder,
        ICutLabSimulationService simulationService)
    {
        _analysisContextBuilder = analysisContextBuilder ?? throw new ArgumentNullException(nameof(analysisContextBuilder));
        _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
    }

    /// <inheritdoc />
    public async Task<CutLabUiPatchDto> BuildAsync(
        CutLabState state,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        IReadOnlyDictionary<string, int> floorByRole,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        IReadOnlyList<CutLabDecideFloorWarningDto>? floorWarnings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(commanderNames);
        ArgumentNullException.ThrowIfNull(floorByRole);

        WorkingListProjection projection = BuildWorkingListProjection(state);
        IReadOnlyList<CutLabPoolCard> workingList = projection.WorkingList;
        string resolvedPoolKey = poolKey ?? CutLabResolvedCardCache.ComputePoolKey(workingList);
        CutLabAnalysisContext context = await _analysisContextBuilder.BuildAsync(
            workingList,
            playExperience,
            commanderNames,
            preResolvedCards,
            resolvedPoolKey,
            cancellationToken).ConfigureAwait(false);
        (CutLabStructuralFindingsResult findings, CutLabRoundPlan roundPlan) = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
            workingList,
            context,
            floorByRole,
            state.Decisions);

        CutLabDecideProposalDeltasDto? proposalDeltas = null;
        if (roundPlan.NextProposal is not null)
        {
            CutLabProposalDeltas deltas = await _simulationService.ComputeProposalDeltas(
                workingList,
                roundPlan.NextProposal.CardName,
                playExperience,
                trialsOverride: ICutLabSimulationService.InLoopTrials,
                poolKey: resolvedPoolKey,
                goals: state.Goals,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            proposalDeltas = BuildProposalDeltas(deltas);
        }

        return new CutLabUiPatchDto
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
            CurrentCount = projection.CurrentCount,
            CardsRemaining = projection.CardsRemaining,
            CanBuildExport = projection.CanBuildExport,
            NextProposal = BuildNextProposal(roundPlan, findings),
            ProposalDeltas = proposalDeltas,
            FloorWarnings = floorWarnings ?? BuildFloorWarningsForNextProposal(workingList, context, floorByRole, roundPlan),
            CutsMade = BuildCutsMade(state.Decisions),
            StructuralFindings = BuildStructuralFindings(findings),
            ComboDataAvailable = findings.ComboDataAvailable,
            CategoryDataAvailable = findings.CategoryDataAvailable,
            WhatifCardOutOptions = projection.WhatifCardOutOptions,
            WhatifCardInOptions = projection.WhatifCardInOptions,
            QuantityTuners = BuildQuantityTuners(projection.WorkingList, projection.OriginalPoolNames, context.RolesByCardName),
            AddableBasics = projection.AddableBasics,
        };
    }

    /// <summary>Builds a light UI patch for quantity nudges without re-running analysis or simulation.</summary>
    /// <param name="state">Current authoritative Cut Lab state.</param>
    /// <param name="commanderNames">Resolved commander names for the current state.</param>
    /// <returns>The server-authored live UI patch for the provided adjustment.</returns>
    public CutLabUiPatchDto BuildAdjustPatch(CutLabState state, IReadOnlyList<string> commanderNames)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(commanderNames);

        WorkingListProjection projection = BuildWorkingListProjection(state);
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName = BuildAdjustRoleAssignments(projection.WorkingList);

        return new CutLabUiPatchDto
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
            CurrentCount = projection.CurrentCount,
            CardsRemaining = projection.CardsRemaining,
            CanBuildExport = projection.CanBuildExport,
            NextProposal = null!,
            ProposalDeltas = null,
            FloorWarnings = [],
            CutsMade = BuildCutsMade(state.Decisions),
            StructuralFindings = [],
            ComboDataAvailable = false,
            CategoryDataAvailable = false,
            WhatifCardOutOptions = projection.WhatifCardOutOptions,
            WhatifCardInOptions = projection.WhatifCardInOptions,
            QuantityTuners = BuildQuantityTuners(projection.WorkingList, projection.OriginalPoolNames, roleAssignmentsByCardName),
            AddableBasics = projection.AddableBasics,
        };
    }

    private static IReadOnlyList<CutLabDecideFloorWarningDto> BuildFloorWarningsForNextProposal(
        IReadOnlyList<CutLabPoolCard> workingList,
        CutLabAnalysisContext context,
        IReadOnlyDictionary<string, int> floorByRole,
        CutLabRoundPlan roundPlan)
    {
        if (roundPlan.NextProposal is null)
        {
            return [];
        }

        return BuildFloorWarnings(workingList, context, floorByRole, roundPlan.NextProposal.CardName);
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

    private static IReadOnlyList<CutLabDecideFindingGroupDto> BuildStructuralFindings(CutLabStructuralFindingsResult findings)
        => CutLabFindingPresenter.BuildFindingGroups(CutLabFindingPresenter.BuildFindings(findings.Findings))
            .Select(group => new CutLabDecideFindingGroupDto
            {
                Kind = group.Kind,
                Heading = group.Heading,
                Items = group.Items
                    .Select(item => new CutLabDecideFindingDto
                    {
                        Kind = item.Kind,
                        Heading = item.Heading,
                        Lead = item.Lead,
                        Evidence = item.Evidence,
                    })
                    .ToArray(),
            })
            .ToArray();

    private static WorkingListProjection BuildWorkingListProjection(CutLabState state)
    {
        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(state.Pool, state.Decisions, state.QuantityAdjustments);
        IReadOnlySet<string> originalPoolNames = state.Pool
            .Select(card => CutLabCardNames.Normalize(card.Name))
            .ToHashSet(CutLabCardNames.Comparer);
        IReadOnlyList<string> whatifCardOutOptions = BuildWhatifCardOutOptions(workingList);
        IReadOnlyList<string> whatifCardInOptions = BuildWhatifCardInOptions(state.Pool, state.Decisions);
        int currentCount = workingList.Sum(card => card.Quantity);
        IReadOnlyList<string> addableBasics = BuildAddableBasics(workingList);

        return new WorkingListProjection(
            workingList,
            originalPoolNames,
            currentCount,
            Math.Max(currentCount - 100, 0),
            currentCount == 100,
            whatifCardOutOptions,
            whatifCardInOptions,
            addableBasics);
    }

    private static IReadOnlyList<CutLabQuantityTunerRowDto> BuildQuantityTuners(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlySet<string> originalPoolNames,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
    {
        return workingList
            .Select(card =>
            {
                bool isLegalMultiple = CutLabLegality.IsLegalMultiple(card.Name);
                int legalMax = CutLabLegality.LegalMax(card.Name);
                bool isAddedBasic = CutLabBasicLands.Contains(card.Name)
                    && !originalPoolNames.Contains(CutLabCardNames.Normalize(card.Name));

                return new CutLabQuantityTunerRowDto
                {
                    CardName = card.Name,
                    CurrentQuantity = card.Quantity,
                    LegalMax = legalMax,
                    RemoveDisabled = card.Quantity == 0,
                    AddDisabled = card.Quantity >= legalMax,
                    IsLockedOrCommander = card.IsLocked || card.IsCommander,
                    IsVisible = true,
                    RoleLabel = RoleLabelFor(card.Name, roleAssignmentsByCardName),
                    IsLegalMultiple = isLegalMultiple,
                    IsAddedBasic = isAddedBasic,
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<string> BuildAddableBasics(IReadOnlyList<CutLabPoolCard> workingList)
    {
        HashSet<string> presentBasicNames = workingList
            .Select(card => CutLabCardNames.Normalize(card.Name))
            .ToHashSet(CutLabCardNames.Comparer);

        return CutLabBasicLands.Names
            .Where(name => !presentBasicNames.Contains(CutLabCardNames.Normalize(name)))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildAdjustRoleAssignments(IReadOnlyList<CutLabPoolCard> workingList)
    {
        Dictionary<string, IReadOnlyList<string>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (CutLabPoolCard card in workingList)
        {
            if (CutLabBasicLands.Contains(card.Name)
                || card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase))
            {
                result[card.Name] = ["lands"];
                continue;
            }

            result[card.Name] = [];
        }

        return result;
    }

    private static string RoleLabelFor(
        string cardName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
        => roleAssignmentsByCardName.TryGetValue(cardName, out IReadOnlyList<string>? roles)
            ? string.Join(" · ", roles.Select(DisplayLabelFor))
            : string.Empty;

    private static string DisplayLabelFor(string roleKey)
        => RoleDisplayLabels.TryGetValue(roleKey, out string? label) ? label : roleKey;

    private static IReadOnlyList<string> BuildWhatifCardOutOptions(IReadOnlyList<CutLabPoolCard> workingList)
        => workingList
            .Where(card => !card.IsLocked && !card.IsCommander)
            .Select(card => card.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> BuildWhatifCardInOptions(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyList<CutLabDecision> decisions)
    {
        IReadOnlySet<string> accepted = CutLabWorkingList.AcceptedCardNames(decisions);
        return pool
            .Where(card => accepted.Contains(card.Name))
            .Select(card => card.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record WorkingListProjection(
        IReadOnlyList<CutLabPoolCard> WorkingList,
        IReadOnlySet<string> OriginalPoolNames,
        int CurrentCount,
        int CardsRemaining,
        bool CanBuildExport,
        IReadOnlyList<string> WhatifCardOutOptions,
        IReadOnlyList<string> WhatifCardInOptions,
        IReadOnlyList<string> AddableBasics);
}
