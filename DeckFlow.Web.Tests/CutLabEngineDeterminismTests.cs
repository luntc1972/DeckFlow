using System.Diagnostics;
using System.Text.Json;
using DeckFlow.Core.Manabase;
using Xunit;
using Xunit.Abstractions;

namespace DeckFlow.Web.Tests;

/// <summary>Guards the existing manabase engine's deterministic behavior for Cut Lab simulation work.</summary>
public sealed class CutLabEngineDeterminismTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Initializes a new test sink for timing output.</summary>
    /// <param name="output">xUnit output writer.</param>
    public CutLabEngineDeterminismTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Two analyses of the same card facts produce a byte-identical numeric projection.</summary>
    [Fact]
    public void Analyze_SameFactsTwice_ProducesByteIdenticalNumericSnapshot()
    {
        IReadOnlyList<CardFact> facts = BuildDeterministicFacts();

        string first = SerializeProjection(AnalyzeFacts(facts));
        string second = SerializeProjection(AnalyzeFacts(facts));

        Assert.Equal(first, second);
    }

    /// <summary>Reordering the same card facts does not change the projected numeric output.</summary>
    [Fact]
    public void Analyze_ShuffledFacts_ProducesTheSameNumericSnapshot()
    {
        IReadOnlyList<CardFact> facts = BuildDeterministicFacts();
        IReadOnlyList<CardFact> shuffled =
        [
            facts[4],
            facts[10],
            facts[1],
            facts[8],
            facts[0],
            facts[6],
            facts[11],
            facts[3],
            facts[9],
            facts[2],
            facts[7],
            facts[5],
        ];

        string original = SerializeProjection(AnalyzeFacts(facts));
        string reordered = SerializeProjection(AnalyzeFacts(shuffled));

        Assert.Equal(original, reordered);
    }

    /// <summary>Measures one default-trial analyze pass for a realistic oversized singleton pool.</summary>
    [Fact]
    public void TimingSpike_DefaultTrialAnalyze_OnLargeSingletonPool_EmitsElapsedMilliseconds()
    {
        IReadOnlyList<CardFact> facts = BuildTimingFacts();

        Stopwatch stopwatch = Stopwatch.StartNew();
        ManabaseReport report = AnalyzeFacts(facts);
        stopwatch.Stop();

        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        _output.WriteLine($"Cut Lab timing spike (~130-card Analyze): {elapsedMilliseconds} ms");
        Assert.True(elapsedMilliseconds >= 0);
        Assert.NotNull(report.MulliganEvaluation);
    }

    private static ManabaseReport AnalyzeFacts(IReadOnlyList<CardFact> facts)
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(
            facts,
            isSingleton: true,
            rampCreditV2: false,
            landRampSim: false,
            payLifeUntapped: false,
            checkLandUntapped: false,
            restrictedLands: false);

        deck = deck with
        {
            Spells = deck.Spells.Select(ApplyPlanRoleMetadata).ToList(),
        };

        return ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            interactionLens: true,
            keepShapes: true);
    }

    private static SpellRequirement ApplyPlanRoleMetadata(SpellRequirement spell)
        => spell.Name switch
        {
            "Kinnan, Bonder Prodigy" => spell with { PlanRoles = PlanRole.Payoff, IsCommander = true },
            "Rhystic Study" => spell with { PlanRoles = PlanRole.Engine },
            "Thassa's Oracle" => spell with { PlanRoles = PlanRole.Payoff },
            "Mystic Remora" => spell with { PlanRoles = PlanRole.Engine },
            "Counterspell" => spell with { PlanRoles = PlanRole.None, IsInteractionSpell = true, Kinds = SpellKinds.Instant },
            "Swan Song" => spell with { PlanRoles = PlanRole.None, IsInteractionSpell = true, Kinds = SpellKinds.Instant },
            _ => spell,
        };

    private static string SerializeProjection(ManabaseReport report)
        => JsonSerializer.Serialize(
            new NumericProjection(
                report.LandDelta,
                report.MulliganEvaluation?.KeepableHandPercent ?? 0,
                report.MulliganEvaluation?.MulliganTo5Percent ?? 0,
                report.MulliganEvaluation?.PlanPresence?.PlanPresencePercent ?? 0,
                report.InteractionLens?.QualifyingCount ?? 0,
                report.Castability.Single(row => row.IsCommander).EarlyCastPercents.ToArray(),
                report.ColorFindings.Select(
                    finding => new ColorProjection(
                        finding.Color.ToString(),
                        finding.AverageCastPercent)).ToArray()));

    private static IReadOnlyList<CardFact> BuildDeterministicFacts()
    {
        return
        [
            Creature("Kinnan, Bonder Prodigy", "{G}{U}", 2, "Legendary Creature — Human Druid", isCommander: true),
            Land("Forest", "{T}: Add {G}.", "G"),
            Land("Island", "{T}: Add {U}.", "U"),
            Land("Breeding Pool", "({T}: Add {G} or {U}.) As this land enters, you may pay 2 life. If you don't, it enters tapped.", "G", "U"),
            Land("Command Tower", "{T}: Add one mana of any color in your commander's color identity.", "G", "U"),
            Artifact("Sol Ring", "{1}", 1, "{T}: Add {C}{C}."),
            Artifact("Arcane Signet", "{2}", 2, "{T}: Add one mana of any color in your commander's color identity."),
            Instant("Counterspell", "{U}{U}", 2, "Counter target spell."),
            Instant("Swan Song", "{U}", 1, "Counter target enchantment, instant, or sorcery spell."),
            Enchantment("Rhystic Study", "{2}{U}", 3, "Whenever an opponent casts a spell, you may draw a card unless that player pays {1}."),
            Creature("Thassa's Oracle", "{U}{U}", 2, "Creature — Merfolk Wizard"),
            Enchantment("Mystic Remora", "{U}", 1, "Cumulative upkeep {1}. Whenever an opponent casts a noncreature spell, you may draw a card unless that player pays {4}."),
        ];
    }

    private static IReadOnlyList<CardFact> BuildTimingFacts()
    {
        var facts = new List<CardFact>
        {
            Creature("Kinnan, Bonder Prodigy", "{G}{U}", 2, "Legendary Creature — Human Druid", isCommander: true),
        };

        for (int i = 1; i <= 18; i++)
        {
            facts.Add(Land($"Forest {i:00}", "{T}: Add {G}.", "G"));
            facts.Add(Land($"Island {i:00}", "{T}: Add {U}.", "U"));
        }

        for (int i = 1; i <= 4; i++)
        {
            facts.Add(Land($"Dual Grove {i:00}", "{T}: Add {G} or {U}.", "G", "U"));
        }

        for (int i = 1; i <= 20; i++)
        {
            facts.Add(Artifact($"Mana Rock {i:00}", "{2}", 2, "{T}: Add one mana of any color."));
        }

        for (int i = 1; i <= 20; i++)
        {
            facts.Add(Instant($"Interaction {i:00}", "{U}", 1, "Counter target spell."));
        }

        for (int i = 1; i <= 25; i++)
        {
            facts.Add(Enchantment($"Engine {i:00}", "{2}{U}", 3, "Whenever you cast a spell, draw a card."));
        }

        for (int i = 1; i <= 24; i++)
        {
            facts.Add(Creature($"Payoff {i:00}", "{3}{G}{U}", 5, "Creature — Beast"));
        }

        for (int i = 1; i <= 17; i++)
        {
            facts.Add(Sorcery($"Support {i:00}", "{1}{G}", 2, "Draw a card."));
        }

        return facts;
    }

    private static CardFact Land(string name, string oracleText, params string[] producedMana)
        => new()
        {
            Name = name,
            Quantity = 1,
            TypeLine = "Land",
            OracleText = oracleText,
            FrontFaceOracleText = oracleText,
            LandFaceOracleText = oracleText,
            ProducedMana = producedMana,
            ManaValue = 0,
            HasLandFace = true,
        };

    private static CardFact Artifact(string name, string manaCost, double manaValue, string oracleText)
        => new()
        {
            Name = name,
            Quantity = 1,
            ManaCost = manaCost,
            ManaValue = manaValue,
            TypeLine = "Artifact",
            OracleText = oracleText,
            FrontFaceOracleText = oracleText,
        };

    private static CardFact Instant(string name, string manaCost, double manaValue, string oracleText)
        => new()
        {
            Name = name,
            Quantity = 1,
            ManaCost = manaCost,
            ManaValue = manaValue,
            TypeLine = "Instant",
            OracleText = oracleText,
            FrontFaceOracleText = oracleText,
        };

    private static CardFact Sorcery(string name, string manaCost, double manaValue, string oracleText)
        => new()
        {
            Name = name,
            Quantity = 1,
            ManaCost = manaCost,
            ManaValue = manaValue,
            TypeLine = "Sorcery",
            OracleText = oracleText,
            FrontFaceOracleText = oracleText,
        };

    private static CardFact Enchantment(string name, string manaCost, double manaValue, string oracleText)
        => new()
        {
            Name = name,
            Quantity = 1,
            ManaCost = manaCost,
            ManaValue = manaValue,
            TypeLine = "Enchantment",
            OracleText = oracleText,
            FrontFaceOracleText = oracleText,
        };

    private static CardFact Creature(string name, string manaCost, double manaValue, string typeLine, bool isCommander = false)
        => new()
        {
            Name = name,
            Quantity = 1,
            ManaCost = manaCost,
            ManaValue = manaValue,
            TypeLine = typeLine,
            OracleText = string.Empty,
            FrontFaceOracleText = string.Empty,
            IsCommander = isCommander,
        };

    private sealed record NumericProjection(
        double LandDelta,
        int KeepableHandPercent,
        int MulliganTo5Percent,
        int PlanPresencePercent,
        int InteractionQualifyingCount,
        IReadOnlyList<int> CommanderEarlyCastPercents,
        IReadOnlyList<ColorProjection> ColorFindings);

    private sealed record ColorProjection(
        string Color,
        double AverageCastPercent);
}
