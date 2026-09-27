namespace DeckFlow.Studio;

/// <summary>
/// Indicates whether the production Studio connection and SCP artifact-transport are configured
/// (presence-only; never carries underlying connection string, SSH, or credential values).
/// </summary>
/// <param name="IsProdConfigured">Whether <c>Studio:ProdConnectionString</c> is present.</param>
/// <param name="IsScpConfigured">Whether all required <c>Studio:Scp:*</c> keys are present.</param>
public sealed record StudioConfig(bool IsProdConfigured, bool IsScpConfigured);
