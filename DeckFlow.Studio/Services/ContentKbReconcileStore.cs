using System.Data.Common;
using Dapper;
using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Default implementation of <see cref="IContentKbReconcileStore"/>, backed by a local SQLite
/// <c>content_kb_reconcile_discrepancy</c> table in the operator's <c>content-kb.db</c> (D-05).
/// Mirrors <see cref="ContentHarvestRunStore"/>'s shape exactly: a <see cref="SemaphoreSlim"/>-gated
/// <see cref="EnsureSchemaAsync"/>, dialect-guarded <c>CREATE TABLE IF NOT EXISTS</c>, and a plain
/// <see cref="RelationalDatabaseConnection"/> handle (dialect-capable, though Studio always
/// constructs this via <see cref="ContentKbReconcileStore(string)"/> against the local SQLite file).
/// </summary>
public sealed class ContentKbReconcileStore : IContentKbReconcileStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed discrepancy store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file (Studio's <c>content-kb.db</c>).</param>
    public ContentKbReconcileStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a discrepancy store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public ContentKbReconcileStore(RelationalDatabaseConnection connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
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
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            // Why: schema creation is an intentional raw ADO.NET carve-out for this phase (mirrors
            // ContentHarvestRunStore.EnsureSchemaAsync).
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
    public async Task PersistRunAsync(
        string scopeTag,
        IReadOnlyList<ContentKbReconcileDiscrepancy> seen,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeTag);
        ArgumentNullException.ThrowIfNull(seen);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var upsertSql = _connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql;
            if (seen.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    upsertSql,
                    seen.Select(d => new
                    {
                        discrepancyId = d.Id,
                        kind = ToKindText(d.Kind),
                        naturalKeyType = d.NaturalKeyType,
                        naturalKeyValue = d.NaturalKeyValue,
                        artifactPath = d.ArtifactPath,
                        title = d.Title,
                        scopeTag,
                        now
                    }),
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            if (seen.Count > 0)
            {
                // Why: NOT IN @seenIds resolves every open discrepancy in this scope that was NOT
                // just re-affirmed by the upsert above (Dapper expands @seenIds into an IN list).
                await connection.ExecuteAsync(new CommandDefinition(
                    ResolveAbsentWithSeenSql,
                    new { scopeTag, now, seenIds = seen.Select(d => d.Id).ToArray() },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            else
            {
                // Why: guard the empty-seen case explicitly (D-05) rather than relying on dialect
                // behavior for "NOT IN ()" — an empty run legitimately resolves the entire scope
                // (nothing was seen, so everything previously open in this scope is now absent).
                await connection.ExecuteAsync(new CommandDefinition(
                    ResolveAllInScopeSql,
                    new { scopeTag, now },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredReconcileDiscrepancy>> GetOpenAsync(
        string? scopeTag,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = scopeTag is null ? GetOpenAllScopesSql : GetOpenByScopeSql;
        var rows = await connection.QueryAsync<StoredReconcileDiscrepancyRow>(new CommandDefinition(
            sql,
            new { scopeTag },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new StoredReconcileDiscrepancy(
            r.DiscrepancyId,
            ParseKind(r.Kind),
            r.NaturalKeyType,
            r.NaturalKeyValue,
            r.ArtifactPath,
            r.Title,
            r.ScopeTag,
            r.FirstSeenUtc,
            r.LastSeenUtc,
            r.ResolvedUtc)).ToList();
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    // Why: the persisted kind vocabulary MUST match ContentKbReconcileKind's XML doc comment
    // (published_orphan / file_orphan / seed_drift / body_hash_mismatch) — this is the store's own
    // text<->enum boundary, kept local because ContentKbReconcileDiscrepancy.KindToken is private.
    private static string ToKindText(ContentKbReconcileKind kind) => kind switch
    {
        ContentKbReconcileKind.PublishedOrphan => "published_orphan",
        ContentKbReconcileKind.FileOrphan => "file_orphan",
        ContentKbReconcileKind.SeedDrift => "seed_drift",
        ContentKbReconcileKind.BodyHashMismatch => "body_hash_mismatch",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown reconcile discrepancy kind.")
    };

    private static ContentKbReconcileKind ParseKind(string kindText) => kindText switch
    {
        "published_orphan" => ContentKbReconcileKind.PublishedOrphan,
        "file_orphan" => ContentKbReconcileKind.FileOrphan,
        "seed_drift" => ContentKbReconcileKind.SeedDrift,
        "body_hash_mismatch" => ContentKbReconcileKind.BodyHashMismatch,
        _ => throw new InvalidOperationException($"Unknown persisted reconcile discrepancy kind '{kindText}'.")
    };

    private sealed class StoredReconcileDiscrepancyRow
    {
        public required string DiscrepancyId { get; set; }
        public required string Kind { get; set; }
        public string? NaturalKeyType { get; set; }
        public string? NaturalKeyValue { get; set; }
        public string? ArtifactPath { get; set; }
        public string? Title { get; set; }
        public required string ScopeTag { get; set; }
        public required DateTimeOffset FirstSeenUtc { get; set; }
        public required DateTimeOffset LastSeenUtc { get; set; }
        public DateTimeOffset? ResolvedUtc { get; set; }
    }

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_kb_reconcile_discrepancy (
          discrepancy_id    TEXT PRIMARY KEY,
          kind              TEXT NOT NULL,
          natural_key_type  TEXT NULL,
          natural_key_value TEXT NULL,
          artifact_path     TEXT NULL,
          title             TEXT NULL,
          scope_tag         TEXT NOT NULL,
          first_seen_utc    TIMESTAMPTZ NOT NULL,
          last_seen_utc     TIMESTAMPTZ NOT NULL,
          resolved_utc      TIMESTAMPTZ NULL
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_kb_reconcile_discrepancy (
          discrepancy_id    TEXT PRIMARY KEY,
          kind              TEXT NOT NULL,
          natural_key_type  TEXT NULL,
          natural_key_value TEXT NULL,
          artifact_path     TEXT NULL,
          title             TEXT NULL,
          scope_tag         TEXT NOT NULL,
          first_seen_utc    TEXT NOT NULL,
          last_seen_utc     TEXT NOT NULL,
          resolved_utc      TEXT NULL
        );
        """;

    // EXCLUDED works on both Postgres and SQLite (feedback_sqlite_postgres_sql_divergence.md).
    // first_seen_utc is deliberately NOT in the DO UPDATE SET list — a re-affirmed discrepancy keeps
    // its original first-seen timestamp; only last_seen_utc refreshes and resolved_utc clears.
    private const string PostgresUpsertSql = """
        INSERT INTO content_kb_reconcile_discrepancy
          (discrepancy_id, kind, natural_key_type, natural_key_value, artifact_path, title, scope_tag, first_seen_utc, last_seen_utc, resolved_utc)
        VALUES
          (@discrepancyId, @kind, @naturalKeyType, @naturalKeyValue, @artifactPath, @title, @scopeTag, @now, @now, NULL)
        ON CONFLICT (discrepancy_id) DO UPDATE SET
          kind              = EXCLUDED.kind,
          natural_key_type  = EXCLUDED.natural_key_type,
          natural_key_value = EXCLUDED.natural_key_value,
          artifact_path     = EXCLUDED.artifact_path,
          title             = EXCLUDED.title,
          scope_tag         = EXCLUDED.scope_tag,
          last_seen_utc     = EXCLUDED.last_seen_utc,
          resolved_utc      = NULL;
        """;

    private const string SqliteUpsertSql = """
        INSERT INTO content_kb_reconcile_discrepancy
          (discrepancy_id, kind, natural_key_type, natural_key_value, artifact_path, title, scope_tag, first_seen_utc, last_seen_utc, resolved_utc)
        VALUES
          (@discrepancyId, @kind, @naturalKeyType, @naturalKeyValue, @artifactPath, @title, @scopeTag, @now, @now, NULL)
        ON CONFLICT (discrepancy_id) DO UPDATE SET
          kind              = excluded.kind,
          natural_key_type  = excluded.natural_key_type,
          natural_key_value = excluded.natural_key_value,
          artifact_path     = excluded.artifact_path,
          title             = excluded.title,
          scope_tag         = excluded.scope_tag,
          last_seen_utc     = excluded.last_seen_utc,
          resolved_utc      = NULL;
        """;

    private const string ResolveAbsentWithSeenSql = """
        UPDATE content_kb_reconcile_discrepancy
           SET resolved_utc = @now
         WHERE scope_tag = @scopeTag
           AND resolved_utc IS NULL
           AND discrepancy_id NOT IN @seenIds;
        """;

    private const string ResolveAllInScopeSql = """
        UPDATE content_kb_reconcile_discrepancy
           SET resolved_utc = @now
         WHERE scope_tag = @scopeTag
           AND resolved_utc IS NULL;
        """;

    private const string GetOpenAllScopesSql = """
        SELECT discrepancy_id    AS DiscrepancyId,
               kind              AS Kind,
               natural_key_type  AS NaturalKeyType,
               natural_key_value AS NaturalKeyValue,
               artifact_path     AS ArtifactPath,
               title             AS Title,
               scope_tag         AS ScopeTag,
               first_seen_utc    AS FirstSeenUtc,
               last_seen_utc     AS LastSeenUtc,
               resolved_utc      AS ResolvedUtc
          FROM content_kb_reconcile_discrepancy
         WHERE resolved_utc IS NULL;
        """;

    private const string GetOpenByScopeSql = """
        SELECT discrepancy_id    AS DiscrepancyId,
               kind              AS Kind,
               natural_key_type  AS NaturalKeyType,
               natural_key_value AS NaturalKeyValue,
               artifact_path     AS ArtifactPath,
               title             AS Title,
               scope_tag         AS ScopeTag,
               first_seen_utc    AS FirstSeenUtc,
               last_seen_utc     AS LastSeenUtc,
               resolved_utc      AS ResolvedUtc
          FROM content_kb_reconcile_discrepancy
         WHERE resolved_utc IS NULL
           AND scope_tag = @scopeTag;
        """;
}
