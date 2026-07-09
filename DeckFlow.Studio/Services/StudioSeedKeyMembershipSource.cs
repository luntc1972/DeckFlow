using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Studio-host <see cref="ISeedKeyMembershipSource"/> resolving the operator's git-checkout
/// <c>index-seed.json</c> at <c>{repoRoot}/content-kb/seed/index-seed.json</c> — the same
/// resolution shape <see cref="GitBodyCoverageAudit"/> uses for the repo-root-relative artifact
/// tree (90-CONTEXT.md). Passes the full <see cref="SeedIndexReadResult"/> from
/// <see cref="SeedIndexFileReader.Read"/> through unchanged.
/// </summary>
/// <remarks>
/// <see cref="ISeedKeyMembershipSource.GetSeedMembership"/> is a synchronous seam shared with the
/// web host's plain file-path resolver; resolving the git repo root is Studio's one inherently
/// async step (an external <c>git rev-parse --show-toplevel</c> process call via
/// <see cref="IGitRepository.ResolveRepoRootAsync"/>). ASP.NET Core hosts carry no
/// <see cref="SynchronizationContext"/>, so blocking on that one call here (a startup-only,
/// once-per-boot path) cannot deadlock. Any failure (not a git checkout, git not on PATH,
/// process failure) is caught and treated exactly like an unavailable seed — never propagated,
/// never collapsed into an empty-but-available result.
/// </remarks>
public sealed class StudioSeedKeyMembershipSource : ISeedKeyMembershipSource
{
    private static readonly IReadOnlySet<string> EmptyKeys = new HashSet<string>(StringComparer.Ordinal);

    private readonly IGitRepository _git;
    private readonly ILogger<StudioSeedKeyMembershipSource> _logger;

    /// <summary>
    /// Creates a Studio seed-membership source over the injected git repository adapter.
    /// </summary>
    /// <param name="git">Git repository adapter used to resolve the operator's checkout root.</param>
    /// <param name="logger">Logger.</param>
    public StudioSeedKeyMembershipSource(IGitRepository git, ILogger<StudioSeedKeyMembershipSource> logger)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(logger);

        _git = git;
        _logger = logger;
    }

    /// <inheritdoc />
    public SeedIndexReadResult GetSeedMembership()
    {
        string repoRoot;
        try
        {
            repoRoot = _git
                .ResolveRepoRootAsync(StudioRepoLocator.ResolveStartDirectory())
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Studio git repo root could not be resolved; seed unavailable for seed_managed backfill.");
            return new SeedIndexReadResult(false, EmptyKeys);
        }

        var seedFilePath = Path.Combine(repoRoot, "content-kb", "seed", "index-seed.json");
        return SeedIndexFileReader.Read(seedFilePath, _logger);
    }
}
