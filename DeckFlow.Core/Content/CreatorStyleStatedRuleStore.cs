using System.Data.Common;
using Dapper;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="ICreatorStyleStatedRuleStore"/> for creator stated rules.
/// </summary>
public sealed class CreatorStyleStatedRuleStore : ICreatorStyleStatedRuleStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly Func<CancellationToken, Task<DbConnection>>? _connectionFactoryOverride;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed creator stated-rule store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public CreatorStyleStatedRuleStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a creator stated-rule store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">
    /// When <c>true</c> (default) the store auto-creates its schema on first use. When
    /// <c>false</c> <see cref="EnsureSchemaAsync"/> is a no-op so the store never issues CREATE/ALTER/DROP.
    /// </param>
    public CreatorStyleStatedRuleStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
        : this(connectionInfo, ensureSchemaEnabled, connectionFactoryOverride: null) { }

    /// <summary>
    /// Test-seam constructor: injects a connection-factory override so tests can wrap the real
    /// connection with a recording double and assert the exact SQL issued.
    /// The public constructors pass <c>null</c> and behave exactly as production.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">Whether schema auto-ensure runs.</param>
    /// <param name="connectionFactoryOverride">
    /// Optional connection factory used by <see cref="OpenConnectionAsync"/> in place of the live one.
    /// </param>
    internal CreatorStyleStatedRuleStore(
        RelationalDatabaseConnection connectionInfo,
        bool ensureSchemaEnabled,
        Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        _ensureSchemaEnabled = ensureSchemaEnabled;
        _connectionFactoryOverride = connectionFactoryOverride;
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!_ensureSchemaEnabled) return;
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpsertAsync(StatedRuleCandidate rule, string slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("slug", slug);
        parameters.Add("category", rule.Category);
        parameters.Add("metric", rule.Metric);
        parameters.Add("value", rule.Value);
        parameters.Add("valueMin", rule.ValueMin);
        parameters.Add("valueMax", rule.ValueMax);
        parameters.Add("comparator", rule.Comparator);
        // Why (grounding correction 2): SQLite treats NULLs as distinct for uniqueness, so an
        // absent condition binds as '' and the read mapper maps '' back to null on the way out.
        parameters.Add("condition", rule.Condition ?? string.Empty);
        parameters.Add("clipTimestampSeconds", rule.ClipTimestampSeconds);
        parameters.Add("sourceClip", rule.SourceClip);
        parameters.Add("confidence", rule.Confidence);
        parameters.Add("cardReference", rule.CardReference);
        parameters.Add("cardGrounded", rule.CardGrounded);
        parameters.Add("videoDateUtc", rule.VideoDateUtc);
        parameters.Add("provenance", rule.Provenance);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StatedRuleCandidate>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<StatedRuleReadModel>(new CommandDefinition(
            $"""
            SELECT {SelectColumnList}
              FROM content_stated_rules
             WHERE slug = @slug
             ORDER BY metric, condition;
            """,
            new { slug },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToCandidate).ToList();
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connectionFactoryOverride is not null)
        {
            return await _connectionFactoryOverride(cancellationToken).ConfigureAwait(false);
        }

        return await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    private static StatedRuleCandidate ToCandidate(StatedRuleReadModel row)
        => new()
        {
            Category = row.Category,
            Metric = row.Metric,
            Value = row.Value,
            ValueMin = row.ValueMin,
            ValueMax = row.ValueMax,
            Comparator = row.Comparator,
            // Why (grounding correction 2): the empty-string key sentinel maps back to null here
            // so a round-tripped unconditioned rule is field-equal to the one that went in.
            Condition = string.IsNullOrEmpty(row.Condition) ? null : row.Condition,
            ClipTimestampSeconds = row.ClipTimestampSeconds,
            SourceClip = row.SourceClip,
            Confidence = row.Confidence,
            CardReference = row.CardReference,
            CardGrounded = row.CardGrounded,
            VideoDateUtc = row.VideoDateUtc,
            Provenance = row.Provenance,
        };

    private const string SelectColumnList =
        "category, metric, value, value_min, value_max, comparator, condition, clip_timestamp_seconds, source_clip, confidence, card_reference, card_grounded, video_date_utc, provenance";

    private const string UpsertSql = """
        INSERT INTO content_stated_rules (
            slug,
            category,
            metric,
            value,
            value_min,
            value_max,
            comparator,
            condition,
            clip_timestamp_seconds,
            source_clip,
            confidence,
            card_reference,
            card_grounded,
            video_date_utc,
            provenance)
        VALUES (
            @slug,
            @category,
            @metric,
            @value,
            @valueMin,
            @valueMax,
            @comparator,
            @condition,
            @clipTimestampSeconds,
            @sourceClip,
            @confidence,
            @cardReference,
            @cardGrounded,
            @videoDateUtc,
            @provenance)
        ON CONFLICT (slug, metric, condition) DO UPDATE
        SET category = EXCLUDED.category,
            value = EXCLUDED.value,
            value_min = EXCLUDED.value_min,
            value_max = EXCLUDED.value_max,
            comparator = EXCLUDED.comparator,
            clip_timestamp_seconds = EXCLUDED.clip_timestamp_seconds,
            source_clip = EXCLUDED.source_clip,
            confidence = EXCLUDED.confidence,
            card_reference = EXCLUDED.card_reference,
            card_grounded = EXCLUDED.card_grounded,
            video_date_utc = EXCLUDED.video_date_utc,
            provenance = EXCLUDED.provenance;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_stated_rules (
            slug TEXT NOT NULL,
            category TEXT NOT NULL,
            metric TEXT NOT NULL,
            value DOUBLE PRECISION NULL,
            value_min DOUBLE PRECISION NULL,
            value_max DOUBLE PRECISION NULL,
            comparator TEXT NOT NULL,
            condition TEXT NOT NULL DEFAULT '',
            clip_timestamp_seconds INTEGER NULL,
            source_clip TEXT NOT NULL,
            confidence DOUBLE PRECISION NOT NULL,
            card_reference TEXT NULL,
            card_grounded BOOLEAN NULL,
            video_date_utc TIMESTAMPTZ NOT NULL,
            provenance TEXT NULL,
            created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (slug, metric, condition)
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_stated_rules (
            slug TEXT NOT NULL,
            category TEXT NOT NULL,
            metric TEXT NOT NULL,
            value REAL NULL,
            value_min REAL NULL,
            value_max REAL NULL,
            comparator TEXT NOT NULL,
            condition TEXT NOT NULL DEFAULT '',
            clip_timestamp_seconds INTEGER NULL,
            source_clip TEXT NOT NULL,
            confidence REAL NOT NULL,
            card_reference TEXT NULL,
            card_grounded INTEGER NULL,
            video_date_utc TEXT NOT NULL,
            provenance TEXT NULL,
            created_utc TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (slug, metric, condition)
        );
        """;

    /// <summary>
    /// Dapper materialization target for stated-rule read queries. A dedicated read model (rather
    /// than <c>SELECT *</c>) so a column rename cannot silently bind to nothing.
    /// </summary>
    private sealed class StatedRuleReadModel
    {
        public required string Category { get; init; }
        public required string Metric { get; init; }
        public double? Value { get; init; }
        public double? ValueMin { get; init; }
        public double? ValueMax { get; init; }
        public required string Comparator { get; init; }
        public string? Condition { get; init; }
        public int? ClipTimestampSeconds { get; init; }
        public required string SourceClip { get; init; }
        public double Confidence { get; init; }
        public string? CardReference { get; init; }
        public bool? CardGrounded { get; init; }
        public DateTimeOffset VideoDateUtc { get; init; }
        public string? Provenance { get; init; }
    }
}
