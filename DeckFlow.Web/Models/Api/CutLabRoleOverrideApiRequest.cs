using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Models.Api;

/// <summary>JSON role override request.</summary>
public sealed record CutLabRoleOverrideApiRequest
{
    /// <summary>Serialized session state.</summary>
    public string CutLabStateJson { get; init; } = "";

    /// <summary>Card whose roles are being edited.</summary>
    public string CardName { get; init; } = "";

    /// <summary>Role key for an add or remove operation.</summary>
    public string? Role { get; init; }

    /// <summary>Requested role edit operation.</summary>
    public CutLabRoleOverrideOperation Operation { get; init; }
}
