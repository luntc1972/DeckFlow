using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models.Api;

/// <summary>JSON request payload for applying one Cut Lab quantity adjustment.</summary>
public sealed record CutLabAdjustApiRequest
{
    /// <summary>Serialized Cut Lab working-session state envelope.</summary>
    [Required]
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Display card name receiving the adjustment.</summary>
    [Required]
    public string CardName { get; init; } = string.Empty;

    /// <summary>Signed quantity delta to apply to the named card.</summary>
    public int Delta { get; init; }

    /// <summary>True when materializing a known basic not present in the imported pool.</summary>
    public bool IsAddedBasic { get; init; }
}
