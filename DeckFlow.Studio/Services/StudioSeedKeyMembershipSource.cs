using DeckFlow.Core.Content;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Studio-host <see cref="ISeedKeyMembershipSource"/> resolving the private-root
/// <c>index-seed.json</c>. Passes the full <see cref="SeedIndexReadResult"/> from
/// <see cref="SeedIndexFileReader.Read"/> through unchanged.
/// </summary>
public sealed class StudioSeedKeyMembershipSource : ISeedKeyMembershipSource
{
    private readonly IPrivateKbRootProvider _privateKbRootProvider;
    private readonly ILogger<StudioSeedKeyMembershipSource> _logger;

    /// <summary>
    /// Creates a Studio seed-membership source over the private KB root.
    /// </summary>
    /// <param name="privateKbRootProvider">Lazily resolves the private KB root.</param>
    /// <param name="logger">Logger.</param>
    public StudioSeedKeyMembershipSource(
        IPrivateKbRootProvider privateKbRootProvider,
        ILogger<StudioSeedKeyMembershipSource> logger)
    {
        ArgumentNullException.ThrowIfNull(privateKbRootProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _privateKbRootProvider = privateKbRootProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public SeedIndexReadResult GetSeedMembership()
    {
        return SeedIndexFileReader.Read(_privateKbRootProvider.GetRoot().SeedFile, _logger);
    }
}
