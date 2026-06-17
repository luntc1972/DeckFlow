namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Null-safe maintenance-operation outcome contract. Callers must explicitly construct <see cref="Success"/>,
/// while delete counts and flags remain usable for both dry-run and mutating maintenance flows.
/// </summary>
public sealed record ContentMaintenanceResult
{
    /// <summary>Gets whether the maintenance operation completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the number of content rows deleted or projected for deletion.</summary>
    public int DeletedContentRows { get; init; }

    /// <summary>Gets the number of site-index rows deleted or projected for deletion.</summary>
    public int DeletedSiteIndexRows { get; init; }

    /// <summary>Gets the number of video rows deleted or projected for deletion.</summary>
    public int DeletedVideos { get; init; }

    /// <summary>Gets whether the operation was a dry-run projection.</summary>
    public bool DryRun { get; init; }

    /// <summary>Gets whether an existing blocked-video row was removed during unblock.</summary>
    public bool RemovedExistingBlock { get; init; }

    /// <summary>Gets an optional status or error message for the host.</summary>
    public string? Message { get; init; }
}
