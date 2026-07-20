using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Models;

/// <summary>View model for the Cut Lab page.</summary>
public sealed record CutLabViewModel
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

    /// <summary>Role-group views in the fixed structural analysis order.</summary>
    public IReadOnlyList<CutLabRoleGroupView> RoleGroups { get; init; } = [];

    /// <summary>Structural findings rendered for the current pool.</summary>
    public IReadOnlyList<CutLabFindingView> Findings { get; init; } = [];

    /// <summary>Structural findings grouped for display in the findings panel.</summary>
    public IReadOnlyList<CutLabFindingGroupView> FindingGroups { get; init; } = [];

    /// <summary>True when combo-backed findings are incomplete because combo lookup was unavailable.</summary>
    public bool ComboDataUnavailable { get; init; }

    /// <summary>True when category-backed findings are incomplete because category lookup was unavailable.</summary>
    public bool CategoryDataUnavailable { get; init; }

    /// <summary>Role floor rows rendered in the fixed Cut Lab order.</summary>
    public IReadOnlyList<CutLabFloorRowView> FloorRows { get; init; } = [];

    /// <summary>Sticky round/count bar values for the Cut rounds workspace.</summary>
    public CutLabStickyBarView StickyBar { get; init; } = new();

    /// <summary>Current one-at-a-time proposal state for the Cut rounds workspace.</summary>
    public CutLabProposalView Proposal { get; init; } = new();

    /// <summary>Accepted cuts rendered in restore-list order.</summary>
    public IReadOnlyList<CutLabCutMadeRowView> CutsMade { get; init; } = [];

    /// <summary>Baseline-versus-current comparison rows.</summary>
    public IReadOnlyList<CutLabCompareRowView> CompareRows { get; init; } = [];

    /// <summary>Total card count of the original imported pool.</summary>
    public int BaselineCount { get; init; }

    /// <summary>Total card count of the current derived working list.</summary>
    public int CurrentCount { get; init; }

    /// <summary>Per-card display labels for the pool table, keyed by card name.</summary>
    public IReadOnlyDictionary<string, string> RoleListByCardName { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-card raw role-key token strings for the pool table, keyed by card name.</summary>
    public IReadOnlyDictionary<string, string> RoleKeysByCardName { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Builds the page model from the request and service result.</summary>
    /// <param name="request">Current request values.</param>
    /// <param name="result">Processed Cut Lab result.</param>
    public static CutLabViewModel From(CutLabRequest request, CutLabProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        IReadOnlyList<CutLabPoolCard> pool = result.State?.Pool ?? [];
        IReadOnlyList<CutLabRoleGroupView> roleGroups = BuildRoleGroups(pool, result.RoleAssignmentsByCardName);
        IReadOnlyList<CutLabFindingView> findings = result.Findings.Findings
            .Select(finding => new CutLabFindingView
            {
                Kind = finding.Kind,
                Heading = finding.Heading,
                Lead = finding.Lead,
                Evidence = finding.Evidence
                    .Select(evidence => evidence.ManaValue is double manaValue
                        ? $"{evidence.CardName} · MV {manaValue:0.##}"
                        : evidence.CardName)
                    .ToArray(),
            })
            .ToArray();
        IReadOnlyList<CutLabFindingGroupView> findingGroups = BuildFindingGroups(findings);
        IReadOnlyList<CutLabFloorRowView> floorRows = BuildFloorRows(pool, result.ResolvedFloors, result.RoleAssignmentsByCardName, request.PlayExperience);
        IReadOnlyDictionary<string, string> roleListByCardName = BuildRoleListByCardName(pool, result.RoleAssignmentsByCardName);
        IReadOnlyDictionary<string, string> roleKeysByCardName = BuildRoleKeysByCardName(pool, result.RoleAssignmentsByCardName);
        IReadOnlyDictionary<CutLabFindingKind, string> findingHeadingsByKind = BuildFindingHeadingsByKind(result.Findings.Findings);
        IReadOnlyList<CutLabCutMadeRowView> cutsMade = BuildCutsMade(result.State?.Decisions);
        int baselineCount = pool.Sum(card => card.Quantity);
        int currentCount = CutLabWorkingList.Derive(pool, result.State?.Decisions ?? []).Sum(card => card.Quantity);
        CutLabStickyBarView stickyBar = BuildStickyBar(result.RoundPlan, result.State?.Decisions);
        CutLabProposalView proposal = BuildProposal(
            result.RoundPlan,
            result.InitialProposalDeltas,
            result.State,
            result.ResolvedFloors,
            result.RoleAssignmentsByCardName,
            findingHeadingsByKind);
        IReadOnlyList<CutLabCompareRowView> compareRows = BuildCompareRows(result.State?.BaselineSnapshot, result.CurrentSnapshot);

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
            RoleGroups = roleGroups,
            Findings = findings,
            FindingGroups = findingGroups,
            ComboDataUnavailable = result.HasResult && !result.Findings.ComboDataAvailable,
            CategoryDataUnavailable = result.HasResult && !result.Findings.CategoryDataAvailable,
            FloorRows = floorRows,
            StickyBar = stickyBar,
            Proposal = proposal,
            CutsMade = cutsMade,
            CompareRows = compareRows,
            BaselineCount = baselineCount,
            CurrentCount = currentCount,
            RoleListByCardName = roleListByCardName,
            RoleKeysByCardName = roleKeysByCardName,
        };
    }

    private static IReadOnlyList<CutLabRoleGroupView> BuildRoleGroups(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
    {
        return CutLabFloorRules.RoleKeys
            .Select(roleKey =>
            {
                IReadOnlyList<CutLabRoleMemberView> members = pool
                    .Where(card => roleAssignmentsByCardName.TryGetValue(card.Name, out IReadOnlyList<string>? roles)
                        && roles.Contains(roleKey, StringComparer.Ordinal))
                    .Select(card => new CutLabRoleMemberView
                    {
                        Name = card.Name,
                        IsLocked = card.IsLocked,
                        IsCommander = card.IsCommander,
                    })
                    .ToArray();

                return new CutLabRoleGroupView
                {
                    RoleKey = roleKey,
                    DisplayLabel = DisplayLabelFor(roleKey),
                    Members = members,
                    LockedCount = pool
                        .Where(card => card.IsLocked
                            && roleAssignmentsByCardName.TryGetValue(card.Name, out IReadOnlyList<string>? roles)
                            && roles.Contains(roleKey, StringComparer.Ordinal))
                        .Sum(card => card.Quantity),
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<CutLabFloorRowView> BuildFloorRows(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName,
        string playExperience)
    {
        Dictionary<string, int> countsByRole = CountRoles(pool, roleAssignmentsByCardName);
        return resolvedFloors
            .Select(floor => new CutLabFloorRowView
            {
                RoleKey = floor.Role,
                DisplayLabel = DisplayLabelFor(floor.Role),
                InPoolCount = countsByRole.TryGetValue(floor.Role, out int count) ? count : 0,
                Floor = floor.Floor,
                DefaultValue = floor.DefaultValue,
                IsUserSet = floor.IsUserSet,
                AtFloor = (countsByRole.TryGetValue(floor.Role, out count) ? count : 0) <= floor.Floor + 1,
                SourceLabel = floor.BracketWasFallback
                    ? $"Default: {floor.DefaultValue} — based on {FallbackSource(playExperience)}"
                    : $"Default for B{floor.ResolvedBracket}: {floor.DefaultValue}",
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> BuildRoleListByCardName(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (CutLabPoolCard card in pool)
        {
            result[card.Name] = roleAssignmentsByCardName.TryGetValue(card.Name, out IReadOnlyList<string>? roles)
                ? string.Join(" · ", roles.Select(DisplayLabelFor))
                : string.Empty;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> BuildRoleKeysByCardName(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (CutLabPoolCard card in pool)
        {
            result[card.Name] = roleAssignmentsByCardName.TryGetValue(card.Name, out IReadOnlyList<string>? roles)
                ? string.Join(" ", roles)
                : string.Empty;
        }

        return result;
    }

    private static IReadOnlyList<CutLabFindingGroupView> BuildFindingGroups(IReadOnlyList<CutLabFindingView> findings)
    {
        List<CutLabFindingGroupView> groups = [];
        List<CutLabFindingView>? weakFloorItems = null;
        int weakFloorInsertIndex = -1;

        foreach (CutLabFindingView finding in findings)
        {
            if (finding.Kind == CutLabFindingKind.WeakFloorCase)
            {
                weakFloorItems ??= [];
                if (weakFloorInsertIndex < 0)
                {
                    weakFloorInsertIndex = groups.Count;
                }

                weakFloorItems.Add(finding);
                continue;
            }

            groups.Add(new CutLabFindingGroupView
            {
                Kind = finding.Kind,
                Heading = finding.Heading,
                Items = [finding],
            });
        }

        if (weakFloorItems is { Count: > 0 })
        {
            groups.Insert(weakFloorInsertIndex, new CutLabFindingGroupView
            {
                Kind = CutLabFindingKind.WeakFloorCase,
                Heading = weakFloorItems[0].Heading,
                Items = weakFloorItems.ToArray(),
            });
        }

        return groups;
    }

    private static IReadOnlyDictionary<CutLabFindingKind, string> BuildFindingHeadingsByKind(IReadOnlyList<CutLabFinding> findings)
    {
        Dictionary<CutLabFindingKind, string> result = [];
        foreach (CutLabFinding finding in findings)
        {
            if (!result.ContainsKey(finding.Kind))
            {
                result[finding.Kind] = finding.Heading;
            }
        }

        return result;
    }

    private static CutLabStickyBarView BuildStickyBar(
        CutLabRoundPlan? roundPlan,
        IReadOnlyList<CutLabDecision>? decisions)
    {
        CutLabRoundQueueItem? nextProposal = roundPlan?.NextProposal;
        return new CutLabStickyBarView
        {
            HasStickyBar = nextProposal is not null,
            RoundLabel = nextProposal?.RoundLabel ?? string.Empty,
            CardsRemainingToCut = roundPlan?.CardsRemainingToTarget ?? 0,
            CutsAcceptedCount = decisions?.Count(decision => decision.Kind == CutLabDecisionKind.Accepted) ?? 0,
        };
    }

    private static CutLabProposalView BuildProposal(
        CutLabRoundPlan? roundPlan,
        CutLabProposalDeltas? proposalDeltas,
        CutLabState? state,
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName,
        IReadOnlyDictionary<CutLabFindingKind, string> findingHeadingsByKind)
    {
        CutLabRoundQueueItem? nextProposal = roundPlan?.NextProposal;
        if (nextProposal is null)
        {
            bool isAtTarget = (roundPlan?.CardsRemainingToTarget ?? 0) == 0;
            return new CutLabProposalView
            {
                HasProposal = false,
                IsTerminal = true,
                IsAtTarget = isAtTarget,
                IsNothingToCut = !isAtTarget,
            };
        }

        if (proposalDeltas is null)
        {
            return new CutLabProposalView();
        }

        IReadOnlyList<CutLabDeltaLineView> fullDeltaLines = BuildDeltaLines(nextProposal.CardName, proposalDeltas?.Deltas ?? []);
        IReadOnlyList<CutLabDeltaLineView> changedDeltaLines = fullDeltaLines
            .Where(line => line.IsMeaningful)
            .ToArray();
        IReadOnlyList<string> findingChips = nextProposal.DiscriminatingFindingKinds
            .Where(findingHeadingsByKind.ContainsKey)
            .Select(kind => findingHeadingsByKind[kind])
            .ToArray();
        IReadOnlyList<string> floorWarnings = BuildFloorWarnings(nextProposal.CardName, state, resolvedFloors, roleAssignmentsByCardName);

        return new CutLabProposalView
        {
            HasProposal = true,
            CardName = nextProposal.CardName,
            RoundKey = nextProposal.RoundKey,
            RoundLabel = nextProposal.RoundLabel,
            RoundBannerBody = RoundBannerBodyFor(nextProposal.RoundKey),
            FindingCount = nextProposal.FindingCount,
            FindingSummary = nextProposal.FindingCount > 0
                ? $"Flagged by {nextProposal.FindingCount} findings:"
                : "No structural finding flags this card — it's a preference call.",
            FindingChips = findingChips,
            ChangedDeltaLines = changedDeltaLines,
            FullDeltaLines = fullDeltaLines,
            ChangedFamilyCount = proposalDeltas?.ChangedFamilyCount ?? 0,
            FloorWarnings = floorWarnings,
        };
    }

    private static IReadOnlyList<CutLabDeltaLineView> BuildDeltaLines(
        string cardName,
        IReadOnlyList<CutLabMetricDelta> deltas)
    {
        return deltas
            .Select(delta =>
            {
                CutLabMetricUnit unit = MetricUnitFor(delta.Kind);
                return new CutLabDeltaLineView
                {
                    MetricLabel = delta.Label,
                    Direction = delta.Direction,
                    FormattedValueToken = FormatDeltaToken(delta.Delta, unit),
                    IsMeaningful = delta.IsMeaningful,
                    Sentence = delta.IsMeaningful
                        ? $"cutting {cardName} {DirectionVerbFor(delta.Direction)} {delta.Label.ToLowerInvariant()} by {FormatDeltaToken(delta.Delta, unit, includeDirectionGlyph: false)}."
                        : $"{delta.Label}: no meaningful change",
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<string> BuildFloorWarnings(
        string cardName,
        CutLabState? state,
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
    {
        if (state is null)
        {
            return [];
        }

        Dictionary<string, int> floorByRole = resolvedFloors.ToDictionary(
            floor => floor.Role,
            floor => floor.Floor,
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> countsByRole = CountRoles(state.Pool, roleAssignmentsByCardName);
        CutLabPoolCard? card = state.Pool.FirstOrDefault(poolCard => string.Equals(poolCard.Name, cardName, StringComparison.OrdinalIgnoreCase));
        if (card is null || !roleAssignmentsByCardName.TryGetValue(card.Name, out IReadOnlyList<string>? roles))
        {
            return [];
        }

        return CutLabFloorRules.Evaluate(countsByRole, floorByRole, roles, cardName, card.Quantity)
            .Select(warning => warning.Message)
            .ToArray();
    }

    private static IReadOnlyList<CutLabCutMadeRowView> BuildCutsMade(IReadOnlyList<CutLabDecision>? decisions)
    {
        if (decisions is null)
        {
            return [];
        }

        return decisions
            .Where(decision => decision.Kind == CutLabDecisionKind.Accepted)
            .OrderByDescending(decision => decision.Ordinal)
            .Select(decision => new CutLabCutMadeRowView
            {
                CardName = decision.CardName,
                RoundKey = decision.Round,
                RoundLabel = RoundLabelFor(decision.Round),
            })
            .ToArray();
    }

    private static IReadOnlyList<CutLabCompareRowView> BuildCompareRows(
        CutLabMetricSnapshot? baselineSnapshot,
        CutLabMetricSnapshot? currentSnapshot)
    {
        if (baselineSnapshot is null || currentSnapshot is null)
        {
            return [];
        }

        IReadOnlyDictionary<CutLabMetricKind, CutLabMetricValue> currentByKind = currentSnapshot.Metrics
            .ToDictionary(metric => metric.Kind);
        return baselineSnapshot.Metrics
            .Where(metric => currentByKind.ContainsKey(metric.Kind))
            .Select(metric =>
            {
                CutLabMetricValue current = currentByKind[metric.Kind];
                CutLabMetricDelta? delta = CreateCompareDelta(metric, current);
                return new CutLabCompareRowView
                {
                    MetricLabel = metric.Label,
                    BaselineValue = FormatMetricValue(metric.Value, metric.Unit),
                    CurrentValue = FormatMetricValue(current.Value, current.Unit),
                    DeltaValueToken = delta is null ? string.Empty : FormatDeltaToken(delta.Delta, metric.Unit),
                    Direction = delta?.Direction ?? CutLabMetricDirection.None,
                };
            })
            .ToArray();
    }

    private static CutLabMetricDelta? CreateCompareDelta(CutLabMetricValue baseline, CutLabMetricValue current)
    {
        if (!double.IsFinite(baseline.Value) || !double.IsFinite(current.Value))
        {
            return null;
        }

        double delta = current.Value - baseline.Value;
        double threshold = baseline.Unit == CutLabMetricUnit.Cards
            ? CutLabNoiseFloor.Cards
            : CutLabNoiseFloor.PercentPoints;
        bool isMeaningful = Math.Abs(delta) > threshold;
        CutLabMetricDirection direction = !isMeaningful
            ? CutLabMetricDirection.None
            : delta > 0
                ? CutLabMetricDirection.Up
                : CutLabMetricDirection.Down;

        return new CutLabMetricDelta
        {
            Kind = baseline.Kind,
            Family = baseline.Family,
            Label = baseline.Label,
            Before = baseline.Value,
            After = current.Value,
            Delta = delta,
            Direction = direction,
            IsMeaningful = isMeaningful,
        };
    }

    private static Dictionary<string, int> CountRoles(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (string roleKey in CutLabFloorRules.RoleKeys)
        {
            counts[roleKey] = 0;
        }

        foreach (CutLabPoolCard card in pool)
        {
            if (!roleAssignmentsByCardName.TryGetValue(card.Name, out IReadOnlyList<string>? roles))
            {
                continue;
            }

            foreach (string role in roles)
            {
                if (counts.ContainsKey(role))
                {
                    counts[role] += card.Quantity;
                }
            }
        }

        return counts;
    }

    private static string DisplayLabelFor(string roleKey)
        => RoleDisplayLabels.TryGetValue(roleKey, out string? label) ? label : roleKey;

    private static CutLabMetricUnit MetricUnitFor(CutLabMetricKind kind)
        => kind is CutLabMetricKind.Flood or CutLabMetricKind.Curve
            ? CutLabMetricUnit.Cards
            : CutLabMetricUnit.Percent;

    private static string FormatMetricValue(double value, CutLabMetricUnit unit)
        => unit == CutLabMetricUnit.Cards
            ? FormatCardValue(value)
            : $"{value:0.0}%";

    private static string FormatDeltaToken(double delta, CutLabMetricUnit unit, bool includeDirectionGlyph = true)
    {
        double magnitude = Math.Abs(delta);
        string prefix = includeDirectionGlyph
            ? delta > 0
                ? "▲"
                : delta < 0
                    ? "▼"
                    : string.Empty
            : string.Empty;

        return unit == CutLabMetricUnit.Cards
            ? $"{prefix}{FormatCardValue(magnitude)}"
            : $"{prefix}{magnitude:0.0}%";
    }

    private static string FormatCardValue(double value)
    {
        double rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        string count = $"{rounded:0}";
        return rounded == 1d ? $"{count} card" : $"{count} cards";
    }

    private static string DirectionVerbFor(CutLabMetricDirection direction)
        => direction == CutLabMetricDirection.Down ? "lowers" : "raises";

    private static string RoundBannerBodyFor(string roundKey)
        => roundKey switch
        {
            CutLabCutRoundEngine.Round1Key => "Cards flagged by 2 or more structural findings from the section above.",
            CutLabCutRoundEngine.Round2Key => "Cards flagged by exactly one structural finding.",
            CutLabCutRoundEngine.Round3Key => "Everything else, ordered by smallest measurable tradeoff first.",
            CutLabCutRoundEngine.SecondPassDeferredKey or CutLabCutRoundEngine.SecondPassRejectedKey => "Still over 100 cards. These were deferred or kept earlier; take another look.",
            _ => string.Empty,
        };

    private static string RoundLabelFor(string roundKey)
        => roundKey switch
        {
            CutLabCutRoundEngine.Round1Key => CutLabCutRoundEngine.Round1Label,
            CutLabCutRoundEngine.Round2Key => CutLabCutRoundEngine.Round2Label,
            CutLabCutRoundEngine.Round3Key => CutLabCutRoundEngine.Round3Label,
            CutLabCutRoundEngine.SecondPassDeferredKey => CutLabCutRoundEngine.SecondPassDeferredLabel,
            CutLabCutRoundEngine.SecondPassRejectedKey => CutLabCutRoundEngine.SecondPassRejectedLabel,
            _ => roundKey,
        };

    private static string FallbackSource(string playExperience)
    {
        if (!string.IsNullOrWhiteSpace(playExperience))
        {
            return playExperience;
        }

        return "your play experience";
    }
}

/// <summary>View-ready slot-competition group for one fixed Cut Lab role.</summary>
public sealed record CutLabRoleGroupView
{
    /// <summary>Stable role key for the group.</summary>
    public string RoleKey { get; init; } = string.Empty;

    /// <summary>User-facing label for the role group.</summary>
    public string DisplayLabel { get; init; } = string.Empty;

    /// <summary>Pool members that currently belong to the role group.</summary>
    public IReadOnlyList<CutLabRoleMemberView> Members { get; init; } = [];

    /// <summary>Number of locked cards inside the role group.</summary>
    public int LockedCount { get; init; }
}

/// <summary>View-ready role-group member entry for a single pool card.</summary>
public sealed record CutLabRoleMemberView
{
    /// <summary>Display card name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>True when the card is currently locked in the working session.</summary>
    public bool IsLocked { get; init; }

    /// <summary>True when the card is the resolved commander.</summary>
    public bool IsCommander { get; init; }
}

/// <summary>View-ready structural finding with preformatted evidence text.</summary>
public sealed record CutLabFindingView
{
    /// <summary>Underlying finding kind used for display grouping.</summary>
    public CutLabFindingKind Kind { get; init; }

    /// <summary>UI heading for the finding.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>Lead sentence describing the measured issue.</summary>
    public string Lead { get; init; } = string.Empty;

    /// <summary>Preformatted supporting evidence lines for the finding.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];
}

/// <summary>View-ready group of one or more structural findings for panel rendering.</summary>
public sealed record CutLabFindingGroupView
{
    /// <summary>Underlying finding kind represented by this rendered block.</summary>
    public CutLabFindingKind Kind { get; init; }

    /// <summary>UI heading for the rendered group.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>One or more findings rendered inside the group.</summary>
    public IReadOnlyList<CutLabFindingView> Items { get; init; } = [];
}

/// <summary>View-ready role-floor row including count state and provenance text.</summary>
public sealed record CutLabFloorRowView
{
    /// <summary>Stable role key for the floor row.</summary>
    public string RoleKey { get; init; } = string.Empty;

    /// <summary>User-facing role label.</summary>
    public string DisplayLabel { get; init; } = string.Empty;

    /// <summary>Current number of pool cards filling the role.</summary>
    public int InPoolCount { get; init; }

    /// <summary>Effective floor after merging defaults and user overrides.</summary>
    public int Floor { get; init; }

    /// <summary>Freshly derived default value before user override merge.</summary>
    public int DefaultValue { get; init; }

    /// <summary>True when the user has explicitly overridden the floor.</summary>
    public bool IsUserSet { get; init; }

    /// <summary>True when the pool count is at the caution band of floor plus one or below.</summary>
    public bool AtFloor { get; init; }

    /// <summary>Prebuilt UI copy describing the floor's default source.</summary>
    public string SourceLabel { get; init; } = string.Empty;
}

/// <summary>Sticky round/count bar state for the Cut rounds workspace.</summary>
public sealed record CutLabStickyBarView
{
    /// <summary>True when a current round exists and the sticky bar should render.</summary>
    public bool HasStickyBar { get; init; }

    /// <summary>Round label shown in the left slot of the sticky bar.</summary>
    public string RoundLabel { get; init; } = string.Empty;

    /// <summary>Cards still remaining to cut to reach the target size.</summary>
    public int CardsRemainingToCut { get; init; }

    /// <summary>Accepted cuts recorded in the current session.</summary>
    public int CutsAcceptedCount { get; init; }
}

/// <summary>Current one-at-a-time proposal state for the Cut rounds workspace.</summary>
public sealed record CutLabProposalView
{
    /// <summary>True when there is a proposal card to render.</summary>
    public bool HasProposal { get; init; }

    /// <summary>True when the queue is terminal and there is no next proposal.</summary>
    public bool IsTerminal { get; init; }

    /// <summary>True when the terminal state means the working list is already at 100 cards.</summary>
    public bool IsAtTarget { get; init; }

    /// <summary>True when the terminal state means all remaining cards are locked or protected.</summary>
    public bool IsNothingToCut { get; init; }

    /// <summary>Display card name for the current proposal.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>Stable round key for decision routing and restore context.</summary>
    public string RoundKey { get; init; } = string.Empty;

    /// <summary>Round banner heading copy.</summary>
    public string RoundLabel { get; init; } = string.Empty;

    /// <summary>Round banner supporting copy.</summary>
    public string RoundBannerBody { get; init; } = string.Empty;

    /// <summary>Count of discriminating findings attached to the proposal.</summary>
    public int FindingCount { get; init; }

    /// <summary>Evidence-line sentence shown above the finding chips.</summary>
    public string FindingSummary { get; init; } = string.Empty;

    /// <summary>Neutral evidence chips naming the findings attached to the proposal.</summary>
    public IReadOnlyList<string> FindingChips { get; init; } = [];

    /// <summary>Meaningful delta lines shown in the compact proposal summary.</summary>
    public IReadOnlyList<CutLabDeltaLineView> ChangedDeltaLines { get; init; } = [];

    /// <summary>All proposal delta lines rendered in the full metric breakdown expander.</summary>
    public IReadOnlyList<CutLabDeltaLineView> FullDeltaLines { get; init; } = [];

    /// <summary>Count of metric families whose deltas exceeded the noise floor.</summary>
    public int ChangedFamilyCount { get; init; }

    /// <summary>Non-blocking floor-warning copy for the proposed cut.</summary>
    public IReadOnlyList<string> FloorWarnings { get; init; } = [];
}

/// <summary>One rendered metric delta sentence for the proposal workspace.</summary>
public sealed record CutLabDeltaLineView
{
    /// <summary>User-facing metric label.</summary>
    public string MetricLabel { get; init; } = string.Empty;

    /// <summary>Meaningful display direction for the numeric token.</summary>
    public CutLabMetricDirection Direction { get; init; }

    /// <summary>Formatted numeric token including any directional glyph.</summary>
    public string FormattedValueToken { get; init; } = string.Empty;

    /// <summary>True when the delta exceeds the configured noise floor.</summary>
    public bool IsMeaningful { get; init; }

    /// <summary>Neutral sentence or no-change label shown beside the numeric token.</summary>
    public string Sentence { get; init; } = string.Empty;
}

/// <summary>One restore-list row for an accepted cut.</summary>
public sealed record CutLabCutMadeRowView
{
    /// <summary>Display card name for the accepted cut.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>Stable round key where the cut was recorded.</summary>
    public string RoundKey { get; init; } = string.Empty;

    /// <summary>User-facing round label for the row's muted context text.</summary>
    public string RoundLabel { get; init; } = string.Empty;
}

/// <summary>One baseline-versus-current comparison table row.</summary>
public sealed record CutLabCompareRowView
{
    /// <summary>User-facing metric label.</summary>
    public string MetricLabel { get; init; } = string.Empty;

    /// <summary>Formatted baseline snapshot value.</summary>
    public string BaselineValue { get; init; } = string.Empty;

    /// <summary>Formatted current working-list value.</summary>
    public string CurrentValue { get; init; } = string.Empty;

    /// <summary>Formatted delta token including any directional glyph.</summary>
    public string DeltaValueToken { get; init; } = string.Empty;

    /// <summary>Display direction for the delta token.</summary>
    public CutLabMetricDirection Direction { get; init; }
}
