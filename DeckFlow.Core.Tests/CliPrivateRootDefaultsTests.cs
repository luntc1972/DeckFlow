using DeckFlow.Core.Content;
using DeckFlow.CLI;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

public sealed class CliPrivateRootDefaultsTests
{
    private static readonly SemaphoreSlim ConsoleErrorLock = new(1, 1);

    [Fact]
    public async Task ContentIndexExport_DefaultOutputUnsetRoot_FailsClosed()
    {
        using var root = new TempDirectory();
        var contentKbExisted = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));

        var (exitCode, error) = await CaptureStandardErrorAsync(() => ContentKbCommandRunners.RunContentIndexExportAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            output: null,
            environmentVariableGetter: _ => null));

        Assert.NotEqual(0, exitCode);
        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, error, StringComparison.Ordinal);
        AssertCurrentDirectoryContentKbUnchanged(contentKbExisted);
    }

    [Fact]
    public async Task CreatorStyleImportStated_DefaultFileUnsetRoot_FailsClosed()
    {
        using var root = new TempDirectory();
        var contentKbExisted = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));

        var (exitCode, error) = await CaptureStandardErrorAsync(() => CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(
            file: null,
            db: new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            environmentVariableGetter: _ => null));

        Assert.NotEqual(0, exitCode);
        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, error, StringComparison.Ordinal);
        AssertCurrentDirectoryContentKbUnchanged(contentKbExisted);
    }

    [Fact]
    public async Task CreatorStyleIndexExport_DefaultOutputsUnsetRoot_FailsClosed()
    {
        using var root = new TempDirectory();
        var contentKbExisted = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));

        var (exitCode, error) = await CaptureStandardErrorAsync(() => CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            profilesOutput: null,
            deckCacheOutput: null,
            environmentVariableGetter: _ => null));

        Assert.NotEqual(0, exitCode);
        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, error, StringComparison.Ordinal);
        AssertCurrentDirectoryContentKbUnchanged(contentKbExisted);
    }

    [Fact]
    public async Task Distill_DefaultArtifactRootUnset_FailsClosed()
    {
        using var root = new TempDirectory();
        var contentKbExisted = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));

        var (exitCode, error) = await CaptureStandardErrorAsync(() => ContentKbCommandRunners.RunDistillAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            limit: 1,
            dryRun: true,
            Serilog.Log.Logger,
            CancellationToken.None,
            environmentVariableGetter: _ => null));

        Assert.NotEqual(0, exitCode);
        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, error, StringComparison.Ordinal);
        AssertCurrentDirectoryContentKbUnchanged(contentKbExisted);
    }

    [Fact]
    public async Task ContentKbCheck_DefaultArtifactRootUnset_FailsClosed()
    {
        using var root = new TempDirectory();
        var contentKbExisted = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "content-kb"));

        var (exitCode, error) = await CaptureStandardErrorAsync(() => ContentKbCommandRunners.RunContentKbCheckAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            artifactRoot: null,
            environmentVariableGetter: _ => null));

        Assert.NotEqual(0, exitCode);
        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, error, StringComparison.Ordinal);
        AssertCurrentDirectoryContentKbUnchanged(contentKbExisted);
    }

    [Fact]
    public async Task Harvest_UnsetRoot_DoesNotFailBecauseOfPrivateRoot()
    {
        using var root = new TempDirectory();

        var (_, error) = await CaptureStandardErrorAsync(() => ContentKbCommandRunners.RunHarvestAsync(
            new FileInfo(Path.Combine(root.Path, "content-kb.db")),
            limit: 1,
            enableWhisper: false,
            Serilog.Log.Logger,
            CancellationToken.None));

        SqliteConnection.ClearAllPools();
        Assert.DoesNotContain(PrivateKbRoot.EnvironmentVariableName, error, StringComparison.Ordinal);
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

    private static async Task<(int ExitCode, string Error)> CaptureStandardErrorAsync(Func<Task<int>> action)
    {
        await ConsoleErrorLock.WaitAsync();
        var originalError = Console.Error;
        using var writer = new StringWriter();
        try
        {
            Console.SetError(writer);
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            ConsoleErrorLock.Release();
        }
    }

    private static void AssertCurrentDirectoryContentKbUnchanged(bool existedBefore)
        => Assert.Equal(existedBefore, Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "content-kb")));

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
