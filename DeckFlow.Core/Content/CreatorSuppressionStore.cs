using System.Data.Common;
using System.Text.Json;
using Dapper;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

#pragma warning disable CS1591

/// <summary>Relational creator suppression store for SQLite and PostgreSQL.</summary>
public sealed class CreatorSuppressionStore : ICreatorSuppressionStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    public CreatorSuppressionStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
    {
        _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        _ensureSchemaEnabled = ensureSchemaEnabled;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!_ensureSchemaEnabled) return;
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(GetSql(_connectionInfo.Provider).Schema, cancellationToken: cancellationToken)).ConfigureAwait(false);
            _schemaReady = true;
        }
        finally { _schemaGate.Release(); }
    }

    public async Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var sql = GetSql(_connectionInfo.Provider);
        await WriteAsync(slug, aliases, async (connection, transaction) =>
        {
            await connection.ExecuteAsync(new CommandDefinition(sql.Upsert, new { slug, aliases = JsonSerializer.Serialize(Normalize(aliases)), reason, requestedUtc, note }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM creator_suppression WHERE slug = @slug;", new { slug }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await BumpRevisionAsync(connection, transaction, GetSql(_connectionInfo.Provider).Increment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default)
    {
        await WriteAsync(slug, aliases, async (connection, transaction) =>
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition("UPDATE creator_suppression SET aliases = @aliases WHERE slug = @slug;", new { slug, aliases = JsonSerializer.Serialize(Normalize(aliases)) }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (changed == 0) throw new KeyNotFoundException($"Creator suppression '{slug}' was not found.");
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> GetRevisionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("SELECT revision FROM creator_suppression_state WHERE id = 1;", cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT synced_revision FROM creator_suppression_sync_state WHERE id = 1;", cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var revision = await connection.ExecuteScalarAsync<long>(new CommandDefinition("SELECT revision FROM creator_suppression_state WHERE id = 1;", transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition("SELECT slug, aliases, reason, requested_utc AS RequestedUtc, note FROM creator_suppression ORDER BY slug;", transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CreatorSuppressionSnapshot(revision, rows.Select(ToSuppression).ToList());
    }

    public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => ApplySnapshotCoreAsync(snapshot, false, cancellationToken);
    public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, bool failAfterDelete, CancellationToken cancellationToken = default) => ApplySnapshotCoreAsync(snapshot, failAfterDelete, cancellationToken);

    public async Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productionStore);
        return (await GetSyncedRevisionAsync(cancellationToken).ConfigureAwait(false)) != await productionStore.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition("SELECT slug, aliases, reason, requested_utc AS RequestedUtc, note FROM creator_suppression ORDER BY slug;", cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToSuppression).ToList();
    }

    public async Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameOrAlias);
        return new CreatorSuppressionMatcher(await ListAsync(cancellationToken).ConfigureAwait(false)).IsSuppressed(nameOrAlias);
    }

    private async Task WriteAsync(string slug, IReadOnlyList<string> aliases, Func<DbConnection, DbTransaction, Task> write, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ListAliasesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        foreach (var alias in Normalize(aliases))
        {
            if (existing.TryGetValue(NormalizeValue(alias), out var owner) && !string.Equals(owner, slug, StringComparison.OrdinalIgnoreCase)) throw new CreatorAliasConflictException(alias, owner);
        }
        await write(connection, transaction).ConfigureAwait(false);
        await BumpRevisionAsync(connection, transaction, GetSql(_connectionInfo.Provider).Increment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplySnapshotCoreAsync(CreatorSuppressionSnapshot snapshot, bool failAfterDelete, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var slugs = snapshot.Rows.Select(row => row.Slug).ToArray();
        var deleteSql = slugs.Length == 0 ? "DELETE FROM creator_suppression;" : "DELETE FROM creator_suppression WHERE slug NOT IN @slugs;";
        await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { slugs }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (failAfterDelete) throw new InvalidOperationException("Injected snapshot failure.");
        foreach (var row in snapshot.Rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(GetSql(_connectionInfo.Provider).Upsert, new { slug = row.Slug, aliases = JsonSerializer.Serialize(Normalize(row.Aliases)), reason = row.Reason, requestedUtc = row.RequestedUtc, note = row.Note }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        await connection.ExecuteAsync(new CommandDefinition("UPDATE creator_suppression_sync_state SET synced_revision = @revision WHERE id = 1;", new { revision = snapshot.Revision }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ListAliasesAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<Row>(new CommandDefinition("SELECT slug, aliases, reason, requested_utc AS RequestedUtc, note FROM creator_suppression;", transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.SelectMany(row => (JsonSerializer.Deserialize<string[]>(row.Aliases) ?? Array.Empty<string>()).Append(row.Slug).Select(alias => new { Key = NormalizeValue(alias), row.Slug })).ToDictionary(item => item.Key, item => item.Slug, StringComparer.OrdinalIgnoreCase);
    }

    private static CreatorSuppression ToSuppression(Row row) => new()
    {
        Slug = row.Slug,
        Aliases = JsonSerializer.Deserialize<string[]>(row.Aliases) ?? Array.Empty<string>(),
        Reason = row.Reason,
        RequestedUtc = row.RequestedUtc,
        Note = row.Note,
    };

    private static Task BumpRevisionAsync(DbConnection connection, DbTransaction transaction, string sql, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(sql, new { updatedUtc = DateTimeOffset.UtcNow }, transaction, cancellationToken: cancellationToken));

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> aliases) => aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static string NormalizeValue(string value) => CreatorSuppressionMatcher.NormalizeValue(value);

    internal sealed record SqlText(string Schema, string Upsert, string Increment);

    internal static SqlText GetSql(RelationalDatabaseProvider provider)
        => provider switch
        {
            RelationalDatabaseProvider.Sqlite or RelationalDatabaseProvider.Postgres => new SqlText(CreateSql, UpsertSql, IncrementSql),
            _ => throw new NotSupportedException($"Unsupported database provider '{provider}'.")
        };

    private const string CreateSql = """
        CREATE TABLE IF NOT EXISTS creator_suppression (slug TEXT PRIMARY KEY, aliases TEXT NOT NULL, reason TEXT NOT NULL, requested_utc TEXT NOT NULL, note TEXT NULL);
        CREATE TABLE IF NOT EXISTS creator_suppression_state (id INTEGER PRIMARY KEY CHECK (id = 1), revision BIGINT NOT NULL, updated_utc TEXT NOT NULL);
        INSERT INTO creator_suppression_state (id, revision, updated_utc) VALUES (1, 0, CURRENT_TIMESTAMP) ON CONFLICT (id) DO NOTHING;
        CREATE TABLE IF NOT EXISTS creator_suppression_sync_state (id INTEGER PRIMARY KEY CHECK (id = 1), synced_revision BIGINT NULL);
        INSERT INTO creator_suppression_sync_state (id, synced_revision) VALUES (1, NULL) ON CONFLICT (id) DO NOTHING;
        """;
    private const string UpsertSql = "INSERT INTO creator_suppression (slug, aliases, reason, requested_utc, note) VALUES (@slug, @aliases, @reason, @requestedUtc, @note) ON CONFLICT (slug) DO UPDATE SET aliases = EXCLUDED.aliases, reason = EXCLUDED.reason, requested_utc = EXCLUDED.requested_utc, note = EXCLUDED.note;";
    private const string IncrementSql = "UPDATE creator_suppression_state SET revision = revision + 1, updated_utc = @updatedUtc WHERE id = 1;";
    private sealed class Row { public required string Slug { get; init; } public required string Aliases { get; init; } public required string Reason { get; init; } public required DateTimeOffset RequestedUtc { get; init; } public string? Note { get; init; } }
}
#pragma warning restore CS1591
