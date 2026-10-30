namespace DeckFlow.Web.Models.Api;

/// <summary>JSON role override response.</summary>
public sealed record CutLabRoleOverrideApiResponse
{
    /// <summary>Full workspace patch after a changed edit.</summary>
    public CutLabUiPatchDto? Patch { get; init; }

    /// <summary>Whether the edit left the session unchanged.</summary>
    public bool Unchanged { get; init; }

    /// <summary>Card whose roles were edited.</summary>
    public string CardName { get; init; } = "";

    /// <summary>Effective role keys after the edit.</summary>
    public IReadOnlyList<string> RoleKeys { get; init; } = [];

    /// <summary>Display label for the effective roles.</summary>
    public string RoleLabel { get; init; } = "";

    /// <summary>Whether this card has a stored role override.</summary>
    public bool RoleEdited { get; init; }
}
