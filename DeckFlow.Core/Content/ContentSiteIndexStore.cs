using System.Data.Common;
using System.Globalization;
using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="IContentSiteIndexStore"/> for the only Render-bound
/// Content KB table; it carries no transcript, audio, or spend data.
/// </summary>
public sealed class ContentSiteIndexStore : IContentSiteIndexStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly Func<CancellationToken, Task<DbConnection>>? _connectionFactoryOverride;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed site-index store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public ContentSiteIndexStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a site-index store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">
    /// When <c>true</c> (default) the store auto-creates/backfills its schema on first use. When
    /// <c>false</c> (prod-pointed stores, D-09/D-10) <see cref="EnsureSchemaAsync"/> is a no-op so the
    /// store never issues CREATE/ALTER/DROP — prod schema is owned by the web app's startup path.
    /// </param>
    public ContentSiteIndexStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
        : this(connectionInfo, ensureSchemaEnabled, connectionFactoryOverride: null) { }

    /// <summary>
    /// Test-seam constructor: injects a connection-factory override so tests can wrap the real
    /// connection with a recording double and assert the exact SQL issued (house test-seam pattern;
    /// <see cref="RelationalDatabaseConnection"/> is a sealed record and cannot be subclassed).
    /// The public constructors pass <c>null</c> and behave exactly as production.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">Whether schema auto-ensure runs.</param>
    /// <param name="connectionFactoryOverride">
    /// Optional connection factory used by <see cref="OpenConnectionAsync"/> in place of the live one.
    /// </param>
    internal ContentSiteIndexStore(
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
        // Why: prod-pointed stores (D-09) disable schema-ensure entirely — no CREATE/ALTER/DROP is
        // issued against prod. Placed before the _schemaReady fast-path so the ~20 call sites are untouched.
        if (!_ensureSchemaEnabled) return;
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            // Why: schema creation, ALTER backfills, and schema introspection are intentional raw ADO.NET carve-outs for this phase.
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            var columns = await GetTableColumnsAsync(connection, "content_site_index", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("is_visible"))
            {
                await using var addVisible = connection.CreateCommand();
                addVisible.CommandText = _connectionInfo.IsPostgres
                    ? "ALTER TABLE content_site_index ADD COLUMN is_visible BOOLEAN NOT NULL DEFAULT FALSE;"
                    : "ALTER TABLE content_site_index ADD COLUMN is_visible INTEGER NOT NULL DEFAULT 0;";
                await addVisible.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("is_evergreen"))
            {
                await using var addEvergreen = connection.CreateCommand();
                addEvergreen.CommandText = _connectionInfo.IsPostgres
                    ? "ALTER TABLE content_site_index ADD COLUMN is_evergreen BOOLEAN NOT NULL DEFAULT FALSE;"
                    : "ALTER TABLE content_site_index ADD COLUMN is_evergreen INTEGER NOT NULL DEFAULT 0;";
                await addEvergreen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("is_hidden"))
            {
                await using var addHidden = connection.CreateCommand();
                addHidden.CommandText = _connectionInfo.IsPostgres
                    ? "ALTER TABLE content_site_index ADD COLUMN is_hidden BOOLEAN NOT NULL DEFAULT FALSE;"
                    : "ALTER TABLE content_site_index ADD COLUMN is_hidden INTEGER NOT NULL DEFAULT 0;";
                await addHidden.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("approval_status"))
            {
                await using var addApprovalStatus = connection.CreateCommand();
                addApprovalStatus.CommandText =
                    "ALTER TABLE content_site_index ADD COLUMN approval_status TEXT NOT NULL DEFAULT 'pending';";
                await addApprovalStatus.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("pushed_to_prod_utc"))
            {
                await using var addPushedToProdUtc = connection.CreateCommand();
                addPushedToProdUtc.CommandText = _connectionInfo.IsPostgres
                    ? "ALTER TABLE content_site_index ADD COLUMN pushed_to_prod_utc TIMESTAMPTZ NULL;"
                    : "ALTER TABLE content_site_index ADD COLUMN pushed_to_prod_utc TEXT NULL;";
                await addPushedToProdUtc.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("body_sha256"))
            {
                // Why: TEXT NULL is valid in both dialects — no IsPostgres branch needed (D-09).
                await using var addBodySha256 = connection.CreateCommand();
                addBodySha256.CommandText = "ALTER TABLE content_site_index ADD COLUMN body_sha256 TEXT NULL;";
                await addBodySha256.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Why: grandfather the already-published seed to approved and re-run safely after
            // an ALTER-then-crash; only still-pending visible rows are updated on later passes.
            await using (var grandfatherApprovalStatus = connection.CreateCommand())
            {
                grandfatherApprovalStatus.CommandText = """
                    UPDATE content_site_index
                       SET approval_status = 'approved'
                     WHERE approval_status = 'pending'
                       AND is_visible = @visible;
                    """;
                RelationalDatabaseConnection.AddParameter(grandfatherApprovalStatus, "@visible", true);
                await grandfatherApprovalStatus.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        ValidateRowForUpsert(row);
        var naturalKey = GetNaturalKey(row);
        ValidateArtifactPath(row.ArtifactPath);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var parameters = BuildUpsertParameters(row, naturalKey);
        parameters.Add("isHidden", row.IsHidden);
        parameters.Add("isEvergreen", row.IsEvergreen);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        ValidateRowForUpsert(row);
        var naturalKey = GetNaturalKey(row);
        ValidateArtifactPath(row.ArtifactPath);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var parameters = BuildUpsertParameters(row, naturalKey);
        parameters.Add("isVisible", false);
        parameters.Add("isHidden", false);
        parameters.Add("isEvergreen", false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertPreservingVisibilitySql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        ValidateRowForUpsert(row);
        var naturalKey = GetNaturalKey(row);
        ValidateArtifactPath(row.ArtifactPath);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var parameters = BuildUpsertParameters(row, naturalKey);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertContentColumnsOnlySql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
        string naturalKeyType,
        string naturalKeyValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyValue);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
            SELECT id,
                   source,
                   title,
                   video_url,
                   artifact_path,
                   published_utc,
                   pushed_to_prod_utc,
                   indexed_utc,
                   archetype_tags,
                   bracket_tags,
                   card_category_tags,
                   natural_key_type,
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen,
                   approval_status,
                   body_sha256
              FROM content_site_index
             WHERE natural_key_type = @naturalKeyType
               AND natural_key_value = @naturalKeyValue;
            """,
            new { naturalKeyType, naturalKeyValue },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToContentSiteIndexRow(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
            SELECT id,
                   source,
                   title,
                   video_url,
                   artifact_path,
                   published_utc,
                   pushed_to_prod_utc,
                   indexed_utc,
                   archetype_tags,
                   bracket_tags,
                   card_category_tags,
                   natural_key_type,
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen,
                   approval_status,
                   body_sha256
              FROM content_site_index
             WHERE is_visible = @visible
               AND approval_status = 'approved'
             ORDER BY source, title, id;
            """,
            new { visible = true },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToContentSiteIndexRow).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
            SELECT id,
                   source,
                   title,
                   video_url,
                   artifact_path,
                   published_utc,
                   pushed_to_prod_utc,
                   indexed_utc,
                   archetype_tags,
                   bracket_tags,
                   card_category_tags,
                   natural_key_type,
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen,
                   approval_status,
                   body_sha256
              FROM content_site_index
             WHERE approval_status = 'approved'
             ORDER BY source, title, id;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToContentSiteIndexRow).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
            SELECT id,
                   source,
                   title,
                   video_url,
                   artifact_path,
                   published_utc,
                   pushed_to_prod_utc,
                   indexed_utc,
                   archetype_tags,
                   bracket_tags,
                   card_category_tags,
                   natural_key_type,
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen,
                   approval_status,
                   body_sha256
              FROM content_site_index
             ORDER BY source, title, id;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToContentSiteIndexRow).ToList();
    }

    /// <inheritdoc />
    public async Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
            SELECT id,
                   source,
                   title,
                   video_url,
                   artifact_path,
                   published_utc,
                   pushed_to_prod_utc,
                   indexed_utc,
                   archetype_tags,
                   bracket_tags,
                   card_category_tags,
                   natural_key_type,
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen,
                   approval_status,
                   body_sha256
              FROM content_site_index
             WHERE id = @id;
            """,
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToContentSiteIndexRow(row);
    }

    /// <inheritdoc />
    public async Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Why: defense-in-depth on the public detail route — a drifted visible-but-pending row
        // must never render at /content-kb/{id}; GetByIdAsync stays unfiltered for admin/Studio.
        var row = await connection.QuerySingleOrDefaultAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
            SELECT id,
                   source,
                   title,
                   video_url,
                   artifact_path,
                   published_utc,
                   pushed_to_prod_utc,
                   indexed_utc,
                   archetype_tags,
                   bracket_tags,
                   card_category_tags,
                   natural_key_type,
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen,
                   approval_status,
                   body_sha256
              FROM content_site_index
             WHERE id = @id
               AND is_visible = @visible
               AND approval_status = 'approved';
            """,
            new { id, visible = true },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToContentSiteIndexRow(row);
    }

    /// <inheritdoc />
    public async Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_visible = @visible,
                   is_hidden = FALSE
             WHERE id = @id;
            """,
            new { visible, id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_hidden = @hidden,
                   is_visible = CASE WHEN @hidden THEN FALSE ELSE is_visible END
             WHERE id = @id;
            """,
            new { hidden, id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM content_site_index;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM content_site_index
             WHERE id = @id;
            """,
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets evergreen flag for a single site-index row.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="evergreen">Whether the row should be evergreen.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    public async Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_evergreen = @evergreen
             WHERE id = @id;
            """,
            new { evergreen, id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_visible = @visible,
                   is_hidden = FALSE
             WHERE source = @source;
            """,
            new { visible, source },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_hidden = @hidden,
                   is_visible = CASE WHEN @hidden THEN FALSE ELSE is_visible END
             WHERE source = @source;
            """,
            new { hidden, source },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetApprovalStatusAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyValue);
        ValidateApprovalStatus(status);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET approval_status = @status
             WHERE natural_key_type = @type
               AND natural_key_value = @value;
            """,
            new { status, type = naturalKeyType, value = naturalKeyValue },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetApprovalStatusAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ValidateApprovalStatus(status);
        if (keys.Count == 0)
        {
            return 0;
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        // Why: one transaction = atomic + one logical round-trip (D-06); partial approvals are forbidden.
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            UPDATE content_site_index
               SET approval_status = @status
             WHERE natural_key_type = @type
               AND natural_key_value = @value;
            """;
        var total = 0;
        foreach (var (type, value) in keys)
        {
            total += await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { status, type, value },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    /// <inheritdoc />
    public async Task<int> StampPushedToProdAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        DateTimeOffset pushedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return 0;
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            UPDATE content_site_index
               SET pushed_to_prod_utc = @pushed
             WHERE natural_key_type = @type
               AND natural_key_value = @value;
            """;
        var total = 0;
        foreach (var (type, value) in keys)
        {
            total += await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { pushed = pushedUtc, type, value },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    /// <inheritdoc />
    public async Task UpsertContentColumnsOnlyBatchAsync(
        IReadOnlyList<ContentSiteIndexRow> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Why: validation runs inside the transaction loop (not all up-front) so a bad row
        // aborts AFTER prior rows have been written in the current statement scope, proving
        // true rollback semantics — not just "skip the bad row" (T-qyc-01).
        foreach (var row in rows)
        {
            (string Type, string Value) naturalKey;
            try
            {
                ValidateRowForUpsert(row);
                naturalKey = GetNaturalKey(row);
                ValidateArtifactPath(row.ArtifactPath);

                var parameters = BuildUpsertParameters(row, naturalKey);

                await connection.ExecuteAsync(new CommandDefinition(
                    UpsertContentColumnsOnlySql,
                    parameters,
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Why: propagate cancellation directly — do not wrap in ContentSiteIndexBatchUpsertException
                // as the caller already handles it and the transaction will be cleaned up by Dispose.
                // Roll back with CancellationToken.None: the incoming token is already cancelled here, so
                // passing it would abort the rollback itself; None ensures the rollback actually runs.
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                // Why: only the non-secret row identity is carried in ContentSiteIndexBatchUpsertException;
                // the secret-bearing DB exception stays in InnerException for the log sink,
                // never surfaced to the UI (D-07 / SC5 / T-qyc-02).
                var rowTitle = row?.Title ?? "(unknown)";
                var keyType = "(unknown)";
                var keyValue = "(unknown)";
                try
                {
                    if (row is not null)
                    {
                        var k = GetNaturalKey(row);
                        keyType = k.Type;
                        keyValue = k.Value;
                    }
                }
                catch
                {
                    // Swallow — we're already in the error path; use the placeholders.
                }

                throw new ContentSiteIndexBatchUpsertException(
                    rowTitle,
                    keyType,
                    keyValue,
                    $"Batch upsert aborted at row '{rowTitle}' — entire batch was rolled back.",
                    ex);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetVisibilityAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        bool visible,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return 0;
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        // Why: one transaction = atomic + one logical round-trip (mirrors StampPushedToProdAsync).
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        // is_hidden cleared unconditionally, exactly mirroring the single-row SetVisibilityAsync(long, bool).
        const string sql = """
            UPDATE content_site_index
               SET is_visible = @visible,
                   is_hidden = FALSE
             WHERE natural_key_type = @type
               AND natural_key_value = @value;
            """;
        var total = 0;
        foreach (var (type, value) in keys)
        {
            total += await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { visible, type, value },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    /// <summary>
    /// Shared required-field validation for all three <c>Upsert*Async</c> row variants and the
    /// batch upsert's per-row loop — extracted so the same checks (and their exact exception
    /// types/messages) run identically everywhere a row is upserted.
    /// </summary>
    private static void ValidateRowForUpsert(ContentSiteIndexRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.VideoUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.ArtifactPath);
        ArgumentNullException.ThrowIfNull(row.ArchetypeTags);
        ArgumentNullException.ThrowIfNull(row.BracketTags);
        ArgumentNullException.ThrowIfNull(row.CardCategoryTags);
    }

    /// <summary>
    /// Shared parameter-binding helper behind all three <c>Upsert*Async</c> row variants (row #4 in
    /// 82-REVIEW.md): builds the column set common to every upsert SQL variant — source/title/url/
    /// artifact path/timestamps/serialized tags/natural key — as a <see cref="DynamicParameters"/>
    /// bag. Callers add their own variant-specific visibility/hidden/evergreen columns (or none, for
    /// the content-columns-only variant) before executing.
    /// </summary>
    private static DynamicParameters BuildUpsertParameters(ContentSiteIndexRow row, (string Type, string Value) naturalKey)
    {
        var parameters = new DynamicParameters();
        parameters.Add("source", row.Source);
        parameters.Add("title", row.Title);
        parameters.Add("videoUrl", row.VideoUrl);
        parameters.Add("artifactPath", row.ArtifactPath);
        parameters.Add("publishedUtc", row.PublishedUtc);
        parameters.Add("indexedUtc", row.IndexedUtc);
        parameters.Add("archetypeTags", ContentArtifactSpec.SerializeTags(row.ArchetypeTags));
        parameters.Add("bracketTags", ContentArtifactSpec.SerializeTags(row.BracketTags));
        parameters.Add("cardCategoryTags", ContentArtifactSpec.SerializeTags(row.CardCategoryTags));
        parameters.Add("naturalKeyType", naturalKey.Type);
        parameters.Add("naturalKeyValue", naturalKey.Value);
        // Why: mirror the source row's approval_status (D-01) so the content-columns-only upsert
        // carries approval on insert AND heals a drifted prod row on update; other upsert variants
        // ignore this unbound-to-their-SQL parameter harmlessly.
        parameters.Add("approvalStatus", row.ApprovalStatus);
        // Why: body_sha256 (D-01/D-09) is bound here so all three upsert variants can bind it;
        // variants whose SQL doesn't reference @bodySha256 ignore this parameter harmlessly.
        parameters.Add("bodySha256", row.BodySha256);
        return parameters;
    }

    private static readonly string[] AllowedApprovalStatuses = ["pending", "approved", "rejected"];

    private static void ValidateApprovalStatus(string status)
    {
        if (!AllowedApprovalStatuses.Contains(status, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Invalid approval status '{status}'. Must be one of: pending, approved, rejected.",
                nameof(status));
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => _connectionFactoryOverride is not null
            ? await _connectionFactoryOverride(cancellationToken).ConfigureAwait(false)
            : await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlySet<string>> GetTableColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        // Why: PRAGMA/information_schema schema introspection is an intentional raw ADO.NET carve-out for this phase.
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_connectionInfo.IsSqlite)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(1))
                {
                    columns.Add(reader.GetString(1));
                }
            }

            return columns;
        }

        var pgCommand = connection.CreateCommand();
        pgCommand.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @tableName
            ORDER BY ordinal_position;
            """;
        RelationalDatabaseConnection.AddParameter(pgCommand, "@tableName", tableName);
        await using var pgReader = await pgCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await pgReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!pgReader.IsDBNull(0))
            {
                columns.Add(pgReader.GetString(0));
            }
        }

        return columns;
    }

    private static (string Type, string Value) GetNaturalKey(ContentSiteIndexRow row)
    {
        var hasYoutubeVideoId = !string.IsNullOrWhiteSpace(row.YoutubeVideoId);
        var hasRssGuid = !string.IsNullOrWhiteSpace(row.RssGuid);
        if (hasYoutubeVideoId == hasRssGuid)
        {
            throw new ArgumentException(
                "Exactly one of YoutubeVideoId or RssGuid must be supplied for a content site-index row.",
                nameof(row));
        }

        return hasYoutubeVideoId
            ? (ContentSourceType.Youtube, row.YoutubeVideoId!)
            : (ContentSourceType.Podcast, row.RssGuid!);
    }

    private static void ValidateArtifactPath(string artifactPath)
    {
        // Why: REVIEW #5 requires rejecting traversal or rooted paths before later phases
        // resolve this relative pointer against MTG_DATA_DIR.
        if (Path.IsPathRooted(artifactPath) || IsWindowsRootedPath(artifactPath))
        {
            throw new ArgumentException(
                "Artifact path must be relative.",
                nameof(ContentSiteIndexRow.ArtifactPath));
        }

        var segments = artifactPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Artifact path must not contain '..' path segments.",
                nameof(ContentSiteIndexRow.ArtifactPath));
        }
    }

    private static bool IsWindowsRootedPath(string artifactPath)
        => artifactPath.Length >= 3
            && char.IsLetter(artifactPath[0])
            && artifactPath[1] == ':'
            && (artifactPath[2] == '\\' || artifactPath[2] == '/');

    private static ContentSiteIndexRow ToContentSiteIndexRow(ContentSiteIndexRowData row)
    {
        var naturalKeyType = row.NaturalKeyType;
        var naturalKeyValue = row.NaturalKeyValue;
        var youtubeVideoId = naturalKeyType == ContentSourceType.Youtube ? naturalKeyValue : null;
        var rssGuid = naturalKeyType == ContentSourceType.Podcast ? naturalKeyValue : null;

        if (youtubeVideoId is null && rssGuid is null)
        {
            throw new InvalidOperationException($"Unknown content_site_index.natural_key_type value '{naturalKeyType}'.");
        }

        return new ContentSiteIndexRow
        {
            Id = row.Id,
            Source = row.Source,
            Title = row.Title,
            VideoUrl = row.VideoUrl,
            ArtifactPath = row.ArtifactPath,
            PublishedUtc = row.PublishedUtc,
            PushedToProdUtc = row.PushedToProdUtc,
            IndexedUtc = row.IndexedUtc,
            ArchetypeTags = ContentArtifactSpec.DeserializeTags(row.ArchetypeTags),
            BracketTags = ContentArtifactSpec.DeserializeTags(row.BracketTags),
            CardCategoryTags = ContentArtifactSpec.DeserializeTags(row.CardCategoryTags),
            YoutubeVideoId = youtubeVideoId,
            RssGuid = rssGuid,
            IsVisible = row.IsVisible,
            IsHidden = row.IsHidden,
            IsEvergreen = row.IsEvergreen,
            ApprovalStatus = row.ApprovalStatus,
            BodySha256 = row.BodySha256
        };
    }

    private const string UpsertSql = """
        INSERT INTO content_site_index (
          source,
          title,
          video_url,
          artifact_path,
          published_utc,
          indexed_utc,
          archetype_tags,
          bracket_tags,
          card_category_tags,
          natural_key_type,
          natural_key_value,
          is_hidden,
          is_evergreen,
          body_sha256)
        VALUES (
          @source,
          @title,
          @videoUrl,
          @artifactPath,
          @publishedUtc,
          @indexedUtc,
          @archetypeTags,
          @bracketTags,
          @cardCategoryTags,
          @naturalKeyType,
          @naturalKeyValue,
          @isHidden,
          @isEvergreen,
          @bodySha256)
        ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
          source             = EXCLUDED.source,
          title              = EXCLUDED.title,
          video_url          = EXCLUDED.video_url,
          artifact_path      = EXCLUDED.artifact_path,
          published_utc      = EXCLUDED.published_utc,
          indexed_utc        = EXCLUDED.indexed_utc,
          archetype_tags     = EXCLUDED.archetype_tags,
          bracket_tags       = EXCLUDED.bracket_tags,
          card_category_tags = EXCLUDED.card_category_tags,
          is_hidden          = EXCLUDED.is_hidden,
          is_evergreen       = EXCLUDED.is_evergreen,
          body_sha256        = EXCLUDED.body_sha256;
        """;

    private const string UpsertPreservingVisibilitySql = """
        INSERT INTO content_site_index (
          source,
          title,
          video_url,
          artifact_path,
          published_utc,
          indexed_utc,
          archetype_tags,
          bracket_tags,
          card_category_tags,
          natural_key_type,
          natural_key_value,
          is_visible,
          is_hidden,
          is_evergreen,
          body_sha256)
        VALUES (
          @source,
          @title,
          @videoUrl,
          @artifactPath,
          @publishedUtc,
          @indexedUtc,
          @archetypeTags,
          @bracketTags,
          @cardCategoryTags,
          @naturalKeyType,
          @naturalKeyValue,
          @isVisible,
          @isHidden,
          @isEvergreen,
          @bodySha256)
        ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
          source             = EXCLUDED.source,
          title              = EXCLUDED.title,
          video_url          = EXCLUDED.video_url,
          artifact_path      = EXCLUDED.artifact_path,
          published_utc      = EXCLUDED.published_utc,
          indexed_utc        = EXCLUDED.indexed_utc,
          archetype_tags     = EXCLUDED.archetype_tags,
          bracket_tags       = EXCLUDED.bracket_tags,
          card_category_tags = EXCLUDED.card_category_tags,
          is_visible         = content_site_index.is_visible,
          is_hidden          = content_site_index.is_hidden,
          is_evergreen       = content_site_index.is_evergreen,
          -- body_sha256 is OVERWRITTEN from EXCLUDED (like indexed_utc), NOT preserved (WARNING 1):
          -- a corrected seed hash must propagate on reseed, protecting D-08's one-time backfill intent.
          body_sha256        = EXCLUDED.body_sha256;
        """;

    private const string UpsertContentColumnsOnlySql = """
        INSERT INTO content_site_index (
          source,
          title,
          video_url,
          artifact_path,
          published_utc,
          indexed_utc,
          archetype_tags,
          bracket_tags,
          card_category_tags,
          natural_key_type,
          natural_key_value,
          approval_status,
          body_sha256)
        VALUES (
          @source,
          @title,
          @videoUrl,
          @artifactPath,
          @publishedUtc,
          @indexedUtc,
          @archetypeTags,
          @bracketTags,
          @cardCategoryTags,
          @naturalKeyType,
          @naturalKeyValue,
          @approvalStatus,
          @bodySha256)
        ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
          source             = EXCLUDED.source,
          title              = EXCLUDED.title,
          video_url          = EXCLUDED.video_url,
          artifact_path      = EXCLUDED.artifact_path,
          published_utc      = EXCLUDED.published_utc,
          indexed_utc        = EXCLUDED.indexed_utc,
          archetype_tags     = EXCLUDED.archetype_tags,
          bracket_tags       = EXCLUDED.bracket_tags,
          card_category_tags = EXCLUDED.card_category_tags,
          approval_status    = EXCLUDED.approval_status,
          body_sha256        = EXCLUDED.body_sha256;
        -- approval_status is now mirrored from the source row on insert and update (D-01/D-02);
        -- is_visible, is_hidden, is_evergreen remain operator-owned and are intentionally excluded.
        -- body_sha256 is OVERWRITTEN from EXCLUDED (D-09) so a re-distill's new hash always lands.
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_site_index (
          id                 BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          source             TEXT NOT NULL,
          title              TEXT NOT NULL,
          video_url          TEXT NOT NULL,
          artifact_path      TEXT NOT NULL,
          published_utc      TIMESTAMPTZ NULL,
          pushed_to_prod_utc TIMESTAMPTZ NULL,
          indexed_utc        TIMESTAMPTZ NOT NULL DEFAULT now(),
          archetype_tags     TEXT NOT NULL DEFAULT '[]',
          bracket_tags       TEXT NOT NULL DEFAULT '[]',
          card_category_tags TEXT NOT NULL DEFAULT '[]',
          natural_key_type   TEXT NOT NULL CHECK (natural_key_type IN ('youtube_channel','podcast_rss')),
          natural_key_value  TEXT NOT NULL,
          is_visible         BOOLEAN NOT NULL DEFAULT FALSE,
          is_hidden          BOOLEAN NOT NULL DEFAULT FALSE,
          is_evergreen       BOOLEAN NOT NULL DEFAULT FALSE,
          approval_status    TEXT NOT NULL DEFAULT 'pending',
          body_sha256        TEXT NULL,
          UNIQUE (natural_key_type, natural_key_value)
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_site_index (
          id                 INTEGER PRIMARY KEY AUTOINCREMENT,
          source             TEXT NOT NULL,
          title              TEXT NOT NULL,
          video_url          TEXT NOT NULL,
          artifact_path      TEXT NOT NULL,
          published_utc      TEXT NULL,
          pushed_to_prod_utc TEXT NULL,
          indexed_utc        TEXT NOT NULL DEFAULT (datetime('now')),
          archetype_tags     TEXT NOT NULL DEFAULT '[]',
          bracket_tags       TEXT NOT NULL DEFAULT '[]',
          card_category_tags TEXT NOT NULL DEFAULT '[]',
          natural_key_type   TEXT NOT NULL CHECK (natural_key_type IN ('youtube_channel','podcast_rss')),
          natural_key_value  TEXT NOT NULL,
          is_visible         INTEGER NOT NULL DEFAULT 0,
          is_hidden          INTEGER NOT NULL DEFAULT 0,
          is_evergreen       INTEGER NOT NULL DEFAULT 0,
          approval_status    TEXT NOT NULL DEFAULT 'pending',
          body_sha256        TEXT NULL,
          UNIQUE (natural_key_type, natural_key_value)
        );
        """;

    private sealed class ContentSiteIndexRowData
    {
        public long Id { get; init; }
        public required string Source { get; init; }
        public required string Title { get; init; }
        public required string VideoUrl { get; init; }
        public required string ArtifactPath { get; init; }
        public DateTimeOffset? PublishedUtc { get; init; }
        public DateTimeOffset? PushedToProdUtc { get; init; }
        public DateTimeOffset IndexedUtc { get; init; }
        public required string ArchetypeTags { get; init; }
        public required string BracketTags { get; init; }
        public required string CardCategoryTags { get; init; }
        public required string NaturalKeyType { get; init; }
        public required string NaturalKeyValue { get; init; }
        public bool IsVisible { get; init; }
        public bool IsHidden { get; init; }
        public bool IsEvergreen { get; init; }
        public required string ApprovalStatus { get; init; }
        public string? BodySha256 { get; init; }
    }
}
