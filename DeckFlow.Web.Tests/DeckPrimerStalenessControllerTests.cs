using System.IO.Compression;
using System.Text.RegularExpressions;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckPrimerStalenessControllerTests
{
    private const string DeckV1Text = "sentinel-deck-v1";
    private const string DeckV2Text = "sentinel-deck-v2";

    [Fact]
    public async Task FlagOff_DoesNotStampStalenessStateOrPersistHash()
    {
        var loadCallCount = 0;
        var service = CreateService(source =>
        {
            loadCallCount++;
            return EntriesForSource(source);
        });
        var controller = CreateController(service, staleFlagEnabled: false);
        var request = CreateRequest(DeckV1Text);

        var postModel = await PostModel(controller, request);
        var download = Assert.IsType<FileContentResult>(await controller.DeckPrimerDownload(request));
        var uploadModel = await UploadModel(controller, download.FileContents, new DeckPrimerRequest
        {
            DeckText = DeckV2Text
        });

        Assert.False(postModel.StaleDetectionEnabled);
        Assert.Null(postModel.GeneratedPrimerHash);
        Assert.False(postModel.IsStale);
        Assert.Null(postModel.ChangedCardCount);
        Assert.False(uploadModel.StaleDetectionEnabled);
        Assert.Null(uploadModel.GeneratedPrimerHash);
        Assert.False(uploadModel.IsStale);
        Assert.Null(uploadModel.ChangedCardCount);
        Assert.DoesNotContain("02-primer-deck-hash.txt", ZipEntryNames(download.FileContents));
        Assert.True(loadCallCount > 0);
    }

    [Fact]
    public async Task DeckPrimerDownload_UsesDeckNamePrefix_WhenPresent()
    {
        var service = CreateService(EntriesForSource);
        var controller = CreateController(service, staleFlagEnabled: false);
        var request = CreateRequest(DeckV1Text);
        request.DeckName = "  Primer Deck  ";

        var download = Assert.IsType<FileContentResult>(await controller.DeckPrimerDownload(request));

        Assert.Matches(new Regex(@"^primer-deck-primer-chatgpt-\d{8}-\d{6}\.zip$"), download.FileDownloadName);
    }

    [Fact]
    public async Task DeckPrimerDownload_UsesCommanderPrefix_WhenDeckNameBlank()
    {
        var service = CreateService(EntriesForSource);
        var controller = CreateController(service, staleFlagEnabled: false);
        var request = CreateRequest(DeckV1Text);
        request.DeckName = "   ";

        var download = Assert.IsType<FileContentResult>(await controller.DeckPrimerDownload(request));

        Assert.Matches(new Regex(@"^atraxa--praetors--voice-primer-chatgpt-\d{8}-\d{6}\.zip$"), download.FileDownloadName);
    }

    [Fact]
    public async Task FlagOn_PostRearmsFreshHash()
    {
        var service = CreateService(EntriesForSource);
        var controller = CreateController(service, staleFlagEnabled: true);

        var model = await PostModel(controller, CreateRequest(DeckV1Text));

        Assert.True(model.StaleDetectionEnabled);
        Assert.False(model.IsStale);
        Assert.Null(model.ChangedCardCount);
        Assert.Equal(Hash(EntriesV1()), model.GeneratedPrimerHash);
    }

    [Fact]
    public async Task FlagOn_UploadChangedCurrentDeck_RendersRestoredPrimerAndMarksStaleWithoutRebuild()
    {
        var loadCallCount = 0;
        var service = CreateService(source =>
        {
            loadCallCount++;
            return EntriesForSource(source);
        });
        var controller = CreateController(service, staleFlagEnabled: true);
        var request = CreateRequest(DeckV1Text);
        var generatedModel = await PostModel(controller, request);
        var download = Assert.IsType<FileContentResult>(await controller.DeckPrimerDownload(request));
        var callsBeforeUpload = loadCallCount;

        var uploadModel = await UploadModel(controller, download.FileContents, new DeckPrimerRequest
        {
            DeckText = DeckV2Text,
            TargetAiPlatform = AiPlatform.ChatGpt.Key
        });

        Assert.True(uploadModel.StaleDetectionEnabled);
        Assert.Equal(generatedModel.GeneratedPrimerHash, uploadModel.GeneratedPrimerHash);
        Assert.True(uploadModel.IsStale);
        Assert.Equal(3, uploadModel.ChangedCardCount);
        Assert.Equal(generatedModel.PrimerPromptText, uploadModel.PrimerPromptText);
        Assert.Equal(callsBeforeUpload, loadCallCount);
    }

    [Fact]
    public async Task FlagOn_UploadWithoutCurrentDeck_RestoresSnapshotAndSuppressesStaleBanner()
    {
        var service = CreateService(EntriesForSource);
        var controller = CreateController(service, staleFlagEnabled: true);
        var request = CreateRequest(DeckV1Text);
        var generatedModel = await PostModel(controller, request);
        var download = Assert.IsType<FileContentResult>(await controller.DeckPrimerDownload(request));

        var uploadModel = await UploadModel(controller, download.FileContents, new DeckPrimerRequest());

        Assert.True(uploadModel.StaleDetectionEnabled);
        Assert.Equal(generatedModel.GeneratedPrimerHash, uploadModel.GeneratedPrimerHash);
        Assert.False(uploadModel.IsStale);
        Assert.Null(uploadModel.ChangedCardCount);
        Assert.Equal(DeckInputSource.PasteText, uploadModel.Request.DeckInputSource);
        Assert.Equal(generatedModel.PrimerPromptText, uploadModel.Request.DeckText);
        Assert.Equal(generatedModel.PrimerPromptText, uploadModel.PrimerPromptText);
    }

    [Fact]
    public async Task FlagOn_UploadOldZipWithoutHash_RendersPrimerAndSuppressesStaleBanner()
    {
        var service = CreateService(EntriesForSource);
        var controller = CreateController(service, staleFlagEnabled: true);
        var request = CreateRequest(DeckV1Text);
        var generatedModel = await PostModel(controller, request);
        var oldZipBytes = PacketArtifactStore.BuildPrimerZip(
            request,
            generatedModel.InputSummary!,
            BuildRequestContext(request),
            generatedModel.PrimerPromptText,
            null,
            null,
            generatedModel.PrimerPromptText,
            DeckV1Text);

        var uploadModel = await UploadModel(controller, oldZipBytes, new DeckPrimerRequest
        {
            DeckText = DeckV2Text
        });

        Assert.True(uploadModel.StaleDetectionEnabled);
        Assert.Null(uploadModel.GeneratedPrimerHash);
        Assert.False(uploadModel.IsStale);
        Assert.Null(uploadModel.ChangedCardCount);
        Assert.Equal(generatedModel.PrimerPromptText, uploadModel.PrimerPromptText);
    }

    private static async Task<DeckPrimerViewModel> PostModel(DeckPrimerController controller, DeckPrimerRequest request)
    {
        var result = await controller.DeckPrimer(request);
        return ViewModel(result);
    }

    private static async Task<DeckPrimerViewModel> UploadModel(DeckPrimerController controller, byte[] zipBytes, DeckPrimerRequest request)
    {
        var result = await controller.DeckPrimerUpload(CreateFormFile(zipBytes), request);
        return ViewModel(result);
    }

    private static DeckPrimerViewModel ViewModel(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<DeckPrimerViewModel>(view.Model);
    }

    private static DeckPrimerController CreateController(DeckPrimerPacketService service, bool staleFlagEnabled)
        => new(
            service,
            new PacketSessionCache(),
            NullLogger<DeckPrimerController>.Instance,
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [DeckPrimerPacketService.StaleFlag] = staleFlagEnabled
            }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private static DeckPrimerPacketService CreateService(Func<string, IReadOnlyList<DeckEntry>> loadEntries)
        => new(
            new PrimerPromptVariantRegistry([new TestPrimerPromptVariant(AiPlatform.ChatGpt)]),
            new PacketSessionCache(),
            loadDeckEntriesAsyncOverride: (source, _) => Task.FromResult(loadEntries(source).ToList()),
            findCombosAsyncOverride: (_, _) => Task.FromResult<CommanderSpellbookResult?>(null),
            getTopArchetypesAsyncOverride: (_, _) => Task.FromResult<IReadOnlyList<EdhTop16Entry>>([]),
            getCategoryRowsForCommanderAsyncOverride: (_, _) => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>([]),
            parseDeckTextLocalOverride: ParseDeckTextLocal);

    private static IReadOnlyList<DeckEntry> EntriesForSource(string source)
        => string.Equals(source, DeckV2Text, StringComparison.Ordinal)
            ? EntriesV2()
            : EntriesV1();

    private static IReadOnlyList<DeckEntry>? ParseDeckTextLocal(string text)
    {
        if (string.Equals(text, DeckV2Text, StringComparison.Ordinal))
        {
            return EntriesV2();
        }

        if (string.Equals(text, DeckV1Text, StringComparison.Ordinal)
            || text.Contains("Atraxa, Praetors' Voice", StringComparison.Ordinal))
        {
            return EntriesV1();
        }

        return null;
    }

    private static DeckPrimerRequest CreateRequest(string deckText)
        => new()
        {
            DeckText = deckText,
            TargetCommanderBracket = "Upgraded",
            TargetAiPlatform = AiPlatform.ChatGpt.Key,
            SelectedSectionIds = ["verified-combos"]
        };

    private static IReadOnlyList<DeckEntry> EntriesV1()
        =>
        [
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 2),
            CreateDeckEntry("Swords to Plowshares", "mainboard")
        ];

    private static IReadOnlyList<DeckEntry> EntriesV2()
        =>
        [
            CreateDeckEntry("Atraxa, Praetors' Voice", "commander"),
            CreateDeckEntry("Arcane Signet", "mainboard", quantity: 1),
            CreateDeckEntry("Cyclonic Rift", "mainboard")
        ];

    private static DeckEntry CreateDeckEntry(string name, string board, int quantity = 1)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = board
        };

    private static string Hash(IReadOnlyList<DeckEntry> entries)
        => PacketSessionCache.ComputeKey(DeckPrimerPacketService.BuildCanonicalDeckSourceText(entries));

    private static IFormFile CreateFormFile(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "zipFile", "primer.zip");
    }

    private static IReadOnlyList<string> ZipEntryNames(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.Select(entry => entry.FullName).ToList();
    }

    private static string BuildRequestContext(DeckPrimerRequest request)
        => $"""
            workflow_step: {request.WorkflowStep}
            target_commander_bracket: {request.TargetCommanderBracket}
            target_ai_platform: {request.TargetAiPlatform}
            primer_style: {request.PrimerStyle}
            selected_section_ids:
            - verified-combos
            """;

    private sealed class TestPrimerPromptVariant : IPrimerPromptVariant
    {
        public TestPrimerPromptVariant(AiPlatform platform)
        {
            Platform = platform;
        }

        public AiPlatform Platform { get; }

        public string Build(
            DeckPrimerRequest request,
            string decklistText,
            IReadOnlyList<PrimerSectionEntry> selectedSections,
            CommanderSpellbookResult? comboResult,
            IReadOnlyList<EdhTop16Entry>? top16Entries,
            CategoryDistributionSummary? categoryDistribution,
            int bracketNumber,
            CancellationToken cancellationToken = default)
        {
            return decklistText;
        }
    }
}
