using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Threading;
using Dapper;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;
using Microsoft.Extensions.Logging;
using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Services;

/// <inheritdoc/>
public sealed class CategoryKnowledgeStore : ICategoryKnowledgeStore
{
    private const int HarvestDeckCount = 20;
    private readonly string _artifactsPath;
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly string? _databasePath;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private readonly SemaphoreSlim _sweepGate = new(1, 1);
    private readonly CategoryKnowledgeRepository _repository;
    private readonly ArchidektApiDeckImporter _archidektImporter;
    private readonly ArchidektRecentDecksImporter _recentDeckImporter;
    private volatile bool _schemaReady;

    /// <summary>
    /// Initializes the knowledge store for the web app environment.
    /// </summary>
    /// <param name="environment">Web host environment for locating artifacts.</param>
    /// <param name="logger">Optional logger forwarded to the category repository.</param>
    public CategoryKnowledgeStore(IWebHostEnvironment environment, ILogger<CategoryKnowledgeStore>? logger = null)
    {
        _connectionInfo = DeckFlowDatabaseConnectionFactory.CreateCategoryKnowledgeConnection(environment);
        _artifactsPath = ResolveArtifactsPath(environment);
        _databasePath = _connectionInfo.IsSqlite
            ? Path.Combine(_artifactsPath, "category-knowledge.db")
            : null;
        _repository = new CategoryKnowledgeRepository(_connectionInfo, logger);
        _archidektImporter = new ArchidektApiDeckImporter(logger: logger);
        _recentDeckImporter = new ArchidektRecentDecksImporter();
    }

    private static string ResolveArtifactsPath(IWebHostEnvironment environment)
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
        {
            return Path.GetFullPath(dataDir);
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "artifacts"));
    }

    /// <summary>
    /// Gets the resolved category knowledge database path, when available.
    /// </summary>
    public string? DatabasePath => _databasePath;

    /// <summary>
    /// Gets cached categories for a given card from the repository.
    /// </summary>
    /// <param name="cardName">Card name to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCategoriesAsync(cardName, cancellationToken);
    }

    /// <summary>
    /// Gets cached per-category deck counts for a given card from the repository.
    /// </summary>
    /// <param name="cardName">Card name to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyDictionary<string, int>> GetCategoryDeckCountsAsync(string cardName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCategoryDeckCountsAsync(cardName, cancellationToken);
    }

    /// <summary>
    /// Gets cached categories for many cards from the repository in a single query.
    /// </summary>
    /// <param name="cardNames">Card names to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardNames);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCategoriesForNamesAsync(cardNames, cancellationToken);
    }

    /// <summary>
    /// Persists observed categories emitted during runtime lookups.
    /// </summary>
    /// <param name="source">Source label for categories.</param>
    /// <param name="cardName">Card name.</param>
    /// <param name="categories">Categories to persist.</param>
    /// <param name="quantity">Quantity recorded.</param>
    /// <param name="board">Deck board where the observation was recorded.</param>
    /// <param name="deckCountIncrement">Amount to add to processed deck counters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(cardName) || categories.Count == 0 || quantity <= 0)
        {
            return;
        }

        await EnsureSchemaReadyAsync(cancellationToken);
        await _repository.PersistObservedCategoriesAsync(source, cardName, categories, quantity, board, deckCountIncrement, cancellationToken);
    }

    /// <inheritdoc/>
    public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default) => _repository.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken: cancellationToken);

    /// <inheritdoc/>
    public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, ArchidektDeckMetadata? metadata, CancellationToken cancellationToken = default)
        => _repository.MarkUrlDeckProcessedAsync(deckId, commanderName, metadata, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return CoerceCount(await connection.ExecuteScalarAsync<object?>(new CommandDefinition(
            "SELECT COUNT(1) FROM deck_queue WHERE processed = 1;",
            cancellationToken: cancellationToken)).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Why: inserted_utc is a TEXT column on both dialects, but the Dapper DateTime
        // handler binds @cutoff as a native timestamptz on Postgres — and Postgres has
        // no `text >= timestamptz` operator (42883), so the comparison must cast the
        // column to timestamptz there. SQLite keeps its lexical TEXT comparison
        // unchanged. (F-51-PG-01)
        var column = _connectionInfo.IsSqlite
            ? "inserted_utc"
            : "inserted_utc::timestamptz";
        return CoerceCount(await connection.ExecuteScalarAsync<object?>(new CommandDefinition(
            $"SELECT COUNT(1) FROM deck_queue WHERE processed = 1 AND {column} >= @cutoff;",
            new { cutoff = cutoffUtc },
            cancellationToken: cancellationToken)).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (_connectionInfo.IsPostgres)
        {
            // Why: reltuples is a planner estimate refreshed by ANALYZE/autovacuum; the
            // schema-qualified to_regclass lookup avoids cross-schema name collisions, and
            // the <= 0 guard handles fresh deploys before planner stats exist.
            var estimate = CoerceCount(await connection.ExecuteScalarAsync<object?>(new CommandDefinition(
                "SELECT reltuples::bigint FROM pg_class WHERE oid = to_regclass(current_schema() || '.card_category_observations');",
                cancellationToken: cancellationToken)).ConfigureAwait(false));
            if (estimate > 0)
            {
                return estimate;
            }
        }

        return CoerceCount(await connection.ExecuteScalarAsync<object?>(new CommandDefinition(
            "SELECT COUNT(1) FROM card_category_observations;",
            cancellationToken: cancellationToken)).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TopCommanderRow>(new CommandDefinition(
            """
            SELECT commander_name, COUNT(1) AS deck_count
            FROM deck_queue
            WHERE processed = 1 AND commander_name IS NOT NULL
            GROUP BY commander_name
            ORDER BY deck_count DESC
            LIMIT @n;
            """,
            new { n },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Max(pageSize, 1);

        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        var rows = await _repository.GetPagedProcessedCommanderRowsAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
        return rows
            .Select(row => new HarvestedCommanderRow(row.CommanderName, row.DeckCount, row.LastProcessedUtc))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        return await _repository.GetDistinctProcessedCommanderCountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        if (!_connectionInfo.IsPostgres)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var result = await connection.ExecuteScalarAsync<object?>(new CommandDefinition(
            "SELECT pg_database_size(current_database())",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result is long bytes ? bytes : null;
    }

    /// <summary>
    /// Runs an extended cache sweep for the specified duration.
    /// </summary>
    /// <param name="logger">Logger for the sweep.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional progress reporter for processed deck counts.</param>
    public async Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
    {
        await EnsureSchemaReadyAsync(cancellationToken);
        await _sweepGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_artifactsPath);
            var session = new ArchidektDeckCacheSession(_repository, _archidektImporter, _recentDeckImporter, logger);
            var result = await session.RunAsync(
                TimeSpan.FromSeconds(durationSeconds),
                queueBatchSize: 5,
                fetchBatchSize: HarvestDeckCount,
                cancellationToken: cancellationToken,
                progress: progress);
            logger.LogInformation(
                "Archidekt cache sweep completed with {DecksAdded} added, {DecksUpdated} updated, {DecksUnchanged} unchanged, and {DecksSkipped} skipped decks.",
                result.DecksAdded,
                result.DecksUpdated,
                result.DecksUnchanged,
                result.DecksSkipped);
            return result.DecksProcessed;
        }
        finally
        {
            _sweepGate.Release();
        }
    }

    /// <summary>
    /// Retrieves cached category rows for a card.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="boardFilter">Optional board name used to filter observations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCategoryRowsForCardAsync(cardName, boardFilter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCategoryRowsForCommanderAsync(commanderName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCategoryDeckMembershipForCommanderAsync(commanderName, cancellationToken: cancellationToken); // Why: production deliberately stays unfiltered so CommanderCategoryService behavior does not change.
    }

    /// <summary>
    /// Retrieves overall deck totals for the provided card.
    /// </summary>
    public async Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCardDeckTotalsAsync(cardName, boardFilter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetCommanderDeckCountAsync(commanderName, cancellationToken);
    }

    /// <summary>
    /// Gets the number of decks whose categories have been cached.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
    {
        if (!_schemaReady)
        {
            return GetProcessedDeckCountCoreAsync(cancellationToken);
        }

        return _repository.GetProcessedDeckCountAsync(cancellationToken);
    }

    private async Task<int> GetProcessedDeckCountCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaReadyAsync(cancellationToken);
        return await _repository.GetProcessedDeckCountAsync(cancellationToken);
    }

    private async Task EnsureSchemaReadyAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            if (_connectionInfo.IsSqlite)
            {
                Directory.CreateDirectory(_artifactsPath);
            }
            await _repository.EnsureSchemaAsync(cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    internal static int CoerceCount(object? result)
    {
        return result switch
        {
            null => 0,
            DBNull => 0,
            long value => ClampCount(value),
            int value => Math.Max(value, 0),
            _ => ClampCount(Convert.ToInt64(result, CultureInfo.InvariantCulture))
        };
    }

    private static int ClampCount(long value)
        => value <= 0 ? 0 : value > int.MaxValue ? int.MaxValue : (int)value;
}
