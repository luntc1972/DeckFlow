using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using DeckFlow.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Guards the Cut Lab commander-floors dark-launch gate across rendering and service orchestration.</summary>
public sealed class CutLabCommanderFloorsFlagTests
{
    [Fact]
    public async Task RenderAsync_CommanderFloorsFlagOff_OmitsCommanderColumnsAndMarker()
    {
        string html = await RenderAsync(BuildModel(commanderFloorsEnabled: false), new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            [CutLabPageService.CommanderFloorsFlagKey] = false,
        }));

        Assert.DoesNotContain("<th scope=\"col\">Bracket</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Commander</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-cut-lab-commander-floors", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Floor</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Source</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_CommanderFloorsFlagOn_RendersCommanderColumnsAndMarker()
    {
        string html = await RenderAsync(BuildModel(commanderFloorsEnabled: true), new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            [CutLabPageService.CommanderFloorsFlagKey] = true,
        }));

        Assert.Contains("<th scope=\"col\">Bracket</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Commander</th>", html, StringComparison.Ordinal);
        Assert.Contains("data-cut-lab-commander-floors=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Floor</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Source</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_CommanderFloorsFlagOff_IgnoresCommanderRoleFloorBaseline()
    {
        var entries = BuildPoolEntries(nonCommanderCount: 120, commanderName: "Focused Commander");
        var cards = BuildResolvedCards(entries);
        var roleFloorBaseline = new FakeRoleFloorBaselineProvider(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["engines"] = 9,
            ["payoffs"] = 8,
        });
        var service = new CutLabPageService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeBanListService([]),
            manabaseBaseline: new FakeManabaseBaselineProvider(new ManabaseBracketBaseline
            {
                Bracket = 4,
                AvgLands = 36.0,
                DeckCount = 100,
            }),
            cedhBaseline: new FakeCedhLandBaselineProvider(),
            roleFloorBaseline: roleFloorBaseline,
            logger: NullLogger<CutLabPageService>.Instance,
            featureFlags: new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [CutLabPageService.CommanderFloorsFlagKey] = false,
            }));
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            SelectedCommander = "Focused Commander",
            Bracket = 4,
            PlayExperience = "Focused",
        };

        CutLabProcessResult result = await service.ProcessAsync(request);
        IReadOnlyList<CutLabResolvedFloor> expected = CutLabFloorDefaults.ResolveDefaults(
            declaredBracket: 4,
            playExperience: "Focused",
            commanderManaValue: 3.0,
            commanderNames: ["Focused Commander"],
            baseline: new FakeManabaseBaselineProvider(new ManabaseBracketBaseline
            {
                Bracket = 4,
                AvgLands = 36.0,
                DeckCount = 100,
            }),
            cedhBaseline: new FakeCedhLandBaselineProvider(),
            roleFloorBaseline: null,
            priorFloors: []);

        Assert.False(result.CommanderFloorsEnabled);
        Assert.Empty(roleFloorBaseline.QueriedRoles);
        Assert.Equal(expected.Count, result.ResolvedFloors.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Role, result.ResolvedFloors[i].Role);
            Assert.Equal(expected[i].Floor, result.ResolvedFloors[i].Floor);
            Assert.Equal(expected[i].DefaultValue, result.ResolvedFloors[i].DefaultValue);
            Assert.Equal(expected[i].BracketValue, result.ResolvedFloors[i].BracketValue);
            Assert.Equal(expected[i].CommanderValue, result.ResolvedFloors[i].CommanderValue);
        }
    }

    private static CutLabViewModel BuildModel(bool commanderFloorsEnabled)
        => new()
        {
            ActiveTab = DeckPageTab.CutLab,
            HasResult = true,
            IsLegal = true,
            Request = new CutLabRequest
            {
                SelectedCommander = "Focused Commander",
                Bracket = 4,
                PlayExperience = "Focused",
            },
            FloorRows =
            [
                new CutLabFloorRowView
                {
                    RoleKey = "engines",
                    DisplayLabel = "Engines",
                    InPoolCount = 7,
                    BracketValue = 6,
                    CommanderValue = 9,
                    SupportsCommanderFloor = true,
                    CommanderDisplay = "9",
                    Floor = commanderFloorsEnabled ? 9 : 6,
                    DefaultValue = commanderFloorsEnabled ? 9 : 6,
                    SourceLabel = commanderFloorsEnabled ? "Commander" : "Bracket",
                    SourceDetail = commanderFloorsEnabled
                        ? "Commander baseline raised this default."
                        : "Bracket baseline set this default.",
                },
            ],
            CommanderFloorsEnabled = commanderFloorsEnabled,
        };

    private static async Task<string> RenderAsync(CutLabViewModel model, IFeatureFlagCache flagCache)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton(flagCache);
        services.AddControllersWithViews().AddApplicationPart(typeof(CutLabController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "CutLab", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'CutLab' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View!,
            viewData,
            new TempDataDictionary(httpContext, new StubTempDataProvider()),
            writer,
            new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var contentRoot = AppContext.BaseDirectory;
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(CutLabController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private static List<DeckEntry> BuildPoolEntries(int nonCommanderCount, string commanderName)
    {
        var entries = new List<DeckEntry> { Entry(commanderName, "commander") };
        entries.AddRange(Enumerable.Range(1, nonCommanderCount).Select(index => Entry($"Card {index:000}", "mainboard")));
        return entries;
    }

    private static List<ScryfallCard> BuildResolvedCards(IEnumerable<DeckEntry> entries)
        => entries.Select(entry => string.Equals(entry.Name, "Focused Commander", StringComparison.Ordinal)
            ? Spell(entry.Name, "Legendary Creature — Human Wizard", manaCost: "{1}{G}{U}", cmc: 3)
            : Spell(entry.Name, "Artifact", manaCost: "{2}", cmc: 2))
            .ToList();

    private static DeckEntry Entry(string name, string board)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = board,
        };

    private static ScryfallCard Spell(
        string name,
        string typeLine,
        string? manaCost = null,
        double cmc = 0)
        => new(
            name,
            manaCost,
            typeLine,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Cmc: cmc);

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }

    private sealed class FakeLoader(List<DeckEntry> entries) : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(entries, null));

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
            => throw new NotSupportedException();
    }

    private sealed class FakeResolver(IReadOnlyList<ScryfallCard> cards) : IScryfallCardResolver
    {
        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(cards.ToList(), []),
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);
    }

    private sealed class FakeBanListService(IReadOnlyList<string> bannedCards) : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(bannedCards);
    }

    private sealed class FakeManabaseBaselineProvider(ManabaseBracketBaseline? baseline = null) : IManabaseBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
            => baseline;

        public ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames)
            => null;
    }

    private sealed class FakeCedhLandBaselineProvider : ICedhLandBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double mean, out int n, out double sd, out string? generated)
        {
            mean = default;
            n = default;
            sd = default;
            generated = default;
            return false;
        }
    }

    private sealed class FakeRoleFloorBaselineProvider(IReadOnlyDictionary<string, int>? floorsByRole = null) : IRoleFloorBaselineProvider
    {
        private readonly IReadOnlyDictionary<string, int> _floorsByRole =
            floorsByRole ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _queriedRoles = [];

        internal IReadOnlyList<string> QueriedRoles => _queriedRoles;

        public void EnsureLoaded()
        {
        }

        public bool TryGetRoleFloor(IReadOnlyList<string> commanderNames, string role, out int floor)
        {
            _queriedRoles.Add(role);
            return _floorsByRole.TryGetValue(role, out floor);
        }
    }
}
