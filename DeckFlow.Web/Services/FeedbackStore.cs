using System.Data;
using System.Security.Cryptography;
using System.Text;
using DeckFlow.Web.Models;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Web.Services;

public sealed class FeedbackStore : IFeedbackStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;
    private string? _ipSalt;

    public FeedbackStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path required", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={databasePath}";
    }

    public FeedbackStore(IWebHostEnvironment environment)
        : this(ResolveDatabasePath(environment))
    {
    }

    public async Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO feedback (created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status)
            VALUES ($created, $type, $message, $email, $pageUrl, $userAgent, $ipHash, $appVersion, $status);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$type", submission.Type.ToString());
        command.Parameters.AddWithValue("$message", submission.Message);
        command.Parameters.AddWithValue("$email", (object?)submission.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("$pageUrl", (object?)Truncate(context.PageUrl, 500) ?? DBNull.Value);
        command.Parameters.AddWithValue("$userAgent", (object?)Truncate(context.UserAgent, 500) ?? DBNull.Value);
        command.Parameters.AddWithValue("$ipHash", (object?)HashIpInternal(context.Ip) ?? DBNull.Value);
        command.Parameters.AddWithValue("$appVersion", (object?)context.AppVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", FeedbackStatus.New.ToString());

        var idObj = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(idObj);
    }

    public async Task<FeedbackItem?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status FROM feedback WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadItem(reader);
    }

    public async Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var where = BuildWhereClause(query.Status, query.Type, command);
        command.CommandText = $"""
            SELECT id, created_utc, type, message, email, page_url, user_agent, ip_hash, app_version, status
            FROM feedback
            {where}
            ORDER BY datetime(created_utc) DESC, id DESC
            LIMIT $limit OFFSET $offset
            """;
        command.Parameters.AddWithValue("$limit", pageSize);
        command.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

        var results = new List<FeedbackItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadItem(reader));
        }

        return results;
    }

    public async Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var where = BuildWhereClause(status, type, command);
        command.CommandText = $"SELECT COUNT(*) FROM feedback {where}";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status, COUNT(*) FROM feedback GROUP BY status";
        var map = new Dictionary<FeedbackStatus, int>
        {
            [FeedbackStatus.New] = 0,
            [FeedbackStatus.Read] = 0,
            [FeedbackStatus.Archived] = 0,
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (Enum.TryParse<FeedbackStatus>(reader.GetString(0), out var status))
            {
                map[status] = reader.GetInt32(1);
            }
        }

        return map;
    }

    public async Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE feedback SET status = $status WHERE id = $id";
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM feedback WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public string HashIp(string? ip) => HashIpInternal(ip) ?? string.Empty;

    private string? HashIpInternal(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        var salt = _ipSalt ?? throw new InvalidOperationException("Schema not initialized; call EnsureSchemaAsync first.");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip + "|" + salt));
        return Convert.ToHexString(bytes);
    }

    private static string BuildWhereClause(FeedbackStatus? status, FeedbackType? type, SqliteCommand command)
    {
        var clauses = new List<string>();
        if (status.HasValue)
        {
            clauses.Add("status = $status");
            command.Parameters.AddWithValue("$status", status.Value.ToString());
        }

        if (type.HasValue)
        {
            clauses.Add("type = $type");
            command.Parameters.AddWithValue("$type", type.Value.ToString());
        }

        return clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value.Substring(0, max);

    private static FeedbackItem ReadItem(SqliteDataReader reader)
    {
        return new FeedbackItem(
            Id: reader.GetInt64(0),
            CreatedUtc: DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
            Type: Enum.Parse<FeedbackType>(reader.GetString(2)),
            Message: reader.GetString(3),
            Email: reader.IsDBNull(4) ? null : reader.GetString(4),
            PageUrl: reader.IsDBNull(5) ? null : reader.GetString(5),
            UserAgent: reader.IsDBNull(6) ? null : reader.GetString(6),
            IpHash: reader.IsDBNull(7) ? null : reader.GetString(7),
            AppVersion: reader.IsDBNull(8) ? null : reader.GetString(8),
            Status: Enum.Parse<FeedbackStatus>(reader.GetString(9)));
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
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
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS feedback (
                      id           INTEGER PRIMARY KEY AUTOINCREMENT,
                      created_utc  TEXT    NOT NULL,
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
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            _ipSalt = await ResolveSaltAsync(connection, cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private static async Task<string> ResolveSaltAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var envSalt = Environment.GetEnvironmentVariable("FEEDBACK_IP_SALT");
        if (!string.IsNullOrWhiteSpace(envSalt))
        {
            return envSalt;
        }

        await using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT value FROM feedback_meta WHERE key = 'ip_salt'";
            var existing = await select.ExecuteScalarAsync(cancellationToken);
            if (existing is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }

        var bytes = RandomNumberGenerator.GetBytes(32);
        var generated = Convert.ToHexString(bytes);
        await using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO feedback_meta (key, value) VALUES ('ip_salt', $value)";
        insert.Parameters.AddWithValue("$value", generated);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return generated;
    }

    private static string ResolveDatabasePath(IWebHostEnvironment environment)
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var root = !string.IsNullOrWhiteSpace(dataDir)
            ? Path.GetFullPath(dataDir)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "artifacts"));
        return Path.Combine(root, "feedback.db");
    }
}
