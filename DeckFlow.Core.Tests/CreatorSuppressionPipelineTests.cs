using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Storage;
using DeckFlow.CLI;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

public sealed class CreatorSuppressionPipelineTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _databaseDirectory = Path.Combine(Path.GetTempPath(), $"creator-suppression-pipeline-{Guid.NewGuid():N}");
    private readonly string _databasePath;

    public CreatorSuppressionPipelineTests()
    {
        Directory.CreateDirectory(_databaseDirectory);
        _databasePath = Path.Combine(_databaseDirectory, "creator-suppression-pipeline.db");
    }

    [Fact]
    public async Task CliHarvest_SuppressedCreator_IsNotListedForHarvest()
    {
        var sourceId = await CreateSuppressedSourceAsync();
        var exitCode = await ContentKbCommandRunners.RunHarvestAsync(new FileInfo(_databasePath), 10, false, Serilog.Log.Logger, CancellationToken.None);
        Assert.Equal(0, exitCode);
        Assert.Empty(await new ContentVideoStore(_databasePath).ListVideosPendingDistillAsync(sourceId));
    }

    [Fact]
    public async Task CliDistill_SuppressedCreator_IsNotDistilled()
    {
        var sourceId = await CreateSuppressedSourceAsync();
        await new ContentVideoStore(_databasePath).InsertVideoAsync(sourceId, "suppressed-video", null, "Suppressed video", "https://example.test/video", DateTimeOffset.UtcNow, TranscriptStatus.Pending);
        var exitCode = await ContentKbCommandRunners.RunDistillAsync(new FileInfo(_databasePath), 10, true, Serilog.Log.Logger, CancellationToken.None);
        Assert.Equal(0, exitCode);
        Assert.Equal(TranscriptStatus.Pending, (await new ContentVideoStore(_databasePath).GetVideoByYoutubeIdAsync(sourceId, "suppressed-video"))?.TranscriptStatus);
    }

    [Fact]
    public async Task CliImportStated_SuppressedCreator_SkipsRules()
    {
        await SuppressAsync("suppressed-creator");
        var seedPath = Path.ChangeExtension(_databasePath, ".json");
        await File.WriteAllTextAsync(seedPath, "{\"suppressed-creator\":[{\"category\":\"deckbuilding\",\"metric\":\"ramp\",\"comparator\":\"lte\",\"sourceClip\":\"test\",\"confidence\":0.9,\"videoDateUtc\":\"2026-01-01T00:00:00Z\"}]}");
        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(new FileInfo(seedPath), new FileInfo(_databasePath));
        Assert.Equal(2, exitCode);
        File.Delete(seedPath);
    }

    [Fact]
    public async Task CliFuseProfile_SuppressedCreator_Refuses()
    {
        await SuppressAsync("suppressed-creator");
        Assert.Equal(3, await CreatorStyleCommandRunners.RunFuseProfileAsync("suppressed-creator", new FileInfo(_databasePath)));
    }

    [Fact]
    public async Task CliIndexExport_SuppressedCreator_SkipsProfile()
    {
        await new CreatorStyleProfileStore(_databasePath).UpsertAsync(new CreatorStyleProfile { Slug = "suppressed-creator", Platform = "youtube", MinDecks = 1, UpdatedUtc = DateTimeOffset.UtcNow });
        await SuppressAsync("suppressed-creator");
        var profilesPath = Path.ChangeExtension(_databasePath, ".profiles.json");
        var deckCachePath = Path.ChangeExtension(_databasePath, ".deck-cache.json");
        Assert.Equal(0, await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(new FileInfo(_databasePath), new FileInfo(profilesPath), new FileInfo(deckCachePath)));
        Assert.Equal("[]\n", await File.ReadAllTextAsync(profilesPath));
        File.Delete(profilesPath);
        File.Delete(deckCachePath);
    }

    [Fact]
    public async Task FailClosedCliHarvest_UnreadableSuppressionStore_Refuses()
    {
        await CreateUnreadableSuppressionStoreAsync();
        Assert.NotEqual(0, await ContentKbCommandRunners.RunHarvestAsync(new FileInfo(_databasePath), 10, false, Serilog.Log.Logger, CancellationToken.None));
    }

    [Fact]
    public async Task FailClosedCliDistill_UnreadableSuppressionStore_Refuses()
    {
        await CreateUnreadableSuppressionStoreAsync();
        Assert.NotEqual(0, await ContentKbCommandRunners.RunDistillAsync(new FileInfo(_databasePath), 10, true, Serilog.Log.Logger, CancellationToken.None));
    }

    [Fact]
    public async Task FailClosedCliImportStated_UnreadableSuppressionStore_Refuses()
    {
        var seedPath = Path.ChangeExtension(_databasePath, ".json");
        await File.WriteAllTextAsync(seedPath, "{\"valid-creator\":[{\"category\":\"deckbuilding\",\"metric\":\"ramp\",\"comparator\":\"lte\",\"sourceClip\":\"test\",\"confidence\":0.9,\"videoDateUtc\":\"2026-01-01T00:00:00Z\"}]}");
        await CreateUnreadableSuppressionStoreAsync();
        Assert.Equal(1, await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(new FileInfo(seedPath), new FileInfo(_databasePath)));
        Assert.Empty(await new CreatorStyleStatedRuleStore(_databasePath).GetBySlugAsync("valid-creator"));
        File.Delete(seedPath);
    }

    [Fact]
    public async Task CliImportStated_ValidCreatorWithReadableSuppressionStore_Succeeds()
    {
        var seedPath = Path.ChangeExtension(_databasePath, ".json");
        await File.WriteAllTextAsync(seedPath, "{\"valid-creator\":[{\"category\":\"deckbuilding\",\"metric\":\"ramp\",\"comparator\":\"lte\",\"sourceClip\":\"test\",\"confidence\":0.9,\"videoDateUtc\":\"2026-01-01T00:00:00Z\"}]}");
        Assert.Equal(0, await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(new FileInfo(seedPath), new FileInfo(_databasePath)));
        File.Delete(seedPath);
    }

    [Fact]
    public async Task FailClosedCliFuseProfile_UnreadableSuppressionStore_Refuses()
    {
        await CreateFusableProfileAsync("valid-creator");
        await CreateUnreadableSuppressionStoreAsync();
        Assert.Equal(1, await CreatorStyleCommandRunners.RunFuseProfileAsync("valid-creator", new FileInfo(_databasePath)));
        Assert.Empty((await new CreatorStyleProfileStore(_databasePath).GetBySlugAsync("valid-creator"))!.FusedTargets);
    }

    [Fact]
    public async Task CliFuseProfile_ValidCreatorWithReadableSuppressionStore_Succeeds()
    {
        await CreateFusableProfileAsync("valid-creator");
        Assert.Equal(0, await CreatorStyleCommandRunners.RunFuseProfileAsync("valid-creator", new FileInfo(_databasePath)));
    }

    [Fact]
    public async Task FailClosedCliIndexExport_UnreadableSuppressionStore_Refuses()
    {
        var profilesPath = Path.ChangeExtension(_databasePath, ".profiles.json");
        var deckCachePath = Path.ChangeExtension(_databasePath, ".deck-cache.json");
        await new CreatorStyleProfileStore(_databasePath).UpsertAsync(new CreatorStyleProfile { Slug = "valid-creator", Platform = "youtube", MinDecks = 1, UpdatedUtc = DateTimeOffset.UtcNow });
        await CreateUnreadableSuppressionStoreAsync();
        Assert.Equal(1, await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(new FileInfo(_databasePath), new FileInfo(profilesPath), new FileInfo(deckCachePath)));
        Assert.False(File.Exists(profilesPath));
        Assert.False(File.Exists(deckCachePath));
    }

    [Fact]
    public async Task CliIndexExport_ValidCreatorWithReadableSuppressionStore_Succeeds()
    {
        var profilesPath = Path.ChangeExtension(_databasePath, ".profiles.json");
        var deckCachePath = Path.ChangeExtension(_databasePath, ".deck-cache.json");
        await new CreatorStyleProfileStore(_databasePath).UpsertAsync(new CreatorStyleProfile { Slug = "valid-creator", Platform = "youtube", MinDecks = 1, UpdatedUtc = DateTimeOffset.UtcNow });
        Assert.Equal(0, await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(new FileInfo(_databasePath), new FileInfo(profilesPath), new FileInfo(deckCachePath)));
        File.Delete(profilesPath);
        File.Delete(deckCachePath);
    }

    private Task SuppressAsync(string slug)
        => new CreatorSuppressionStore(RelationalDatabaseConnection.FromSqlitePath(_databasePath)).SuppressAsync(slug, [], "test", DateTimeOffset.UtcNow, null);

    private async Task<long> CreateSuppressedSourceAsync()
    {
        var sourceId = await new ContentSourceStore(_databasePath).InsertSourceAsync("suppressed-creator", "Suppressed Creator", ContentSourceType.Youtube, "https://example.test/channel");
        await SuppressAsync("suppressed-creator");
        return sourceId;
    }

    private async Task CreateUnreadableSuppressionStoreAsync()
    {
        await SuppressAsync("unrelated-creator");
        await using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(_databasePath)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE creator_suppression; CREATE VIEW creator_suppression AS SELECT 'unrelated-creator' AS slug;";
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateFusableProfileAsync(string slug)
    {
        await new CreatorStyleProfileStore(_databasePath).UpsertAsync(CreatorStyleProfileTestData.CreateFullProfile(slug) with { FusedTargets = [] });
        await new CreatorStyleStatedRuleStore(_databasePath).UpsertAsync(new StatedRuleCandidate
        {
            Category = "deckbuilding",
            Metric = "lands",
            Comparator = "lte",
            SourceClip = "test",
            Confidence = 0.9,
            VideoDateUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        }, slug);
    }

    public void Dispose()
    {
        ClearPool(_databasePath);
        ClearPool(ContentKbCliPaths.ResolveCreatorDeckCacheDatabasePath(new FileInfo(_databasePath)));

        if (Directory.Exists(_databaseDirectory))
        {
            Directory.Delete(_databaseDirectory, recursive: true);
        }
    }
}
