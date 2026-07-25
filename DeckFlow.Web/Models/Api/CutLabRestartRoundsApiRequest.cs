using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models.Api;

/// <summary>JSON request payload for restarting the first two Cut Lab rounds.</summary>
public sealed record CutLabRestartRoundsApiRequest
{
    /// <summary>Serialized Cut Lab working-session state envelope.</summary>
    [Required]
    public string CutLabStateJson { get; init; } = string.Empty;
}
