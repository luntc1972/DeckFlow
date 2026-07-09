using DeckFlow.Core.Content;

namespace DeckFlow.Web.Services;

/// <summary>
/// Web-host <see cref="ISeedKeyMembershipSource"/> resolving the DEPLOYED
/// <c>index-seed.json</c> the same way <see cref="ContentKbSeedLoader"/> does
/// (<see cref="ContentKbArtifactPathResolver.SeedFilePath"/>), so the D-02
/// <see cref="SeedManagedBackfill"/> classifies against the exact seed the web app just loaded.
/// Passes the full <see cref="SeedIndexReadResult"/> from <see cref="SeedIndexFileReader.Read"/>
/// through unchanged — never collapsing an unavailable seed into an empty set.
/// </summary>
public sealed class WebSeedKeyMembershipSource : ISeedKeyMembershipSource
{
    private readonly ContentKbArtifactPathResolver _resolver;
    private readonly ILogger<WebSeedKeyMembershipSource> _logger;

    /// <summary>
    /// Creates a web-host seed-membership source bound to the resolved deployed seed path.
    /// </summary>
    /// <param name="resolver">Content KB artifact path resolver (owns <c>SeedFilePath</c>).</param>
    /// <param name="logger">Logger.</param>
    public WebSeedKeyMembershipSource(
        ContentKbArtifactPathResolver resolver,
        ILogger<WebSeedKeyMembershipSource> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);

        _resolver = resolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public SeedIndexReadResult GetSeedMembership()
        => SeedIndexFileReader.Read(_resolver.SeedFilePath, _logger);
}
