using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Manabase;
using Xunit;

namespace DeckFlow.Web.Tests.Manabase;

/// <summary>
/// Regression guard for the health-band/castability coupling fix (debug session
/// manabase-health-band-coupling, Gate C). The Avatar (Sokka/Aang Jeskai) fixture is the
/// calibration deck: with the flag OFF the band is "Solid" (no color issue fires because
/// White's Karsten source count is generous); with the flag ON White counts as an issue
/// because Suki, Courageous Rescuer is color:White-limited below the 80% threshold AND
/// ColorLimitedUnderSupportedCount >= 1 (Gate C condition).
///
/// The test reads a committed Avatar facts fixture directly — no HTTP — so it runs in CI
/// without any network dependency.
/// </summary>
public sealed class ManabaseHealthBandRegressionTests
{
    // Path to the committed Avatar facts fixture (copied next to the test assembly via the
    // csproj Content item, so it is present in CI with no network dependency). Re-derives
    // ManaAmount from OracleText on load (same as the baseline harness) to survive older
    // cache formats.
    private static readonly string FactsCachePath =
        Path.Combine(AppContext.BaseDirectory, "Manabase", "avatar-facts.json");

    private static readonly IReadOnlyList<CalibrationDeck> CalibrationDecks =
    [
        new("Stale Brago (WU control)", ".manabase-brago-facts.json", "Needs work", "Needs work"),
        new("Kenrith 5-color rocks", ".manabase-5c-facts.json", "Excellent", "Excellent"),
        new("Meren Golgari ramp/ritual", ".manabase-golgari-facts.json", "Solid", "Solid"),
        new("Avatar - Sokka/Aang", "avatar-facts.json", "Solid", "Solid", IsAssemblyFixture: true),
        new("Archidekt 23563520 - Marchesa", ".manabase-arch-23563520-facts.json", "Needs work", "Needs work"),
        new("Archidekt 23753514 - graveyard fungus", ".manabase-arch-23753514-facts.json", "Solid", "Solid"),
        new("Archidekt 23638601 - Townos", ".manabase-arch-23638601-facts.json", "Excellent", "Excellent"),
        new("Archidekt 8066726 - The Necrobloom", ".manabase-arch-8066726-facts.json", "Needs work", "Needs work"),
        new("Archidekt 7084567 - army now", ".manabase-arch-7084567-facts.json", "Needs work", "Needs work"),
    ];

    private static readonly CalibrationDeck BragoPromoteDeck =
        new("Brago promote (WU control)", ".manabase-brago-promote-facts.json", "Needs work", "Workable");

    [Fact]
    public async Task Avatar_FlagOff_BandIsSolid()
    {
        IReadOnlyList<CardFact> facts = await LoadFactsAsync();
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandCastability: false);

        string label = ManabaseDisplay.HealthLabel(report.Health);
        Assert.Equal("Solid", label);
    }

    [Fact]
    public async Task Avatar_FlagOn_BandIsWorkable_WeakestColorWhite()
    {
        IReadOnlyList<CardFact> facts = await LoadFactsAsync();
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandCastability: true);

        string label = ManabaseDisplay.HealthLabel(report.Health);
        Assert.Equal("Workable", label);

        // Weakest color must be White (the sim-identified tight color).
        ColorSourceFinding? weakest = report.ColorFindings.Count > 0 ? report.ColorFindings[0] : null;
        Assert.NotNull(weakest);
        Assert.Equal(ManaColor.White, weakest!.Color);
    }

    public static IEnumerable<object[]> HeadlineFloorCalibrationCases() =>
        CalibrationDecks.Select(d => new object[] { d });

    [Theory]
    [MemberData(nameof(HeadlineFloorCalibrationCases))]
    public async Task HealthBandHeadlineFloor_FlagOffVersusFlagOn_CalibrationDecks(CalibrationDeck calibration)
    {
        IReadOnlyList<CardFact> facts = await LoadFactsAsync(calibration);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);

        ManabaseReport off = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandHeadlineFloor: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandHeadlineFloor: true);

        string offLabel = ManabaseDisplay.HealthLabel(off.Health);
        string onLabel = ManabaseDisplay.HealthLabel(on.Health);
        Assert.True(offLabel == calibration.FlagOffLabel,
            $"{calibration.Name} flag OFF expected {calibration.FlagOffLabel}, got {offLabel}; "
            + $"avg {off.AvgOnCurvePercent}, worst {off.WorstColorCastPercent:F0}");
        Assert.True(onLabel == calibration.FlagOnLabel,
            $"{calibration.Name} flag ON expected {calibration.FlagOnLabel}, got {onLabel}; "
            + $"avg {on.AvgOnCurvePercent}, worst {on.WorstColorCastPercent:F0}");
    }

    [Fact]
    public async Task HealthBandHeadlineFloor_BragoPromoteFixture_FlagOffNeedsWork_FlagOnWorkable()
    {
        IReadOnlyList<CardFact> facts = await LoadFactsAsync(BragoPromoteDeck);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);

        ManabaseReport off = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandHeadlineFloor: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandHeadlineFloor: true);

        Assert.Equal("Needs work", ManabaseDisplay.HealthLabel(off.Health));
        Assert.Equal("Workable", ManabaseDisplay.HealthLabel(on.Health));
    }

    [Fact]
    public async Task HealthBandHeadlineFloor_StaleBragoSevereDeficit_StaysNeedsWork()
    {
        CalibrationDeck stale = CalibrationDecks.Single(d => d.Name.StartsWith("Stale Brago", StringComparison.Ordinal));
        IReadOnlyList<CardFact> facts = await LoadFactsAsync(stale);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandHeadlineFloor: true);

        Assert.Contains(report.ColorFindings, f => f.Deficit > 2);
        Assert.Equal("Needs work", ManabaseDisplay.HealthLabel(report.Health));
    }

    [Fact]
    public void HealthBandHeadlineFloor_BothFlagsOn_DoesNotOverrideTwoColorHardFail()
    {
        ManabaseReport report = SyntheticReport(
            true,
            true,
            new ColorSourceFinding
            {
                Color = ManaColor.Blue,
                ActualSources = 30,
                RequiredSources = 24,
                DrivingSpell = "Sim weak",
                WorstSpell = "Sim weak",
                WorstSpellCastPercent = 70,
                UnderSupportedCount = 1,
                ColorLimitedUnderSupportedCount = 1,
            },
            new ColorSourceFinding
            {
                Color = ManaColor.White,
                ActualSources = 23.5,
                RequiredSources = 25,
                DrivingSpell = "Raw short",
                WorstSpell = "Raw short",
                WorstSpellCastPercent = 90,
            });

        Assert.Equal(90, report.AvgOnCurvePercent);
        Assert.Equal(70, report.WorstColorCastPercent);
        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void HealthBandHeadlineFloor_BroadUnderSupport_StaysNeedsWork()
    {
        ManabaseReport report = SyntheticReport(
            false,
            true,
            new ColorSourceFinding
            {
                Color = ManaColor.White,
                ActualSources = 23.5,
                RequiredSources = 25,
                DrivingSpell = "Raw short",
                WorstSpell = "Raw short",
                WorstSpellCastPercent = 90,
                UnderSupportedCount = 9,
                ColorLimitedUnderSupportedCount = 9,
            });

        Assert.Equal(90, report.AvgOnCurvePercent);
        Assert.Equal(90, report.WorstColorCastPercent);
        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public async Task HealthBandHeadlineFloor_BragoPromotion_CouplesRampAndPrimaryFix()
    {
        IReadOnlyList<CardFact> facts = await LoadFactsAsync(BragoPromoteDeck);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandHeadlineFloor: true);

        Assert.Equal("Workable", ManabaseDisplay.HealthLabel(report.Health));
        Assert.True(report.LandShortfallCoveredByRamp);
        Assert.NotEqual(ManabaseFixKind.Lands, report.PrimaryFix.Kind);
    }

    [Fact]
    public async Task ResolveBragoPromoteFactsCache()
    {
        if (!HarnessEnabled())
        {
            return;
        }

        string deckPath = Path.Combine(RepoRoot(), ".planning", "debug", "manabase-brago-promote-deck.txt");
        string cachePath = Path.Combine(RepoRoot(), "DeckFlow.Web.Tests", "Manabase", "fixtures", BragoPromoteDeck.FactsFile);
        string list = await File.ReadAllTextAsync(deckPath);
        IReadOnlyList<DeckCardEntry> entries = await ResolveAsync(ParseDeck(list));
        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(entries).ToList();
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(facts));

        Assert.True(File.Exists(cachePath));
    }

    [Fact]
    public async Task DumpHeadlineFloorMeasurements()
    {
        if (!HarnessEnabled())
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("| Deck | Avg | WorstColor | MaxColorLimited | MaxUnderSupported | BroadColorUnderSupport | AnySevereColorDeficit | MaxColorDeficit | Flag OFF | Flag ON |");
        sb.AppendLine("|---|---:|---:|---:|---:|---|---|---:|---|---|");

        AppendMeasurement(sb, BragoPromoteDeck, await LoadFactsAsync(BragoPromoteDeck));
        foreach (CalibrationDeck calibration in CalibrationDecks)
        {
            AppendMeasurement(sb, calibration, await LoadFactsAsync(calibration));
        }

        string table = sb.ToString();
        System.Console.WriteLine(table);
    }

    private static async Task<IReadOnlyList<CardFact>> LoadFactsAsync()
    {
        Assert.True(File.Exists(FactsCachePath),
            $"Avatar facts cache not found at {FactsCachePath}. Run the baseline harness once to populate it.");

        List<CardFact> facts = JsonSerializer.Deserialize<List<CardFact>>(
            await File.ReadAllTextAsync(FactsCachePath))!;

        // Re-derive ManaAmount from oracle text: older caches may predate this field.
        return facts.Select(f => f with { ManaAmount = ManaProductionAmount.Parse(f.OracleText) }).ToList();
    }

    private static async Task<IReadOnlyList<CardFact>> LoadFactsAsync(CalibrationDeck calibration)
    {
        string path = calibration.IsAssemblyFixture
            ? FactsCachePath
            : Path.Combine(RepoRoot(), "DeckFlow.Web.Tests", "Manabase", "fixtures", calibration.FactsFile);

        Assert.True(File.Exists(path), $"Facts cache not found at {path}.");

        List<CardFact> facts = JsonSerializer.Deserialize<List<CardFact>>(
            await File.ReadAllTextAsync(path))!;

        return facts.Select(f => f with { ManaAmount = ManaProductionAmount.Parse(f.OracleText) }).ToList();
    }

    private static void AppendMeasurement(StringBuilder sb, CalibrationDeck calibration, IReadOnlyList<CardFact> facts)
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard);
        ManabaseReport on = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            useHealthBandHeadlineFloor: true);
        (int maxColorLimited, int maxUnderSupported, bool broadColor, bool severe, double maxDeficit) = Signals(on);

        sb.AppendLine($"| {calibration.Name} | {on.AvgOnCurvePercent} | {on.WorstColorCastPercent:F0} | "
            + $"{maxColorLimited} | {maxUnderSupported} | {broadColor} | {severe} | {maxDeficit:F1} | "
            + $"{ManabaseDisplay.HealthLabel(off.Health)} | {ManabaseDisplay.HealthLabel(on.Health)} |");
    }

    private static (int MaxColorLimited, int MaxUnderSupported, bool BroadColorUnderSupport, bool AnySevereColorDeficit, double MaxColorDeficit)
        Signals(ManabaseReport report)
    {
        int maxColorLimited = report.ColorFindings.Count == 0 ? 0 : report.ColorFindings.Max(f => f.ColorLimitedUnderSupportedCount);
        int maxUnderSupported = report.ColorFindings.Count == 0 ? 0 : report.ColorFindings.Max(f => f.UnderSupportedCount);
        bool broadColor = report.ColorFindings.Any(f =>
        {
            int colorCards = report.ColorSpellCounts.TryGetValue(f.Color, out int count) ? count : 0;
            int tolerance = Math.Max(1, (int)Math.Ceiling(colorCards * 0.15));
            return f.ColorLimitedUnderSupportedCount > tolerance;
        });
        bool severe = report.ColorFindings.Any(f => f.Deficit > 2);
        double maxDeficit = report.ColorFindings.Count == 0 ? 0 : report.ColorFindings.Max(f => f.Deficit);

        return (maxColorLimited, maxUnderSupported, broadColor, severe, maxDeficit);
    }

    private static ManabaseReport SyntheticReport(
        bool useHealthBandCastability = false,
        bool useHealthBandHeadlineFloor = false,
        params ColorSourceFinding[] findings) =>
        new()
        {
            ActualLands = 35,
            TargetLands = 37,
            ColorFindings = findings,
            Castability =
            [
                new CardCastability { Name = "A", ManaValue = 2, OnCurveTurn = 2, CastPercent = 90, LimitingFactor = "mana" },
                new CardCastability { Name = "B", ManaValue = 3, OnCurveTurn = 3, CastPercent = 90, LimitingFactor = "mana" },
            ],
            ColorSpellCounts = findings.ToDictionary(f => f.Color, _ => 40),
            Summary = "test",
            UseHealthBandCastability = useHealthBandCastability,
            UseHealthBandHeadlineFloor = useHealthBandHeadlineFloor,
        };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeckFlow.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static bool HarnessEnabled() =>
        Environment.GetEnvironmentVariable("DECKFLOW_MANABASE_HARNESS") == "1"
        || File.Exists(Path.Combine(RepoRoot(), ".manabase-harness-on"));

    private static List<(int Qty, string Name, bool IsCommander)> ParseDeck(string list)
    {
        var result = new List<(int, string, bool)>();
        bool first = true;
        foreach (string raw in list.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int sp = raw.IndexOf(' ');
            int qty = int.Parse(raw[..sp]);
            string rest = raw[(sp + 1)..];
            int paren = rest.IndexOf(" (", StringComparison.Ordinal);
            string name = paren > 0 ? rest[..paren] : rest;
            int slash = name.IndexOf(" / ", StringComparison.Ordinal);
            if (slash > 0)
            {
                name = name[..slash];
            }

            result.Add((qty, name.Trim(), first));
            first = false;
        }

        return result;
    }

    private static async Task<IReadOnlyList<DeckCardEntry>> ResolveAsync(
        List<(int Qty, string Name, bool IsCommander)> lines)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "DeckFlow-manabase-harness/1.0");
        http.DefaultRequestHeaders.Add("Accept", "application/json");

        var byName = new Dictionary<string, (int Qty, string Name, bool IsCommander)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            byName[line.Name] = line;
        }

        var entries = new List<DeckCardEntry>();
        foreach (var batch in lines.Chunk(75))
        {
            var body = new { identifiers = batch.Select(line => new { name = line.Name }).ToArray() };
            using HttpResponseMessage response = await http.PostAsJsonAsync(
                "https://api.scryfall.com/cards/collection", body);
            response.EnsureSuccessStatusCode();
            ScryfallCollectionResponse? data = await response.Content.ReadFromJsonAsync<ScryfallCollectionResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (ScryfallCard card in data!.Data)
            {
                ScryfallCardData mapped = ScryfallCardDataMapper.ToCardData(card);
                string key = card.Name.Split(" // ")[0];
                (int Qty, string Name, bool IsCommander) line =
                    byName.TryGetValue(key, out var front) ? front :
                    byName.TryGetValue(card.Name, out var exact) ? exact : (1, card.Name, false);
                entries.Add(new DeckCardEntry { Card = mapped, Quantity = line.Qty, IsCommander = line.IsCommander });
            }

            await Task.Delay(120);
        }

        return entries;
    }

    public sealed record CalibrationDeck(
        string Name,
        string FactsFile,
        string FlagOffLabel,
        string FlagOnLabel,
        bool IsAssemblyFixture = false)
    {
        public override string ToString() => Name;
    }
}
