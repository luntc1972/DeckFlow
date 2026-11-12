namespace DeckFlow.Web.Models;

/// <summary>Stable names and positions for the Cut Lab workflow.</summary>
public static class CutLabSteps
{
    /// <summary>Pool intake and reference information.</summary>
    public const int Process = 1;

    /// <summary>Plan selection and pool locks.</summary>
    public const int Plan = 2;

    /// <summary>Cut decisions and rounds.</summary>
    public const int Decide = 3;

    /// <summary>Goals, scenarios, and what-if swaps.</summary>
    public const int Goals = 4;

    /// <summary>Validated deck export.</summary>
    public const int Export = 5;

    /// <summary>Resolves a posted dialog origin, defaulting to the pool's Plan step.</summary>
    public static int FromOrigin(string? originStep) => originStep switch
    {
        nameof(Process) => Process,
        nameof(Plan) => Plan,
        nameof(Decide) => Decide,
        nameof(Goals) => Goals,
        nameof(Export) => Export,
        _ => Plan,
    };
}
