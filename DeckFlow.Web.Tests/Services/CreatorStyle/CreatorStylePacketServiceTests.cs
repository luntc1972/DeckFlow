using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Knowledge.CreatorStyleRubric;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using System.Globalization;
using Xunit;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

public sealed class CreatorStylePacketServiceTests
{
    [Fact]
    public async Task BuildAsync_ProfileExists_ReturnsRubricScoresAndAcceptedCollections()
    {
        var request = new CreatorStyleRequest
        {
            CreatorSlug = "alpha",
            DeckText = "1 Arcane Signet",
        };

        SubmittedDeckAnalysis analysis = CreateAnalysis(
            deckSize: 99,
            entries:
            [
                DeckEntry("Commander One", 1, "commander"),
                DeckEntry("Arcane Signet", 1, "mainboard"),
            ]);
        var expectedRubric = new RubricScoreResult
        {
            CreatorSlug = "alpha",
            MetricScores =
            [
                new RubricMetricScore
                {
                    Metric = "category_ratio:ramp",
                    TargetValue = 12.5,
                    SubmittedValue = 10.5,
                    Delta = -2,
                    Weight = 0.8,
                    Verdict = "under",
                    Confidence = "high",
                },
            ],
        };

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(CreateProfile("alpha")),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(analysis),
            buildWhitelistAsync: (_, _, _) => Task.FromResult(new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = ["Sol Ring"],
                HasUpstreamFailure = false,
            }),
            validateAdditionalCardsAsync: (_, _, _) => Task.FromResult(new CardGroundingBatchResult
            {
                Verdicts =
                [
                    Accepted("Arcane Signet"),
                    Accepted("Commander One"),
                    Accepted("Dockside Extortionist"),
                ],
                HasUpstreamFailure = false,
            }),
            getCreatorDecksAsync: (_, _) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(
            [
                CreatorDeck("deck-1", "trusted-folder", "high", "Commander One", "Arcane Signet"),
            ]),
            findCombosAsync: (_, _) => Task.FromResult<CommanderSpellbookResult?>(new CommanderSpellbookResult(
            [
                new SpellbookCombo(["Dockside Extortionist", "Sol Ring"], ["Treasure"], "Do the thing."),
            ],
            [])),
            scoreRubric: (_, _, _) => expectedRubric);

        CreatorStylePacketResult result = await sut.BuildAsync(request);

        Assert.Same(expectedRubric, result.RubricScores);
        Assert.Equal(["Sol Ring"], result.ValidatedWhitelist);
        Assert.Equal(["Dockside Extortionist"], result.ValidatedComboCards);
        Assert.Equal(
            ["Arcane Signet", "Commander One"],
            Assert.Single(result.Exemplars).CardNames.OrderBy(static cardName => cardName, StringComparer.Ordinal).ToArray());
        Assert.False(result.GroundingDegraded);
    }

    [Fact]
    public async Task BuildAsync_AdditionalGroundingRejectsCards_ExcludesThemAndSetsGroundingDegraded()
    {
        var request = new CreatorStyleRequest
        {
            CreatorSlug = "alpha",
            DeckText = "1 Arcane Signet",
        };

        SubmittedDeckAnalysis analysis = CreateAnalysis(
            deckSize: 99,
            entries:
            [
                DeckEntry("Commander One", 1, "commander"),
                DeckEntry("Arcane Signet", 1, "mainboard"),
            ]);

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(CreateProfile("alpha")),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(analysis),
            buildWhitelistAsync: (_, _, _) => Task.FromResult(new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = ["Sol Ring"],
                HasUpstreamFailure = false,
            }),
            validateAdditionalCardsAsync: (_, _, _) => Task.FromResult(new CardGroundingBatchResult
            {
                Verdicts =
                [
                    Accepted("Arcane Signet"),
                    Accepted("Commander One"),
                    Rejected("Hullbreacher", CardGroundingRejectReason.NotLegal),
                    Rejected("Jeska's Will", CardGroundingRejectReason.UpstreamUnavailable),
                ],
                HasUpstreamFailure = true,
            }),
            getCreatorDecksAsync: (_, _) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(
            [
                CreatorDeck("deck-1", "trusted-folder", "high", "Commander One", "Arcane Signet", "Hullbreacher"),
            ]),
            findCombosAsync: (_, _) => Task.FromResult<CommanderSpellbookResult?>(new CommanderSpellbookResult(
            [
                new SpellbookCombo(["Jeska's Will", "Sol Ring"], ["Treasure"], "Do the thing."),
            ],
            [])),
            scoreRubric: (_, _, _) => EmptyRubric("alpha"));

        CreatorStylePacketResult result = await sut.BuildAsync(request);

        Assert.True(result.GroundingDegraded);
        Assert.DoesNotContain("Hullbreacher", result.Exemplars.SelectMany(exemplar => exemplar.CardNames));
        Assert.DoesNotContain("Jeska's Will", result.ValidatedComboCards);
        Assert.DoesNotContain("Hullbreacher", result.ArtifactText);
        Assert.DoesNotContain("Jeska's Will", result.ArtifactText);
    }

    [Fact]
    public async Task BuildAsync_UsesOneDistinctValidationBatchMinusWhitelist()
    {
        var request = new CreatorStyleRequest
        {
            CreatorSlug = "alpha",
            DeckText = "1 Arcane Signet",
        };

        SubmittedDeckAnalysis analysis = CreateAnalysis(
            deckSize: 100,
            entries:
            [
                DeckEntry("Commander One", 1, "commander"),
                DeckEntry("Arcane Signet", 1, "mainboard"),
            ]);
        List<IReadOnlyList<string>> validationBatches = [];

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(CreateProfile("alpha")),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(analysis),
            buildWhitelistAsync: (_, _, _) => Task.FromResult(new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = ["Sol Ring", "Arcane Signet"],
                HasUpstreamFailure = false,
            }),
            validateAdditionalCardsAsync: (candidateNames, _, _) =>
            {
                validationBatches.Add(candidateNames.ToArray());
                return Task.FromResult(new CardGroundingBatchResult
                {
                    Verdicts = candidateNames.Select(Accepted).ToArray(),
                    HasUpstreamFailure = false,
                });
            },
            getCreatorDecksAsync: (_, _) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(
            [
                CreatorDeck("deck-1", "trusted-folder", "high", "Commander One", "Arcane Signet", "Sol Ring", "Smothering Tithe"),
            ]),
            findCombosAsync: (_, _) => Task.FromResult<CommanderSpellbookResult?>(new CommanderSpellbookResult(
            [
                new SpellbookCombo(["Smothering Tithe", "Dockside Extortionist", "Sol Ring"], ["Treasure"], "Loop."),
            ],
            [])),
            scoreRubric: (_, _, _) => EmptyRubric("alpha"));

        CreatorStylePacketResult result = await sut.BuildAsync(request);

        IReadOnlyList<string> batch = Assert.Single(validationBatches);
        Assert.Equal(["Commander One", "Smothering Tithe", "Dockside Extortionist"], batch);
        Assert.DoesNotContain("Arcane Signet", batch);
        Assert.DoesNotContain("Sol Ring", batch);
        Assert.Equal(["Sol Ring", "Arcane Signet"], result.ValidatedWhitelist);
    }

    [Fact]
    public async Task BuildAsync_WhitelistDiagnosticsHasUpstreamFailure_SetsGroundingDegraded()
    {
        var request = new CreatorStyleRequest
        {
            CreatorSlug = "alpha",
            DeckText = "1 Arcane Signet",
        };

        SubmittedDeckAnalysis analysis = CreateAnalysis(
            deckSize: 99,
            entries:
            [
                DeckEntry("Commander One", 1, "commander"),
                DeckEntry("Arcane Signet", 1, "mainboard"),
            ]);

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(CreateProfile("alpha")),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(analysis),
            buildWhitelistAsync: (_, _, _) => Task.FromResult(new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = ["Sol Ring"],
                HasUpstreamFailure = true,
            }),
            validateAdditionalCardsAsync: (_, _, _) => Task.FromResult(new CardGroundingBatchResult
            {
                Verdicts =
                [
                    Accepted("Arcane Signet"),
                    Accepted("Commander One"),
                ],
                HasUpstreamFailure = false,
            }),
            getCreatorDecksAsync: (_, _) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(
            [
                CreatorDeck("deck-1", "trusted-folder", "high", "Commander One", "Arcane Signet"),
            ]),
            findCombosAsync: (_, _) => Task.FromResult<CommanderSpellbookResult?>(null),
            scoreRubric: (_, _, _) => EmptyRubric("alpha"));

        CreatorStylePacketResult result = await sut.BuildAsync(request);

        Assert.True(result.GroundingDegraded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BuildAsync_MissingOrInsufficientProfile_ReturnsDegradedEmptyResult(bool insufficientSample)
    {
        var request = new CreatorStyleRequest
        {
            CreatorSlug = "alpha",
            DeckText = "1 Arcane Signet",
        };

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(insufficientSample ? CreateProfile("alpha", insufficientSample: true) : null),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(CreateAnalysis(
                deckSize: 99,
                entries:
                [
                    DeckEntry("Commander One", 1, "commander"),
                ])),
            buildWhitelistAsync: (_, _, _) => throw new Xunit.Sdk.XunitException("Whitelist should not run."),
            validateAdditionalCardsAsync: (_, _, _) => throw new Xunit.Sdk.XunitException("Guard should not run."),
            getCreatorDecksAsync: (_, _) => throw new Xunit.Sdk.XunitException("Deck cache should not run."),
            findCombosAsync: (_, _) => throw new Xunit.Sdk.XunitException("Spellbook should not run."),
            scoreRubric: (_, _, _) => throw new Xunit.Sdk.XunitException("Rubric should not run."));

        CreatorStylePacketResult result = await sut.BuildAsync(request);

        Assert.True(result.GroundingDegraded);
        Assert.Empty(result.Exemplars);
        Assert.Empty(result.ValidatedWhitelist);
        Assert.Empty(result.ValidatedComboCards);
        Assert.NotNull(result.Notice);
    }

    [Fact]
    public async Task BuildAsync_AssemblesArtifactTextWithFiveSectionsAndAcceptedCardsOnly()
    {
        var request = new CreatorStyleRequest
        {
            CreatorSlug = "alpha\ncreator",
            DeckText = "1 Arcane Signet",
        };

        SubmittedDeckAnalysis analysis = CreateAnalysis(
            deckSize: 99,
            entries:
            [
                DeckEntry("Commander One", 1, "commander"),
                DeckEntry("Arcane Signet", 1, "mainboard"),
            ]);
        RubricScoreResult rubric = new()
        {
            CreatorSlug = "alpha",
            MetricScores =
            [
                new RubricMetricScore
                {
                    Metric = "category_ratio:ramp",
                    TargetValue = 12.5,
                    SubmittedValue = 10.5,
                    Delta = -2,
                    Weight = 0.75,
                    Verdict = "under",
                    Confidence = "high",
                },
            ],
        };

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(CreateProfile("alpha")),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(analysis),
            buildWhitelistAsync: (_, _, _) => Task.FromResult(new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = ["Sol Ring"],
                HasUpstreamFailure = false,
            }),
            validateAdditionalCardsAsync: (_, _, _) => Task.FromResult(new CardGroundingBatchResult
            {
                Verdicts =
                [
                    Accepted("Arcane Signet"),
                    Accepted("Commander One"),
                    Accepted("Dockside Extortionist"),
                    Rejected("Hullbreacher", CardGroundingRejectReason.NotLegal),
                ],
                HasUpstreamFailure = false,
            }),
            getCreatorDecksAsync: (_, _) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(
            [
                CreatorDeck("deck-1", "trusted-folder", "high", "Commander One", "Arcane Signet", "Hullbreacher"),
            ]),
            findCombosAsync: (_, _) => Task.FromResult<CommanderSpellbookResult?>(new CommanderSpellbookResult(
            [
                new SpellbookCombo(["Dockside Extortionist", "Sol Ring", "Hullbreacher"], ["Treasure"], "Loop."),
            ],
            [])),
            scoreRubric: (_, _, _) => rubric);

        CreatorStylePacketResult result = await sut.BuildAsync(request);

        Assert.Contains("Creator Targets", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("Exemplar Decklists", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("Validated Synergy Context", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("Rubric Scores", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("Critique this deck ONLY using the cards provided above.", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("alpha creator", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("Arcane Signet", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("Dockside Extortionist", result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("Sol Ring", result.ArtifactText, StringComparison.Ordinal);
        Assert.DoesNotContain("Hullbreacher", result.ArtifactText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_GroundingDegraded_ArtifactTextIncludesVisibleCaveatAndCapsUserText()
    {
        string longSlug = "creator\nsecond-line-" + new string('x', 260);
        var request = new CreatorStyleRequest
        {
            CreatorSlug = longSlug,
            DeckText = "1 Arcane Signet",
        };

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(CreateProfile("alpha")),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(CreateAnalysis(
                deckSize: 99,
                entries:
                [
                    DeckEntry("Commander One", 1, "commander"),
                ])),
            buildWhitelistAsync: (_, _, _) => Task.FromResult(new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = ["Sol Ring"],
                HasUpstreamFailure = true,
            }),
            validateAdditionalCardsAsync: (_, _, _) => Task.FromResult(new CardGroundingBatchResult
            {
                Verdicts = [Accepted("Commander One")],
                HasUpstreamFailure = false,
            }),
            getCreatorDecksAsync: (_, _) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(
            [
                CreatorDeck("deck-1", "trusted-folder", "high", "Commander One"),
            ]),
            findCombosAsync: (_, _) => Task.FromResult<CommanderSpellbookResult?>(null),
            scoreRubric: (_, _, _) => EmptyRubric("alpha"));

        CreatorStylePacketResult result = await sut.BuildAsync(request);

        Assert.True(result.GroundingDegraded);
        Assert.Contains("Grounding caveat", result.ArtifactText, StringComparison.Ordinal);
        Assert.DoesNotContain("creator\nsecond-line", result.ArtifactText, StringComparison.Ordinal);
        Assert.DoesNotContain(longSlug, result.ArtifactText, StringComparison.Ordinal);
        Assert.Contains("second-line", result.ArtifactText, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 260), result.ArtifactText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_DeDeCulture_ArtifactTextRemainsByteIdentical()
    {
        var request = new CreatorStyleRequest
        {
            CreatorSlug = "alpha",
            DeckText = "1 Arcane Signet",
        };

        var sut = new CreatorStylePacketService(
            getProfileAsync: (_, _) => Task.FromResult<CreatorStyleProfile?>(CreateProfile("alpha")),
            buildSubmittedDeckAsync: (_, _) => Task.FromResult(CreateAnalysis(
                deckSize: 99,
                entries:
                [
                    DeckEntry("Commander One", 1, "commander"),
                    DeckEntry("Arcane Signet", 1, "mainboard"),
                ])),
            buildWhitelistAsync: (_, _, _) => Task.FromResult(new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = ["Sol Ring"],
                HasUpstreamFailure = false,
            }),
            validateAdditionalCardsAsync: (_, _, _) => Task.FromResult(new CardGroundingBatchResult
            {
                Verdicts =
                [
                    Accepted("Commander One"),
                    Accepted("Arcane Signet"),
                    Accepted("Dockside Extortionist"),
                ],
                HasUpstreamFailure = false,
            }),
            getCreatorDecksAsync: (_, _) => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(
            [
                CreatorDeck("deck-1", "trusted-folder", "high", "Commander One", "Arcane Signet"),
            ]),
            findCombosAsync: (_, _) => Task.FromResult<CommanderSpellbookResult?>(new CommanderSpellbookResult(
            [
                new SpellbookCombo(["Dockside Extortionist", "Sol Ring"], ["Treasure"], "Loop."),
            ],
            [])),
            scoreRubric: (_, _, _) => new RubricScoreResult
            {
                CreatorSlug = "alpha",
                MetricScores =
                [
                    new RubricMetricScore
                    {
                        Metric = "category_ratio:ramp",
                        TargetValue = 12.5,
                        SubmittedValue = 10.5,
                        Delta = -2,
                        Weight = 0.75,
                        Verdict = "under",
                        Confidence = "high",
                    },
                ],
            });

        string invariantArtifact = await WithCultureAsync(CultureInfo.InvariantCulture, () => sut.BuildAsync(request).ContinueWith(task => task.Result.ArtifactText));
        string germanArtifact = await WithCultureAsync(new CultureInfo("de-DE"), () => sut.BuildAsync(request).ContinueWith(task => task.Result.ArtifactText));

        Assert.Equal(invariantArtifact, germanArtifact);
        Assert.Contains("12.5", invariantArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("12,5", germanArtifact, StringComparison.Ordinal);
    }

    private static CreatorStyleProfile CreateProfile(string slug, bool insufficientSample = false)
        => new()
        {
            Slug = slug,
            Platform = "archidekt",
            MinDecks = 12,
            InsufficientSample = insufficientSample,
            FusedTargets =
            [
                new FusedTarget
                {
                    Metric = "category_ratio:ramp",
                    Value = 12.5,
                    Weight = 0.8,
                    Source = "fused",
                    StatedMin = 10,
                    StatedMax = 15,
                    Confidence = "high",
                },
            ],
            UpdatedUtc = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero),
        };

    private static SubmittedDeckAnalysis CreateAnalysis(int deckSize, IReadOnlyList<DeckEntry> entries)
        => new()
        {
            Stats = new SubmittedDeckStats
            {
                DeckSize = deckSize,
                CommanderCount = entries.Count(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)),
                Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["category_ratio:ramp"] = 10.5,
                },
            },
            DeckContext = new CardGroundingDeckContext
            {
                CommanderColorIdentity = new HashSet<string>(StringComparer.Ordinal) { "U", "R" },
                DeckProducedColors = new HashSet<char> { 'U', 'R' },
                DeckCardNames = entries.Select(entry => CardNormalizer.Normalize(entry.Name)).ToHashSet(StringComparer.Ordinal),
            },
            Entries = entries,
            ResolvedCommanderName = "Commander One",
            ImportNotice = null,
        };

    private static CreatorDeckCacheEntry CreatorDeck(
        string deckId,
        string folderName,
        string confidenceMarker,
        params string[] cardNames)
        => new()
        {
            CreatorSlug = "alpha",
            DeckId = deckId,
            ContentHash = $"{deckId}-hash",
            FolderName = folderName,
            Size = cardNames.Length,
            ConfidenceMarker = confidenceMarker,
            Entries = cardNames.Select(cardName => DeckEntry(cardName, 1, "mainboard")).ToArray(),
            CachedUtc = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero),
        };

    private static DeckEntry DeckEntry(string name, int quantity, string board)
        => new()
        {
            Name = name,
            NormalizedName = CardNormalizer.Normalize(name),
            Quantity = quantity,
            Board = board,
        };

    private static CardGroundingVerdict Accepted(string canonicalName)
        => new()
        {
            Accepted = true,
            CanonicalName = canonicalName,
            RejectReason = CardGroundingRejectReason.None,
        };

    private static CardGroundingVerdict Rejected(string canonicalName, CardGroundingRejectReason rejectReason)
        => new()
        {
            Accepted = false,
            CanonicalName = canonicalName,
            RejectReason = rejectReason,
        };

    private static RubricScoreResult EmptyRubric(string creatorSlug)
        => new()
        {
            CreatorSlug = creatorSlug,
            MetricScores = [],
        };

    private static async Task<string> WithCultureAsync(CultureInfo culture, Func<Task<string>> action)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return await action().ConfigureAwait(false);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
