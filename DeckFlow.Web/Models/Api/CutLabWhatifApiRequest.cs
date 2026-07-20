using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models.Api;

/// <summary>JSON request payload for Cut Lab what-if swap preview and commit actions.</summary>
public sealed record CutLabWhatifApiRequest
{
    /// <summary>Serialized Cut Lab working-session state envelope.</summary>
    [Required]
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Working-list card to remove during the hypothetical swap.</summary>
    [Required]
    public string CardOut { get; init; } = string.Empty;

    /// <summary>Accepted cut-pile card to restore during the hypothetical swap.</summary>
    [Required]
    public string CardIn { get; init; } = string.Empty;
}
