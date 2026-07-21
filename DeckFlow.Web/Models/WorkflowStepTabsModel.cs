namespace DeckFlow.Web.Models;

/// <summary>Single tab entry in a shared deck workflow step strip.</summary>
/// <param name="Step">One-based step number represented by the tab.</param>
/// <param name="Label">Display label for the workflow tab.</param>
/// <param name="IsComplete">Whether the workflow step has been completed.</param>
/// <param name="IsEnabled">Whether the workflow step can be activated.</param>
/// <param name="SubmitFormId">Optional form id to bind as a submit activator instead of a client-side button.</param>
public sealed record WorkflowStepTab(int Step, string Label, bool IsComplete, bool IsEnabled = true, string? SubmitFormId = null);

/// <summary>Configuration for rendering shared workflow step tabs.</summary>
/// <param name="AriaLabel">Accessible label for the tab list.</param>
/// <param name="TabIdPrefix">Prefix used to build stable tab element identifiers.</param>
/// <param name="PanelIdPrefix">Prefix used to build stable tab panel identifiers.</param>
/// <param name="DataShowStepAttribute">Data attribute used by scripts to show the selected step.</param>
/// <param name="Steps">Ordered workflow steps rendered by the shared partial.</param>
public sealed record WorkflowStepTabsModel(
    string AriaLabel,
    string TabIdPrefix,
    string PanelIdPrefix,
    string DataShowStepAttribute,
    IReadOnlyList<WorkflowStepTab> Steps);
