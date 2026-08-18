using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Research;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Http;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;
using RestSharp;
using CoreScryfallCollectionIdentifier = DeckFlow.Core.Normalization.ScryfallCollectionIdentifier;

namespace DeckFlow.CLI;

internal static class EdhrecRoleGridCommandRunner
{
    private const int ScryfallRateLimitRetryMaxAttempts = 4;
    private static readonly TimeSpan FallbackSearchPacingDelay = TimeSpan.FromMilliseconds(350);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<int> RunAsync(
        string edhrecCsvPath,
        string averagesCsvPath,
        string cardsCachePath,
        string mode,
        int minDecks,
        string outputPath,
        string outputJsonPath,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(edhrecCsvPath))
        {
            Console.Error.WriteLine("--edhrec-csv is required.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(averagesCsvPath))
        {
            Console.Error.WriteLine("--averages is required.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(cardsCachePath))
        {
            Console.Error.WriteLine("--cards-cache is required.");
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

        if (minDecks < 0)
        {
            Console.Error.WriteLine("--min-decks must be >= 0.");
            return 1;
        }

        try
        {
            ManabaseMode resolvedMode = CutLabRoleAssigner.ResolveMode(mode);
            string[] roleKeys = GetTargetRoleKeys();
            string? taxonomyError = ValidateTaxonomy(resolvedMode, roleKeys);
            if (taxonomyError is not null)
            {
                Console.Error.WriteLine(taxonomyError);
                return 1;
            }

            string resolvedEdhrecCsvPath = Path.GetFullPath(edhrecCsvPath);
            string resolvedAveragesCsvPath = Path.GetFullPath(averagesCsvPath);
            string resolvedCardsCachePath = Path.GetFullPath(cardsCachePath);
            string resolvedOutputPath = Path.GetFullPath(outputPath);
            string resolvedOutputJsonPath = Path.GetFullPath(outputJsonPath);

            if (!File.Exists(resolvedEdhrecCsvPath))
            {
                Console.Error.WriteLine(FormattableString.Invariant($"--edhrec-csv path not found: {resolvedEdhrecCsvPath}"));
                return 1;
            }

            if (!File.Exists(resolvedAveragesCsvPath))
            {
                Console.Error.WriteLine(FormattableString.Invariant($"--averages path not found: {resolvedAveragesCsvPath}"));
                return 1;
            }

            if (dryRun)
            {
                // Why: plan 02-08 uses this dry run as the operator pre-flight, so it must fail on bad input paths.
                Console.WriteLine("edhrec-role-grid dry run");
                Console.WriteLine(FormattableString.Invariant($"  edhrec.csv: {DescribeDryRunPath(resolvedEdhrecCsvPath, mustExist: true)}"));
                Console.WriteLine(FormattableString.Invariant($"  averages.csv: {DescribeDryRunPath(resolvedAveragesCsvPath, mustExist: true)}"));
                Console.WriteLine(FormattableString.Invariant($"  cards-cache: {DescribeDryRunPath(resolvedCardsCachePath, mustExist: false)}"));
                Console.WriteLine(FormattableString.Invariant($"  mode: {resolvedMode}"));
                Console.WriteLine(FormattableString.Invariant($"  min-decks: {minDecks}"));
                Console.WriteLine(FormattableString.Invariant($"  out: {resolvedOutputPath}"));
                Console.WriteLine(FormattableString.Invariant($"  out-json: {resolvedOutputJsonPath}"));
                Console.WriteLine(FormattableString.Invariant($"  target roles: {string.Join(", ", roleKeys)}"));
                Console.WriteLine("  residual role excluded from grid: other");
                Console.WriteLine("  taxonomy: OK");
                Console.WriteLine("  archive read: skipped");
                Console.WriteLine("  scryfall calls: skipped");
                return 0;
            }

            string runTimestampUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string harnessCommitSha = DescribeHarnessCommitSha();

            int malformedRowsPass1;
            IReadOnlyCollection<string> distinctCardNames = EdhrecCardCountsReader.ReadDistinctCardNames(
                resolvedEdhrecCsvPath,
                out malformedRowsPass1);

            using ServiceProvider serviceProvider = BuildScryfallServiceProvider();
            IScryfallCardResolver resolver = serviceProvider.GetRequiredService<IScryfallCardResolver>();
            CardResolutionResult cardResolution = await ResolveCardsAsync(
                resolver,
                distinctCardNames,
                resolvedCardsCachePath,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, IReadOnlyList<string>> cardRoles = ClassifyResolvedCards(
                cardResolution.ResolvedCards,
                roleKeys,
                resolvedMode);

            IReadOnlyDictionary<string, long> denominators = EdhrecCardCountsReader.ReadSoloDenominators(resolvedAveragesCsvPath);
            EdhrecBulkGridResult accumulation = EdhrecCardCountsReader.Accumulate(
                resolvedEdhrecCsvPath,
                denominators,
                cardRoles,
                roleKeys);

            if (accumulation.Failure is not null)
            {
                Console.Error.WriteLine(accumulation.Failure);
                return 1;
            }

            List<EdhrecBulkRoleExpectation> figures = accumulation.Commanders
                .Where(commander => commander.Denominator >= minDecks)
                .SelectMany(commander => roleKeys.Select(role => new EdhrecBulkRoleExpectation
                {
                    Source = RoleFloorSource.EdhrecBulk,
                    CommanderName = commander.Commander,
                    Role = role,
                    ExpectedCount = commander.ExpectedByRole.TryGetValue(role, out double expectedCount) ? expectedCount : 0.0,
                    DeckCount = commander.Denominator,
                    RowsConsumed = commander.RowsConsumed,
                    MaxCardInclusion = commander.MaxRatio,
                }))
                .OrderBy(figure => figure.CommanderName, StringComparer.Ordinal)
                .ThenBy(figure => figure.Role, StringComparer.Ordinal)
                .ToList();

            int survivingCommanders = figures
                .Select(figure => figure.CommanderName)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (RoleFloorGuards.HasNoQualifyingCommanders(survivingCommanders))
            {
                Console.Error.WriteLine(FormattableString.Invariant(
                    $"Zero commanders survived the denominator gate and --min-decks {minDecks}."));
                Console.Error.WriteLine("NO artifact was written.");
                return 2;
            }

            var report = new EdhrecRoleGridReport
            {
                RunTimestampUtc = runTimestampUtc,
                HarnessCommitSha = harnessCommitSha,
                EdhrecCsv = DescribeInputFile(resolvedEdhrecCsvPath),
                AveragesCsv = DescribeInputFile(resolvedAveragesCsvPath),
                CardsCachePath = resolvedCardsCachePath,
                Mode = resolvedMode.ToString(),
                MinDecks = minDecks,
                RowsRead = accumulation.RowsRead,
                DistinctCards = accumulation.DistinctCardCount,
                CommandersAccumulated = survivingCommanders,
                DenominatorMismatchCount = accumulation.DenominatorMismatches.Count,
                MissingDenominatorCount = accumulation.MissingDenominators.Count,
                MalformedRows = accumulation.MalformedRows + malformedRowsPass1,
                UnresolvedCardNames = cardResolution.UnresolvedNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                Figures = figures,
                DenominatorMismatches = accumulation.DenominatorMismatches,
                MissingDenominators = accumulation.MissingDenominators.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                CommanderTotals = accumulation.Commanders
                    .Where(commander => commander.Denominator >= minDecks)
                    .OrderBy(commander => commander.Commander, StringComparer.Ordinal)
                    .ToArray(),
            };

            string markdown = BuildMarkdownReport(report);
            string json = JsonSerializer.Serialize(BuildJsonPayload(report), CreateJsonOptions());
            SnapshotFileWriter.WriteLfFile(resolvedOutputPath, markdown);
            SnapshotFileWriter.WriteLfFile(resolvedOutputJsonPath, json);

            Console.WriteLine(FormattableString.Invariant(
                $"RowsRead={report.RowsRead}, DistinctCards={report.DistinctCards}, Commanders={report.CommandersAccumulated}, DenominatorMismatches={report.DenominatorMismatchCount}, MissingDenominators={report.MissingDenominatorCount}, UnresolvedCards={report.UnresolvedCardNames.Count}"));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string[] GetTargetRoleKeys()
    {
        string? roleKeyReadError = RoleFloorGuards.TryReadShippedRoleKeys(
            typeof(CutLabRoleAssigner),
            "RoleKeys",
            out string[]? shippedRoleKeys);
        if (roleKeyReadError is not null)
        {
            throw new InvalidOperationException(roleKeyReadError);
        }

        return shippedRoleKeys!;
    }

    private static string? ValidateTaxonomy(ManabaseMode mode, IReadOnlyCollection<string> roleKeys)
    {
        string? roleKeyReadError = RoleFloorGuards.TryReadShippedRoleKeys(
            typeof(CutLabRoleAssigner),
            "RoleKeys",
            out string[]? shippedRoleKeys);
        if (roleKeyReadError is not null)
        {
            return roleKeyReadError;
        }

        var emittedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CardFact probe in BuildTaxonomyProbes())
        {
            foreach (string role in CutLabRoleAssigner
                         .AssignRoles(probe, [], isComboPiece: false, mode))
            {
                emittedKeys.Add(role);
            }
        }

        return RoleFloorGuards.FindTaxonomyDrift(shippedRoleKeys!, roleKeys, emittedKeys, residualRoleKey: "other");
    }

    private static CardFact[] BuildTaxonomyProbes()
    {
        return
        [
            new()
            {
                Name = "Forest",
                Quantity = 1,
                TypeLine = "Basic Land — Forest",
            },
            new()
            {
                Name = "Cultivate",
                Quantity = 1,
                TypeLine = "Sorcery",
                OracleText = "Search your library for up to two basic land cards, reveal those cards, put one onto the battlefield tapped and the other into your hand, then shuffle.",
            },
            new()
            {
                Name = "Quick Study",
                Quantity = 1,
                TypeLine = "Instant",
                OracleText = "Draw two cards.",
            },
            new()
            {
                Name = "Swords to Plowshares",
                Quantity = 1,
                TypeLine = "Instant",
                OracleText = "Exile target creature. Its controller gains life equal to its power.",
            },
            new()
            {
                Name = "Wrath of God",
                Quantity = 1,
                TypeLine = "Sorcery",
                OracleText = "Destroy all creatures. They can't be regenerated.",
            },
            new()
            {
                Name = "Protection Wand",
                Quantity = 1,
                TypeLine = "Artifact",
                OracleText = "{T}: Target creature you control gains hexproof until end of turn.",
            },
            new()
            {
                Name = "Phyrexian Arena",
                Quantity = 1,
                TypeLine = "Enchantment",
                OracleText = "At the beginning of your upkeep, draw a card and you lose 1 life.",
            },
            new()
            {
                Name = "Avatar Finisher",
                Quantity = 1,
                TypeLine = "Creature — Avatar",
                OracleText = "Whenever this attacks, each opponent loses 3 life.",
            },
            new()
            {
                Name = "Torment of Hailfire",
                Quantity = 1,
                TypeLine = "Sorcery",
                OracleText = "Repeat the following process X times. Each opponent loses 3 life unless that player sacrifices a nonland permanent or discards a card.",
            },
        ];
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

        Console.WriteLine(FormattableString.Invariant(
            $"Distinct card names={distinctCardNames.Count}; cached hits={distinctCardNames.Count - uncachedNames.Count}; cache misses={uncachedNames.Count}."));

        // Why: this command reuses the existing resolver, the shared cards_full.json cache file, and
        // the same name -> ScryfallCardData JSON format; only the batching loop is duplicated here
        // because RoleFloorResearchCommandRunner.ResolveCardsAsync is a private static inside a file
        // four other plans wrote across four waves, so extracting it now would be a merge hazard.
        // Unifying the two loops into one DeckFlow.Core component is a recorded follow-up, not this
        // plan's work.
        for (int offset = 0; offset < uncachedNames.Count; offset += 75)
        {
            List<string> batchNames = uncachedNames.Skip(offset).Take(75).ToList();
            string[] batchIdentifiers = batchNames
                .Select(CoreScryfallCollectionIdentifier.ToFaceIdentifier)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Console.WriteLine(FormattableString.Invariant(
                $"Resolving Scryfall batch {offset / 75 + 1}/{Math.Max(1, (int)Math.Ceiling(uncachedNames.Count / 75.0))} ({Math.Min(offset + batchNames.Count, uncachedNames.Count)}/{uncachedNames.Count})."));

            var request = new RestRequest("cards/collection", Method.Post);
            // Why: Scryfall cards/collection name identifiers match a single face name; combined A // B returns not_found.
            request.AddJsonBody(new { identifiers = batchIdentifiers.Select(cardName => (object)new { name = cardName }).ToArray() });

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
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during edhrec-role-grid.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            var cardsByNormalizedName = response.Data.Data
                .GroupBy(card => CardNormalizer.Normalize(card.Name), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (string cardName in batchNames)
            {
                string normalizedName = CardNormalizer.Normalize(cardName);
                ScryfallCard? hit = cardsByNormalizedName.TryGetValue(normalizedName, out ScryfallCard? directHit)
                    ? directHit
                    : null;
                ScryfallCard? resolvedCard = hit
                    ?? await ExecuteWithScryfall429RetryAsync(
                        operationName: $"fallback search for {cardName}",
                        operation: token => resolver.SearchFallbackCardAsync(cardName, token),
                        cancellationToken).ConfigureAwait(false);

                if (resolvedCard is null)
                {
                    if (unresolvedRateLimitedNames.Contains(cardName))
                    {
                        continue;
                    }

                    unresolvedNotFoundNames.Add(cardName);
                    continue;
                }

                cache[cardName] = ScryfallCardDataMapper.ToCardData(resolvedCard);

                if (hit is null)
                {
                    await Task.Delay(FallbackSearchPacingDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            SnapshotFileWriter.WriteLfFile(cardsCachePath, JsonSerializer.Serialize(cache, JsonOptions));
        }

        return new CardResolutionResult(
            cache,
            unresolvedNotFoundNames,
            unresolvedRateLimitedNames);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ClassifyResolvedCards(
        IReadOnlyDictionary<string, ScryfallCardData> resolvedCards,
        IReadOnlyCollection<string> roleKeys,
        ManabaseMode resolvedMode)
    {
        var roleKeySet = new HashSet<string>(roleKeys, StringComparer.Ordinal);
        var cardRoles = new Dictionary<string, IReadOnlyList<string>>(resolvedCards.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((string cardName, ScryfallCardData card) in resolvedCards.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            IReadOnlyList<string> roles = CutLabRoleAssigner.AssignRoles(ScryfallCardFactMapper.ToCardFact(card, quantity: 1, isCommander: false), [], isComboPiece: false, resolvedMode);
            cardRoles[cardName] = roles
                .Where(roleKeySet.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return cardRoles;
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
                    Console.WriteLine(FormattableString.Invariant(
                        $"Scryfall 429 persisted for {operationName}; excluding from tallies after {attempt} attempts."));
                    return default;
                }

                TimeSpan delay = ComputeScryfall429Backoff(attempt);
                Console.WriteLine(FormattableString.Invariant(
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

    private static ServiceProvider BuildScryfallServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDeckFlowHttpClients();
        services.AddDeckFlowResiliencePipelines();
        services.AddSingleton<IScryfallRestClientFactory, ScryfallRestClientFactory>();
        services.AddSingleton<ScryfallCollectionCardCache>();
        services.AddSingleton<IScryfallCardResolver>(serviceProvider =>
            new ScryfallCardResolver(
                serviceProvider.GetRequiredService<IScryfallRestClientFactory>(),
                serviceProvider.GetRequiredService<ResiliencePipelineProvider<string>>(),
                serviceProvider.GetRequiredService<ScryfallCollectionCardCache>()));
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

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return (1, string.Empty);
        }

        string stdout = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout);
    }

    private static InputFileDescriptor DescribeInputFile(string path)
    {
        var info = new FileInfo(path);
        return new InputFileDescriptor
        {
            Path = path,
            SizeBytes = info.Length,
            Sha256 = info.Length <= 16 * 1024 * 1024 ? ComputeSha256(path) : null,
        };
    }

    private static string DescribeDryRunPath(string path, bool mustExist)
    {
        if (!File.Exists(path))
        {
            return mustExist
                ? FormattableString.Invariant($"{path} (not found)")
                : FormattableString.Invariant($"{path} (not found - will be created)");
        }

        long length = new FileInfo(path).Length;
        return FormattableString.Invariant($"{path} (found, {length:N0} bytes)");
    }

    private static string? ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildMarkdownReport(EdhrecRoleGridReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# EDHREC Role Grid");
        builder.AppendLine();
        builder.AppendLine("## Run Provenance");
        builder.AppendLine();
        builder.AppendLine(FormattableString.Invariant($"- Run timestamp (UTC): {report.RunTimestampUtc}"));
        builder.AppendLine(FormattableString.Invariant($"- Harness commit: {report.HarnessCommitSha}"));
        builder.AppendLine(FormattableString.Invariant($"- EDHREC CSV: `{report.EdhrecCsv.Path}` ({report.EdhrecCsv.SizeBytes} bytes{FormatOptionalSha(report.EdhrecCsv.Sha256)})"));
        builder.AppendLine(FormattableString.Invariant($"- Averages CSV: `{report.AveragesCsv.Path}` ({report.AveragesCsv.SizeBytes} bytes{FormatOptionalSha(report.AveragesCsv.Sha256)})"));
        builder.AppendLine(FormattableString.Invariant($"- Cards cache: `{report.CardsCachePath}`"));
        builder.AppendLine(FormattableString.Invariant($"- Mode: `{report.Mode}`"));
        builder.AppendLine(FormattableString.Invariant($"- Rows read: {report.RowsRead}"));
        builder.AppendLine(FormattableString.Invariant($"- Distinct cards: {report.DistinctCards}"));
        builder.AppendLine(FormattableString.Invariant($"- Commanders accumulated: {report.CommandersAccumulated}"));
        builder.AppendLine(FormattableString.Invariant($"- Commanders excluded by denominator gate: {report.DenominatorMismatchCount}"));
        builder.AppendLine(FormattableString.Invariant($"- Commanders missing denominator: {report.MissingDenominatorCount}"));
        builder.AppendLine(FormattableString.Invariant($"- Malformed rows: {report.MalformedRows}"));
        builder.AppendLine(FormattableString.Invariant($"- Unresolved card names: {report.UnresolvedCardNames.Count}"));
        builder.AppendLine();
        builder.AppendLine("## What this measures — and what it does not");
        builder.AppendLine();
        builder.AppendLine("For commander `C` and role `R`:");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine("expected[C, R] = SUM over cards ( count[C, card] / denominator[C] ) x isRole(card, R)");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("This is a mean-style expected count, not a percentile. Two commanders can have the same expected role count while having different lower-tail deck shapes, so this corpus does not support a floor. These figures feed no floor and no go/no-go.");
        builder.AppendLine();
        builder.AppendLine("## Denominator integrity");
        builder.AppendLine();
        builder.AppendLine(FormattableString.Invariant($"- Denominator mismatches: {report.DenominatorMismatchCount}"));
        builder.AppendLine("- `edhrec.csv` aggregates by a single commander name while `averages.csv` carries 3,372 solo rows and 3,213 partner-pair rows, so it is not established whether partner-pair decks are included in a commander's counts; a ratio above 1.0 proves the denominator is wrong for that commander and those commanders are excluded rather than clamped.");
        builder.AppendLine("- Worst five mismatches (already ordered by ratio descending):");
        foreach (EdhrecDenominatorMismatch mismatch in report.DenominatorMismatches.Take(5))
        {
            builder.AppendLine(FormattableString.Invariant(
                $"  - {EscapePipe(mismatch.Commander)} | {EscapePipe(mismatch.Card)} | count={mismatch.Count} | denominator={mismatch.Denominator} | ratio={FormatMetric(mismatch.Ratio)}"));
        }

        builder.AppendLine();
        builder.AppendLine("## Expected role counts");
        builder.AppendLine();
        AppendMarkdownTableHeader(builder, RoleFloorFigureTable.EdhrecBulkColumns);
        foreach (EdhrecBulkRoleExpectation figure in report.Figures)
        {
            builder.AppendLine(BuildFigureRow(figure));
        }

        builder.AppendLine();
        builder.AppendLine("## Commander totals");
        builder.AppendLine();
        foreach (EdhrecBulkCommanderTotals commander in report.CommanderTotals)
        {
            builder.AppendLine(FormattableString.Invariant(
                $"- {EscapePipe(commander.Commander)}: rows consumed={commander.RowsConsumed}; total inclusion rate={FormatMetric(commander.TotalInclusionRate)}"));
        }

        builder.AppendLine();
        builder.AppendLine("## Known gaps");
        builder.AppendLine();
        builder.AppendLine("- No bracket dimension exists in this archive.");
        builder.AppendLine(FormattableString.Invariant($"- Unresolved cards are excluded from classification and counted explicitly: {report.UnresolvedCardNames.Count}."));
        builder.AppendLine("- `other` is excluded because it is a residual classifier-coverage bucket rather than a deck-construction target.");
        builder.AppendLine("- Classification is oracle-only via the shipped Cut Lab assigner call shape used by the other corpus arms.");
        builder.AppendLine("- A mean-style expected count cannot support a floor.");
        return builder.ToString();
    }

    private static object BuildJsonPayload(EdhrecRoleGridReport report)
    {
        return new
        {
            runProvenance = new
            {
                report.RunTimestampUtc,
                report.HarnessCommitSha,
                edhrecCsv = report.EdhrecCsv,
                averagesCsv = report.AveragesCsv,
                report.CardsCachePath,
                report.Mode,
                report.MinDecks,
                report.RowsRead,
                report.DistinctCards,
                report.CommandersAccumulated,
                report.DenominatorMismatchCount,
                report.MissingDenominatorCount,
                report.MalformedRows,
                unresolvedCardNames = report.UnresolvedCardNames,
            },
            whatThisMeasures = new
            {
                estimator = "expected[C, R] = SUM over cards ( count[C, card] / denominator[C] ) x isRole(card, R)",
                summary = "Mean-style expected count; not a percentile; does not feed a floor or go/no-go.",
            },
            denominatorIntegrity = new
            {
                report.DenominatorMismatchCount,
                worstFive = report.DenominatorMismatches.Take(5).ToArray(),
                caveat = "edhrec.csv aggregates by a single commander name while averages.csv carries solo rows and partner-pair rows, so a ratio above 1.0 proves the denominator is wrong for that commander and those commanders are excluded rather than clamped.",
                missingDenominators = report.MissingDenominators,
                commanderTotals = report.CommanderTotals,
            },
            expectedRoleCounts = report.Figures.Select(figure => new
            {
                source = figure.Source,
                commanderName = figure.CommanderName,
                role = figure.Role,
                expectedCount = figure.ExpectedCount,
                deckCount = figure.DeckCount,
                rowsConsumed = figure.RowsConsumed,
                maxCardInclusion = figure.MaxCardInclusion,
            }).ToArray(),
            knownGaps = new[]
            {
                "No bracket dimension exists in this archive.",
                FormattableString.Invariant($"Unresolved cards are excluded from classification and counted explicitly: {report.UnresolvedCardNames.Count}."),
                "`other` is excluded because it is a residual classifier-coverage bucket rather than a deck-construction target.",
                "Classification is oracle-only via the shipped Cut Lab assigner call shape used by the other corpus arms.",
                "A mean-style expected count cannot support a floor.",
            },
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonOptions);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void AppendMarkdownTableHeader(StringBuilder builder, IReadOnlyList<string> columns)
    {
        builder.Append("| ");
        builder.Append(string.Join(" | ", columns.Select(EscapePipe)));
        builder.AppendLine(" |");
        builder.Append("| ");
        builder.Append(string.Join(" | ", columns.Select(_ => "---")));
        builder.AppendLine(" |");
    }

    private static string BuildFigureRow(EdhrecBulkRoleExpectation figure)
    {
        return FormattableString.Invariant(
            $"| {FormatRoleFloorSource(figure.Source)} | {EscapePipe(figure.CommanderName)} | {EscapePipe(figure.Role)} | {FormatMetric(figure.ExpectedCount)} | {figure.DeckCount} | {figure.RowsConsumed} | {FormatMetric(figure.MaxCardInclusion)} |");
    }

    private static string FormatRoleFloorSource(RoleFloorSource source)
        => source.ToString();

    private static string FormatMetric(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string EscapePipe(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string FormatOptionalSha(string? sha256)
        => string.IsNullOrWhiteSpace(sha256) ? string.Empty : FormattableString.Invariant($", sha256={sha256}");

    private sealed record CardResolutionResult(
        IReadOnlyDictionary<string, ScryfallCardData> ResolvedCards,
        IReadOnlyCollection<string> UnresolvedNotFoundNames,
        IReadOnlyCollection<string> UnresolvedRateLimitedNames)
    {
        public IReadOnlyCollection<string> UnresolvedNames =>
            UnresolvedNotFoundNames
                .Concat(UnresolvedRateLimitedNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private sealed record InputFileDescriptor
    {
        public required string Path { get; init; }
        public required long SizeBytes { get; init; }
        public string? Sha256 { get; init; }
    }

    private sealed record EdhrecRoleGridReport
    {
        public required string RunTimestampUtc { get; init; }
        public required string HarnessCommitSha { get; init; }
        public required InputFileDescriptor EdhrecCsv { get; init; }
        public required InputFileDescriptor AveragesCsv { get; init; }
        public required string CardsCachePath { get; init; }
        public required string Mode { get; init; }
        public required int MinDecks { get; init; }
        public required long RowsRead { get; init; }
        public required int DistinctCards { get; init; }
        public required int CommandersAccumulated { get; init; }
        public required int DenominatorMismatchCount { get; init; }
        public required int MissingDenominatorCount { get; init; }
        public required int MalformedRows { get; init; }
        public required IReadOnlyCollection<string> UnresolvedCardNames { get; init; }
        public required IReadOnlyList<EdhrecBulkRoleExpectation> Figures { get; init; }
        public required IReadOnlyList<EdhrecDenominatorMismatch> DenominatorMismatches { get; init; }
        public required IReadOnlyList<string> MissingDenominators { get; init; }
        public required IReadOnlyList<EdhrecBulkCommanderTotals> CommanderTotals { get; init; }
    }

}
