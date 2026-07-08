namespace DeckFlow.Studio;

/// <summary>
/// Indicates whether the production Studio connection, SCP artifact-transport, and the SYNC-09
/// deploy-confirm endpoint (base URL + admin credentials) have been configured (presence-only;
/// never carries the underlying connection string, SSH, or credential values).
/// </summary>
/// <param name="IsProdConfigured">Whether <c>Studio:ProdConnectionString</c> is present.</param>
/// <param name="IsScpConfigured">Whether all required <c>Studio:Scp:*</c> keys are present.</param>
/// <param name="IsConfirmerConfigured">
/// Whether <c>Studio:PublicSiteBaseUrl</c>, <c>Studio:AdminUser</c>, and
/// <c>Studio:AdminPassword</c> are all present — the deploy-confirm poll (D-09 REVISED) refuses to
/// start when this is <see langword="false"/> so a missing-creds push can never silently hang.
/// Defaults to <see langword="false"/> so existing 2-argument construction sites (tests) keep
/// compiling unchanged.
/// </param>
public sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured, bool IsConfirmerConfigured = false);
