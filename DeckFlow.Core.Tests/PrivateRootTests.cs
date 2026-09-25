using DeckFlow.Core.Content;
using DeckFlow.CLI;

namespace DeckFlow.Core.Tests;

public sealed class PrivateRootTests
{
    [Fact]
    public void Constructor_UnsetRoot_ThrowsNamingEnvironmentVariable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(null));

        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_EmptyRoot_ThrowsNamingEnvironmentVariable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(string.Empty));

        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ValidRoot_ResolvesAllHelperPaths()
    {
        using var root = new TempDirectory();

        var result = new PrivateKbRoot(root.Path);

        Assert.Equal(Path.GetFullPath(root.Path), result.Root);
        Assert.Equal(Path.Combine(result.Root, "content-kb"), result.ContentKbDir);
        Assert.Equal(Path.Combine(result.Root, "content-kb", "seed", "index-seed.json"), result.SeedFile);
        Assert.Equal(Path.Combine(result.Root, "creator-style-seed"), result.CreatorStyleSeedDir);
    }

    [Fact]
    public void Constructor_RootInsideCheckoutWithGitDirectory_Throws()
    {
        using var root = new TempDirectory();
        File.WriteAllText(Path.Combine(root.Path, "DeckFlow.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(root.Path, ".git"));
        var nestedRoot = Directory.CreateDirectory(Path.Combine(root.Path, "artifacts"));

        Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(nestedRoot.FullName));
    }

    [Fact]
    public void Constructor_RootInsideCheckoutWithGitWorktreeFile_Throws()
    {
        using var root = new TempDirectory();
        File.WriteAllText(Path.Combine(root.Path, "DeckFlow.sln"), string.Empty);
        File.WriteAllText(Path.Combine(root.Path, ".git"), "gitdir: elsewhere");

        Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(root.Path));
    }

    [Fact]
    public void Constructor_RootWithStraySolutionAndNoGit_AcceptsRoot()
    {
        using var root = new TempDirectory();
        File.WriteAllText(Path.Combine(root.Path, "DeckFlow.sln"), string.Empty);

        var result = new PrivateKbRoot(root.Path);

        Assert.Equal(Path.GetFullPath(root.Path), result.Root);
    }

    [Fact]
    public void Constructor_SymlinkOutsideCheckoutPointingIntoCheckout_Throws()
    {
        using var checkout = new TempDirectory();
        using var outside = new TempDirectory();
        File.WriteAllText(Path.Combine(checkout.Path, "DeckFlow.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(checkout.Path, ".git"));
        var target = Directory.CreateDirectory(Path.Combine(checkout.Path, "artifacts"));
        var linkPath = Path.Combine(outside.Path, "artifacts-link");
        Directory.CreateSymbolicLink(linkPath, target.FullName);

        Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(linkPath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

public sealed class CliPrivateRootDefaultsTests
{
    [Fact]
    public async Task ContentIndexExport_DefaultOutputUnsetRoot_FailsClosed()
    {
        using var root = new TempDirectory();

        var exitCode = await ContentKbCommandRunners.RunContentIndexExportAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            output: null,
            environmentVariableGetter: _ => null);

        Assert.NotEqual(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(root.Path, "content-kb")));
    }

    [Fact]
    public async Task CreatorStyleImportStated_DefaultFileUnsetRoot_FailsClosed()
    {
        using var root = new TempDirectory();

        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(
            file: null,
            db: new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            environmentVariableGetter: _ => null);

        Assert.NotEqual(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(root.Path, "content-kb")));
    }

    [Fact]
    public async Task CreatorStyleIndexExport_DefaultOutputsUnsetRoot_FailsClosed()
    {
        using var root = new TempDirectory();

        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            profilesOutput: null,
            deckCacheOutput: null,
            environmentVariableGetter: _ => null);

        Assert.NotEqual(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(root.Path, "content-kb")));
    }

    [Fact]
    public async Task Distill_DefaultArtifactRootUnset_FailsClosed()
    {
        using var root = new TempDirectory();

        var exitCode = await ContentKbCommandRunners.RunDistillAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            limit: 1,
            dryRun: true,
            Serilog.Log.Logger,
            CancellationToken.None,
            environmentVariableGetter: _ => null);

        Assert.NotEqual(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(root.Path, "content-kb")));
    }

    [Fact]
    public async Task CreatorStyleImportStated_ExplicitFileDoesNotReadRoot()
    {
        using var root = new TempDirectory();
        var rootWasRead = false;

        await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(
            new FileInfo(Path.Combine(root.Path, "seed.json")),
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            _ =>
            {
                rootWasRead = true;
                return null;
            });

        Assert.False(rootWasRead);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
