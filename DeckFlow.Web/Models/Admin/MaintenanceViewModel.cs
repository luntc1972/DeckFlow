namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model bound to <c>Views/Shared/_MaintenancePage.cshtml</c>. Used by
/// <c>FeatureFlagGateAttribute</c> (Phase 6, FLAG-05, D-17) to render a 503 maintenance
/// response when a route's feature flag is disabled. Title and Message are operator-supplied
/// via the attribute; defaults below are safe generic copy.
/// </summary>
public sealed class MaintenanceViewModel
{
    public string Title { get; init; } = "Temporarily unavailable";
    public string Message { get; init; } = "This page is offline for maintenance. Please try again shortly.";
    public string? PrimaryActionLabel { get; init; }
    public string? PrimaryActionUrl { get; init; }
}
