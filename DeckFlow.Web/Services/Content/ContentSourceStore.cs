using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.Content;

/// <summary>
/// Default implementation of <see cref="IContentSourceStore"/> backed by the local Content KB database.
/// </summary>
public sealed class ContentSourceStore : IContentSourceStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public ContentSourceStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public ContentSourceStore(RelationalDatabaseConnection connectionInfo)
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

    /// <summary>
    /// DI constructor that resolves the always-local Content KB connection.
    /// </summary>
    /// <param name="environment">Web host environment used by the connection factory.</param>
    public ContentSourceStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection(environment)) { }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
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
    public async Task<long> InsertSourceAsync(
        string sourceSlug,
        string displayName,
        string sourceType,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = InsertSourceSql;
        RelationalDatabaseConnection.AddParameter(command, "@sourceSlug", sourceSlug);
        RelationalDatabaseConnection.AddParameter(command, "@displayName", displayName);
        RelationalDatabaseConnection.AddParameter(command, "@sourceType", sourceType);
        RelationalDatabaseConnection.AddParameter(command, "@sourceUrl", sourceUrl);

        var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ContentStoreGeneratedId.Read(id);
    }

    /// <inheritdoc />
    public async Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, source_slug, display_name, source_type, source_url, is_enabled, created_utc
              FROM content_sources
             WHERE id = @id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadSource(reader);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, source_slug, display_name, source_type, source_url, is_enabled, created_utc
              FROM content_sources
             WHERE is_enabled = @isEnabled
             ORDER BY source_slug;
            """;
        RelationalDatabaseConnection.AddParameter(
            command,
            "@isEnabled",
            _connectionInfo.IsPostgres ? (object)true : 1);

        var sources = new List<ContentSource>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sources.Add(ReadSource(reader));
        }

        return sources;
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private static ContentSource ReadSource(DbDataReader reader)
        => new()
        {
            Id = reader.GetInt64(0),
            SourceSlug = reader.GetString(1),
            DisplayName = reader.GetString(2),
            SourceType = reader.GetString(3),
            SourceUrl = reader.GetString(4),
            IsEnabled = ReadBool(reader, 5),
            CreatedUtc = ReadDateTimeOffset(reader, 6)
        };

    private static bool ReadBool(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            bool b => b,
            long l => l != 0,
            int i => i != 0,
            short s => s != 0,
            string str => str == "1" || string.Equals(str, "true", StringComparison.OrdinalIgnoreCase),
            _ => Convert.ToBoolean(raw, CultureInfo.InvariantCulture)
        };
    }

    private static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            DateTimeOffset dto => dto.ToUniversalTime(),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
            string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
            _ => new DateTimeOffset(Convert.ToDateTime(raw, CultureInfo.InvariantCulture), TimeSpan.Zero)
        };
    }

    private const string InsertSourceSql = """
        INSERT INTO content_sources (source_slug, display_name, source_type, source_url)
        VALUES (@sourceSlug, @displayName, @sourceType, @sourceUrl)
        RETURNING id;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_sources (
          id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          source_slug  TEXT NOT NULL,
          display_name TEXT NOT NULL,
          source_type  TEXT NOT NULL CHECK (source_type IN ('youtube_channel','podcast_rss')),
          source_url   TEXT NOT NULL,
          is_enabled   BOOLEAN NOT NULL DEFAULT TRUE,
          created_utc  TIMESTAMPTZ NOT NULL DEFAULT now(),
          UNIQUE (source_url),
          UNIQUE (source_slug)
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_sources (
          id           INTEGER PRIMARY KEY AUTOINCREMENT,
          source_slug  TEXT NOT NULL,
          display_name TEXT NOT NULL,
          source_type  TEXT NOT NULL CHECK (source_type IN ('youtube_channel','podcast_rss')),
          source_url   TEXT NOT NULL,
          is_enabled   INTEGER NOT NULL DEFAULT 1,
          created_utc  TEXT NOT NULL DEFAULT (datetime('now')),
          UNIQUE (source_url),
          UNIQUE (source_slug)
        );
        """;
}
