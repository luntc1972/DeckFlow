namespace DeckFlow.Web.Services.CutLab;

/// <summary>Defines display roles independently of the roles that carry floors.</summary>
public static class CutLabRoleKeys
{
    /// <summary>Role key for graveyard recursion.</summary>
    public const string Recursion = "recursion";

    /// <summary>Role key for continuing opponent restrictions and taxes.</summary>
    public const string Stax = "stax";

    /// <summary>Display-only fallback role key.</summary>
    public const string Residual = "other";

    /// <summary>
    /// Ordered floor-less roles and their default display labels. Adding a floor-less role requires
    /// one key/label entry here plus its assignment rule.
    /// </summary>
    internal static readonly IReadOnlyList<(string Key, string Label)> FloorlessRegistry = Array.AsReadOnly(new[]
    {
        (Key: Recursion, Label: "Recursion"),
        (Key: Stax, Label: "Stax"),
    });

    /// <summary>Roles that may be displayed and assigned without a floor.</summary>
    public static readonly IReadOnlyList<string> Floorless = Array.AsReadOnly(FloorlessRegistry.Select(entry => entry.Key).ToArray());

    /// <summary>Floor roles followed by floor-less roles and the display-only fallback.</summary>
    public static readonly IReadOnlyList<string> Display = Array.AsReadOnly(
        CutLabFloorRules.RoleKeys.Concat(Floorless).Append(Residual).ToArray());
}
