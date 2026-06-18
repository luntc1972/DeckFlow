using System.Data.Common;
using Dapper;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Models;
using DeckFlow.Web.Security;

namespace DeckFlow.Web.Services;

/// <inheritdoc/>
public sealed class FeedbackStore : IFeedbackStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly FeedbackDialect _feedbackDialect;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;
    private string? _ipSalt;

    /// <summary>
    /// Initializes the feedback store using a SQLite database path.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite feedback database.</param>
    public FeedbackStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath))
    {
    }

    /// <summary>
    /// Initializes the feedback store using a resolved relational database connection.
    /// </summary>
    /// <param name="connectionInfo">Database provider and connection details for feedback persistence.</param>
    public FeedbackStore(RelationalDatabaseConnection connectionInfo)
    {
        _connectionInfo = connectionInfo;
        _feedbackDialect = FeedbackDialect.For(_connectionInfo);
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
    /// Initializes the feedback store from the web host environment configuration.
    /// </summary>
    /// <param name="environment">Web host environment used to resolve feedback database settings.</param>
    public FeedbackStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateFeedbackConnection(environment))
    {
    }

    /// <inheritdoc/>
    public async Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            _feedbackDialect.FeedbackInsertReturningIdSql,
            new
            {
                created = DateTime.UtcNow,
                type = submission.Type.ToString(),
                message = submission.Message,
                email = submission.Email,
                pageUrl = Truncate(context.PageUrl, 500),
                userAgent = Truncate(context.UserAgent, 500),
                ipHash = HashIpInternal(context.Ip),
                appVersion = context.AppVersion,
                status = FeedbackStatus.New.ToString()
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<FeedbackItem?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<FeedbackItem>(new CommandDefinition(
            "SELECT id, created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status FROM feedback WHERE id = @id",
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var parameters = BuildQueryParameters(query.Status, query.Type);
        var where = BuildWhereClause(parameters);
        var sql = $"""
            SELECT id, created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status
            FROM feedback
            {where}
            ORDER BY {_feedbackDialect.FeedbackOrderByClause}
            LIMIT @limit OFFSET @offset
            """;
        parameters.Add("limit", pageSize);
        parameters.Add("offset", (page - 1) * pageSize);

        var results = await connection.QueryAsync<FeedbackItem>(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return results.ToList();
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var parameters = BuildQueryParameters(status, type);
        var where = BuildWhereClause(parameters);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM feedback {where}",
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var map = new Dictionary<FeedbackStatus, int>
        {
            [FeedbackStatus.New] = 0,
            [FeedbackStatus.Read] = 0,
            [FeedbackStatus.Archived] = 0,
        };

        var rows = await connection.QueryAsync<FeedbackStatusCountRow>(new CommandDefinition(
            "SELECT status, COUNT(*) AS count FROM feedback GROUP BY status",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (var row in rows)
        {
            if (Enum.TryParse<FeedbackStatus>(row.Status, out var status))
            {
                map[status] = checked((int)row.Count);
            }
        }

        return map;
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE feedback SET status = @status WHERE id = @id",
            new { status = status.ToString(), id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM feedback WHERE id = @id",
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public string HashIp(string? ip) => HashIpInternal(ip) ?? string.Empty;

    private string? HashIpInternal(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        var salt = _ipSalt ?? throw new InvalidOperationException("Schema not initialized; call EnsureSchemaAsync first.");
        return IpHasher.Hash(ip, salt);
    }

    private static string BuildWhereClause(DynamicParameters parameters)
    {
        var clauses = new List<string>();
        if (parameters.ParameterNames.Contains("status", StringComparer.Ordinal))
        {
            clauses.Add("status = @status");
        }

        if (parameters.ParameterNames.Contains("type", StringComparer.Ordinal))
        {
            clauses.Add("type = @type");
        }

        return clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
    }

    private static DynamicParameters BuildQueryParameters(FeedbackStatus? status, FeedbackType? type)
    {
        var parameters = new DynamicParameters();
        if (status.HasValue)
        {
            parameters.Add("status", status.Value.ToString());
        }

        if (type.HasValue)
        {
            parameters.Add("type", type.Value.ToString());
        }

        return parameters;
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value.Substring(0, max);

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
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

            await using var connection = await OpenConnectionAsync(cancellationToken);
            // Why: schema management is an intentional raw ADO.NET carve-out for this phase.
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS feedback (
                      id           __ID_COLUMN_TYPE__,
                      created_utc  __CREATED_UTC_COLUMN_TYPE__ NOT NULL,
                      type         TEXT    NOT NULL,
                      message      TEXT    NOT NULL,
                      email        TEXT    NULL,
                      page_url     TEXT    NULL,
                      user_agent   TEXT    NULL,
                      ip_hash      TEXT    NULL,
                      app_version  TEXT    NULL,
                      status       TEXT    NOT NULL DEFAULT 'New'
                    );
                    CREATE INDEX IF NOT EXISTS idx_feedback_status_created ON feedback(status, created_utc DESC);
                    CREATE TABLE IF NOT EXISTS feedback_meta (
                      key   TEXT PRIMARY KEY,
                      value TEXT NOT NULL
                    );
                    """;
                create.CommandText = create.CommandText
                    .Replace("__ID_COLUMN_TYPE__", _connectionInfo.Dialect.SurrogateIdColumnType, StringComparison.Ordinal)
                    .Replace("__CREATED_UTC_COLUMN_TYPE__", _feedbackDialect.FeedbackCreatedUtcColumnType, StringComparison.Ordinal);
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            _ipSalt = await IpHasher.ResolveSaltAsync(connection, cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private sealed class FeedbackStatusCountRow
    {
        public string Status { get; init; } = string.Empty;
        public long Count { get; init; }
    }
}
