using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models.Api;

/// <summary>JSON request payload for applying one Cut Lab cut decision.</summary>
public sealed record CutLabDecideApiRequest
{
    /// <summary>Serialized Cut Lab working-session state envelope.</summary>
    [Required]
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Display card name receiving the decision.</summary>
    [Required]
    public string CardName { get; init; } = string.Empty;

    /// <summary>Decision to apply to the named card.</summary>
    public CutLabDecideAction Decision { get; init; }
}

/// <summary>Supported decision actions for the async and no-JS Cut Lab flows.</summary>
public enum CutLabDecideAction
{
    /// <summary>Accept the proposed cut.</summary>
    Accept,

    /// <summary>Reject the proposed cut.</summary>
    Reject,

    /// <summary>Defer the proposed cut to a later loop pass.</summary>
    Defer,

    /// <summary>Restore a previously accepted cut by removing all recorded decisions for the card.</summary>
    Restore,
}
