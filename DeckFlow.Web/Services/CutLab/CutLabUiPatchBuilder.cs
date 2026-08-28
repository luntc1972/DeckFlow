using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds a server-authored Cut Lab live UI patch from authoritative session state.</summary>
public interface ICutLabUiPatchBuilder
{
    /// <summary>Projects the provided Cut Lab session state into the live UI patch contract.</summary>
    /// <param name="state">Current authoritative Cut Lab state.</param>
    /// <param name="playExperience">Resolved play-experience label for the current state.</param>
    /// <param name="commanderNames">Resolved commander names for the current state.</param>
    /// <param name="twinsEnabled">Functional-twins flag value captured for this request.</param>
    /// <param name="preResolvedCards">Optional resolved card payloads that can seed analysis.</param>
    /// <param name="poolKey">Optional precomputed pool key for the derived working list.</param>
    /// <param name="floorWarnings">Optional current-proposal floor warnings that should be preserved as-is.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server-authored live UI patch for the provided state.</returns>
    Task<CutLabUiPatchDto> BuildAsync(
        CutLabState state,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        bool twinsEnabled,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        IReadOnlyList<CutLabDecideFloorWarningDto>? floorWarnings = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Default Cut Lab live-patch projection service.</summary>
public sealed class CutLabUiPatchBuilder : ICutLabUiPatchBuilder
{
    private readonly ICutLabAnalysisContextBuilder _analysisContextBuilder;
    private readonly ICutLabSimulationService _simulationService;
    private readonly ICutLabFloorResolver _floorResolver;

    /// <summary>Creates the Cut Lab live-patch builder.</summary>
    /// <param name="analysisContextBuilder">Shared Cut Lab analysis-context builder.</param>
    /// <param name="simulationService">Shared Cut Lab simulation service.</param>
    /// <param name="floorResolver">Shared floor resolver reused across Cut Lab transports.</param>
    public CutLabUiPatchBuilder(
        ICutLabAnalysisContextBuilder analysisContextBuilder,
        ICutLabSimulationService simulationService,
        ICutLabFloorResolver floorResolver)
    {
        _analysisContextBuilder = analysisContextBuilder ?? throw new ArgumentNullException(nameof(analysisContextBuilder));
        _simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
        _floorResolver = floorResolver ?? throw new ArgumentNullException(nameof(floorResolver));
    }

    /// <inheritdoc />
    public async Task<CutLabUiPatchDto> BuildAsync(
        CutLabState state,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        bool twinsEnabled,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        IReadOnlyList<CutLabDecideFloorWarningDto>? floorWarnings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(commanderNames);

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
        IReadOnlyDictionary<string, int> floorByRole = _floorResolver.Resolve(state, context.CommanderManaValue, commanderNames)
            .ToDictionary(
                floor => floor.Role,
                floor => floor.Floor,
                StringComparer.OrdinalIgnoreCase);
        (CutLabStructuralFindingsResult findings, CutLabRoundPlan roundPlan) = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
            workingList,
            context,
            floorByRole,
            state.Decisions,
            twinsEnabled);
        CutLabSimulationResult snapshotResult = await _simulationService.BuildSnapshotResult(
            workingList,
            playExperience,
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: resolvedPoolKey,
            goals: state.Goals,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        state = state with
        {
            Pool = CutLabSimulationResult.ApplySimulationCardData(state.Pool, snapshotResult.CastabilityByCardName),
        };

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
            ActualLands = snapshotResult.ActualLands,
            TargetLands = snapshotResult.TargetLands,
            CanBuildExport = projection.CanBuildExport,
            CardTextByCardName = BuildPopupCardTextPatch(state.Pool),
            NextProposal = BuildNextProposal(roundPlan, findings),
            ProposalDeltas = proposalDeltas,
            FloorWarnings = floorWarnings ?? BuildFloorWarningsForNextProposal(workingList, context, floorByRole, roundPlan),
            CutsMade = BuildCutsMade(state.Decisions),
            StructuralFindings = BuildStructuralFindings(findings),
            LockedOvershootAdvisory = BuildLockedOvershootAdvisory(roundPlan.LockedOvershootAdvisory),
            ComboBadgeByCardName = BuildComboBadgeByCardName(state.Pool, context.Classification.CardComboMembership),
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
            CardTextByCardName = BuildPopupCardTextPatch(state.Pool),
            NextProposal = projection.CanBuildExport
                ? new CutLabDecideNextProposalDto
                {
                    IsTerminal = true,
                    IsAtTarget = true,
                }
                : null,
            ProposalDeltas = null,
            FloorWarnings = [],
            CutsMade = BuildCutsMade(state.Decisions),
            StructuralFindings = [],
            LockedOvershootAdvisory = null,
            ComboBadgeByCardName = new Dictionary<string, CutLabDecideComboBadgeDto>(StringComparer.Ordinal),
            ComboDataAvailable = false,
            CategoryDataAvailable = false,
            WhatifCardOutOptions = projection.WhatifCardOutOptions,
            WhatifCardInOptions = projection.WhatifCardInOptions,
            QuantityTuners = BuildQuantityTuners(projection.WorkingList, projection.OriginalPoolNames, roleAssignmentsByCardName),
            AddableBasics = projection.AddableBasics,
        };
    }

    private static IReadOnlyDictionary<string, CutLabCardTextView> BuildPopupCardTextPatch(IReadOnlyList<CutLabPoolCard> pool)
    {
        Dictionary<string, CutLabCardTextView> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (CutLabPoolCard card in pool)
        {
            if (card.LastKnownCmc is null && card.LastKnownCastPercent is null)
            {
                continue;
            }

            result[card.Name] = new CutLabCardTextView
            {
                Cmc = card.LastKnownCmc,
                CastPercent = card.LastKnownCastPercent,
            };
        }

        return result;
    }

    private static CutLabLockedOvershootAdvisoryDto? BuildLockedOvershootAdvisory(CutLabLockedOvershootAdvisory? advisory)
    {
        if (advisory is null)
        {
            return null;
        }

        IReadOnlyList<CutLabLockedOvershootGroupProjection> groups = CutLabRoleAssigner.BuildLockedOvershootGroups(advisory.Groups);

        return new CutLabLockedOvershootAdvisoryDto
        {
            CardsOverTarget = advisory.CardsOverTarget,
            HiddenCount = advisory.HiddenCount,
            Groups = groups
                .Select(group => new CutLabLockedOvershootGroupDto
                {
                    RoleLabel = group.RoleLabel,
                    CardNames = group.CardNames,
                })
                .ToArray(),
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
        => CutLabNextProposalBuilder.Build(roundPlan, findings);

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
                        Roles = item.Roles,
                    })
                    .ToArray(),
            })
            .ToArray();

    // Why: D-24. The JavaScript consumer looks badges up by the raw rendered pool name and property
    // lookup there is case-sensitive, so the normalized membership map is re-keyed once here, onto
    // every raw pool spelling, under StringComparer.Ordinal. Several raw names — a DFC's long and
    // short forms, or two spellings differing only by case — can share one normalized identity, and
    // each of them gets its own entry.
    private static IReadOnlyDictionary<string, CutLabDecideComboBadgeDto> BuildComboBadgeByCardName(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyDictionary<string, CutLabCardComboMembership> cardComboMembership)
    {
        Dictionary<string, CutLabDecideComboBadgeDto> comboBadgeByCardName = new(StringComparer.Ordinal);

        foreach (CutLabPoolCard card in pool)
        {
            if (comboBadgeByCardName.ContainsKey(card.Name)
                || !cardComboMembership.TryGetValue(CutLabCardNames.Normalize(card.Name), out CutLabCardComboMembership? membership))
            {
                continue;
            }

            CutLabDecideComboBadgeDto? badge = BuildComboBadge(membership);
            if (badge is not null)
            {
                comboBadgeByCardName[card.Name] = badge;
            }
        }

        return comboBadgeByCardName;
    }

    private static CutLabDecideComboBadgeDto? BuildComboBadge(CutLabCardComboMembership membership)
    {
        if (membership.CompleteCombos.Count > 0)
        {
            return new CutLabDecideComboBadgeDto
            {
                BadgeState = ComboBadgeState.CompletePiece,
                Context = JoinCardNames(
                    membership.CompleteCombos
                        .SelectMany(combo => combo.Results)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(result => result, StringComparer.OrdinalIgnoreCase)
                        .ToArray()),
            };
        }

        if (membership.NearCombos.Count > 0)
        {
            return new CutLabDecideComboBadgeDto
            {
                BadgeState = ComboBadgeState.NeedsPartner,
                Context = $"Needs {JoinCardNames(membership.NearCombos.Select(combo => combo.MissingCard).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(cardName => cardName, StringComparer.OrdinalIgnoreCase).ToArray())}",
            };
        }

        return null;
    }

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
                bool isLockedOrCommander = card.IsLocked || card.IsCommander;
                bool isAddedBasic = CutLabBasicLands.Contains(card.Name)
                    && !originalPoolNames.Contains(CutLabCardNames.Normalize(card.Name));

                return new CutLabQuantityTunerRowDto
                {
                    CardName = card.Name,
                    CurrentQuantity = card.Quantity,
                    LegalMax = legalMax,
                    RemoveDisabled = card.Quantity == 0 || isLockedOrCommander,
                    AddDisabled = card.Quantity >= legalMax || isLockedOrCommander,
                    IsLockedOrCommander = isLockedOrCommander,
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

    private static string JoinCardNames(IReadOnlyList<string> cardNames)
        => cardNames.Count switch
        {
            0 => string.Empty,
            1 => cardNames[0],
            2 => $"{cardNames[0]} and {cardNames[1]}",
            _ => $"{string.Join(", ", cardNames.Take(cardNames.Count - 1))} and {cardNames[^1]}",
        };

    private static string DisplayLabelFor(string roleKey)
        => CutLabRoleAssigner.DisplayLabelFor(roleKey);

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
