using System.ComponentModel.DataAnnotations;

using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Models.Api;

/// <summary>
/// JSON request payload for applying a checked plan-panel profile. The client embeds the checked
/// generic strategy and commander theme slugs into <see cref="CutLabStateJson"/>'s carried
/// <c>intent.planProfile</c> before posting; the server re-validates every slug rather than trusting
/// the client-supplied selection (T-08-07-01).
/// </summary>
public sealed record CutLabPlanApplyApiRequest
{
    /// <summary>Serialized Cut Lab working-session state envelope carrying the checked plan profile.</summary>
    [Required]
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Archetype selected before this plan-panel edit.</summary>
    public string? PriorArchetype { get; init; }

    /// <summary>Archetype selected before a coalesced series of plan-panel edits.</summary>
    public string? ChainStartArchetype { get; init; }

    /// <summary>Goals stored before a coalesced series of plan-panel edits.</summary>
    public CutLabGoalSettings? ChainStartGoals { get; init; }
}
