using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Regression tests for the AJAX Cut Lab floor supply; these must fail on the pre-fix tree.</summary>
public sealed class CutLabAjaxFloorByRoleRegressionTests
{
    [Fact]
    public async Task T1_DecidePath_ResolvesDefaultFloorsForWarningsFindingsAndLockedOvershootOrdering()
    {
        CutLabState state = CreateEmptyFloorState(
            pool:
            [
                Card("Focused Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 7, isLocked: true),
                Card("Wincon Sorcery", quantity: 4, isLocked: true),
                Card("Basic Filler", quantity: 89, isLocked: true),
            ]);
        FakeAnalysisContextBuilder analysisBuilder = new();
        CutLabApiController controller = CreateApiController(analysisBuilder);
        IReadOnlyDictionary<string, int> expectedFloors = ResolveExpectedFloors(state);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        CutLabDecideApiResponse payload = AssertOk(response);
        CutLabDecideFloorWarningDto rampWarning = Assert.Single(payload.Patch.FloorWarnings, warning => warning.Role == "ramp");
        Assert.Equal(expectedFloors["ramp"], rampWarning.Floor);

        CutLabDecideFindingDto targetedWeakFloor = Assert.Single(
            WeakFloorItems(payload.Patch.StructuralFindings),
            item => item.Lead.Contains("Targeted removal is at 7 against a floor of 7", StringComparison.Ordinal));
        Assert.Equal(["Counterspell"], targetedWeakFloor.Evidence);

        CutLabLockedOvershootAdvisoryDto advisory = Assert.IsType<CutLabLockedOvershootAdvisoryDto>(payload.Patch.LockedOvershootAdvisory);
        Assert.Equal(["Wincon Sorcery"], advisory.Groups[0].CardNames);
        Assert.Equal(["Counterspell"], advisory.Groups[1].CardNames);
    }

    [Fact]
    public async Task T2_PageAndAjaxParity_ProduceSameLockedOvershootOrdering()
    {
        CutLabState state = CreateEmptyFloorState(
            pool:
            [
                Card("Focused Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 7, isLocked: true),
                Card("Wincon Sorcery", quantity: 4, isLocked: true),
                Card("Basic Filler", quantity: 89, isLocked: true),
            ]);
        FakeAnalysisContextBuilder analysisBuilder = new();
        CutLabState afterState = CutLabDecisionApplier.Apply(state, "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);
        CutLabProcessResult pageResult = await CreatePageService(afterState, analysisBuilder).ProcessAsync(CreateRequest(afterState));
        CutLabApiController controller = CreateApiController(analysisBuilder);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        CutLabDecideApiResponse payload = AssertOk(response);
        Assert.Equal(
            pageResult.RoundPlan!.LockedOvershootAdvisory!.Groups
                .SelectMany(group => group.CardNames)
                .Where(name => name is "Wincon Sorcery" or "Counterspell")
                .ToArray(),
            payload.Patch.LockedOvershootAdvisory!.Groups
                .SelectMany(group => group.CardNames)
                .Where(name => name is "Wincon Sorcery" or "Counterspell")
                .ToArray());
    }

    [Fact]
    public async Task T3_MixedOverride_OverrideSurvivesAndDefaultsAreRestoredForNonOverriddenRoles()
    {
        CutLabState state = CreateEmptyFloorState(
            pool:
            [
                Card("Focused Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 11, isLocked: true),
                Card("Wincon Sorcery", quantity: 3, isLocked: true),
                Card("Forest", quantity: 36, isLocked: true, typeLine: "Basic Land - Forest"),
                Card("Basic Filler", quantity: 49, isLocked: true),
            ],
            roleFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "interaction-targeted",
                    Floor = 11,
                    IsUserSet = true,
                },
            ]);
        FakeAnalysisContextBuilder analysisBuilder = new();
        CutLabApiController controller = CreateApiController(analysisBuilder);
        IReadOnlyDictionary<string, int> expectedFloors = ResolveExpectedFloors(state);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        CutLabDecideApiResponse payload = AssertOk(response);
        CutLabDecideFloorWarningDto rampWarning = Assert.Single(payload.Patch.FloorWarnings, warning => warning.Role == "ramp");
        Assert.Equal(expectedFloors["ramp"], rampWarning.Floor);

        IReadOnlyList<CutLabDecideFindingDto> weakFloorItems = WeakFloorItems(payload.Patch.StructuralFindings);
        Assert.Contains(weakFloorItems, item => item.Lead.Contains("Targeted removal is at 11 against a floor of 11", StringComparison.Ordinal));
        Assert.Contains(weakFloorItems, item => item.Lead.Contains($"You have no ramp cards yet; the suggested floor is {expectedFloors["ramp"]}.", StringComparison.Ordinal));
        Assert.Contains(weakFloorItems, item => item.Lead.Contains("floor of 3", StringComparison.Ordinal));
        Assert.Contains(weakFloorItems, item => item.Lead.Contains("Lands is at 36 against a floor of 36", StringComparison.Ordinal));
    }

    [Fact]
    public async Task T4_FlagOff_AjaxMatchesBracketOnlyPagePathInsteadOfCommanderRaisedDefaults()
    {
        CutLabState state = CreateEmptyFloorState(
            pool:
            [
                Card("Focused Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 7, isLocked: true),
                Card("Wincon Sorcery", quantity: 4, isLocked: true),
                Card("Basic Filler", quantity: 89, isLocked: true),
            ]);
        FakeAnalysisContextBuilder analysisBuilder = new();
        CutLabState afterState = CutLabDecisionApplier.Apply(state, "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);
        FakeRoleFloorBaselineProvider roleFloorBaseline = new(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["wincons"] = 9,
        });
        CutLabProcessResult pageResult = await CreatePageService(
            afterState,
            analysisBuilder,
            roleFloorBaseline: roleFloorBaseline,
            featureFlags: new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [CutLabPageService.CommanderFloorsFlagKey] = false,
            })).ProcessAsync(CreateRequest(afterState));
        CutLabApiController controller = CreateApiController(analysisBuilder);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        CutLabDecideApiResponse payload = AssertOk(response);
        Assert.Equal(
            pageResult.RoundPlan!.LockedOvershootAdvisory!.Groups
                .SelectMany(group => group.CardNames)
                .Where(name => name is "Wincon Sorcery" or "Counterspell")
                .ToArray(),
            payload.Patch.LockedOvershootAdvisory!.Groups
                .SelectMany(group => group.CardNames)
                .Where(name => name is "Wincon Sorcery" or "Counterspell")
                .ToArray());
        Assert.Empty(roleFloorBaseline.QueriedRoles);
    }

    [Fact]
    public async Task T5_RestartRoundsAndWhatifCommit_ProduceStructuralFindingsFromResolvedDefaults()
    {
        CutLabState restartState = CreateEmptyFloorState(
            pool:
            [
                Card("Focused Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Counterspell", quantity: 7),
                Card("Wincon Sorcery", quantity: 4),
                Card("Basic Filler", quantity: 89, isLocked: true),
            ],
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Counterspell",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ]);
        FakeAnalysisContextBuilder restartAnalysisBuilder = new();
        CutLabApiController restartController = CreateApiController(restartAnalysisBuilder);

        ActionResult<CutLabDecideApiResponse> restartResponse = await restartController.PostRestartRoundsAsync(
            new CutLabRestartRoundsApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(restartState),
            },
            CancellationToken.None);

        CutLabDecideApiResponse restartPayload = AssertOk(restartResponse);
        Assert.NotEmpty(WeakFloorItems(restartPayload.Patch.StructuralFindings));

        CutLabState committedState = CreateEmptyFloorState(
            pool:
            [
                Card("Focused Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Counterspell", quantity: 7, isLocked: true),
                Card("Wincon Sorcery", quantity: 4, isLocked: true),
                Card("Arcane Signet", quantity: 1),
                Card("Basic Filler", quantity: 89, isLocked: true),
            ],
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Cut Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ]);
        FakeAnalysisContextBuilder whatifAnalysisBuilder = new();
        CutLabApiController whatifController = CreateApiController(
            whatifAnalysisBuilder,
            whatifService: new FakeCutLabWhatifService
            {
                CommitResultFactory = (_, _, _) => new CutLabWhatifCommitResult
                {
                    Applied = true,
                    State = committedState,
                    CardOut = "Arcane Signet",
                    CardIn = "Cut Card",
                },
            });

        ActionResult<CutLabWhatifApiResponse> whatifResponse = await whatifController.PostWhatifCommitAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(committedState),
                CardOut = "Arcane Signet",
                CardIn = "Cut Card",
            },
            CancellationToken.None);

        CutLabWhatifApiResponse whatifPayload = AssertOk(whatifResponse);
        Assert.NotEmpty(WeakFloorItems(whatifPayload.Patch!.StructuralFindings));
    }

    private static CutLabApiController CreateApiController(
        FakeAnalysisContextBuilder analysisBuilder,
        IRoleFloorBaselineProvider? roleFloorBaseline = null,
        IFeatureFlagCache? featureFlags = null,
        ICutLabUiPatchBuilder? patchBuilder = null,
        ICutLabWhatifService? whatifService = null)
    {
        ICutLabFloorResolver floorResolver = new CutLabFloorResolver(
            new FakeManabaseBaselineProvider(new ManabaseBracketBaseline
            {
                Bracket = 4,
                AvgLands = 36.0,
                DeckCount = 100,
            }),
            new FakeCedhLandBaselineProvider(),
            roleFloorBaseline,
            featureFlags);
        CutLabApiController controller = new(
            analysisBuilder,
            floorResolver,
            patchBuilder ?? new CutLabUiPatchBuilder(analysisBuilder, new FakeSimulationService(), floorResolver),
            new FakeSimulationService(),
            whatifService ?? new FakeCutLabWhatifService(),
            NullLogger<CutLabApiController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("deckflow.test");
        controller.Request.Headers.Origin = "https://deckflow.test";
        return controller;
    }

    private static CutLabPageService CreatePageService(
        CutLabState state,
        FakeAnalysisContextBuilder analysisBuilder,
        IRoleFloorBaselineProvider? roleFloorBaseline = null,
        IFeatureFlagCache? featureFlags = null)
        => new(
            new FakeLoader(BuildEntries(state)),
            new FakeResolver(BuildResolvedCards()),
            new FakeBanListService(),
            manabaseBaseline: new FakeManabaseBaselineProvider(new ManabaseBracketBaseline
            {
                Bracket = 4,
                AvgLands = 36.0,
                DeckCount = 100,
            }),
            cedhBaseline: new FakeCedhLandBaselineProvider(),
            roleFloorBaseline: roleFloorBaseline,
            analysisContextBuilder: analysisBuilder,
            simulationService: new FakeSimulationService(),
            logger: NullLogger<CutLabPageService>.Instance,
            featureFlags: featureFlags);

    private static CutLabRequest CreateRequest(CutLabState state)
        => new()
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            SelectedCommander = "Focused Commander",
            Bracket = 4,
            PlayExperience = "Focused",
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
        };

    private static CutLabState CreateEmptyFloorState(
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyList<CutLabDecision>? decisions = null,
        IReadOnlyList<CutLabRoleFloor>? roleFloors = null)
        => new()
        {
            Commander = "Focused Commander",
            Pool = pool,
            Decisions = decisions ?? [],
            RoleFloors = roleFloors ?? [],
            Intent = new CutLabIntent
            {
                Bracket = 4,
                PlayExperience = "Focused",
            },
        };

    private static CutLabPoolCard Card(string name, int quantity = 1, bool isCommander = false, bool isLocked = false, string? typeLine = null)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = typeLine ?? (isCommander ? "Legendary Creature" : "Spell"),
            IsCommander = isCommander,
            IsLocked = isLocked,
        };

    private static IReadOnlyDictionary<string, int> ResolveExpectedFloors(CutLabState state)
        => CutLabFloorDefaults.ResolveDefaults(
                state.Intent.Bracket,
                state.Intent.PlayExperience,
                commanderManaValue: 3,
                commanderNames: ["Focused Commander"],
                baseline: new FakeManabaseBaselineProvider(new ManabaseBracketBaseline
                {
                    Bracket = 4,
                    AvgLands = 36.0,
                    DeckCount = 100,
                }),
                cedhBaseline: new FakeCedhLandBaselineProvider(),
                roleFloorBaseline: null,
                priorFloors: state.RoleFloors)
            .ToDictionary(floor => floor.Role, floor => floor.Floor, StringComparer.OrdinalIgnoreCase);

    private static List<CutLabDecideFindingDto> WeakFloorItems(IReadOnlyList<CutLabDecideFindingGroupDto> groups)
        => groups
            .Where(group => group.Kind == CutLabFindingKind.WeakFloorCase)
            .SelectMany(group => group.Items)
            .ToList();

    private static CutLabDecideApiResponse AssertOk(ActionResult<CutLabDecideApiResponse> response)
    {
        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<CutLabDecideApiResponse>(ok.Value);
    }

    private static CutLabWhatifApiResponse AssertOk(ActionResult<CutLabWhatifApiResponse> response)
    {
        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<CutLabWhatifApiResponse>(ok.Value);
    }

    private static List<DeckEntry> BuildEntries(CutLabState state)
        => state.Pool
            .Select(card => Entry(card.Name, card.IsCommander ? "commander" : "mainboard") with
            {
                Quantity = card.Quantity,
            })
            .ToList();

    private static DeckEntry Entry(string name, string board)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = board,
        };

    private static Dictionary<string, ScryfallCard> BuildResolvedCards()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Focused Commander"] = Spell("Focused Commander", "Legendary Creature - Human Wizard", manaCost: "{1}{G}{U}", cmc: 3),
            ["Arcane Signet"] = Spell("Arcane Signet", "Artifact", manaCost: "{2}", cmc: 2),
            ["Counterspell"] = Spell("Counterspell", "Instant", manaCost: "{U}{U}", cmc: 2),
            ["Wincon Sorcery"] = Spell("Wincon Sorcery", "Sorcery", manaCost: "{3}{R}", cmc: 4),
            ["Forest"] = Spell("Forest", "Basic Land - Forest"),
            ["Basic Filler"] = Spell("Basic Filler", "Artifact", cmc: 1),
            ["Cut Card"] = Spell("Cut Card", "Artifact", cmc: 2),
        };

    private static ScryfallCard Spell(
        string name,
        string typeLine,
        string? manaCost = null,
        string? oracleText = null,
        double cmc = 0)
        => new(
            name,
            manaCost,
            typeLine,
            oracleText,
            null,
            null,
            null,
            [],
            null,
            null,
            null,
            Cmc: cmc);

    private sealed class FakeLoader(IReadOnlyList<DeckEntry> entries) : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(entries.ToList());

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(entries.ToList(), null));

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entriesToValidate, int requiredDeckSize = 100)
        {
        }
    }

    private sealed class FakeResolver(IReadOnlyDictionary<string, ScryfallCard> cardsByName) : IScryfallCardResolver
    {
        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(cardsByName.Values.ToList(), null),
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(cardsByName.TryGetValue(cardName, out ScryfallCard? card) ? card : null);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);
    }

    private sealed class FakeBanListService : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeManabaseBaselineProvider(ManabaseBracketBaseline? baseline = null) : IManabaseBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
            => baseline is not null && baseline.Bracket == bracket ? baseline : null;

        public ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames)
            => null;
    }

    private sealed class FakeCedhLandBaselineProvider : ICedhLandBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public bool TryGetBaseline(
            IReadOnlyList<string> commanderNames,
            out double mean,
            out int n,
            out double sd,
            out string? generated)
        {
            mean = 0;
            n = 0;
            sd = 0;
            generated = null;
            return false;
        }
    }

    private sealed class FakeRoleFloorBaselineProvider(IReadOnlyDictionary<string, int> floorsByRole) : IRoleFloorBaselineProvider
    {
        public List<string> QueriedRoles { get; } = [];

        public void EnsureLoaded()
        {
        }

        public bool TryGetRoleFloor(IReadOnlyList<string> commanderNames, string roleKey, out int floor)
        {
            QueriedRoles.Add(roleKey);
            return floorsByRole.TryGetValue(roleKey, out floor);
        }
    }

    private sealed class FakeSimulationService : ICutLabSimulationService
    {
        public Task<CutLabSimulationResult> BuildSnapshotResult(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabSimulationResult());

        public Task<CutLabMetricSnapshot> BuildSnapshot(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabMetricSnapshot());

        public Task<CutLabProposalDeltas> ComputeProposalDeltas(
            IReadOnlyList<CutLabPoolCard> currentWorkingList,
            string candidateCardName,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabProposalDeltas
            {
                CardName = candidateCardName,
                ChangedFamilyCount = 0,
                Deltas = [],
            });
    }

    private sealed class FakeCutLabWhatifService : ICutLabWhatifService
    {
        public Func<CutLabState, string, string, CutLabWhatifCommitResult> CommitResultFactory { get; init; }
            = (state, _, _) => new CutLabWhatifCommitResult
            {
                Applied = false,
                State = state,
                ErrorMessage = "invalid",
            };

        public Task<CutLabWhatifPreview> PreviewSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken)
            => Task.FromResult(new CutLabWhatifPreview
            {
                CardOut = cardOut,
                CardIn = cardIn,
                Deltas = [],
            });

        public bool TryValidateSwap(
            CutLabState state,
            string cardOut,
            string cardIn,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
        {
            error = null;
            return true;
        }

        public Task<CutLabWhatifCommitResult> CommitSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken)
            => Task.FromResult(CommitResultFactory(state, cardOut, cardIn));
    }

    private sealed class FakeAnalysisContextBuilder : ICutLabAnalysisContextBuilder
    {
        private readonly Dictionary<string, IReadOnlyList<ScryfallCardData>> _cache = new(StringComparer.Ordinal);

        public Task<CutLabAnalysisContext> BuildAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
        {
            List<CutLabAnalyzedCard> analyzedCards = [];
            Dictionary<string, IReadOnlyList<string>> rolesByCardName = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> roleCounts = new(StringComparer.OrdinalIgnoreCase);

            foreach (CutLabPoolCard card in workingList)
            {
                IReadOnlyList<string> roles = card.Name switch
                {
                    "Arcane Signet" => ["ramp"],
                    "Counterspell" => ["interaction-targeted"],
                    "Wincon Sorcery" => ["wincons"],
                    "Forest" => ["lands"],
                    _ => [],
                };

                rolesByCardName[card.Name] = roles;
                foreach (string role in roles)
                {
                    roleCounts[role] = roleCounts.TryGetValue(role, out int count) ? count + card.Quantity : card.Quantity;
                }

                double manaValue = card.Name switch
                {
                    "Focused Commander" => 3,
                    "Arcane Signet" => 2,
                    "Counterspell" => 2,
                    "Wincon Sorcery" => 4,
                    _ => 1,
                };

                analyzedCards.Add(new CutLabAnalyzedCard(card.Name, manaValue, card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase), roles, [])
                {
                    Quantity = card.Quantity,
                });
            }

            CutLabAnalysisContext context = new(
                analyzedCards,
                rolesByCardName,
                roleCounts,
                3,
                ManabaseMode.Focused,
                new CutLabClassificationContext([], true, true, new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
                workingList.Select(card => new ScryfallCardData
                {
                    Name = card.Name,
                    TypeLine = card.TypeLine,
                    Cmc = card.Name switch
                    {
                        "Focused Commander" => 3,
                        "Arcane Signet" => 2,
                        "Counterspell" => 2,
                        "Wincon Sorcery" => 4,
                        _ => 1,
                    },
                }).ToArray());

            _cache[CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray())] = context.ResolvedCards;
            return Task.FromResult(context);
        }

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards)
            => _cache.TryGetValue(
                CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray()),
                out cards);

        public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            bool failOpenOnLookupErrors = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScryfallCardData>>(workingList.Select(card => new ScryfallCardData
            {
                Name = card.Name,
                TypeLine = card.TypeLine,
                Cmc = card.Name switch
                {
                    "Focused Commander" => 3,
                    "Arcane Signet" => 2,
                    "Counterspell" => 2,
                    "Wincon Sorcery" => 4,
                    _ => 1,
                },
            }).ToArray());

        public void PrimeResolvedCardsCache(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> resolvedCards,
            IReadOnlyCollection<string>? unresolvedCardNames = null)
        {
            _cache[CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray())] = resolvedCards;
        }

        public bool TrySeedDerivedPool(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> sourceCards,
            out IReadOnlyList<ScryfallCardData>? seededCards)
        {
            seededCards = workingList
                .Select(card => sourceCards.FirstOrDefault(source => string.Equals(source.Name, card.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(card => card is not null)
                .Cast<ScryfallCardData>()
                .ToArray();
            return seededCards.Count == workingList.Count;
        }
    }
}
