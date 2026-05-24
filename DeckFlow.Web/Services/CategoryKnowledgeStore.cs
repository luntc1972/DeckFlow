using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
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
    public CategoryKnowledgeStore(IWebHostEnvironment environment)
    {
        _connectionInfo = DeckFlowDatabaseConnectionFactory.CreateCategoryKnowledgeConnection(environment);
        _artifactsPath = ResolveArtifactsPath(environment);
        _databasePath = _connectionInfo.IsSqlite
            ? Path.Combine(_artifactsPath, "category-knowledge.db")
            : null;
        _repository = new CategoryKnowledgeRepository(_connectionInfo);
        _archidektImporter = new ArchidektApiDeckImporter();
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
    /// Persists observed categories emitted during runtime lookups.
    /// </summary>
    /// <param name="source">Source label for categories.</param>
    /// <param name="cardName">Card name.</param>
    /// <param name="categories">Categories to persist.</param>
    /// <param name="quantity">Quantity recorded.</param>
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
    public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default) => _repository.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM deck_queue WHERE processed = 1;";
        return await ExecuteCountAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM deck_queue WHERE processed = 1 AND inserted_utc >= @cutoff;";
        AddTimestampParameter(command, "@cutoff", cutoffUtc);
        return await ExecuteCountAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM card_category_observations;";
        return await ExecuteCountAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT commander_name, COUNT(1) AS deck_count
            FROM deck_queue
            WHERE processed = 1 AND commander_name IS NOT NULL
            GROUP BY commander_name
            ORDER BY deck_count DESC
            LIMIT @n;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@n", n);

        var rows = new List<TopCommanderRow>(capacity: Math.Max(n, 0));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new TopCommanderRow(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
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
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_database_size(current_database())";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is long bytes ? bytes : null;
    }

    /// <summary>
    /// Runs an extended cache sweep for the specified duration.
    /// </summary>
    /// <param name="logger">Logger for the sweep.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    private static async Task<int> ExecuteCountAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result switch
        {
            null => 0,
            DBNull => 0,
            long value => checked((int)value),
            int value => value,
            _ => Convert.ToInt32(result)
        };
    }

    private static void AddTimestampParameter(DbCommand command, string name, DateTime cutoffUtc)
    {
        var iso = DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc).ToString("O");
        RelationalDatabaseConnection.AddParameter(command, name, iso);
    }
}
