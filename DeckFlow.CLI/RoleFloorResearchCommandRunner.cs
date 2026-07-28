using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Research;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Http;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;
using RestSharp;

namespace DeckFlow.CLI;

internal static class RoleFloorResearchCommandRunner
{
    private const double RatioLow = 0.667;
    private const double RatioHigh = 1.5;
    private const double ZThreshold = 2.0;
    // Why: when the corpus 25th percentile is zero, ComputeRatio returns 0.0 and would slide
    // under RatioLow, marking every commander divergent-low; 2.0 cards is the smallest gap worth
    // stating as a floor recommendation, and RFLR-02 requires the whole written bar in one place.
    private const double AbsoluteFloorGap = 2.0;
    private const int BreadthMinimum = 3;
    // Why: the 2026-07-16 prior set the per-cell EDHREC floor at 400 decks backing the cell, and
    // the manifest's min_decks: 8000 is the commander-selection floor for which commanders were
    // fetched, not the per-cell qualifying floor used by this harness.
    private const int EdhrecMinCellDeckCount = 400;
    private const int EdhrecThinBracketThreshold = 50;
    private const int CommanderMembershipMaxConcurrency = 8;
    private const int CommanderMembershipProgressInterval = 200;
    private const int ScryfallRateLimitRetryMaxAttempts = 4;
    private static readonly TimeSpan HarnessFallbackSearchPacingDelay = TimeSpan.FromMilliseconds(350);

    // Why: the prior five-role list was the pre-Phase-1 taxonomy, including merged
    // "interaction", which CutLabRoleAssigner no longer emits; because the tally loop only
    // increments keys already seeded, that stale key would have silently recorded zero for every
    // deck and every commander. Decision D-C also requires lands and ramp, draw stays in because
    // it is a shipped first-class role, and "other" stays out because its residual-bucket count
    // would measure classifier coverage rather than deck construction.
    private static readonly string[] TargetRoles =
    [
        "lands",
        "ramp",
        "draw",
        "interaction-targeted",
        "interaction-mass",
        "protection",
        "engines",
        "payoffs",
        "wincons",
    ];

    private static readonly int[] DiagnosticThresholds = [15, 20, 25, 30, 40, 50, 75, 100];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<int> RunAsync(
        string? connectionString,
        int minDeckCount,
        string mode,
        string cardsCachePath,
        string outputPath,
        string outputJsonPath,
        string? edhrecDataPath = null,
        CancellationToken cancellationToken = default)
    {
        const string connectionStringEnvironmentVariableName = "DECKFLOW_ROLE_FLOOR_CONNECTION_STRING";

        // Why: argv is visible in the process list for the entire multi-hour run, so the
        // environment path avoids forcing the credential into process listings; the flag remains
        // only for backward compatibility and still wins when explicitly supplied.
        string? resolvedConnectionString = RoleFloorProvenance.ResolveConnectionString(
            connectionString,
            Environment.GetEnvironmentVariable(connectionStringEnvironmentVariableName));

        if (string.IsNullOrWhiteSpace(resolvedConnectionString))
        {
            Console.Error.WriteLine(
                FormattableString.Invariant(
                    $"Either --connection-string or the {connectionStringEnvironmentVariableName} environment variable is required."));
            return 1;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Error.WriteLine("--out is required.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(outputJsonPath))
        {
            Console.Error.WriteLine("--out-json is required.");
            return 1;
        }

        try
        {
            string runTimestampUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string normalizedConnectionString = PostgresConnectionStringNormalizer.Normalize(resolvedConnectionString);
            string databaseHost = RoleFloorProvenance.DescribeDatabaseHost(normalizedConnectionString);
            string harnessCommitSha = DescribeHarnessCommitSha();
            var connectionInfo = new RelationalDatabaseConnection(
                RelationalDatabaseProvider.Postgres,
                normalizedConnectionString);
            var repository = new CategoryKnowledgeRepository(connectionInfo);
            using var serviceProvider = BuildScryfallServiceProvider();
            var resolver = serviceProvider.GetRequiredService<IScryfallCardResolver>();
            ManabaseMode resolvedMode = CutLabRoleAssigner.ResolveMode(mode);
            string? taxonomyError = ValidateTaxonomyAgainstAssigner(resolvedMode);
            if (taxonomyError is not null)
            {
                Console.Error.WriteLine(taxonomyError);
                return 1;
            }

            EdhrecReadResult? edhrecReadResult = null;
            if (!string.IsNullOrWhiteSpace(edhrecDataPath))
            {
                edhrecReadResult = EdhrecCellReader.Read(edhrecDataPath, EdhrecMinCellDeckCount);
                if (edhrecReadResult.Failure is not null)
                {
                    Console.Error.WriteLine(edhrecReadResult.Failure);
                    return 1;
                }
            }

            List<(string CommanderName, int DeckCount, string? LastProcessedUtc)> commanderRows =
                await LoadCommanderRowsAsync(repository, cancellationToken).ConfigureAwait(false);

            var commanderDecks = new Dictionary<string, CommanderDeckSet>(StringComparer.OrdinalIgnoreCase);
            var distinctCardNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var commanderDeckSets = new CommanderDeckSet?[commanderRows.Count];
            var distinctCardNameSet = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            int commandersProcessed = 0;
            int commandersWithMembership = 0;
            long rawDecksWithMembership = 0;
            var membershipLoadStopwatch = Stopwatch.StartNew();

            await Parallel.ForEachAsync(
                Enumerable.Range(0, commanderRows.Count),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = CommanderMembershipMaxConcurrency,
                },
                async (index, token) =>
                {
                    string commanderName = commanderRows[index].CommanderName;
                    IReadOnlyList<CategoryDeckMembership> memberships =
                        await repository.GetCategoryDeckMembershipForCommanderAsync(
                            commanderName,
                            boardFilter: "mainboard",
                            cancellationToken: token).ConfigureAwait(false);

                    var rawDecks = memberships
                        .GroupBy(membership => membership.DeckId)
                        .ToDictionary(
                            group => group.Key,
                            group => new HashSet<string>(
                                group.Select(membership => membership.CardName),
                                StringComparer.OrdinalIgnoreCase));

                    foreach (HashSet<string> cardNames in rawDecks.Values)
                    {
                        foreach (string cardName in cardNames)
                        {
                            distinctCardNameSet.TryAdd(cardName, 0);
                        }
                    }

                    if (rawDecks.Count > 0)
                    {
                        Interlocked.Increment(ref commandersWithMembership);
                        Interlocked.Add(ref rawDecksWithMembership, rawDecks.Count);
                    }

                    // RAW N can undercount reality because a processed deck with zero category-tagged
                    // cards is invisible to this reconstruction pipeline, not merely thin.
                    commanderDeckSets[index] = new CommanderDeckSet
                    {
                        CommanderName = commanderName,
                        RawDecks = rawDecks,
                    };

                    int processed = Interlocked.Increment(ref commandersProcessed);
                    if (processed % CommanderMembershipProgressInterval == 0 || processed == commanderRows.Count)
                    {
                        Console.WriteLine(
                            FormattableString.Invariant(
                                $"Loaded commander memberships {processed}/{commanderRows.Count} in {membershipLoadStopwatch.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)} (commandersWithMembership={Volatile.Read(ref commandersWithMembership)}, rawDecks={Volatile.Read(ref rawDecksWithMembership)})."));
                    }
                }).ConfigureAwait(false);

            foreach (CommanderDeckSet commanderDeckSet in commanderDeckSets.Where(set => set is not null).Cast<CommanderDeckSet>())
            {
                commanderDecks[commanderDeckSet.CommanderName] = commanderDeckSet;
            }

            foreach (string cardName in distinctCardNameSet.Keys)
            {
                distinctCardNames.Add(cardName);
            }

            if (edhrecReadResult is not null)
            {
                foreach (EdhrecCell cell in edhrecReadResult.Cells)
                {
                    foreach (EdhrecCard card in cell.Cards)
                    {
                        distinctCardNames.Add(card.Name);
                    }
                }
            }

            IReadOnlyDictionary<long, string?> contentHashes = await repository
                .GetContentHashesByIdsAsync(
                    commanderDecks.Values.SelectMany(set => set.RawDecks.Keys).Distinct().ToList(),
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (CommanderDeckSet commander in commanderDecks.Values)
            {
                commander.RepresentativeDecks = DeduplicateDecks(commander.RawDecks, contentHashes);
            }

            CardResolutionResult cardResolution = await ResolveCardsAsync(
                resolver,
                distinctCardNames,
                cardsCachePath,
                cancellationToken).ConfigureAwait(false);

            List<EdhrecRolePointEstimate> edhrecPointEstimates = [];
            List<EdhrecBracketCoverage> edhrecBracketCoverage = [];
            List<EdhrecLandSelfCheck> edhrecLandSelfChecks = [];
            int edhrecParseFailureCount = 0;
            int edhrecCardCountAnomalyCount = 0;
            string? edhrecMinSaveDate = null;
            string? edhrecMaxSaveDate = null;

            foreach (CommanderDeckSet commander in commanderDecks.Values)
            {
                foreach ((long deckId, HashSet<string> cardNames) in commander.RepresentativeDecks)
                {
                    var roleCounts = TargetRoles.ToDictionary(role => role, _ => 0, StringComparer.Ordinal);
                    foreach (string cardName in cardNames)
                    {
                        if (!cardResolution.ResolvedCards.TryGetValue(cardName, out ScryfallCardData? card))
                        {
                            continue;
                        }

                        // Commander is singleton for every nonland card that can plausibly earn one
                        // of the target roles, so the research harness classifies quantity as 1.
                        CardFact fact = ScryfallCardFactMapper.ToCardFact(card, quantity: 1, isCommander: false);
                        // Commander Spellbook combo-piece resolution is out of scope here, so this
                        // can only undercount wincons, never overcount them.
                        IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(fact, [], isComboPiece: false, resolvedMode);
                        foreach (string role in roles)
                        {
                            if (roleCounts.ContainsKey(role))
                            {
                                roleCounts[role]++;
                            }
                        }
                    }

                    commander.RepresentativeRoleCounts[deckId] = roleCounts;
                }
            }

            if (edhrecReadResult is not null)
            {
                edhrecParseFailureCount = edhrecReadResult.Cells.Sum(cell => cell.ParseFailures.Count);
                edhrecCardCountAnomalyCount = edhrecReadResult.CardCountAnomalies.Count;
                edhrecMinSaveDate = edhrecReadResult.Cells.Count == 0
                    ? null
                    : edhrecReadResult.Cells.Min(cell => cell.MinSaveDate);
                edhrecMaxSaveDate = edhrecReadResult.Cells.Count == 0
                    ? null
                    : edhrecReadResult.Cells.Max(cell => cell.MaxSaveDate);

                foreach (EdhrecCell cell in edhrecReadResult.Cells)
                {
                    IReadOnlyDictionary<string, int> roleCounts = EdhrecRoleTally.TallyRoleCounts(
                        TargetRoles,
                        cell.Cards
                            .Where(cardEntry => cardResolution.ResolvedCards.TryGetValue(cardEntry.Name, out _))
                            .Select(cardEntry =>
                            {
                                ScryfallCardData card = cardResolution.ResolvedCards[cardEntry.Name];
                                CardFact fact = ScryfallCardFactMapper.ToCardFact(card, quantity: 1, isCommander: false);
                                IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(fact, [], isComboPiece: false, resolvedMode);
                                return (Roles: roles, Quantity: cardEntry.Quantity);
                            }));

                    int harnessLandCount = roleCounts["lands"];
                    // Why: EDHREC's aggregate land count and the harness's
                    // CutLabLockRules.IsLand(typeLine) || fact.HasLandFace test can legitimately
                    // disagree on modal double-faced cards, so a mismatch is a methodology finding
                    // for the later lands verdict rather than a run failure.
                    edhrecLandSelfChecks.Add(new EdhrecLandSelfCheck
                    {
                        CellId = BuildEdhrecCellId(cell),
                        EdhrecLandCount = cell.EdhrecLandCount,
                        HarnessLandCount = harnessLandCount,
                        Delta = harnessLandCount - cell.EdhrecLandCount,
                    });

                    foreach (string role in TargetRoles)
                    {
                        edhrecPointEstimates.Add(new EdhrecRolePointEstimate
                        {
                            Source = RoleFloorSource.Edhrec,
                            Role = role,
                            CommanderName = cell.Commander,
                            BracketSlug = cell.Bracket,
                            BracketIndex = cell.BracketIndex,
                            Count = roleCounts[role],
                            DeckCount = cell.NDecks,
                            Qualifies = cell.Qualifies,
                        });
                    }
                }

                edhrecBracketCoverage = BuildEdhrecBracketCoverage(edhrecReadResult);
            }

            Dictionary<string, RoleBaseline> corpusBaseline = BuildCorpusBaseline(commanderDecks.Values);
            var qualifyingCommanders = new Dictionary<string, CommanderResearch>(StringComparer.OrdinalIgnoreCase);
            var postgresDistributions = new List<PostgresRoleDistribution>();
            foreach (CommanderDeckSet commander in commanderDecks.Values.Where(set => set.DedupedN >= minDeckCount))
            {
                var commanderResearch = new CommanderResearch
                {
                    CommanderName = commander.CommanderName,
                    RawN = commander.RawN,
                    N = commander.DedupedN,
                };

                foreach (string role in TargetRoles)
                {
                    List<double> perDeckCounts = commander.RepresentativeRoleCounts.Values
                        .Select(counts => (double)counts[role])
                        .ToList();
                    double commanderMean = perDeckCounts.Count == 0 ? 0.0 : perDeckCounts.Average();
                    double commanderP25 = perDeckCounts.Count == 0
                        ? 0.0
                        : RoleFloorDivergenceStats.ComputePercentile(perDeckCounts, 0.25);
                    RoleBaseline baseline = corpusBaseline[role];
                    commanderResearch.Roles[role] = new CommanderRoleStat
                    {
                        Mean = commanderMean,
                        P25 = commanderP25,
                        Ratio = RoleFloorDivergenceStats.ComputeRatio(commanderMean, baseline.Mean),
                        ZScore = RoleFloorDivergenceStats.ComputeZScore(commanderMean, baseline.Mean, baseline.StdDev, commander.DedupedN),
                        CohensD = RoleFloorDivergenceStats.ComputeCohensD(commanderMean, baseline.Mean, baseline.StdDev),
                        ClearsBar = RoleFloorDivergenceStats.ClearsFloorBar(
                            commander.DedupedN,
                            commanderP25,
                            baseline.P25,
                            commanderMean,
                            baseline.Mean,
                            baseline.StdDev,
                            minDeckCount,
                            ratioLow: RatioLow,
                            ratioHigh: RatioHigh,
                            zThreshold: ZThreshold,
                            absoluteFloorGap: AbsoluteFloorGap),
                    };

                    postgresDistributions.Add(new PostgresRoleDistribution
                    {
                        Source = RoleFloorSource.Postgres,
                        Role = role,
                        CommanderName = commander.CommanderName,
                        DeckCount = commander.RawN,
                        Mean = commanderMean,
                        P25 = commanderP25,
                        StdDev = baseline.StdDev,
                        Ratio = commanderResearch.Roles[role].Ratio,
                        ZScore = commanderResearch.Roles[role].ZScore,
                        CohensD = commanderResearch.Roles[role].CohensD,
                        ClearsBar = commanderResearch.Roles[role].ClearsBar,
                    });
                }

                qualifyingCommanders[commander.CommanderName] = commanderResearch;
            }

            Dictionary<int, int> thresholdCounts = DiagnosticThresholds.ToDictionary(
                threshold => threshold,
                threshold => commanderDecks.Values.Count(set => set.DedupedN >= threshold));
            if (RoleFloorGuards.HasNoQualifyingCommanders(qualifyingCommanders.Count))
            {
                // Why: HasNoQualifyingCommanders is unit-tested in Core, but only plan 02-08's
                // --min-decks 999999 smoke run proves this guard still sits before artifact writes
                // rather than after BuildGoNoGo/WriteFindingsFiles.
                Console.Error.WriteLine(FormattableString.Invariant(
                    $"Zero commanders met the minimum deduped deck count of {minDeckCount}."));
                Console.Error.WriteLine(FormattableString.Invariant(
                    $"Commander rows enumerated: {commanderRows.Count}."));
                Console.Error.WriteLine("ThresholdCounts:");
                foreach ((int threshold, int count) in thresholdCounts.OrderBy(pair => pair.Key))
                {
                    Console.Error.WriteLine(FormattableString.Invariant($"  {threshold}: {count}"));
                }

                Console.Error.WriteLine("NO findings artifact was written.");
                return 2;
            }

            var computation = new ResearchComputation
            {
                MinDeckCount = minDeckCount,
                DatabaseHost = databaseHost,
                RunTimestampUtc = runTimestampUtc,
                HarnessCommitSha = harnessCommitSha,
                CommandersEnumerated = commanderRows.Count,
                RawDeckCount = commanderDecks.Values.Sum(set => set.RawN),
                DedupedDeckCount = commanderDecks.Values.Sum(set => set.DedupedN),
                UnresolvedNotFoundCount = cardResolution.UnresolvedNotFoundCount,
                UnresolvedRateLimitedAfterRetryCount = cardResolution.UnresolvedRateLimitedAfterRetryCount,
                PostgresCoverage = new PostgresCoverage
                {
                    CommandersEnumerated = commanderRows.Count,
                    CommandersWithMembership = commandersWithMembership,
                    RawDeckCount = commanderDecks.Values.Sum(set => set.RawN),
                    DedupedDeckCount = commanderDecks.Values.Sum(set => set.DedupedN),
                    CommandersQualifying = qualifyingCommanders.Count,
                    UnresolvedNotFoundCount = cardResolution.UnresolvedNotFoundCount,
                    UnresolvedRateLimitedAfterRetryCount = cardResolution.UnresolvedRateLimitedAfterRetryCount,
                },
                CorpusBaseline = corpusBaseline,
                Commanders = qualifyingCommanders,
                PostgresDistributions = postgresDistributions,
                EdhrecPointEstimates = edhrecPointEstimates,
                EdhrecCoverage = new EdhrecCoverage
                {
                    CellsFetched = edhrecReadResult?.Cells.Count ?? 0,
                    CellsQualifying = edhrecReadResult?.Cells.Count(cell => cell.Qualifies) ?? 0,
                    CellsMissing = edhrecReadResult is null
                        ? 0
                        : edhrecReadResult.MissingCells.Count + edhrecReadResult.InvalidCells.Count,
                    InvalidCells = edhrecReadResult?.InvalidCells.Count ?? 0,
                    UnexpectedCells = edhrecReadResult?.UnexpectedCells.Count ?? 0,
                    CommandersReached = edhrecReadResult?.Cells
                        .Select(cell => cell.Slug)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() ?? 0,
                    MinCellDeckCount = EdhrecMinCellDeckCount,
                    MinSaveDate = edhrecMinSaveDate,
                    MaxSaveDate = edhrecMaxSaveDate,
                    Brackets = edhrecBracketCoverage,
                    LandSelfChecks = edhrecLandSelfChecks,
                },
                EdhrecParseFailureCount = edhrecParseFailureCount,
                EdhrecCardCountAnomalyCount = edhrecCardCountAnomalyCount,
                ThresholdCounts = thresholdCounts,
            };

            computation.GoNoGo = BuildGoNoGo(computation.Commanders);
            WriteFindingsFiles(computation, outputPath, outputJsonPath);

            string goRoles = string.Join(", ", computation.GoNoGo
                .Where(pair => string.Equals(pair.Value.JsonStatus, "go", StringComparison.Ordinal))
                .Select(pair => pair.Key));
            string signalRoles = string.Join(", ", computation.GoNoGo
                .Where(pair => string.Equals(pair.Value.JsonStatus, "signal-present", StringComparison.Ordinal))
                .Select(pair => pair.Key));

            Console.WriteLine(
                FormattableString.Invariant(
                    $"RawDecks={computation.RawDeckCount}, DedupedDecks={computation.DedupedDeckCount}, Commanders={computation.CommandersEnumerated}, QualifyingCommanders={computation.Commanders.Count}, GoRoles={(string.IsNullOrWhiteSpace(goRoles) ? "none" : goRoles)}, SignalRoles={(string.IsNullOrWhiteSpace(signalRoles) ? "none" : signalRoles)}"));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static ServiceProvider BuildScryfallServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDeckFlowHttpClients();
        services.AddDeckFlowResiliencePipelines();
        services.AddSingleton<IScryfallRestClientFactory, ScryfallRestClientFactory>();
        services.AddSingleton<IScryfallCardResolver>(serviceProvider =>
            new ScryfallCardResolver(
                serviceProvider.GetRequiredService<IScryfallRestClientFactory>(),
                serviceProvider.GetRequiredService<ResiliencePipelineProvider<string>>()));
        return services.BuildServiceProvider();
    }

    private static string DescribeHarnessCommitSha()
    {
        try
        {
            (int revParseExitCode, string revParseStdout) = RunGitCommand("rev-parse", "--short", "HEAD");
            int effectiveExitCode = revParseExitCode;
            string? statusPorcelainStdout = null;
            if (revParseExitCode == 0)
            {
                (int statusExitCode, string statusStdout) = RunGitCommand("status", "--porcelain");
                effectiveExitCode = statusExitCode;
                statusPorcelainStdout = statusStdout;
            }

            return RoleFloorProvenance.FormatCommitSha(effectiveExitCode, revParseStdout, statusPorcelainStdout);
        }
        catch
        {
            return RoleFloorProvenance.FormatCommitSha(exitCode: 1, revParseStdout: null, statusPorcelainStdout: null);
        }
    }

    private static (int ExitCode, string Stdout) RunGitCommand(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Why: the harness may run from a dirty worktree, including its own local edits, and an
        // artifact that claims a clean SHA in that state would misrepresent the code that produced it.
        return (process.ExitCode, stdout);
    }

    private static async Task<List<(string CommanderName, int DeckCount, string? LastProcessedUtc)>> LoadCommanderRowsAsync(
        CategoryKnowledgeRepository repository,
        CancellationToken cancellationToken)
    {
        var rows = new List<(string CommanderName, int DeckCount, string? LastProcessedUtc)>();
        for (int page = 1; ; page++)
        {
            IReadOnlyList<(string CommanderName, int DeckCount, string? LastProcessedUtc)> pageRows =
                await repository.GetPagedProcessedCommanderRowsAsync(page, 200, cancellationToken).ConfigureAwait(false);
            if (pageRows.Count == 0)
            {
                break;
            }

            rows.AddRange(pageRows);
            if (page % 5 == 0)
            {
                Console.WriteLine($"Paged {page} commander batches ({rows.Count} commanders so far).");
            }
        }

        return rows;
    }

    private static Dictionary<long, HashSet<string>> DeduplicateDecks(
        IReadOnlyDictionary<long, HashSet<string>> rawDecks,
        IReadOnlyDictionary<long, string?> contentHashes)
    {
        var representatives = new Dictionary<long, HashSet<string>>();

        foreach (IGrouping<string?, long> hashGroup in rawDecks.Keys.GroupBy(
                     deckId => contentHashes.TryGetValue(deckId, out string? hash) ? hash : null,
                     StringComparer.Ordinal))
        {
            if (hashGroup.Key is null)
            {
                foreach (long deckId in hashGroup)
                {
                    representatives[deckId] = rawDecks[deckId];
                }

                continue;
            }

            long representativeDeckId = hashGroup.Min();
            representatives[representativeDeckId] = rawDecks[representativeDeckId];
        }

        return representatives;
    }

    private static async Task<CardResolutionResult> ResolveCardsAsync(
        IScryfallCardResolver resolver,
        IReadOnlyCollection<string> distinctCardNames,
        string cardsCachePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(distinctCardNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardsCachePath);

        string cacheDirectory = Path.GetDirectoryName(cardsCachePath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(cacheDirectory);

        var cache = LoadCardCache(cardsCachePath);
        var unresolvedNotFoundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolvedRateLimitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> uncachedNames = distinctCardNames
            .Where(name => !cache.ContainsKey(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int offset = 0; offset < uncachedNames.Count; offset += 75)
        {
            List<string> batchNames = uncachedNames.Skip(offset).Take(75).ToList();
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new { identifiers = batchNames.Select(cardName => (object)new { name = cardName }).ToArray() });

            RestResponse<ScryfallCollectionResponse>? response = await ExecuteWithScryfall429RetryAsync(
                operationName: $"cards/collection batch {offset / 75 + 1}",
                operation: token => resolver.ExecuteCollectionAsync(request, token),
                cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                foreach (string cardName in batchNames)
                {
                    unresolvedRateLimitedNames.Add(cardName);
                }

                SnapshotFileWriter.WriteLfFile(cardsCachePath, JsonSerializer.Serialize(cache, JsonOptions));
                continue;
            }

            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during role-floor research.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            var cardsByNormalizedName = response.Data.Data
                .GroupBy(card => CardNormalizer.Normalize(card.Name), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (string cardName in batchNames)
            {
                string normalizedName = CardNormalizer.Normalize(cardName);
                ScryfallCard? resolvedCard = cardsByNormalizedName.TryGetValue(normalizedName, out ScryfallCard? hit)
                    ? hit
                    : await ExecuteWithScryfall429RetryAsync(
                        operationName: $"fallback search for {cardName}",
                        operation: token => resolver.SearchFallbackCardAsync(cardName, token),
                        cancellationToken).ConfigureAwait(false);

                if (resolvedCard is null)
                {
                    if (cardsByNormalizedName.ContainsKey(normalizedName))
                    {
                        continue;
                    }

                    if (unresolvedRateLimitedNames.Contains(cardName))
                    {
                        continue;
                    }

                    unresolvedNotFoundNames.Add(cardName);
                    continue;
                }

                cache[cardName] = ScryfallCardDataMapper.ToCardData(resolvedCard);

                if (!ReferenceEquals(resolvedCard, hit))
                {
                    await Task.Delay(HarnessFallbackSearchPacingDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            SnapshotFileWriter.WriteLfFile(cardsCachePath, JsonSerializer.Serialize(cache, JsonOptions));
        }

        string unresolvedPath = Path.Combine(cacheDirectory, "unresolved-cards.txt");
        string unresolvedNotFoundPath = Path.Combine(cacheDirectory, "unresolved-not-found-cards.txt");
        string unresolvedRateLimitedPath = Path.Combine(cacheDirectory, "unresolved-rate-limited-after-retry-cards.txt");
        SnapshotFileWriter.WriteLfFile(
            unresolvedPath,
            string.Join(
                '\n',
                unresolvedNotFoundNames
                    .Select(name => $"not_found\t{name}")
                    .Concat(unresolvedRateLimitedNames.Select(name => $"rate_limited_after_retry\t{name}"))
                    .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)));
        SnapshotFileWriter.WriteLfFile(
            unresolvedNotFoundPath,
            string.Join('\n', unresolvedNotFoundNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));
        SnapshotFileWriter.WriteLfFile(
            unresolvedRateLimitedPath,
            string.Join('\n', unresolvedRateLimitedNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));
        return new CardResolutionResult(
            cache,
            unresolvedNotFoundNames.Count,
            unresolvedRateLimitedNames.Count);
    }

    private static async Task<T?> ExecuteWithScryfall429RetryAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);

        for (int attempt = 1; attempt <= ScryfallRateLimitRetryMaxAttempts; attempt++)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == ScryfallRateLimitRetryMaxAttempts)
                {
                    Console.WriteLine(
                        FormattableString.Invariant(
                            $"Scryfall 429 persisted for {operationName}; excluding from tallies after {attempt} attempts."));
                    return default;
                }

                TimeSpan delay = ComputeScryfall429Backoff(attempt);
                Console.WriteLine(
                    FormattableString.Invariant(
                        $"Scryfall 429 during {operationName}; retrying in {delay.TotalSeconds:0.#}s (attempt {attempt}/{ScryfallRateLimitRetryMaxAttempts})."));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        return default;
    }

    private static TimeSpan ComputeScryfall429Backoff(int attempt)
    {
        int[] delaySeconds = [5, 8, 12, 15];
        int safeAttemptIndex = Math.Clamp(attempt - 1, 0, delaySeconds.Length - 1);
        return TimeSpan.FromSeconds(delaySeconds[safeAttemptIndex]);
    }

    private static Dictionary<string, ScryfallCardData> LoadCardCache(string cardsCachePath)
    {
        if (!File.Exists(cardsCachePath))
        {
            return new Dictionary<string, ScryfallCardData>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, ScryfallCardData>? persisted =
            JsonSerializer.Deserialize<Dictionary<string, ScryfallCardData>>(File.ReadAllText(cardsCachePath), JsonOptions);
        return persisted is null
            ? new Dictionary<string, ScryfallCardData>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ScryfallCardData>(persisted, StringComparer.OrdinalIgnoreCase);
    }

    // Why: this seam is internal so DeckFlow.Core.Tests can exercise the real CLI guard through the
    // existing InternalsVisibleTo, matching the project rule that CLI additions carry Core test coverage.
    internal static string? ValidateTaxonomyAgainstAssigner(ManabaseMode mode)
    {
        string? roleKeyReadError = RoleFloorGuards.TryReadShippedRoleKeys(
            typeof(CutLabRoleAssigner),
            "RoleKeys",
            out string[]? shippedRoleKeys);
        if (roleKeyReadError is not null)
        {
            return roleKeyReadError;
        }

        // Why: CutLabRoleAssigner.RoleKeys is private static readonly, so the harness reflects the
        // authoritative shipped list instead of hand-copying it; "other" is a separate const
        // outside RoleKeys and is deliberately excluded per D-01; and this turns silent taxonomy
        // drift from a corpus-wide zero into a startup abort for any of the nine shipped keys.
        CardFact[] probes =
        [
            new()
            {
                Name = "Forest",
                Quantity = 1,
                TypeLine = "Basic Land — Forest",
                // Source: DeckFlow.Web.Tests/CutLabRoleAssignerTests.AssignRoles_Forest_MapsToExactlyLands
            },
            new()
            {
                Name = "Cultivate",
                Quantity = 1,
                TypeLine = "Sorcery",
                OracleText = "Search your library for up to two basic land cards, reveal those cards, put one onto the battlefield tapped and the other into your hand, then shuffle.",
                // Source: DeckFlow.Web.Tests/CutLabRoleAssignerTests.AssignRoles_Cultivate_MapsToRampOnly
            },
            new()
            {
                Name = "Quick Study",
                Quantity = 1,
                TypeLine = "Instant",
                OracleText = "Draw two cards.",
                // Source: DeckFlow.Web.Tests/CutLabRoleAssignerTests.AssignRoles_OneShotDrawSpell_NotEngine
            },
            new()
            {
                Name = "Swords to Plowshares",
                Quantity = 1,
                TypeLine = "Instant",
                OracleText = "Exile target creature. Its controller gains life equal to its power.",
                // Source: DeckFlow.Web.Tests/CutLabRoleAssignerTests.AssignRoles_SwordsToPlowshares_IsTargetedOnlyInCasualViaPreGateSignal
            },
            new()
            {
                Name = "Wrath of God",
                Quantity = 1,
                TypeLine = "Sorcery",
                OracleText = "Destroy all creatures. They can't be regenerated.",
                // Source: DeckFlow.Web.Tests/CutLabRoleAssignerTests.AssignRoles_WipeByOracleHeuristic_IsMassOnly
            },
            new()
            {
                Name = "Protection Wand",
                Quantity = 1,
                TypeLine = "Artifact",
                OracleText = "{T}: Target creature you control gains hexproof until end of turn.",
                // Source: DeckFlow.Web.Tests/Manabase/PlanRoleClassifierTests.Classify_ProtectionPermanent_IsInteractionAndNothingElse
            },
            new()
            {
                Name = "Phyrexian Arena",
                Quantity = 1,
                TypeLine = "Enchantment",
                OracleText = "At the beginning of your upkeep, draw a card and you lose 1 life.",
                // Source: DeckFlow.Web.Tests/CutLabRoleAssignerTests.AssignRoles_PermanentDrawEngine_IsEngine;
                // the guard passes empty categories, and this oracle text alone satisfies the heuristic path.
            },
            new()
            {
                Name = "Avatar Finisher",
                Quantity = 1,
                TypeLine = "Creature — Avatar",
                OracleText = "Whenever this attacks, each opponent loses 3 life.",
                // Source: DeckFlow.Web.Tests/Manabase/PlanRoleClassifierTests.Classify_PermanentPayoff_IsKept;
                // the guard passes empty categories, and this oracle text alone satisfies the heuristic path.
            },
            new()
            {
                Name = "Torment of Hailfire",
                Quantity = 1,
                TypeLine = "Sorcery",
                OracleText = "Repeat the following process X times. Each opponent loses 3 life unless that player sacrifices a nonland permanent or discards a card.",
                // Source: DeckFlow.Web.Tests/CutLabRoleAssignerTests.AssignRoles_TormentOfHailfire_IsWinconDespitePlanRolePermanentGate;
                // the guard passes empty categories, and this oracle text alone satisfies the heuristic path.
            },
        ];

        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CardFact probe in probes)
        {
            foreach (string role in CutLabRoleAssigner
                         .AssignRoles(probe, [], isComboPiece: false, mode))
            {
                emittedKeys.Add(role);
            }
        }

        return RoleFloorGuards.FindTaxonomyDrift(shippedRoleKeys!, TargetRoles, emittedKeys, residualRoleKey: "other");
    }

    private static Dictionary<string, RoleBaseline> BuildCorpusBaseline(IEnumerable<CommanderDeckSet> commanderDecks)
    {
        var perRoleCounts = TargetRoles.ToDictionary(
            role => role,
            _ => new List<double>(),
            StringComparer.Ordinal);

        foreach (Dictionary<string, int> deckCounts in commanderDecks.SelectMany(set => set.RepresentativeRoleCounts.Values))
        {
            foreach (string role in TargetRoles)
            {
                perRoleCounts[role].Add(deckCounts[role]);
            }
        }

        var baseline = new Dictionary<string, RoleBaseline>(StringComparer.Ordinal);
        foreach (string role in TargetRoles)
        {
            List<double> counts = perRoleCounts[role];
            if (counts.Count == 0)
            {
                baseline[role] = new RoleBaseline();
                continue;
            }

            double mean = counts.Average();
            baseline[role] = new RoleBaseline
            {
                Mean = mean,
                StdDev = ComputePopulationStdDev(counts, mean),
                P25 = RoleFloorDivergenceStats.ComputePercentile(counts, 0.25),
            };
        }

        return baseline;
    }

    private static List<EdhrecBracketCoverage> BuildEdhrecBracketCoverage(EdhrecReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);

        var brackets = new List<EdhrecBracketCoverage>(readResult.Brackets.Count);
        for (int index = 0; index < readResult.Brackets.Count; index++)
        {
            string bracketSlug = readResult.Brackets[index];
            List<EdhrecCell> bracketCells = readResult.Cells
                .Where(cell => string.Equals(cell.Bracket, bracketSlug, StringComparison.Ordinal))
                .ToList();
            int qualifyingCount = bracketCells.Count(cell => cell.Qualifies);

            brackets.Add(new EdhrecBracketCoverage
            {
                BracketSlug = bracketSlug,
                BracketIndex = index + 1,
                CellsFetched = bracketCells.Count,
                CellsQualifying = qualifyingCount,
                MedianBackingDeckCount = bracketCells.Count == 0
                    ? 0.0
                    : RoleFloorDivergenceStats.ComputePercentile(
                        bracketCells.Select(cell => (double)cell.NDecks).ToList(),
                        0.5),
                SupportLabel = BuildEdhrecSupportLabel(qualifyingCount),
            });
        }

        return brackets;
    }

    private static string BuildEdhrecSupportLabel(int qualifyingCount)
    {
        if (qualifyingCount <= 1)
        {
            return "NOT REPORTED — insufficient cells";
        }

        if (qualifyingCount < EdhrecThinBracketThreshold)
        {
            return FormattableString.Invariant($"THIN — {qualifyingCount} qualifying cells");
        }

        // Why: on the 2026-07-27 corpus this yields B1 NOT REPORTED (1 qualifying cell of 305)
        // and B5 THIN (40), so a one-cell bracket figure is treated as a single deck's number
        // wearing the costume of an average rather than presented as supported. That matches the
        // independent B1 omission already present in ManabaseAnalysisService.cs:603-605 and the
        // committed DeckFlow.Web/Data/manabase-baseline/latest.json snapshot.
        return "reported";
    }

    private static string BuildEdhrecCellId(EdhrecCell cell)
        => FormattableString.Invariant($"{cell.Slug}__{cell.Bracket}");

    private static Dictionary<string, RoleOutcome> BuildGoNoGo(IReadOnlyDictionary<string, CommanderResearch> qualifyingCommanders)
    {
        var outcomes = new Dictionary<string, RoleOutcome>(StringComparer.Ordinal);
        foreach (string role in TargetRoles)
        {
            List<string> citingCommanders = qualifyingCommanders.Values
                .Where(commander => commander.Roles[role].ClearsBar)
                .OrderByDescending(commander => commander.N)
                .ThenBy(commander => commander.CommanderName, StringComparer.Ordinal)
                .Select(commander => commander.CommanderName)
                .ToList();

            if (citingCommanders.Count >= BreadthMinimum)
            {
                outcomes[role] = new RoleOutcome
                {
                    MarkdownStatus = "go",
                    JsonStatus = "go",
                    CitingCommanders = citingCommanders,
                    ClearingCommanderCount = citingCommanders.Count,
                };
                continue;
            }

            if (citingCommanders.Count > 0)
            {
                outcomes[role] = new RoleOutcome
                {
                    MarkdownStatus = "signal present but insufficient breadth",
                    JsonStatus = "signal-present",
                    CitingCommanders = citingCommanders,
                    ClearingCommanderCount = citingCommanders.Count,
                };
                continue;
            }

            outcomes[role] = new RoleOutcome
            {
                MarkdownStatus = "no-go",
                JsonStatus = "no-go",
                CitingCommanders = [],
                ClearingCommanderCount = 0,
            };
        }

        return outcomes;
    }

    private static void WriteFindingsFiles(ResearchComputation computation, string outputPath, string outputJsonPath)
    {
        string? markdownDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(markdownDirectory))
        {
            Directory.CreateDirectory(markdownDirectory);
        }

        string? jsonDirectory = Path.GetDirectoryName(outputJsonPath);
        if (!string.IsNullOrWhiteSpace(jsonDirectory))
        {
            Directory.CreateDirectory(jsonDirectory);
        }

        SnapshotFileWriter.WriteLfFile(outputPath, BuildMarkdownReport(computation));
        SnapshotFileWriter.WriteLfFile(outputJsonPath, JsonSerializer.Serialize(BuildJsonPayload(computation), CreateResearchJsonOptions()));
    }

    private static string BuildMarkdownReport(ResearchComputation computation)
    {
        var builder = new StringBuilder();
        IReadOnlyList<string> provenanceWarnings = RoleFloorProvenance.BuildProvenanceWarnings(
            computation.DatabaseHost,
            computation.HarnessCommitSha,
            computation.RawDeckCount,
            computation.DedupedDeckCount);

        builder.AppendLine("# Role-Floor Divergence Research");
        builder.AppendLine();
        builder.AppendLine("## Run Provenance");
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("|-------|-------|");
        builder.AppendLine($"| Database Host | {EscapePipe(computation.DatabaseHost)} |");
        builder.AppendLine($"| Run Timestamp (UTC) | {EscapePipe(computation.RunTimestampUtc)} |");
        builder.AppendLine($"| Harness Commit SHA | {EscapePipe(computation.HarnessCommitSha)} |");
        builder.AppendLine(FormattableString.Invariant($"| Commanders Enumerated | {computation.CommandersEnumerated} |"));
        builder.AppendLine(FormattableString.Invariant($"| Raw Deck Count | {computation.RawDeckCount} |"));
        builder.AppendLine(FormattableString.Invariant($"| Deduped Deck Count | {computation.DedupedDeckCount} |"));
        builder.AppendLine(FormattableString.Invariant($"| Minimum Deck Count | {computation.MinDeckCount} |"));
        foreach (string warning in provenanceWarnings)
        {
            builder.AppendLine($"> **WARNING — provenance degraded:** {warning}");
        }

        builder.AppendLine();
        builder.AppendLine("## Corpus Coverage");
        builder.AppendLine("### Postgres (within-commander distributions)");
        builder.AppendLine("| Metric | Value |");
        builder.AppendLine("|--------|------:|");
        builder.AppendLine(FormattableString.Invariant($"| Commanders enumerated | {computation.PostgresCoverage.CommandersEnumerated} |"));
        builder.AppendLine(FormattableString.Invariant($"| Commanders with membership | {computation.PostgresCoverage.CommandersWithMembership} |"));
        builder.AppendLine(FormattableString.Invariant($"| Raw deck count | {computation.PostgresCoverage.RawDeckCount} |"));
        builder.AppendLine(FormattableString.Invariant($"| Deduped deck count | {computation.PostgresCoverage.DedupedDeckCount} |"));
        builder.AppendLine(FormattableString.Invariant($"| Commanders qualifying at DEDUPED N >= {computation.MinDeckCount} | {computation.PostgresCoverage.CommandersQualifying} |"));
        builder.AppendLine(FormattableString.Invariant($"| Unresolved cards (not_found) | {computation.PostgresCoverage.UnresolvedNotFoundCount} |"));
        builder.AppendLine(FormattableString.Invariant($"| Unresolved cards (rate_limited_after_retry) | {computation.PostgresCoverage.UnresolvedRateLimitedAfterRetryCount} |"));
        builder.AppendLine(FormattableString.Invariant($"| Unresolved cards (total) | {computation.PostgresCoverage.UnresolvedCardCount} |"));
        builder.AppendLine();
        builder.AppendLine("### EDHREC (commander x bracket grid)");
        builder.AppendLine("| Metric | Value |");
        builder.AppendLine("|--------|------:|");
        builder.AppendLine(FormattableString.Invariant($"| Cells fetched | {computation.EdhrecCoverage.CellsFetched} |"));
        builder.AppendLine(FormattableString.Invariant($"| Cells qualifying at >= {computation.EdhrecCoverage.MinCellDeckCount} decks backing cell | {computation.EdhrecCoverage.CellsQualifying} |"));
        builder.AppendLine(FormattableString.Invariant($"| Cells missing or invalid | {computation.EdhrecCoverage.CellsMissing} |"));
        builder.AppendLine(FormattableString.Invariant($"| Invalid cells | {computation.EdhrecCoverage.InvalidCells} |"));
        builder.AppendLine(FormattableString.Invariant($"| Unexpected cells | {computation.EdhrecCoverage.UnexpectedCells} |"));
        builder.AppendLine(FormattableString.Invariant($"| Commanders reached | {computation.EdhrecCoverage.CommandersReached} |"));
        builder.AppendLine(FormattableString.Invariant($"| Per-cell minimum | {computation.EdhrecCoverage.MinCellDeckCount} |"));
        builder.AppendLine(FormattableString.Invariant($"| Corpus save-date range | {FormatEdhrecDateRange(computation.EdhrecCoverage.MinSaveDate, computation.EdhrecCoverage.MaxSaveDate)} |"));
        if (computation.EdhrecCoverage.Brackets.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("| Bracket | Index | Cells fetched | Cells qualifying | Median backing deck count | Support |");
            builder.AppendLine("|---------|------:|--------------:|-----------------:|--------------------------:|---------|");
            foreach (EdhrecBracketCoverage bracket in computation.EdhrecCoverage.Brackets.OrderBy(row => row.BracketIndex))
            {
                builder.AppendLine(FormattableString.Invariant(
                    $"| {EscapePipe(bracket.BracketSlug)} | {bracket.BracketIndex} | {bracket.CellsFetched} | {bracket.CellsQualifying} | {FormatMetric(bracket.MedianBackingDeckCount)} | {EscapePipe(bracket.SupportLabel)} |"));
            }
        }

        builder.AppendLine();
        builder.AppendLine("### EDHREC land self-check");
        if (computation.EdhrecCoverage.LandSelfChecks.Count == 0)
        {
            builder.AppendLine("_No EDHREC cells were supplied for this run (--edhrec-data not provided)._");
        }
        else
        {
            builder.AppendLine("This comparison is only meaningful after a full Scryfall resolution pass; a run against a partially populated card cache will undercount unresolved names and therefore report artificially low harness land counts.");
            EdhrecLandSelfCheckSummary selfCheckSummary = SummarizeEdhrecLandSelfChecks(computation.EdhrecCoverage.LandSelfChecks);
            builder.AppendLine(FormattableString.Invariant($"- Exact match: {selfCheckSummary.ExactMatchCount}"));
            builder.AppendLine(FormattableString.Invariant($"- Within one: {selfCheckSummary.WithinOneCount}"));
            builder.AppendLine(FormattableString.Invariant($"- Diverged by more than one: {selfCheckSummary.DivergedByMoreThanOneCount}"));
            builder.AppendLine();
            builder.AppendLine("| CellId | EDHREC lands | Harness lands | Delta |");
            builder.AppendLine("|--------|-------------:|--------------:|------:|");
            foreach (EdhrecLandSelfCheck selfCheck in computation.EdhrecCoverage.LandSelfChecks
                         .OrderByDescending(check => Math.Abs(check.Delta))
                         .ThenBy(check => check.CellId, StringComparer.Ordinal)
                         .Take(5))
            {
                builder.AppendLine(FormattableString.Invariant(
                    $"| {EscapePipe(selfCheck.CellId)} | {selfCheck.EdhrecLandCount} | {selfCheck.HarnessLandCount} | {selfCheck.Delta:+#;-#;0} |"));
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Methodology");
        builder.AppendLine(FormattableString.Invariant(
            $"A commander-role row clears the written statistical bar only when DEDUPED N >= {computation.MinDeckCount}, the commander's P25 is >= {RatioHigh:0.###}x or <= {RatioLow:0.###}x the corpus P25 (or differs by at least {AbsoluteFloorGap:0.0} cards when the corpus P25 is zero), and |z| >= {ZThreshold:0.0}; z is computed as (commanderMean - corpusMean) / (corpusStdDev / sqrt(n))."));
        builder.AppendLine("RAW N is the count of distinct reconstructed mainboard deck_queue ids for a commander before content-hash collapse; DEDUPED N collapses same-content_hash near-duplicates to one representative deck per non-null hash, so DEDUPED N is the only N compared against the threshold or passed into ClearsFloorBar.");
        builder.AppendLine("Every card is classified oracle-only with the production role assigner using empty categories and `isComboPiece: false`: categories are intentionally always `[]` because `PlanRoleClassifier.Classify` is categories-first and first-hit-wins, so using live Archidekt tags would partially measure each commander playerbase's tagging habits rather than the card's mechanics. This matches the already-shipped `CutLabSimulationService.cs:517` call shape, but it means these findings do not reproduce what CutLabRoleAssigner outputs today for an actually-tagged production decklist.");
        builder.AppendLine("The verdict is computed from the commander's 25th percentile against the corpus 25th percentile; the mean z-score is retained only as a significance gate because a sample percentile has no closed-form standard error.");
        builder.AppendLine("When the corpus P25 is zero, the multiplicative ratio is undefined and `ComputeRatio` returns 0.0, so the bar falls back to an absolute gap of 2.0 cards; that is the smallest floor difference worth stating as a recommendation.");
        builder.AppendLine("EDHREC cells never enter the go/no-go, because ClearsFloorBar requires a standard deviation and a sample size that a single synthesized average deck cannot supply.");
        builder.AppendLine("Cohen's d is reported alongside ratio and z as a scale-uniform effect size, because a fixed 1.5x / 0.667x ratio gate is not scale-fair across roles with very different corpus-wide means.");
        builder.AppendLine(FormattableString.Invariant(
            $"A role is only a Phase-2 \"go\" when at least {BreadthMinimum} distinct qualifying commanders clear the bar in that role; one or two clearing commanders is recorded as signal present but insufficient breadth."));
        builder.AppendLine("At these defaults the z-gate is largely redundant once N and ratio are both satisfied: for example, N=40, sd=3, mean=6, and a 1.5x ratio implies z~6.3, far above the 2.0 cutoff.");
        builder.AppendLine(FormattableString.Invariant(
            $"This run reconstructed {computation.RawDeckCount} raw mainboard decks, {computation.DedupedDeckCount} deduped representative decks, enumerated {computation.CommandersEnumerated} commanders, and retained {computation.Commanders.Count} qualifying commanders at the primary DEDUPED-N threshold of {computation.MinDeckCount}."));
        builder.AppendLine(FormattableString.Invariant(
            $"Unresolved Scryfall card names excluded from tallies: {computation.UnresolvedCardCount} total ({computation.UnresolvedNotFoundCount} not_found, {computation.UnresolvedRateLimitedAfterRetryCount} rate_limited_after_retry)."));
        builder.AppendLine();
        builder.AppendLine("Known gaps:");
        builder.AppendLine("- Card-level category-tag coverage gap: a tagged deck can still leave individual cards uncategorized, so reconstructed decklists may miss cards.");
        builder.AppendLine("- Deck-level invisibility gap: a processed deck with zero category-tagged cards contributes no membership rows at all and is invisible to this pipeline.");
        builder.AppendLine("- content_hash NULL dedup limitation: dedup is conservative, not perfect, because older deck_queue rows may still have NULL content_hash values.");
        builder.AppendLine("- `isComboPiece` is fixed to `false`, so combo-only win conditions can be undercounted.");
        builder.AppendLine("- ManabaseMode is fixed per run, so this pass does not compare role floors across multiple play-experience modes.");
        builder.AppendLine("- The `other` residual role is deliberately excluded because it measures fallback classifier coverage rather than deck-construction structure.");
        builder.AppendLine("- Postgres decks are classified as singleton card sets because Commander is singleton for the target nonland roles there, while EDHREC cells preserve real decklist quantities so basics and other repeated entries are counted at their actual quantity.");
        builder.AppendLine("- Oracle-only classification means these findings do not reproduce today's category-aware production role output for a tagged decklist.");
        builder.AppendLine("- Cards that still fail after harness-side HTTP 429 retry are tracked separately as `rate_limited_after_retry`; like true `not_found` names, they are excluded from classification tallies, but this run distinguishes the two unresolved reasons.");
        builder.AppendLine(FormattableString.Invariant($"- EDHREC quantity-parse failures excluded rather than dropped silently: {computation.EdhrecParseFailureCount} raw deck entries across all ingested cells failed the quantity-prefix parse and were left out of classification."));
        builder.AppendLine(FormattableString.Invariant($"- EDHREC parsed-card-count anomalies: {computation.EdhrecCardCountAnomalyCount} ingested cells did not sum to 100 parsed cards after quantity parsing."));
        builder.AppendLine();
        builder.AppendLine("## Qualifying Commanders By DEDUPED-N Threshold");
        builder.AppendLine("| Threshold | Qualifying Commanders |");
        builder.AppendLine("|----------:|----------------------:|");
        foreach ((int threshold, int count) in computation.ThresholdCounts.OrderBy(pair => pair.Key))
        {
            builder.AppendLine(FormattableString.Invariant($"| {threshold} | {count} |"));
        }

        builder.AppendLine();
        builder.AppendLine("## Corpus Baseline");
        builder.AppendLine("| Role | Mean | SD | P25 |");
        builder.AppendLine("|------|-----:|---:|----:|");
        foreach (string role in TargetRoles)
        {
            RoleBaseline baseline = computation.CorpusBaseline[role];
            builder.AppendLine(FormattableString.Invariant(
                $"| {role} | {FormatMetric(baseline.Mean)} | {FormatMetric(baseline.StdDev)} | {FormatMetric(baseline.P25)} |"));
        }

        foreach (string role in TargetRoles)
        {
            builder.AppendLine();
            builder.AppendLine($"## {role}");
            if (computation.Commanders.Count == 0)
            {
                builder.AppendLine("No commanders reached the deduped threshold.");
                continue;
            }

            builder.AppendLine("### Postgres — within-commander distribution (n decks per commander)");
            AppendMarkdownTableHeader(builder, RoleFloorFigureTable.PostgresColumns);
            foreach (PostgresRoleDistribution distribution in computation.PostgresDistributions
                         .Where(figure => string.Equals(figure.Role, role, StringComparison.Ordinal))
                         .OrderByDescending(figure => ResolveCommanderDedupedN(computation.Commanders, figure.CommanderName))
                         .ThenBy(figure => figure.CommanderName, StringComparer.Ordinal))
            {
                builder.AppendLine(BuildPostgresFigureRow(computation.Commanders, distribution));
            }

            builder.AppendLine();
            builder.AppendLine("### EDHREC — commander x bracket point estimates");
            if (computation.EdhrecPointEstimates.Count == 0)
            {
                builder.AppendLine("_No EDHREC cells were supplied for this run (--edhrec-data not provided)._");
            }
            else
            {
                AppendMarkdownTableHeader(builder, RoleFloorFigureTable.EdhrecColumns);
                foreach (EdhrecRolePointEstimate pointEstimate in computation.EdhrecPointEstimates
                             .Where(figure => string.Equals(figure.Role, role, StringComparison.Ordinal))
                             .OrderBy(figure => figure.CommanderName, StringComparer.Ordinal)
                             .ThenBy(figure => figure.BracketIndex))
                {
                    builder.AppendLine(BuildEdhrecFigureRow(pointEstimate));
                }

                builder.AppendLine();
                builder.AppendLine("*Each figure above is a point estimate from a single synthesized average deck. It is not a percentile and has no within-cell variance. EDHREC figures do not enter the go/no-go.*");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Go/No-Go");
        foreach (string role in TargetRoles)
        {
            RoleOutcome outcome = computation.GoNoGo[role];
            string commanderCitation = outcome.CitingCommanders.Count == 0
                ? string.Empty
                : $" ({FormatCommanderCitation(outcome)})";
            builder.AppendLine($"- {role}: {outcome.MarkdownStatus}{commanderCitation}");
        }

        return builder.ToString();
    }

    private static object BuildJsonPayload(ResearchComputation computation)
    {
        // Why: fallback provenance values must never block a run or leak a credential, so the
        // warning array is emitted from the same resolved values to prevent "unavailable"/"unknown"
        // from silently masquerading as complete provenance.
        IReadOnlyList<string> provenanceWarnings = RoleFloorProvenance.BuildProvenanceWarnings(
            computation.DatabaseHost,
            computation.HarnessCommitSha,
            computation.RawDeckCount,
            computation.DedupedDeckCount);
        EdhrecLandSelfCheckSummary edhrecLandSelfCheckSummary = SummarizeEdhrecLandSelfChecks(computation.EdhrecCoverage.LandSelfChecks);

        return new
        {
            methodology = new
            {
                minDeckCount = computation.MinDeckCount,
                ratioLow = RatioLow,
                ratioHigh = RatioHigh,
                zThreshold = ZThreshold,
                absoluteFloorGap = AbsoluteFloorGap,
                breadthMinimum = BreadthMinimum,
                databaseHost = computation.DatabaseHost,
                runTimestampUtc = computation.RunTimestampUtc,
                harnessCommitSha = computation.HarnessCommitSha,
                rawDeckCount = computation.RawDeckCount,
                dedupedDeckCount = computation.DedupedDeckCount,
                commandersEnumerated = computation.CommandersEnumerated,
                unresolvedCardCount = computation.UnresolvedCardCount,
                unresolvedNotFoundCount = computation.UnresolvedNotFoundCount,
                unresolvedRateLimitedAfterRetryCount = computation.UnresolvedRateLimitedAfterRetryCount,
                provenanceWarnings,
            },
            corpusBaseline = TargetRoles.ToDictionary(
                role => role,
                role => new
                {
                    mean = computation.CorpusBaseline[role].Mean,
                    stdDev = computation.CorpusBaseline[role].StdDev,
                    p25 = computation.CorpusBaseline[role].P25,
                },
                StringComparer.Ordinal),
            commanders = computation.Commanders.Values
                .OrderByDescending(commander => commander.N)
                .ThenBy(commander => commander.CommanderName, StringComparer.Ordinal)
                .ToDictionary(
                    commander => commander.CommanderName,
                    commander => new
                    {
                        rawN = commander.RawN,
                        n = commander.N,
                        roles = TargetRoles.ToDictionary(
                            role => role,
                            role => new
                            {
                                source = RoleFloorSource.Postgres,
                                mean = commander.Roles[role].Mean,
                                p25 = commander.Roles[role].P25,
                                ratio = commander.Roles[role].Ratio,
                                z = commander.Roles[role].ZScore,
                                cohensD = commander.Roles[role].CohensD,
                                clearsBar = commander.Roles[role].ClearsBar,
                            },
                            StringComparer.Ordinal),
                    },
                    StringComparer.Ordinal),
            edhrec = new
            {
                cells = computation.EdhrecPointEstimates
                    .OrderBy(cell => cell.CommanderName, StringComparer.Ordinal)
                    .ThenBy(cell => cell.BracketIndex)
                    .Select(cell => new
                    {
                        source = cell.Source,
                        role = cell.Role,
                        commander = cell.CommanderName,
                        bracket = cell.BracketSlug,
                        bracketIndex = cell.BracketIndex,
                        count = cell.Count,
                        deckCount = cell.DeckCount,
                        qualifies = cell.Qualifies,
                    })
                    .ToArray(),
                coverage = new
                {
                    cellsFetched = computation.EdhrecCoverage.CellsFetched,
                    cellsQualifying = computation.EdhrecCoverage.CellsQualifying,
                    cellsMissing = computation.EdhrecCoverage.CellsMissing,
                    invalidCells = computation.EdhrecCoverage.InvalidCells,
                    unexpectedCells = computation.EdhrecCoverage.UnexpectedCells,
                    commandersReached = computation.EdhrecCoverage.CommandersReached,
                    minCellDeckCount = computation.EdhrecCoverage.MinCellDeckCount,
                    minSaveDate = computation.EdhrecCoverage.MinSaveDate,
                    maxSaveDate = computation.EdhrecCoverage.MaxSaveDate,
                    brackets = computation.EdhrecCoverage.Brackets
                        .OrderBy(bracket => bracket.BracketIndex)
                        .Select(bracket => new
                        {
                            bracket = bracket.BracketSlug,
                            bracketIndex = bracket.BracketIndex,
                            cellsFetched = bracket.CellsFetched,
                            cellsQualifying = bracket.CellsQualifying,
                            medianBackingDeckCount = bracket.MedianBackingDeckCount,
                            supportLabel = bracket.SupportLabel,
                        })
                        .ToArray(),
                    landSelfCheck = new
                    {
                        summary = new
                        {
                            exactMatch = edhrecLandSelfCheckSummary.ExactMatchCount,
                            withinOne = edhrecLandSelfCheckSummary.WithinOneCount,
                            divergedByMoreThanOne = edhrecLandSelfCheckSummary.DivergedByMoreThanOneCount,
                        },
                        worstFive = computation.EdhrecCoverage.LandSelfChecks
                            .OrderByDescending(check => Math.Abs(check.Delta))
                            .ThenBy(check => check.CellId, StringComparer.Ordinal)
                            .Take(5)
                            .Select(check => new
                            {
                                cellId = check.CellId,
                                edhrecLandCount = check.EdhrecLandCount,
                                harnessLandCount = check.HarnessLandCount,
                                delta = check.Delta,
                            })
                            .ToArray(),
                    },
                },
            },
            goNoGo = TargetRoles.ToDictionary(
                role => role,
                role => new
                {
                    status = computation.GoNoGo[role].JsonStatus,
                    citingCommanders = computation.GoNoGo[role].CitingCommanders,
                },
                StringComparer.Ordinal),
        };
    }

    private static string FormatCommanderCitation(RoleOutcome outcome)
    {
        if (outcome.CitingCommanders.Count <= 5)
        {
            return string.Join(", ", outcome.CitingCommanders);
        }

        string topFive = string.Join(", ", outcome.CitingCommanders.Take(5));
        return FormattableString.Invariant($"{topFive}; top 5 of {outcome.ClearingCommanderCount} clearing commanders");
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string BuildPostgresFigureRow(
        IReadOnlyDictionary<string, CommanderResearch> commanders,
        PostgresRoleDistribution distribution)
    {
        if (!commanders.TryGetValue(distribution.CommanderName, out CommanderResearch? commander))
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"Missing commander research row for {distribution.CommanderName}."));
        }

        // Why: criterion 8 requires every reported figure to state which source it came from, and
        // a heading-based source tag silently stops covering any column a future contributor adds.
        return FormattableString.Invariant(
            $"| {FormatRoleFloorSource(distribution.Source)} | {EscapePipe(distribution.CommanderName)} | {distribution.DeckCount} | {commander.N} | {FormatMetric(distribution.Mean)} | {FormatMetric(distribution.P25)} | {FormatMetric(distribution.Ratio)} | {FormatMetric(distribution.ZScore)} | {FormatMetric(distribution.CohensD)} | {FormatBoolean(distribution.ClearsBar)} |");
    }

    private static string BuildEdhrecFigureRow(EdhrecRolePointEstimate pointEstimate)
        => FormattableString.Invariant(
            $"| {FormatRoleFloorSource(pointEstimate.Source)} | {EscapePipe(pointEstimate.CommanderName)} | {EscapePipe(pointEstimate.BracketSlug)} | {FormatMetric(pointEstimate.Count)} | {pointEstimate.DeckCount} | {FormatBoolean(pointEstimate.Qualifies)} |");

    private static void AppendMarkdownTableHeader(StringBuilder builder, IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(columns);

        builder.AppendLine(FormattableString.Invariant($"| {string.Join(" | ", columns)} |"));
        builder.AppendLine(BuildMarkdownAlignmentRow(columns));
    }

    private static string BuildMarkdownAlignmentRow(IReadOnlyList<string> columns)
        => FormattableString.Invariant(
            $"| {string.Join(" | ", columns.Select(GetMarkdownAlignmentCell))} |");

    private static string GetMarkdownAlignmentCell(string column)
        => column switch
        {
            "Source" => "------",
            "Commander" => "-----------",
            "RAW N" => "------:",
            "DEDUPED N" => "----------:",
            "Mean" => "-----:",
            "P25" => "----:",
            "Ratio" => "------:",
            "Z" => "--:",
            "Cohen's d" => "----------:",
            "ClearsBar" => "----------:",
            "Bracket" => "---------",
            "Count" => "-----:",
            "Decks backing cell" => "------------------:",
            "Qualifies" => "----------:",
            _ => throw new InvalidOperationException(FormattableString.Invariant($"Unsupported markdown column '{column}'.")),
        };

    private static string FormatRoleFloorSource(RoleFloorSource source)
        => source.ToString().ToLowerInvariant();

    private static string FormatBoolean(bool value)
        => value ? "true" : "false";

    private static string FormatEdhrecDateRange(string? minSaveDate, string? maxSaveDate)
        => string.IsNullOrWhiteSpace(minSaveDate) || string.IsNullOrWhiteSpace(maxSaveDate)
            ? "n/a"
            : FormattableString.Invariant($"{minSaveDate} to {maxSaveDate}");

    private static EdhrecLandSelfCheckSummary SummarizeEdhrecLandSelfChecks(IReadOnlyList<EdhrecLandSelfCheck> selfChecks)
    {
        ArgumentNullException.ThrowIfNull(selfChecks);

        return new EdhrecLandSelfCheckSummary
        {
            ExactMatchCount = selfChecks.Count(check => check.Delta == 0),
            WithinOneCount = selfChecks.Count(check => Math.Abs(check.Delta) == 1),
            DivergedByMoreThanOneCount = selfChecks.Count(check => Math.Abs(check.Delta) > 1),
        };
    }

    private static int ResolveCommanderDedupedN(
        IReadOnlyDictionary<string, CommanderResearch> commanders,
        string commanderName)
        => commanders.TryGetValue(commanderName, out CommanderResearch? commander)
            ? commander.N
            : 0;

    private static JsonSerializerOptions CreateResearchJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonOptions);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static string FormatMetric(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static double ComputePopulationStdDev(IReadOnlyList<double> values, double mean)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        double variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private sealed class CommanderDeckSet
    {
        public required string CommanderName { get; init; }
        public required Dictionary<long, HashSet<string>> RawDecks { get; init; }
        public Dictionary<long, HashSet<string>> RepresentativeDecks { get; set; } = [];
        public Dictionary<long, Dictionary<string, int>> RepresentativeRoleCounts { get; } = [];
        public int RawN => RawDecks.Count;
        public int DedupedN => RepresentativeDecks.Count;
    }

    private sealed class ResearchComputation
    {
        public int MinDeckCount { get; init; }
        public required string DatabaseHost { get; init; }
        public required string RunTimestampUtc { get; init; }
        public required string HarnessCommitSha { get; init; }
        public int CommandersEnumerated { get; init; }
        public int RawDeckCount { get; init; }
        public int DedupedDeckCount { get; init; }
        public int UnresolvedNotFoundCount { get; init; }
        public int UnresolvedRateLimitedAfterRetryCount { get; init; }
        public int UnresolvedCardCount => UnresolvedNotFoundCount + UnresolvedRateLimitedAfterRetryCount;
        public required PostgresCoverage PostgresCoverage { get; init; }
        public required Dictionary<string, RoleBaseline> CorpusBaseline { get; init; }
        public required Dictionary<string, CommanderResearch> Commanders { get; init; }
        public IReadOnlyList<PostgresRoleDistribution> PostgresDistributions { get; init; } = [];
        public IReadOnlyList<EdhrecRolePointEstimate> EdhrecPointEstimates { get; init; } = [];
        public required EdhrecCoverage EdhrecCoverage { get; init; }
        public int EdhrecParseFailureCount { get; init; }
        public int EdhrecCardCountAnomalyCount { get; init; }
        public required Dictionary<int, int> ThresholdCounts { get; init; }
        public Dictionary<string, RoleOutcome> GoNoGo { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class CommanderResearch
    {
        public required string CommanderName { get; init; }
        public int RawN { get; init; }
        public int N { get; init; }
        public Dictionary<string, CommanderRoleStat> Roles { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class CommanderRoleStat
    {
        public double Mean { get; init; }
        public double P25 { get; init; }
        public double Ratio { get; init; }
        public double ZScore { get; init; }
        public double CohensD { get; init; }
        public bool ClearsBar { get; init; }
    }

    private sealed class RoleBaseline
    {
        public double Mean { get; init; }
        public double StdDev { get; init; }
        public double P25 { get; init; }
    }

    private sealed class PostgresCoverage
    {
        public int CommandersEnumerated { get; init; }
        public int CommandersWithMembership { get; init; }
        public int RawDeckCount { get; init; }
        public int DedupedDeckCount { get; init; }
        public int CommandersQualifying { get; init; }
        public int UnresolvedNotFoundCount { get; init; }
        public int UnresolvedRateLimitedAfterRetryCount { get; init; }
        public int UnresolvedCardCount => UnresolvedNotFoundCount + UnresolvedRateLimitedAfterRetryCount;
    }

    private sealed class EdhrecCoverage
    {
        public int CellsFetched { get; init; }
        public int CellsQualifying { get; init; }
        public int CellsMissing { get; init; }
        public int InvalidCells { get; init; }
        public int UnexpectedCells { get; init; }
        public int CommandersReached { get; init; }
        public int MinCellDeckCount { get; init; }
        public string? MinSaveDate { get; init; }
        public string? MaxSaveDate { get; init; }
        public IReadOnlyList<EdhrecBracketCoverage> Brackets { get; init; } = [];
        public IReadOnlyList<EdhrecLandSelfCheck> LandSelfChecks { get; init; } = [];
    }

    private sealed class EdhrecBracketCoverage
    {
        public required string BracketSlug { get; init; }
        public int BracketIndex { get; init; }
        public int CellsFetched { get; init; }
        public int CellsQualifying { get; init; }
        public double MedianBackingDeckCount { get; init; }
        public required string SupportLabel { get; init; }
    }

    private sealed class EdhrecLandSelfCheck
    {
        public required string CellId { get; init; }
        public int EdhrecLandCount { get; init; }
        public int HarnessLandCount { get; init; }
        public int Delta { get; init; }
    }

    private sealed class EdhrecLandSelfCheckSummary
    {
        public int ExactMatchCount { get; init; }
        public int WithinOneCount { get; init; }
        public int DivergedByMoreThanOneCount { get; init; }
    }

    private sealed class RoleOutcome
    {
        public required string MarkdownStatus { get; init; }
        public required string JsonStatus { get; init; }
        public required IReadOnlyList<string> CitingCommanders { get; init; }
        public int ClearingCommanderCount { get; init; }
    }

    private sealed class CardResolutionResult
    {
        public CardResolutionResult(
            IReadOnlyDictionary<string, ScryfallCardData> resolvedCards,
            int unresolvedNotFoundCount,
            int unresolvedRateLimitedAfterRetryCount)
        {
            ResolvedCards = resolvedCards;
            UnresolvedNotFoundCount = unresolvedNotFoundCount;
            UnresolvedRateLimitedAfterRetryCount = unresolvedRateLimitedAfterRetryCount;
        }

        public IReadOnlyDictionary<string, ScryfallCardData> ResolvedCards { get; }
        public int UnresolvedNotFoundCount { get; }
        public int UnresolvedRateLimitedAfterRetryCount { get; }
        public int UnresolvedCount => UnresolvedNotFoundCount + UnresolvedRateLimitedAfterRetryCount;
    }
}
