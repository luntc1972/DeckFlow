using System;
using System.Collections.Generic;
using System.Linq;
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ManabaseFocusedModeTests
{
    [Fact]
    public void Focused_ModeLabels_AndTextSurfaces_UseFocusedLabel()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(BuildMonoWhiteDeck(), ManabaseMode.Focused);

        Assert.Equal("Focused", ManabaseLabels.Mode(ManabaseMode.Focused));
        Assert.StartsWith("Mode: Focused", report.Summary, StringComparison.Ordinal);

        string artifact = ManabaseReportTextBuilder.Build(report, "Test Deck", null, ManabaseMode.Focused);
        Assert.Contains("Mode: Focused", artifact, StringComparison.Ordinal);

        string prompt = ManabaseSwapPromptBuilder.Build(report, "Test Deck", "1 Plains", ManabaseMode.Focused);
        Assert.Contains("This is a Focused Commander deck", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Focused_LandTarget_MatchesCasual_AndDiffersFromCedh()
    {
        ManabaseDeck deck = BuildLowCurveDeck();

        ManabaseReport casual = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);
        ManabaseReport focused = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Focused);
        ManabaseReport cedh = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh);

        Assert.Equal(casual.TargetLands, focused.TargetLands);
        Assert.NotEqual(casual.TargetLands, cedh.TargetLands);
    }

    [Fact]
    public void Focused_ColorThreshold_Is85()
    {
        var window = FindThresholdWindow(
            BuildColoredThresholdDeck,
            report => Assert.Single(report.ColorFindings, finding => finding.Color == ManaColor.White),
            analyze: deck => ManabaseAnalyzer.Analyze(deck, deck.Mode));

        Assert.NotNull(window);

        Assert.Equal(0, window!.Casual.UnderSupportedCount);
        Assert.True(window.Focused.UnderSupportedCount > 0);
        Assert.True(window.Cedh.UnderSupportedCount > 0);
        Assert.InRange(window.Focused.WorstSpellCastPercent, 80, 84.999);
    }

    [Fact]
    public void Focused_ColorlessSnowThreshold_Is85()
    {
        var window = FindThresholdWindow(
            BuildColorlessThresholdDeck,
            report => Assert.Single(report.ColorFindings, finding => finding.DisplayColor == "Colorless"),
            analyze: deck => ManabaseAnalyzer.Analyze(deck, deck.Mode, colorlessSnow: true));

        Assert.NotNull(window);

        Assert.Equal(0, window!.Casual.UnderSupportedCount);
        Assert.True(window.Focused.UnderSupportedCount > 0);
        Assert.True(window.Cedh.UnderSupportedCount > 0);
        Assert.InRange(window.Focused.WorstSpellCastPercent, 80, 84.999);
    }

    [Fact]
    public void Focused_HealthBandThreshold_Is85()
    {
        ManabaseReport casual = BuildHealthBandReport(ManabaseMode.Casual);
        ManabaseReport focused = BuildHealthBandReport(ManabaseMode.Focused);
        ManabaseReport cedh = BuildHealthBandReport(ManabaseMode.Cedh);

        Assert.Equal(ManabaseHealth.Functional, casual.Health);
        Assert.Equal(ManabaseHealth.Workable, focused.Health);
        Assert.Equal(ManabaseHealth.Workable, cedh.Health);
    }

    [Fact]
    public void Focused_DoesNotEnableCedhInteractionLens()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildInteractionDeck(),
            ManabaseMode.Focused,
            interactionLens: true);

        Assert.Null(report.InteractionLens);
    }

    private static ThresholdWindow? FindThresholdWindow(
        Func<int, DeckWithMode> build,
        Func<ManabaseReport, ColorSourceFinding> select,
        Func<DeckWithMode, ManabaseReport> analyze)
    {
        for (int sources = 1; sources <= 39; sources++)
        {
            DeckWithMode casualDeck = build(sources) with { Mode = ManabaseMode.Casual };
            DeckWithMode focusedDeck = build(sources) with { Mode = ManabaseMode.Focused };
            DeckWithMode cedhDeck = build(sources) with { Mode = ManabaseMode.Cedh };

            ManabaseReport casual = analyze(casualDeck);
            ManabaseReport focused = analyze(focusedDeck);
            ManabaseReport cedh = analyze(cedhDeck);

            ColorSourceFinding casualFinding = select(casual);
            ColorSourceFinding focusedFinding = select(focused);
            ColorSourceFinding cedhFinding = select(cedh);

            if (casualFinding.UnderSupportedCount == 0
                && focusedFinding.UnderSupportedCount > 0
                && cedhFinding.UnderSupportedCount > 0)
            {
                return new ThresholdWindow(casualFinding, focusedFinding, cedhFinding);
            }
        }

        return null;
    }

    private static ManabaseDeck BuildMonoWhiteDeck()
    {
        var sources = Enumerable.Range(0, 37)
            .Select(i => new ManaSource
            {
                Name = $"Plains {i}",
                Produces = new[] { ManaColor.White },
            })
            .ToList();

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.1,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Swords to Plowshares", ManaValue = 1, Pips = Pip((ManaColor.White, 1)) },
            },
            IsSingleton = true,
        };
    }

    private static ManabaseDeck BuildLowCurveDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = $"Plains {i}", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < 17; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.4,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Counterspell", ManaValue = 2, Pips = Pip((ManaColor.Blue, 2)) },
                new() { Name = "Swords to Plowshares", ManaValue = 1, Pips = Pip((ManaColor.White, 1)) },
            },
            IsSingleton = true,
        };
    }

    private static DeckWithMode BuildColoredThresholdDeck(int whiteSources)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < whiteSources; i++)
        {
            sources.Add(new ManaSource { Name = $"Plains {i}", Produces = new[] { ManaColor.White } });
        }

        for (int i = whiteSources; i < 39; i++)
        {
            sources.Add(new ManaSource { Name = $"Wastes {i}", Produces = Array.Empty<ManaColor>(), ProducesColorless = true });
        }

        return new DeckWithMode(
            new ManabaseDeck
            {
                TotalCards = 100,
                CommanderCount = 1,
                AverageManaValue = 2.0,
                Sources = sources,
                Spells = new List<SpellRequirement>
                {
                    new() { Name = "Grand Abolisher", ManaValue = 2, Pips = Pip((ManaColor.White, 2)) },
                },
                IsSingleton = true,
            },
            ManabaseMode.Casual);
    }

    private static DeckWithMode BuildColorlessThresholdDeck(int specialSources)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < specialSources; i++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Wastes {i}",
                Produces = Array.Empty<ManaColor>(),
                ProducesColorless = true,
            });
        }

        for (int i = specialSources; i < 39; i++)
        {
            sources.Add(new ManaSource { Name = $"Plains {i}", Produces = new[] { ManaColor.White } });
        }

        return new DeckWithMode(
            new ManabaseDeck
            {
                TotalCards = 100,
                CommanderCount = 1,
                AverageManaValue = 2.0,
                Sources = sources,
                Spells = new List<SpellRequirement>
                {
                    new() { Name = "Warping Wail", ManaValue = 2, Pips = Pip(), TrueColorlessPips = 1 },
                },
                IsSingleton = true,
            },
            ManabaseMode.Casual);
    }

    private static ManabaseDeck BuildInteractionDeck()
    {
        var sources = Enumerable.Range(0, 20)
            .Select(i => new ManaSource
            {
                Name = $"Island {i}",
                Produces = new[] { ManaColor.Blue },
            })
            .Cast<ManaSource>()
            .Concat(Enumerable.Range(0, 16).Select(i => new ManaSource
            {
                Name = $"Plains {i}",
                Produces = new[] { ManaColor.White },
            }))
            .ToList();

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.2,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Swan Song", ManaValue = 1, Pips = Pip((ManaColor.Blue, 1)), IsInteractionSpell = true },
                new() { Name = "Counterspell", ManaValue = 2, Pips = Pip((ManaColor.Blue, 2)), IsInteractionSpell = true },
            },
            IsSingleton = true,
        };
    }

    private static ManabaseReport BuildHealthBandReport(ManabaseMode mode) => new()
    {
        ActualLands = 38,
        TargetLands = 37.0,
        Mode = mode,
        UseHealthBandCastability = true,
        ColorSpellCounts = new Dictionary<ManaColor, int> { [ManaColor.White] = 10 },
        ColorFindings = new[]
        {
            new ColorSourceFinding
            {
                Color = ManaColor.White,
                ActualSources = 24.0,
                RequiredSources = 24,
                DrivingSpell = "Grand Abolisher",
                UnderSupportedCount = 1,
                ColorLimitedUnderSupportedCount = 1,
                WorstSpell = "Grand Abolisher",
                WorstSpellCastPercent = 82,
            },
        },
        Summary = "test",
    };

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips) =>
        pips.ToDictionary(p => p.Color, p => p.Count);

    private sealed record DeckWithMode(ManabaseDeck Deck, ManabaseMode Mode)
    {
        public static implicit operator ManabaseDeck(DeckWithMode value) => value.Deck;
    }

    private sealed record ThresholdWindow(
        ColorSourceFinding Casual,
        ColorSourceFinding Focused,
        ColorSourceFinding Cedh);
}
