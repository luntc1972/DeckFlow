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

    /// <summary>True when combo-backed findings are incomplete because combo lookup was unavailable.</summary>
    public bool ComboDataUnavailable { get; init; }

    /// <summary>True when category-backed findings are incomplete because category lookup was unavailable.</summary>
    public bool CategoryDataUnavailable { get; init; }

    /// <summary>Role floor rows rendered in the fixed Cut Lab order.</summary>
    public IReadOnlyList<CutLabFloorRowView> FloorRows { get; init; } = [];

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
                Heading = finding.Heading,
                Lead = finding.Lead,
                Evidence = finding.Evidence
                    .Select(evidence => evidence.ManaValue is double manaValue
                        ? $"{evidence.CardName} · MV {manaValue:0.##}"
                        : evidence.CardName)
                    .ToArray(),
            })
            .ToArray();
        IReadOnlyList<CutLabFloorRowView> floorRows = BuildFloorRows(result.ResolvedFloors, result.RoleAssignmentsByCardName, request.PlayExperience);
        IReadOnlyDictionary<string, string> roleListByCardName = BuildRoleListByCardName(pool, result.RoleAssignmentsByCardName);
        IReadOnlyDictionary<string, string> roleKeysByCardName = BuildRoleKeysByCardName(pool, result.RoleAssignmentsByCardName);

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
            ComboDataUnavailable = result.HasResult && !result.Findings.ComboDataAvailable,
            CategoryDataUnavailable = result.HasResult && !result.Findings.CategoryDataAvailable,
            FloorRows = floorRows,
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
                    LockedCount = members.Count(member => member.IsLocked),
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<CutLabFloorRowView> BuildFloorRows(
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName,
        string playExperience)
    {
        Dictionary<string, int> countsByRole = CountRoles(roleAssignmentsByCardName);
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

    private static Dictionary<string, int> CountRoles(IReadOnlyDictionary<string, IReadOnlyList<string>> roleAssignmentsByCardName)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (string roleKey in CutLabFloorRules.RoleKeys)
        {
            counts[roleKey] = 0;
        }

        foreach (IReadOnlyList<string> roles in roleAssignmentsByCardName.Values)
        {
            foreach (string role in roles)
            {
                if (counts.ContainsKey(role))
                {
                    counts[role]++;
                }
            }
        }

        return counts;
    }

    private static string DisplayLabelFor(string roleKey)
        => RoleDisplayLabels.TryGetValue(roleKey, out string? label) ? label : roleKey;

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
    /// <summary>UI heading for the finding.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>Lead sentence describing the measured issue.</summary>
    public string Lead { get; init; } = string.Empty;

    /// <summary>Preformatted supporting evidence lines for the finding.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];
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
