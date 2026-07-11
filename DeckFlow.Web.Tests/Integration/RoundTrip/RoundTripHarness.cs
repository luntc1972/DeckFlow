using System.Diagnostics;
using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;

namespace DeckFlow.Web.Tests.Integration.RoundTrip;

/// <summary>
/// Reusable round-trip harness for the SYNC-16 integration test (Plan 93-02): pre-creates the
/// Postgres prod schema once over a Testcontainers connection, hands out schema-ensure-OFF prod
/// stores and a distinct local (Studio-side) SQLite store over real connections (D-02), drives a
/// real <c>git init</c> temp-repo bootstrap (D-03), and deploy-copies the committed tree into a
/// distinct <c>/app</c> stand-in directory. Zero production-code change: every member here only
/// calls existing public constructors and interfaces, or a test-only <c>git</c> bootstrap helper
/// scoped to this file.
/// </summary>
public sealed class RoundTripHarness : IDisposable
{
    /// <summary>Deterministic branch name the bootstrap repo is initialized on.</summary>
    public const string Branch = "main";

    /// <summary>Local bare-origin remote name.</summary>
    public const string OriginRemote = "origin";

    private readonly string _localDbPath;
    private string? _priorRepoRootEnv;
    private bool _repoRootEnvSet;
    private bool _disposed;

    /// <summary>
    /// Creates a harness instance with fresh, uniquely-named temp paths (local SQLite database,
    /// git repo root, deploy-copy <c>/app</c> stand-in, and bare origin) for this test's lifetime.
    /// </summary>
    public RoundTripHarness()
    {
        var stamp = Guid.NewGuid().ToString("N");
        _localDbPath = Path.Combine(Path.GetTempPath(), $"roundtrip-local-{stamp}.db");
        RepoRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-repo-{stamp}");
        AppRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-app-{stamp}");
        OriginRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-origin-{stamp}.git");
    }

    /// <summary>Gets the local (Studio-side) SQLite database file path this harness instance owns.</summary>
    public string LocalDbPath => _localDbPath;

    /// <summary>Gets the temp git working-tree root this harness bootstraps and drives.</summary>
    public string RepoRoot { get; }

    /// <summary>Gets the distinct deploy-copy stand-in directory simulating Render's <c>/app</c> git checkout.</summary>
    public string AppRoot { get; }

    /// <summary>Gets the local bare-origin repo path <see cref="RepoRoot"/> pushes to.</summary>
    public string OriginRoot { get; }

    /// <summary>
    /// Builds a Postgres <see cref="RelationalDatabaseConnection"/> descriptor from a raw
    /// connection string (mirrors <c>PostgresStorageTests.CreateConnection</c>).
    /// </summary>
    /// <param name="connectionString">Raw Postgres connection string.</param>
    /// <returns>A Postgres-provider connection descriptor.</returns>
    public static RelationalDatabaseConnection CreateConnection(string connectionString)
        => new(RelationalDatabaseProvider.Postgres, connectionString);

    /// <summary>
    /// Pre-creates the <c>content_site_index</c> schema ONCE over <paramref name="connectionString"/>
    /// by constructing a schema-ensuring <see cref="ContentSiteIndexStore"/> and calling
    /// <see cref="ContentSiteIndexStore.EnsureSchemaAsync"/> — the production
    /// <c>ProdStoreFactory</c> store runs schema-ensure OFF (D-10), so the schema must already
    /// exist before <see cref="CreateProdStore"/> is used, exactly as the web app's startup path
    /// owns prod schema in production.
    /// </summary>
    /// <param name="connectionString">Raw Postgres connection string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureProdSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var schemaEnsuringStore = new ContentSiteIndexStore(CreateConnection(connectionString), ensureSchemaEnabled: true);
        await schemaEnsuringStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the prod (Postgres, schema-ensure OFF) content-site-index store — the exact shape
    /// <c>ProdStoreFactory.Create</c> uses in production (D-02).
    /// </summary>
    /// <param name="connectionString">Raw Postgres connection string.</param>
    /// <returns>A schema-ensure-OFF Postgres-backed store.</returns>
    public IContentSiteIndexStore CreateProdStore(string connectionString)
        => new ContentSiteIndexStore(CreateConnection(connectionString), ensureSchemaEnabled: false);

    /// <summary>
    /// Builds the local (Studio-side) SQLite content-site-index store this harness instance owns —
    /// the distill + Publish-export SOURCE, distinct from <see cref="CreateProdStore"/> (D-02a).
    /// </summary>
    /// <returns>A SQLite-backed store over this harness's local database file.</returns>
    public IContentSiteIndexStore CreateLocalStore()
        => new ContentSiteIndexStore(_localDbPath);

    /// <summary>
    /// Bootstraps <see cref="RepoRoot"/> as a real git working tree with a deterministic identity
    /// and a local bare <see cref="OriginRoot"/>, then makes an initial commit and pushes it so
    /// <c>origin/{Branch}</c> exists before any coordinator runs (CH1/CH2). <see cref="DeckFlow.Core.Integration.GitRepository"/>
    /// has no <c>init</c>/<c>config</c> members, so this one-time sequence uses a test-only,
    /// <see cref="ProcessStartInfo.ArgumentList"/>-only git shell helper scoped to this file — the
    /// SOLE carve-out to the no-hand-rolled-<see cref="ProcessStartInfo"/> rule (D-03). Every
    /// subsequent stage/commit/push/behind-count in the round-trip loop routes through the real
    /// <see cref="DeckFlow.Core.Integration.GitRepository"/> instead.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitRepoAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.Combine(RepoRoot, "content-kb", "seed"));
        Directory.CreateDirectory(AppRoot);

        // (a) deterministic default branch — never depends on the host's init.defaultBranch config.
        await RunGitBootstrapAsync(RepoRoot, ["init", "-b", Branch], cancellationToken).ConfigureAwait(false);
        // (b) per-repo identity — a bare WSL/CI env has no global git identity configured.
        await RunGitBootstrapAsync(RepoRoot, ["config", "user.name", "DeckFlow RoundTrip Test"], cancellationToken).ConfigureAwait(false);
        await RunGitBootstrapAsync(RepoRoot, ["config", "user.email", "roundtrip-test@deckflow.local"], cancellationToken).ConfigureAwait(false);
        // (c) local bare origin — no network, no real GitHub remote.
        await RunGitBootstrapAsync(Path.GetTempPath(), ["init", "--bare", OriginRoot], cancellationToken).ConfigureAwait(false);
        await RunGitBootstrapAsync(RepoRoot, ["remote", "add", OriginRemote, OriginRoot], cancellationToken).ConfigureAwait(false);

        // (d) initial commit + push so origin/{Branch} EXISTS — a bare repo with no matching ref
        // trips DirectPush's GetSubjectsAheadOfRemoteAsync/PushAsync (DirectPushPushBlockedException).
        var placeholderRelativePath = "content-kb/seed/.gitkeep";
        var placeholderPath = Path.Combine(RepoRoot, "content-kb", "seed", ".gitkeep");
        await File.WriteAllTextAsync(placeholderPath, string.Empty, cancellationToken).ConfigureAwait(false);
        await RunGitBootstrapAsync(RepoRoot, ["add", "--", placeholderRelativePath], cancellationToken).ConfigureAwait(false);
        await RunGitBootstrapAsync(RepoRoot, ["commit", "-m", "initial commit"], cancellationToken).ConfigureAwait(false);
        await RunGitBootstrapAsync(RepoRoot, ["push", "-u", OriginRemote, Branch], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets <see cref="StudioRepoLocator.RepoRootEnvironmentVariable"/> = <see cref="RepoRoot"/> for
    /// the harness lifetime, capturing the prior value so <see cref="Dispose"/> can restore it
    /// (CM3) — <see cref="StudioRepoLocator"/> resolves the repo root from this env var (or CWD),
    /// not from constructor injection, so without it the coordinators would target the wrong tree.
    /// </summary>
    public void SetRepoRootEnv()
    {
        _priorRepoRootEnv = Environment.GetEnvironmentVariable(StudioRepoLocator.RepoRootEnvironmentVariable);
        Environment.SetEnvironmentVariable(StudioRepoLocator.RepoRootEnvironmentVariable, RepoRoot);
        _repoRootEnvSet = true;
    }

    /// <summary>
    /// Copies the committed <c>content-kb/**</c> tree from <see cref="RepoRoot"/> into the distinct
    /// <see cref="AppRoot"/> stand-in directory — simulating Render's git checkout deploy. A plain
    /// filesystem copy (not a second git invocation): the harness always stages+commits before
    /// calling this, so the working tree already reflects the committed state.
    /// </summary>
    public Task DeployToAppAsync()
    {
        var sourceRoot = Path.Combine(RepoRoot, "content-kb");
        var targetRoot = Path.Combine(AppRoot, "content-kb");
        if (Directory.Exists(sourceRoot))
        {
            CopyDirectoryRecursive(sourceRoot, targetRoot);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds an in-memory <see cref="IConfiguration"/> pointing <c>ContentKb:ContentBase</c> at
    /// <see cref="AppRoot"/> (web body resolution reads <c>/app</c>, not <c>/data</c>) with a
    /// fixture-ignored <c>Studio:ProdConnectionString</c> placeholder (mirrors
    /// <c>ReconcileFixtureDriveTests</c>'s configuration construction).
    /// </summary>
    /// <returns>An in-memory configuration for the round-trip coordinators.</returns>
    public IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ContentKb:ContentBase"] = AppRoot,
                ["Studio:ProdConnectionString"] = "fixture-ignored",
            })
            .Build();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_repoRootEnvSet)
        {
            Environment.SetEnvironmentVariable(StudioRepoLocator.RepoRootEnvironmentVariable, _priorRepoRootEnv);
        }

        // Why: release SQLite file handles before deleting so the temp .db file isn't left locked.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_localDbPath))
        {
            File.Delete(_localDbPath);
        }

        foreach (var directory in new[] { RepoRoot, AppRoot, OriginRoot })
        {
            ForceDeleteDirectory(directory);
        }
    }

    // Why: git marks loose object files read-only, so a plain Directory.Delete throws
    // UnauthorizedAccessException on Windows. Clear the read-only attribute on every entry
    // before deleting so temp git-repo teardown never fails the test at Dispose time.
    private static void ForceDeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(directory, recursive: true);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(filePath, Path.Combine(targetDir, Path.GetFileName(filePath)), overwrite: true);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDir))
        {
            CopyDirectoryRecursive(directoryPath, Path.Combine(targetDir, Path.GetFileName(directoryPath)));
        }
    }

    /// <summary>
    /// Test-only git bootstrap runner — the SOLE hand-rolled <see cref="ProcessStartInfo"/> git
    /// invocation permitted in this harness (D-03). <see cref="ProcessStartInfo.ArgumentList"/>-only
    /// (never shell-interpolated), <c>UseShellExecute=false</c>, <c>GIT_TERMINAL_PROMPT=0</c> —
    /// mirrors <see cref="DeckFlow.Core.Integration.GitRepository"/>'s own process-safety pattern.
    /// Used ONLY by <see cref="InitRepoAsync"/> for the one-time init/config/bare-origin/push
    /// sequence; every other git operation in the round-trip loop goes through the real
    /// <see cref="DeckFlow.Core.Integration.GitRepository"/>.
    /// </summary>
    private static async Task RunGitBootstrapAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start git bootstrap process: git {string.Join(' ', arguments)}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        await stdoutTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git bootstrap command 'git {string.Join(' ', arguments)}' exited {process.ExitCode}: {stderr}");
        }
    }
}
