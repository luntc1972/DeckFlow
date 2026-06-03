namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model bound to <c>Views/Shared/_MaintenancePage.cshtml</c>. Used by
/// <c>FeatureFlagGateAttribute</c> (Phase 6, FLAG-05, D-17) to render a 503 maintenance
/// response when a route's feature flag is disabled. Title and Message are operator-supplied
/// via the attribute; defaults below are safe generic copy.
/// </summary>
public sealed class MaintenanceViewModel
{
    /// <summary>Headline shown at the top of the maintenance page.</summary>
    public string Title { get; init; } = "Temporarily unavailable";
    /// <summary>Operator-supplied explanation for why the route is unavailable.</summary>
    public string Message { get; init; } = "This page is offline for maintenance. Please try again shortly.";
    /// <summary>Optional label for the primary maintenance-page action.</summary>
    public string? PrimaryActionLabel { get; init; }
    /// <summary>Optional URL for the primary maintenance-page action.</summary>
    public string? PrimaryActionUrl { get; init; }
}
