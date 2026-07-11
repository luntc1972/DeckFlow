using Microsoft.Extensions.Configuration;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Provides the operator-supplied production connection string for Studio workflows.
/// </summary>
public interface IStudioProdConnectionSource
{
    /// <summary>
    /// Gets the configured production connection string, or an empty string when it is missing.
    /// </summary>
    string ConnectionString { get; }
}

/// <summary>
/// Reads the Studio production connection string from configuration.
/// </summary>
public sealed class StudioProdConnectionSource : IStudioProdConnectionSource
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Creates the connection-string source over application configuration.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    public StudioProdConnectionSource(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    // Why: single source for the operator-supplied prod connection string; a missing value becomes
    // empty string so downstream callers fail closed, matching the prior inline idiom.
    /// <inheritdoc />
    public string ConnectionString => _configuration["Studio:ProdConnectionString"] ?? string.Empty;
}
