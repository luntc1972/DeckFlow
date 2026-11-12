namespace DeckFlow.Web.Services.CutLab;

/// <summary>Defines display roles independently of the roles that carry floors.</summary>
public static class CutLabRoleKeys
{
    /// <summary>Role key for graveyard recursion.</summary>
    public const string Recursion = "recursion";

    /// <summary>Display-only fallback role key.</summary>
    public const string Residual = "other";

    /// <summary>Roles that may be displayed and assigned without a floor.</summary>
    public static readonly IReadOnlyList<string> Floorless = Array.AsReadOnly(new[] { Recursion });

    /// <summary>Floor roles followed by floor-less roles and the display-only fallback.</summary>
    public static readonly IReadOnlyList<string> Display = Array.AsReadOnly(
        CutLabFloorRules.RoleKeys.Concat(Floorless).Append(Residual).ToArray());
}
