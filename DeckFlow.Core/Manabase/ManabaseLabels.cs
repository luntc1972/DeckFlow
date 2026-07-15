namespace DeckFlow.Core.Manabase;

/// <summary>
/// Canonical human labels for the mana-base domain enums. Both the on-page verdict
/// (<c>ManabaseDisplay</c> in the Web layer) and the paste-ready text report
/// (<see cref="ManabaseReportTextBuilder"/>) delegate here so the two surfaces can never drift.
/// CSS class names stay in the Web layer — only the user-facing words live here.
/// </summary>
public static class ManabaseLabels
{
    /// <summary>Human label for the four-tier health scale (Excellent / Solid / Workable / Needs work).</summary>
    public static string Health(ManabaseHealth health) => health switch
    {
        ManabaseHealth.Healthy => "Excellent",
        ManabaseHealth.Functional => "Solid",
        ManabaseHealth.Workable => "Workable",
        _ => "Needs work",
    };

    /// <summary>Human label for an analysis mode (used in the results echo line and the text report).</summary>
    public static string Mode(ManabaseMode mode) => mode switch
    {
        ManabaseMode.Cedh => "cEDH",
        ManabaseMode.Focused => "Focused",
        _ => "Casual",
    };

    /// <summary>Human label for a single plan role (used in the plan-presence role breakdown, both surfaces).</summary>
    public static string PlanRole(PlanRole role) => role switch
    {
        Manabase.PlanRole.Payoff => "payoff",
        Manabase.PlanRole.Engine => "engine",
        Manabase.PlanRole.TutorCombo => "tutor/combo",
        Manabase.PlanRole.Interaction => "interaction",
        _ => role.ToString().ToLowerInvariant(),
    };
}
