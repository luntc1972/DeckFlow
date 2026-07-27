using System.Data.Common;
using Dapper;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Owns card_category_observations, card_deck_totals, sources, and cards read/query/upsert
/// operations, plus filtering and normalization helpers.
/// </summary>
internal sealed class CardCategoryRepository
{
    private const string ArchidektLiveSourcePrefix = "archidekt_live:";
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly CategoryCacheSchema _schema;

    /// <summary>
    /// Initializes the card-category collaborator.
    /// </summary>
    /// <param name="connectionInfo">Provider and connection string details for the knowledge database.</param>
    /// <param name="schema">Shared schema collaborator used to initialize tables on first access.</param>
    internal CardCategoryRepository(RelationalDatabaseConnection connectionInfo, CategoryCacheSchema schema)
    {
        _connectionInfo = connectionInfo;
        _schema = schema;
    }

    /// <summary>
    /// Retrieves previously observed categories for the specified card.
    /// </summary>
    /// <param name="cardName">Card name to look up.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await _schema.EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var categories = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT o.category
            FROM card_category_observations o
            JOIN cards c ON c.id = o.card_id
            WHERE c.normalized_card_name = @normalized
            GROUP BY o.category
            ORDER BY LOWER(o.category), o.category
            """,
            new { normalized = CardNormalizer.Normalize(cardName) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return CategoryFilter.IncludedOrFallback(categories);
    }

    /// <summary>
    /// Batch equivalent of <see cref="GetCategoriesAsync"/>: resolves categories for many cards in a
    /// single round-trip (one <c>IN</c> query instead of one query per card). Returns a dictionary
    /// keyed by the ORIGINAL requested name (case-insensitive) so the caller can look each spell up by
    /// the same string it passed in. Every distinct input name gets an entry — including cards with no
    /// stored observations, which receive <see cref="CategoryFilter.IncludedOrFallback"/>'s fallback
    /// exactly as the per-card path does. Blank names are skipped.
    /// </summary>
    /// <param name="cardNames">Card names to resolve (display spellings; duplicates share one lookup).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(
        IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardNames);

        // Distinct normalized keys to query; two spellings of one card collapse to a single key.
        var normalizedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in cardNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                normalizedKeys.Add(CardNormalizer.Normalize(name));
            }
        }

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (normalizedKeys.Count == 0)
        {
            return result;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CardCategoryNameRow>(new CommandDefinition(
            """
            SELECT c.normalized_card_name AS NormalizedCardName, o.category AS Category
            FROM card_category_observations o
            JOIN cards c ON c.id = o.card_id
            WHERE c.normalized_card_name IN @normalized
            GROUP BY c.normalized_card_name, o.category
            ORDER BY c.normalized_card_name, LOWER(o.category), o.category
            """,
            new { normalized = normalizedKeys.ToList() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var categoriesByNormalized = rows
            .GroupBy(row => row.NormalizedCardName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Category), StringComparer.Ordinal);

        // Re-key by the caller's ORIGINAL spelling (re-normalizing to find its row set), so a spell can be
        // looked up by the same string it was passed in as. Every distinct requested name gets an entry,
        // and a card absent from the cache still receives IncludedOrFallback's fallback (per-card parity).
        foreach (var name in cardNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var raw = categoriesByNormalized.TryGetValue(CardNormalizer.Normalize(name), out var cats)
                ? cats
                : Enumerable.Empty<string>();
            result[name] = CategoryFilter.IncludedOrFallback(raw);
        }

        return result;
    }

    /// <summary>
    /// Retrieves detail rows for a card, including display name and count.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="boardFilter">Optional board name to filter the category rows.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCardAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await _schema.EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var queryTemplate = """
            SELECT o.category, o.card_name, SUM(o.count) AS total, SUM(o.deck_count) AS deck_total
            FROM card_category_observations o
            JOIN cards c ON c.id = o.card_id
            WHERE c.normalized_card_name = @normalized
            {0}
            GROUP BY o.category, o.card_name
            ORDER BY total DESC, LOWER(o.category), o.category;
        """;
        var filterClause = boardFilter is null
            ? string.Empty
            : "AND o.board = @board";
        var rawRows = await connection.QueryAsync<CategoryKnowledgeAggregateRow>(new CommandDefinition(
            string.Format(queryTemplate, filterClause),
            new
            {
                normalized = CardNormalizer.Normalize(cardName),
                board = boardFilter is null ? null : NormalizeBoard(boardFilter)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var rows = rawRows
            .Select(row => new CategoryKnowledgeRow(row.Category, row.CardName, checked((int)row.Total), checked((int)row.DeckTotal)))
            .ToList();

        return FilterGenericCategoryRowsWithFallback(rows);
    }

    /// <summary>
    /// Returns all card-category observations from decks led by <paramref name="commanderName"/>,
    /// aggregated across every harvested deck that has this commander in the commander zone.
    /// </summary>
    internal async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await _schema.EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rawRows = await connection.QueryAsync<CategoryKnowledgeAggregateRow>(new CommandDefinition(
            """
            SELECT o.category, o.card_name, SUM(o.count) AS total, COUNT(DISTINCT q.id) AS deck_total
            FROM card_category_observations o
            JOIN sources s ON s.id = o.source_id
            JOIN deck_queue q ON q.id = s.deck_queue_id
            WHERE LOWER(q.commander_name) = LOWER(@commanderName)
              AND q.processed = 1
            GROUP BY o.category, o.card_name
            ORDER BY total DESC, LOWER(o.category), o.category;
            """,
            new { commanderName },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var rows = rawRows
            .Select(row => new CategoryKnowledgeRow(row.Category, row.CardName, checked((int)row.Total), checked((int)row.DeckTotal)))
            .ToList();

        return FilterGenericCategoryRowsWithFallback(rows);
    }

    internal async Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(
        string commanderName,
        CancellationToken cancellationToken = default,
        string? boardFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await _schema.EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var queryTemplate = """
            SELECT DISTINCT o.category AS Category, o.card_name AS CardName, q.id AS DeckId
            FROM card_category_observations o
            JOIN sources s ON s.id = o.source_id
            JOIN deck_queue q ON q.id = s.deck_queue_id
            WHERE LOWER(q.commander_name) = LOWER(@commanderName)
              AND q.processed = 1
              {0};
            """;
        var filterClause = boardFilter is null
            ? string.Empty
            : "AND o.board = @board";
        var memberships = await connection.QueryAsync<CategoryDeckMembership>(new CommandDefinition(
            string.Format(queryTemplate, filterClause),
            new
            {
                commanderName,
                board = boardFilter is null ? null : NormalizeBoard(boardFilter)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return FilterGenericMembershipWithFallback(memberships.ToList());
    }

    /// <summary>
    /// Replaces all observations for a source with the provided rows.
    /// </summary>
    /// <param name="source">Source label for the data.</param>
    /// <param name="rows">Rows to persist.</param>
    /// <param name="board">Board name applied to each persisted row.</param>
    /// <param name="deckCount">Deck-count total applied to each persisted row.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async Task ReplaceSourceRowsAsync(string source, IReadOnlyList<CategoryKnowledgeRow> rows, string board = "mainboard", int deckCount = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (rows is null)
        {
            return;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = rows.Count == 0
            ? await ResolveSourceIdForReadAsync(connection, transaction, source, cancellationToken)
            : await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        if (sourceId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM card_category_observations WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var normalizedBoard = NormalizeBoard(board);
        var lastSeenUtc = DateTime.UtcNow;
        foreach (var row in rows)
        {
            var cardId = await ResolveCardIdAsync(connection, transaction, row.CardName, cardIds, cancellationToken);
            var deckCountValue = row.DeckCount > 0 ? row.DeckCount : deckCount;
            await UpsertCategoryObservationAsync(
                connection,
                transaction,
                sourceId.Value,
                cardId,
                row.CardName,
                row.Category,
                normalizedBoard,
                row.Count,
                deckCountValue,
                lastSeenUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Removes all cached observation and deck total rows for the provided source.
    /// </summary>
    /// <param name="source">Source label to remove.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async Task DeleteSourceDataAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdForReadAsync(connection, transaction, source, cancellationToken);
        if (sourceId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM card_category_observations WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM card_deck_totals WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Persists observed categories for a specific card occurrence.
    /// </summary>
    /// <param name="source">Data source label.</param>
    /// <param name="cardName">Card name.</param>
    /// <param name="categories">Categories to record.</param>
    /// <param name="quantity">Quantity observed.</param>
    /// <param name="board">Board name containing the observed card.</param>
    /// <param name="deckCountIncrement">Deck-total increment applied to the card totals for this observation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(cardName) || categories.Count == 0 || quantity <= 0)
        {
            return;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var cardId = await ResolveCardIdAsync(connection, transaction, cardName, cardIds, cancellationToken);
        var normalizedBoard = NormalizeBoard(board);
        var lastSeenUtc = DateTime.UtcNow;
        foreach (var category in categories)
        {
            await UpsertCategoryObservationAsync(
                connection,
                transaction,
                sourceId,
                cardId,
                cardName,
                category,
                normalizedBoard,
                quantity,
                deckCountIncrement,
                lastSeenUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Persists the number of decks that contain the given card on the specified board.
    /// </summary>
    internal async Task PersistCardDeckTotalsAsync(string source, string cardName, string board = "mainboard", int deckCountIncrement = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(cardName) || deckCountIncrement <= 0)
        {
            return;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var cardId = await ResolveCardIdAsync(connection, transaction, cardName, cardIds, cancellationToken);
        await UpsertCardDeckTotalAsync(
            connection,
            transaction,
            sourceId,
            cardId,
            NormalizeBoard(board),
            deckCountIncrement,
            DateTime.UtcNow,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Persists a batch of card-category observations and card-board totals in a single transaction.
    /// </summary>
    internal async Task PersistDeckCategoryBatchAsync(
        string source,
        IReadOnlyList<(string CardName, string Category, string Board, int Quantity, int DeckCountIncrement)> observations,
        IReadOnlyList<(string CardName, string Board)> cardBoardTotals,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || (observations.Count == 0 && cardBoardTotals.Count == 0))
        {
            return;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var lastSeenUtc = DateTime.UtcNow;

        foreach (var observation in observations)
        {
            if (string.IsNullOrWhiteSpace(observation.CardName) || observation.Quantity <= 0)
            {
                continue;
            }

            var cardId = await ResolveCardIdAsync(connection, transaction, observation.CardName, cardIds, cancellationToken);
            await UpsertCategoryObservationAsync(
                connection,
                transaction,
                sourceId,
                cardId,
                observation.CardName,
                observation.Category,
                NormalizeBoard(observation.Board),
                observation.Quantity,
                observation.DeckCountIncrement,
                lastSeenUtc,
                cancellationToken);
        }

        foreach (var cardBoardTotal in cardBoardTotals)
        {
            if (string.IsNullOrWhiteSpace(cardBoardTotal.CardName))
            {
                continue;
            }

            var cardId = await ResolveCardIdAsync(connection, transaction, cardBoardTotal.CardName, cardIds, cancellationToken);
            await UpsertCardDeckTotalAsync(
                connection,
                transaction,
                sourceId,
                cardId,
                NormalizeBoard(cardBoardTotal.Board),
                1,
                lastSeenUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves deck totals for the card, optionally filtered by board.
    /// </summary>
    internal async Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await _schema.EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var filterClause = boardFilter is null ? string.Empty : "AND t.board = @board";
        var rows = await connection.QueryAsync<BoardDeckTotalRow>(new CommandDefinition(
            $"""
            SELECT t.board, SUM(t.deck_count) AS total
            FROM card_deck_totals t
            JOIN cards c ON c.id = t.card_id
            WHERE c.normalized_card_name = @normalized
            {filterClause}
            GROUP BY t.board;
            """,
            new
            {
                normalized = CardNormalizer.Normalize(cardName),
                board = boardFilter is null ? null : NormalizeBoard(boardFilter)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var boardCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            boardCounts[row.Board] = checked((int)row.Total);
        }

        var totalDecks = boardCounts.Values.Sum();
        return new CardDeckTotals(totalDecks, boardCounts);
    }

    /// <summary>
    /// Checks whether the repository already contains entries for the source.
    /// </summary>
    /// <param name="source">Source label to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async Task<bool> HasSourceDataAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sourceId = await ResolveSourceIdForReadAsync(connection, null, source, cancellationToken);
        if (sourceId is null)
        {
            return false;
        }

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(1) FROM card_category_observations WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result > 0L;
    }

    private static async Task<long> ResolveCardIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        string cardName,
        IDictionary<string, long> cache,
        CancellationToken cancellationToken)
    {
        var normalized = CardNormalizer.Normalize(cardName);
        if (cache.TryGetValue(normalized, out var cachedId))
        {
            return cachedId;
        }

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO cards (normalized_card_name, display_name)
            VALUES (@normalized, @display)
            ON CONFLICT(normalized_card_name)
            DO UPDATE SET display_name = excluded.display_name
            RETURNING id;
            """,
            new { normalized, display = cardName },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        cache[normalized] = id;
        return id;
    }

    private static async Task<long?> ResolveSourceIdForReadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string source,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT id FROM sources WHERE source = @source;",
            new { source },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<long> ResolveSourceIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        string source,
        CancellationToken cancellationToken)
    {
        var deckQueueId = await ResolveDeckQueueIdForSourceAsync(connection, transaction, source, cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO sources (source, deck_queue_id)
            VALUES (@source, @deckQueueId)
            ON CONFLICT(source)
            DO UPDATE SET deck_queue_id = COALESCE(sources.deck_queue_id, excluded.deck_queue_id)
            RETURNING id;
            """,
            new { source, deckQueueId },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<long?> ResolveDeckQueueIdForSourceAsync(
        DbConnection connection,
        DbTransaction transaction,
        string source,
        CancellationToken cancellationToken)
    {
        if (!source.StartsWith(ArchidektLiveSourcePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var deckId = source[ArchidektLiveSourcePrefix.Length..];
        if (string.IsNullOrWhiteSpace(deckId))
        {
            return null;
        }

        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT id FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task UpsertCategoryObservationAsync(
        DbConnection connection,
        DbTransaction transaction,
        long sourceId,
        long cardId,
        string cardName,
        string category,
        string board,
        int quantity,
        int deckCountIncrement,
        DateTime lastSeenUtc,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO card_category_observations (source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc)
            VALUES (@sourceId, @cardId, @cardName, @category, @board, @deckCount, @quantity, @lastSeenUtc)
            ON CONFLICT(source_id, card_id, category, board)
            DO UPDATE SET
                count = card_category_observations.count + excluded.count,
                deck_count = card_category_observations.deck_count + excluded.deck_count,
                card_name = excluded.card_name,
                last_seen_utc = excluded.last_seen_utc;
            """,
            new
            {
                sourceId,
                cardId,
                cardName,
                category,
                board,
                deckCount = deckCountIncrement,
                quantity,
                lastSeenUtc
            },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task UpsertCardDeckTotalAsync(
        DbConnection connection,
        DbTransaction transaction,
        long sourceId,
        long cardId,
        string board,
        int deckCountIncrement,
        DateTime lastSeenUtc,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO card_deck_totals (source_id, card_id, board, deck_count, last_seen_utc)
            VALUES (@sourceId, @cardId, @board, @deckCount, @lastSeenUtc)
            ON CONFLICT(source_id, card_id, board)
            DO UPDATE SET
                deck_count = card_deck_totals.deck_count + excluded.deck_count,
                last_seen_utc = excluded.last_seen_utc;
            """,
            new
            {
                sourceId,
                cardId,
                board,
                deckCount = deckCountIncrement,
                lastSeenUtc
            },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string NormalizeBoard(string? board)
    {
        if (string.IsNullOrWhiteSpace(board))
        {
            return "mainboard";
        }

        return board.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<CategoryKnowledgeRow> FilterGenericCategoryRowsWithFallback(IReadOnlyList<CategoryKnowledgeRow> rows)
        => FilterGenericByCardWithFallback(rows, row => row.CardName, row => row.Category);

    private static IReadOnlyList<CategoryDeckMembership> FilterGenericMembershipWithFallback(IReadOnlyList<CategoryDeckMembership> memberships)
        => FilterGenericByCardWithFallback(memberships, membership => membership.CardName, membership => membership.Category);

    // Drops each card's generic categories when a more specific one is present, keeping
    // a fallback when only generics exist. Shared by the aggregate-row and per-deck
    // membership queries so both apply identical filtering. Distinct guards against the
    // membership query's duplicate (card, category) pairs across decks; it is a no-op for
    // the already-unique aggregate rows.
    private static IReadOnlyList<T> FilterGenericByCardWithFallback<T>(IReadOnlyList<T> items, Func<T, string> cardName, Func<T, string> category)
    {
        var categoriesByCard = items
            .GroupBy(cardName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => CategoryFilter.IncludedOrFallback(group.Select(category).Distinct(StringComparer.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);

        return items
            .Where(item => categoriesByCard[cardName(item)].Contains(category(item), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private DbConnection CreateConnection() => _connectionInfo.CreateConnection();

    private sealed class CategoryKnowledgeAggregateRow
    {
        public string Category { get; init; } = string.Empty;
        public string CardName { get; init; } = string.Empty;
        public long Total { get; init; }
        public long DeckTotal { get; init; }
    }

    private sealed class BoardDeckTotalRow
    {
        public string Board { get; init; } = string.Empty;
        public long Total { get; init; }
    }

    private sealed class CardCategoryNameRow
    {
        public string NormalizedCardName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
    }
}
