namespace DeckFlow.Web.Services.CutLab;

/// <summary>
/// Pure overlap-corrected aggregate floor feasibility check for the resolved Cut Lab floor set.
/// The correction is analytic rather than measured: it collapses the proven engines-draw subset
/// relationship and credits win conditions as free-riding, without introducing any broader
/// heuristic overlap discount. The resulting demand deliberately under-counts and is therefore a
/// conservative estimate. This remains a panel-level notice rather than a <c>CutLabFindingKind</c>
/// because it attaches to no individual card and would otherwise need to be excluded from the
/// round tally immediately.
/// </summary>
public static class CutLabFloorFeasibility
{
    private const int TargetDeckSize = 100;
    private const int CommanderSlots = 1;

    /// <summary>Evaluates whether the resolved floor set can fit inside one 100-card Commander deck.</summary>
    /// <param name="resolvedFloors">Resolved Cut Lab floors, including any user overrides.</param>
    /// <returns>
    /// Null when the resolved floor set fits the available nonland slots; otherwise the conservative
    /// required-slot estimate and the top floor-relaxation candidates.
    /// </returns>
    public static CutLabFloorFeasibilityResult? Evaluate(IReadOnlyList<CutLabResolvedFloor> resolvedFloors)
    {
        ArgumentNullException.ThrowIfNull(resolvedFloors);

        // Why: aggregate feasibility must reflect the floors currently in force. A user-raised
        // override also has to fit, while a user-lowered override legitimately relieves pressure.
        Dictionary<string, CutLabResolvedFloor> floorsByRole = resolvedFloors.ToDictionary(
            floor => floor.Role,
            StringComparer.OrdinalIgnoreCase);

        int landsFloor = GetEffectiveFloor(floorsByRole, "lands");
        // Why: D-06's "~63 slots" reference assumes the default 36-land floor: 100 - 1 - 36 = 63.
        // Reusing that same capacity keeps the corrected feasibility check calibrated to the same deck size.
        int availableNonlandSlots = TargetDeckSize - CommanderSlots - landsFloor;

        int ramp = GetEffectiveFloor(floorsByRole, "ramp");
        int draw = GetEffectiveFloor(floorsByRole, "draw");
        int interactionTargeted = GetEffectiveFloor(floorsByRole, "interaction-targeted");
        int interactionMass = GetEffectiveFloor(floorsByRole, "interaction-mass");
        int protection = GetEffectiveFloor(floorsByRole, "protection");
        int engines = GetEffectiveFloor(floorsByRole, "engines");
        int payoffs = GetEffectiveFloor(floorsByRole, "payoffs");

        int requiredNonlandSlots =
            ramp +
            // Why: engines is a strict subset of draw. Every engines card must satisfy IsDrawCard,
            // which is draw's sole gate, so satisfying both floors costs max(draw, engines) slots.
            Math.Max(draw, engines) +
            // Why: targeted and mass interaction are disjoint by construction, and lands never double
            // count as ramp, so these terms genuinely add and ramp does not consume land slots.
            interactionTargeted +
            interactionMass +
            protection +
            // Why: payoffs can co-occur with engines and wincons, but that is not the proven subset
            // relationship that justifies the engines/draw collapse, and no magnitude was measured.
            // D-06a authorizes exactly two corrections and payoffs is not one of them. Max() raises
            // payoffs harder than any other role — 124 of 124 adopting commanders sit below the band
            // at brackets 4-5 — so discounting it would silence the advisory in the case it must catch.
            payoffs;
        // Why: wincons is intentionally omitted from the required-slot arithmetic. Win conditions can
        // co-occur with any other role through the isComboPiece branch, so a wincons floor is at least
        // partly free whenever the deck already runs qualifying combo pieces. The magnitude is unmeasured,
        // so crediting it fully under-counts demand and makes the advisory fire less often rather than more.

        if (requiredNonlandSlots <= availableNonlandSlots)
        {
            return null;
        }

        string drawOrEnginesRole = draw >= engines ? "draw" : "engines";
        HashSet<string> candidateRoles =
        [
            "ramp",
            drawOrEnginesRole,
            "interaction-targeted",
            "interaction-mass",
            "protection",
            "payoffs",
        ];

        IReadOnlyList<CutLabFloorRelaxCandidate> relaxCandidates = resolvedFloors
            .Where(floor => candidateRoles.Contains(floor.Role))
            .Select(floor =>
            {
                int commanderRaise = floor.CommanderValue is not null && floor.CommanderValue.Value > floor.BracketValue
                    ? floor.CommanderValue.Value - floor.BracketValue
                    : 0;
                return new CutLabFloorRelaxCandidate
                {
                    RoleKey = floor.Role,
                    Floor = floor.Floor,
                    CommanderRaise = commanderRaise > 0 ? commanderRaise : null,
                };
            })
            .OrderByDescending(candidate => candidate.CommanderRaise ?? 0)
            .ThenByDescending(candidate => candidate.Floor)
            .ThenBy(candidate => candidate.RoleKey, StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        return new CutLabFloorFeasibilityResult
        {
            RequiredNonlandSlots = requiredNonlandSlots,
            AvailableNonlandSlots = availableNonlandSlots,
            Deficit = requiredNonlandSlots - availableNonlandSlots,
            LandsFloor = landsFloor,
            RelaxCandidates = relaxCandidates,
        };
    }

    private static int GetEffectiveFloor(
        IReadOnlyDictionary<string, CutLabResolvedFloor> floorsByRole,
        string role)
        => floorsByRole.TryGetValue(role, out CutLabResolvedFloor? floor)
            ? floor.Floor
            : 0;
}

/// <summary>Conservative aggregate nonland-slot feasibility result for the current resolved floor set.</summary>
public sealed record CutLabFloorFeasibilityResult
{
    /// <summary>Conservative estimate of the nonland slots needed to satisfy the active floor set.</summary>
    public int RequiredNonlandSlots { get; init; }

    /// <summary>Available nonland slots after reserving the commander and the current lands floor.</summary>
    public int AvailableNonlandSlots { get; init; }

    /// <summary>Amount by which the conservative required-slot estimate exceeds the available slots.</summary>
    public int Deficit { get; init; }

    /// <summary>Resolved lands floor used to derive the nonland-slot capacity.</summary>
    public int LandsFloor { get; init; }

    /// <summary>Up to three commander-raised floor roles to reconsider first when the set is infeasible.</summary>
    public IReadOnlyList<CutLabFloorRelaxCandidate> RelaxCandidates { get; init; } = [];
}

/// <summary>Commander-aware floor role to reconsider first when the aggregate floor set is infeasible.</summary>
public sealed record CutLabFloorRelaxCandidate
{
    /// <summary>Stable Cut Lab role key for the candidate floor.</summary>
    public string RoleKey { get; init; } = string.Empty;

    /// <summary>Effective floor currently in force for the role.</summary>
    public int Floor { get; init; }

    /// <summary>Amount by which commander data raised this role above the bracket value, when any.</summary>
    public int? CommanderRaise { get; init; }
}
