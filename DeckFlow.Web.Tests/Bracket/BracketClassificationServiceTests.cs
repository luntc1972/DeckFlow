using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Bracket;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Bracket;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests.Bracket;

/// <summary>
/// Service-level tests for <see cref="BracketClassificationService"/> covering BRACKET-01 and
/// BRACKET-03: happy path, combo-null disclosure, two-card-only gating, empty-source guard,
/// and platform artifact build.
/// </summary>
public sealed class BracketClassificationServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static BracketClassificationService BuildService(
        FakeSpellbookService spellbook,
        IGameChangerCatalogService? catalog = null)
    {
        var registry = new BracketPromptVariantRegistry(new IBracketPromptVariant[]
        {
            new ChatGptBracketPromptVariant(),
            new ClaudeBracketPromptVariant(),
            new GeminiBracketPromptVariant(),
        });

        return new BracketClassificationService(
            new FakeDeckEntryLoader(FixtureDeck()),
            spellbook,
            catalog ?? BuildFakeCatalogService(gcCards: []),
            registry,
            NullLogger<BracketClassificationService>.Instance);
    }

    /// <summary>
    /// A small fixture deck with no Game Changer cards so the only B4 signal can be
    /// the combo returned by the fake spellbook service.
    /// </summary>
    private static List<DeckEntry> FixtureDeck() =>
    [
        Entry("Rhystic Study"),
        Entry("Cyclonic Rift"),
        Entry("Mana Vault"),
        Entry("Smothering Tithe"),
        Entry("Armageddon"),
    ];

    private static DeckEntry Entry(string name) => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = 1,
        Board = "mainboard",
    };

    /// <summary>
    /// Catalog with zero Game Changers and no MLD cards by default (so only combo drives B4 in tests).
    /// Pass gcCards/mldCards to add signal cards.
    /// </summary>
    private static FakeGameChangerCatalogService BuildFakeCatalogService(
        IReadOnlyList<string>? gcCards = null,
        IReadOnlyList<string>? mldCards = null)
    {
        var tiers = new List<BracketTier>
        {
            new(1, "Exhibition", "Bracket 1: Exhibition", "Exhibition.", "9+ turns.", 0),
            new(2, "Core",       "Bracket 2: Core",       "Core.",       "8+ turns.", 0),
            new(3, "Upgraded",   "Bracket 3: Upgraded",   "Upgraded.",   "6+ turns.", 3),
            new(4, "Optimized",  "Bracket 4: Optimized",  "Optimized.",  "4+ turns.", -1),
            new(5, "cEDH",       "Bracket 5: cEDH",       "cEDH.",       "Any turn.", -1),
        };
        var catalog = new GameChangerCatalog(
            new DateOnly(2026, 2, 9),
            gcCards ?? [],
            mldCards ?? [],
            ["Time Warp"],
            tiers);
        return new FakeGameChangerCatalogService(catalog);
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// BRACKET-03 / Pitfall 1: a null result from <see cref="ICommanderSpellbookService"/> means
    /// the API was unavailable — ComboDetectionAvailable must be false and the paste artifact must
    /// disclose the unavailability. It must NOT claim "0 two-card combos" or assert zero as
    /// detection evidence.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_NullSpellbook_SetsComboDetectionAvailableFalse()
    {
        var service = BuildService(new FakeSpellbookService(comboResult: null));

        var result = await service.ClassifyAsync(
            "paste", targetBracketNumber: null, "ChatGPT", "Test Deck");

        Assert.False(result.Classification.ComboDetectionAvailable,
            "ComboDetectionAvailable must be false when spellbook returns null (API unavailable).");
        Assert.Null(result.Classification.TwoCardCombos); // TwoCardCombos must be null when detection is unavailable — not an empty list.

        // The artifact must disclose that combo detection was unavailable (BRACKET-03).
        Assert.Contains("combo detection", result.PromptArtifact, StringComparison.OrdinalIgnoreCase);

        // The artifact must NOT claim "0 two-card combos" or "no combos found" as if detection ran.
        Assert.DoesNotContain("0 two-card combo", result.PromptArtifact, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A deck with a two-card combo returned by the Spellbook service must be classified as B4.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_TwoCardCombo_GatesB4()
    {
        var twoCardComboResult = new CommanderSpellbookResult(
            IncludedCombos:
            [
                new SpellbookCombo(
                    ["Thassa's Oracle", "Demonic Consultation"],
                    ["Win the game"],
                    "Cast Demonic Consultation then Oracle."),
            ],
            AlmostIncludedCombos: []);

        var service = BuildService(new FakeSpellbookService(twoCardComboResult));

        var result = await service.ClassifyAsync(
            "paste", targetBracketNumber: null, "ChatGPT", null);

        Assert.True(result.Classification.BracketNumber >= 4,
            $"A two-card combo must gate the deck to B4; got B{result.Classification.BracketNumber}.");
        Assert.True(result.Classification.ComboDetectionAvailable,
            "ComboDetectionAvailable must be true when the spellbook returned a non-null result.");
        Assert.NotNull(result.Classification.TwoCardCombos);
        Assert.Single(result.Classification.TwoCardCombos!);
    }

    /// <summary>
    /// A deck with ONLY a three-card combo (no GC, no MLD) must NOT be gated to B4 by the combo.
    /// Per the WotC rubric the two-card B4 gate applies to combos with exactly two cards; three-card
    /// combos are excluded from this check.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_ThreeCardCombo_NotCountedAsTwoCardGate()
    {
        var threeCardComboResult = new CommanderSpellbookResult(
            IncludedCombos:
            [
                new SpellbookCombo(
                    ["Hive Mind", "Pact of Negation", "Any Pact"],
                    ["Opponent loses"],
                    "Cast Hive Mind then a Pact."),
            ],
            AlmostIncludedCombos: []);

        var service = BuildService(new FakeSpellbookService(threeCardComboResult));

        var result = await service.ClassifyAsync(
            "paste", targetBracketNumber: null, "ChatGPT", null);

        // Three-card combos do not trigger the two-card B4 gate.
        // With zero GC, no MLD, and no two-card combo, the result must be B2 or B3.
        Assert.True(result.Classification.BracketNumber < 4,
            $"A three-card-only combo must NOT gate the deck to B4; got B{result.Classification.BracketNumber}.");
        Assert.True(result.Classification.ComboDetectionAvailable,
            "ComboDetectionAvailable must be true when the spellbook returned a non-null result.");

        // The three-card combo should NOT appear in TwoCardCombos.
        Assert.Empty(result.Classification.TwoCardCombos ?? []);
    }

    /// <summary>
    /// A blank or whitespace deck source must throw <see cref="InvalidOperationException"/>
    /// with a user-facing message (not a 500).
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_EmptySource_Throws()
    {
        var service = BuildService(new FakeSpellbookService(comboResult: null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ClassifyAsync("   ", null, "ChatGPT", null));
    }

    /// <summary>
    /// The paste artifact must be non-empty and contain the canonical "WHY THIS BRACKET" marker
    /// for the Claude platform.
    /// </summary>
    [Fact]
    public async Task ClassifyAsync_BuildsArtifactForPlatform()
    {
        var service = BuildService(
            new FakeSpellbookService(comboResult: null));

        var result = await service.ClassifyAsync(
            "paste", targetBracketNumber: null, "Claude", "My Deck");

        Assert.False(string.IsNullOrWhiteSpace(result.PromptArtifact),
            "PromptArtifact must not be empty after a successful classification.");
        Assert.Contains("WHY THIS BRACKET", result.PromptArtifact, StringComparison.Ordinal);
    }

    // ── Test doubles ───────────────────────────────────────────────────────

    private sealed class FakeDeckEntryLoader : IDeckEntryLoader
    {
        private readonly List<DeckEntry> _entries;

        public FakeDeckEntryLoader(List<DeckEntry> entries) => _entries = entries;

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(_entries, FallbackNotice: null));

        public Task<List<DeckEntry>> LoadAsync(
            DeckLoadRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void ValidateCommanderDeckSize(
            string systemName,
            IReadOnlyList<DeckEntry> entries,
            int requiredDeckSize = 100)
        {
        }
    }

    private sealed class FakeSpellbookService : ICommanderSpellbookService
    {
        private readonly CommanderSpellbookResult? _comboResult;

        public FakeSpellbookService(CommanderSpellbookResult? comboResult) =>
            _comboResult = comboResult;

        public Task<CommanderSpellbookResult?> FindCombosAsync(
            IReadOnlyList<DeckEntry> entries,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_comboResult);
    }

    private sealed class FakeGameChangerCatalogService : IGameChangerCatalogService
    {
        private readonly GameChangerCatalog _catalog;

        public FakeGameChangerCatalogService(GameChangerCatalog catalog) => _catalog = catalog;

        public GameChangerCatalog GetCatalog() => _catalog;
    }
}
