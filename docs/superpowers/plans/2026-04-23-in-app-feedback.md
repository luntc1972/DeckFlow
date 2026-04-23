# In-App Feedback Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a public `/Feedback` form (Bug / Suggestion / Comment) that persists submissions to SQLite and an admin `/Admin/Feedback` page (HTTP Basic Auth) to read, mark read, archive, and delete them.

**Architecture:** New vertical slice in `DeckFlow.Web`. Public `FeedbackController` renders a themed form, validates, checks a honeypot, enforces a per-IP rate limit, and writes to a new `FeedbackStore` (SQLite under `MTG_DATA_DIR`). Admin `AdminFeedbackController` sits behind a minimal `BasicAuthMiddleware` scoped to `/Admin/*` that reads credentials from env vars and fails closed if they are not set. Footer link site-wide plus a card on the existing Help page provide the entry points.

**Tech Stack:** ASP.NET Core MVC (net10.0), `Microsoft.Data.Sqlite` 10.0.0 (already referenced), .NET built-in `AddRateLimiter`, xUnit. No TypeScript or JS changes.

**Spec:** `docs/superpowers/specs/2026-04-23-in-app-feedback-design.md`

---

## File Structure

Created:
- `DeckFlow.Web/Models/FeedbackType.cs` — enum (`Bug`, `Suggestion`, `Comment`).
- `DeckFlow.Web/Models/FeedbackStatus.cs` — enum (`New`, `Read`, `Archived`).
- `DeckFlow.Web/Models/FeedbackSubmission.cs` — form binding model with validation attributes and honeypot.
- `DeckFlow.Web/Models/FeedbackItem.cs` — persistence / read model.
- `DeckFlow.Web/Models/FeedbackListQuery.cs` — admin filter/pagination parameters.
- `DeckFlow.Web/Services/IFeedbackStore.cs` — feedback persistence interface.
- `DeckFlow.Web/Services/FeedbackStore.cs` — sealed SQLite-backed implementation with IP hashing and lazy schema init.
- `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` — scoped admin auth middleware.
- `DeckFlow.Web/Controllers/FeedbackController.cs` — public GET/POST `/Feedback`.
- `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs` — protected list + detail + actions.
- `DeckFlow.Web/Views/Feedback/Index.cshtml` — form + TempData success banner.
- `DeckFlow.Web/Views/Admin/Feedback/Index.cshtml` — filter bar, table, pagination.
- `DeckFlow.Web/Views/Admin/Feedback/Detail.cshtml` — single-item view with action forms.
- `DeckFlow.Web.Tests/FeedbackStoreTests.cs`
- `DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs`
- `DeckFlow.Web.Tests/FeedbackControllerTests.cs`
- `DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs`

Modified:
- `DeckFlow.Web/Program.cs` — register `IFeedbackStore` singleton, add rate limiter policy, insert `BasicAuthMiddleware` via `UseWhen` on `/Admin`.
- `DeckFlow.Web/Views/Shared/_Layout.cshtml` — add `Feedback` footer link alongside existing footer links.
- `DeckFlow.Web/Views/Help/Index.cshtml` — add "Found a bug or have an idea?" card linking to `/Feedback`.
- `DeckFlow.Web/wwwroot/css/site-common.css` — styles for feedback form, admin table, honeypot hide, success banner.
- `README.md` — document feedback link, admin URL, and new env vars.

---

## Task 1: Add Feedback enums and models

**Files:**
- Create: `DeckFlow.Web/Models/FeedbackType.cs`
- Create: `DeckFlow.Web/Models/FeedbackStatus.cs`
- Create: `DeckFlow.Web/Models/FeedbackSubmission.cs`
- Create: `DeckFlow.Web/Models/FeedbackItem.cs`
- Create: `DeckFlow.Web/Models/FeedbackListQuery.cs`

- [ ] **Step 1: Write `FeedbackType.cs`**

```csharp
namespace DeckFlow.Web.Models;

public enum FeedbackType
{
    Bug = 0,
    Suggestion = 1,
    Comment = 2
}
```

- [ ] **Step 2: Write `FeedbackStatus.cs`**

```csharp
namespace DeckFlow.Web.Models;

public enum FeedbackStatus
{
    New = 0,
    Read = 1,
    Archived = 2
}
```

- [ ] **Step 3: Write `FeedbackSubmission.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models;

public sealed class FeedbackSubmission
{
    [Required]
    public FeedbackType Type { get; set; } = FeedbackType.Comment;

    [Required]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Message must be 10–4000 characters.")]
    public string Message { get; set; } = string.Empty;

    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    // Honeypot field. Must remain empty. Never surface to users.
    public string? Website { get; set; }
}
```

- [ ] **Step 4: Write `FeedbackItem.cs`**

```csharp
namespace DeckFlow.Web.Models;

public sealed record FeedbackItem(
    long Id,
    DateTime CreatedUtc,
    FeedbackType Type,
    string Message,
    string? Email,
    string? PageUrl,
    string? UserAgent,
    string? IpHash,
    string? AppVersion,
    FeedbackStatus Status);
```

- [ ] **Step 5: Write `FeedbackListQuery.cs`**

```csharp
namespace DeckFlow.Web.Models;

public sealed class FeedbackListQuery
{
    public FeedbackStatus? Status { get; set; } = FeedbackStatus.New;
    public FeedbackType? Type { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
```

- [ ] **Step 6: Build**

Run: `dotnet build DeckFlow.Web/DeckFlow.Web.csproj`
Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add DeckFlow.Web/Models/FeedbackType.cs DeckFlow.Web/Models/FeedbackStatus.cs DeckFlow.Web/Models/FeedbackSubmission.cs DeckFlow.Web/Models/FeedbackItem.cs DeckFlow.Web/Models/FeedbackListQuery.cs
git commit -m "Add feedback models and enums"
```

---

## Task 2: Write FeedbackStore with SQLite (TDD)

**Files:**
- Create: `DeckFlow.Web/Services/IFeedbackStore.cs`
- Create: `DeckFlow.Web/Services/FeedbackStore.cs`
- Create: `DeckFlow.Web.Tests/FeedbackStoreTests.cs`

- [ ] **Step 1: Write `IFeedbackStore.cs`**

```csharp
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

public interface IFeedbackStore
{
    Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default);
    Task<FeedbackItem?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken cancellationToken = default);
    Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    string HashIp(string? ip);
}

public sealed record FeedbackRequestContext(
    string? Ip,
    string? UserAgent,
    string? PageUrl,
    string? AppVersion);
```

- [ ] **Step 2: Write initial failing test `FeedbackStoreTests.cs`**

```csharp
using System.IO;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class FeedbackStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FeedbackStore _store;

    public FeedbackStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"feedback-test-{Guid.NewGuid():N}.db");
        _store = new FeedbackStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private static FeedbackSubmission SampleSubmission(FeedbackType type = FeedbackType.Bug, string message = "Something is broken please help.") =>
        new() { Type = type, Message = message, Email = "user@example.com" };

    private static FeedbackRequestContext SampleContext(string ip = "203.0.113.7") =>
        new(ip, "UA/1.0", "https://decksync.test/deck/1", "1.2.3");

    [Fact]
    public async Task AddAsync_PersistsItem_AndReturnsNewId()
    {
        var id = await _store.AddAsync(SampleSubmission(), SampleContext());

        Assert.True(id > 0);
        var item = await _store.GetAsync(id);
        Assert.NotNull(item);
        Assert.Equal(FeedbackType.Bug, item!.Type);
        Assert.Equal("Something is broken please help.", item.Message);
        Assert.Equal("user@example.com", item.Email);
        Assert.Equal("UA/1.0", item.UserAgent);
        Assert.Equal("https://decksync.test/deck/1", item.PageUrl);
        Assert.Equal("1.2.3", item.AppVersion);
        Assert.Equal(FeedbackStatus.New, item.Status);
        Assert.False(string.IsNullOrWhiteSpace(item.IpHash));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~FeedbackStoreTests`
Expected: build failure (FeedbackStore does not exist yet).

- [ ] **Step 4: Write minimal `FeedbackStore.cs` to compile + pass the first test**

```csharp
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
            if (_schemaReady) return;

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
```

- [ ] **Step 5: Run test to verify the first test passes**

Run: `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~FeedbackStoreTests.AddAsync_PersistsItem`
Expected: PASS.

- [ ] **Step 6: Add remaining tests to `FeedbackStoreTests.cs`**

Append inside the `FeedbackStoreTests` class:

```csharp
[Fact]
public async Task GetAsync_UnknownId_ReturnsNull()
{
    var item = await _store.GetAsync(9999);
    Assert.Null(item);
}

[Fact]
public async Task ListAsync_FiltersByStatus()
{
    var id1 = await _store.AddAsync(SampleSubmission(FeedbackType.Bug, "First message, ten plus chars."), SampleContext());
    var id2 = await _store.AddAsync(SampleSubmission(FeedbackType.Suggestion, "Second message, ten plus chars."), SampleContext());
    await _store.UpdateStatusAsync(id2, FeedbackStatus.Read);

    var newItems = await _store.ListAsync(new FeedbackListQuery { Status = FeedbackStatus.New });
    Assert.Single(newItems);
    Assert.Equal(id1, newItems[0].Id);

    var readItems = await _store.ListAsync(new FeedbackListQuery { Status = FeedbackStatus.Read });
    Assert.Single(readItems);
    Assert.Equal(id2, readItems[0].Id);
}

[Fact]
public async Task ListAsync_FiltersByType()
{
    await _store.AddAsync(SampleSubmission(FeedbackType.Bug, "Bug report message text."), SampleContext());
    await _store.AddAsync(SampleSubmission(FeedbackType.Suggestion, "Suggestion idea message here."), SampleContext());

    var bugs = await _store.ListAsync(new FeedbackListQuery { Status = null, Type = FeedbackType.Bug });
    Assert.Single(bugs);
    Assert.Equal(FeedbackType.Bug, bugs[0].Type);
}

[Fact]
public async Task ListAsync_OrdersByCreatedDesc()
{
    var first = await _store.AddAsync(SampleSubmission(FeedbackType.Comment, "Older message text here."), SampleContext());
    await Task.Delay(50);
    var second = await _store.AddAsync(SampleSubmission(FeedbackType.Comment, "Newer message text here."), SampleContext());

    var items = await _store.ListAsync(new FeedbackListQuery { Status = null });
    Assert.Equal(second, items[0].Id);
    Assert.Equal(first, items[1].Id);
}

[Fact]
public async Task ListAsync_Pagination_ReturnsRequestedSlice()
{
    for (int i = 0; i < 5; i++)
    {
        await _store.AddAsync(SampleSubmission(FeedbackType.Comment, $"Message number {i} of five."), SampleContext());
    }

    var page1 = await _store.ListAsync(new FeedbackListQuery { Status = null, Page = 1, PageSize = 2 });
    var page2 = await _store.ListAsync(new FeedbackListQuery { Status = null, Page = 2, PageSize = 2 });
    var page3 = await _store.ListAsync(new FeedbackListQuery { Status = null, Page = 3, PageSize = 2 });

    Assert.Equal(2, page1.Count);
    Assert.Equal(2, page2.Count);
    Assert.Single(page3);
}

[Fact]
public async Task UpdateStatusAsync_TransitionsStatus()
{
    var id = await _store.AddAsync(SampleSubmission(), SampleContext());
    await _store.UpdateStatusAsync(id, FeedbackStatus.Archived);
    var item = await _store.GetAsync(id);
    Assert.Equal(FeedbackStatus.Archived, item!.Status);
}

[Fact]
public async Task DeleteAsync_RemovesItem()
{
    var id = await _store.AddAsync(SampleSubmission(), SampleContext());
    await _store.DeleteAsync(id);
    Assert.Null(await _store.GetAsync(id));
}

[Fact]
public async Task CountsByStatusAsync_ReturnsCorrectTotals()
{
    var a = await _store.AddAsync(SampleSubmission(), SampleContext());
    var b = await _store.AddAsync(SampleSubmission(), SampleContext());
    var c = await _store.AddAsync(SampleSubmission(), SampleContext());
    await _store.UpdateStatusAsync(b, FeedbackStatus.Read);
    await _store.UpdateStatusAsync(c, FeedbackStatus.Archived);

    var counts = await _store.CountsByStatusAsync();
    Assert.Equal(1, counts[FeedbackStatus.New]);
    Assert.Equal(1, counts[FeedbackStatus.Read]);
    Assert.Equal(1, counts[FeedbackStatus.Archived]);
}

[Fact]
public async Task HashIp_SameInput_ProducesSameHash()
{
    // Warm schema + salt
    _ = await _store.AddAsync(SampleSubmission(), SampleContext("10.0.0.1"));

    var h1 = _store.HashIp("198.51.100.9");
    var h2 = _store.HashIp("198.51.100.9");
    var h3 = _store.HashIp("198.51.100.10");

    Assert.Equal(h1, h2);
    Assert.NotEqual(h1, h3);
}

[Fact]
public async Task HashIp_DifferentSalts_ProduceDifferentHashes()
{
    _ = await _store.AddAsync(SampleSubmission(), SampleContext("10.0.0.1"));
    var firstHash = _store.HashIp("203.0.113.50");

    var otherDb = Path.Combine(Path.GetTempPath(), $"feedback-test-{Guid.NewGuid():N}.db");
    try
    {
        var other = new FeedbackStore(otherDb);
        _ = await other.AddAsync(SampleSubmission(), SampleContext("10.0.0.1"));
        var otherHash = other.HashIp("203.0.113.50");
        Assert.NotEqual(firstHash, otherHash);
    }
    finally
    {
        if (File.Exists(otherDb)) File.Delete(otherDb);
    }
}
```

- [ ] **Step 7: Run full FeedbackStoreTests**

Run: `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~FeedbackStoreTests`
Expected: all 10 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add DeckFlow.Web/Services/IFeedbackStore.cs DeckFlow.Web/Services/FeedbackStore.cs DeckFlow.Web.Tests/FeedbackStoreTests.cs
git commit -m "Add FeedbackStore with SQLite persistence and tests"
```

---

## Task 3: Write BasicAuthMiddleware (TDD)

**Files:**
- Create: `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs`
- Create: `DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs`

- [ ] **Step 1: Write failing tests `BasicAuthMiddlewareTests.cs`**

```csharp
using System.Text;
using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class BasicAuthMiddlewareTests
{
    private const string EnvUser = "FEEDBACK_ADMIN_USER";
    private const string EnvPass = "FEEDBACK_ADMIN_PASSWORD";

    [Fact]
    public async Task EnvVarsMissing_Returns503()
    {
        using var _ = EnvScope.Clear(EnvUser, EnvPass);
        var context = new DefaultHttpContext();
        var nextCalled = false;

        var middleware = new BasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, "DeckFlow Admin");
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task NoAuthHeader_Returns401_WithChallenge()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, "DeckFlow Admin");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("Basic", context.Response.Headers["WWW-Authenticate"].ToString());
        Assert.Contains("realm=\"DeckFlow Admin\"", context.Response.Headers["WWW-Authenticate"].ToString());
    }

    [Fact]
    public async Task MalformedHeader_Returns401()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "NotBasic xxx";
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, "DeckFlow Admin");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task WrongCredentials_Returns401()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));
        context.Request.Headers["Authorization"] = $"Basic {encoded}";
        var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, "DeckFlow Admin");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task CorrectCredentials_InvokesNext()
    {
        using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
        var context = new DefaultHttpContext();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));
        context.Request.Headers["Authorization"] = $"Basic {encoded}";
        var nextCalled = false;
        var middleware = new BasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, "DeckFlow Admin");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();

        private EnvScope(params string[] keys)
        {
            foreach (var key in keys)
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
            }
        }

        public static EnvScope Clear(params string[] keys)
        {
            var scope = new EnvScope(keys);
            foreach (var key in keys) Environment.SetEnvironmentVariable(key, null);
            return scope;
        }

        public static EnvScope Set(string k1, string v1, string k2, string v2)
        {
            var scope = new EnvScope(k1, k2);
            Environment.SetEnvironmentVariable(k1, v1);
            Environment.SetEnvironmentVariable(k2, v2);
            return scope;
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
```

- [ ] **Step 2: Run to verify build fails (middleware missing)**

Run: `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~BasicAuthMiddlewareTests`
Expected: build failure.

- [ ] **Step 3: Write `BasicAuthMiddleware.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace DeckFlow.Web.Infrastructure;

public sealed class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _realm;

    public BasicAuthMiddleware(RequestDelegate next, string realm)
    {
        _next = next;
        _realm = realm;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_USER");
        var password = Environment.GetEnvironmentVariable("FEEDBACK_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Admin not configured.");
            return;
        }

        var header = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(context);
            return;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring(6).Trim()));
        }
        catch (FormatException)
        {
            Challenge(context);
            return;
        }

        var separator = decoded.IndexOf(':');
        if (separator <= 0)
        {
            Challenge(context);
            return;
        }

        var suppliedUser = decoded.Substring(0, separator);
        var suppliedPass = decoded.Substring(separator + 1);

        if (!FixedTimeEquals(suppliedUser, user) || !FixedTimeEquals(suppliedPass, password))
        {
            Challenge(context);
            return;
        }

        await _next(context);
    }

    private void Challenge(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{_realm}\", charset=\"UTF-8\"";
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length)
        {
            // Still do a constant-time compare against a copy of ba to keep timing predictable.
            var sink = new byte[ba.Length];
            CryptographicOperations.FixedTimeEquals(ba, sink);
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~BasicAuthMiddlewareTests`
Expected: all 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs
git commit -m "Add BasicAuthMiddleware for admin routes"
```

---

## Task 4: Build public FeedbackController (TDD)

**Files:**
- Create: `DeckFlow.Web/Controllers/FeedbackController.cs`
- Create: `DeckFlow.Web.Tests/FeedbackControllerTests.cs`

- [ ] **Step 1: Write failing tests `FeedbackControllerTests.cs`**

```csharp
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class FeedbackControllerTests
{
    [Fact]
    public void Index_Get_ReturnsView()
    {
        var controller = BuildController(out _);
        var result = controller.Index();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Index_Post_HoneypotFilled_RedirectsSuccess_WithoutStoring()
    {
        var controller = BuildController(out var store);
        var submission = new FeedbackSubmission
        {
            Type = FeedbackType.Bug,
            Message = "Serious problem here please read.",
            Website = "http://bot.example",
        };

        var result = await controller.Index(submission, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(0, store.AddCalls);
        Assert.True(controller.TempData.ContainsKey("FeedbackSuccess"));
    }

    [Fact]
    public async Task Index_Post_InvalidModel_ReturnsView_WithoutStoring()
    {
        var controller = BuildController(out var store);
        controller.ModelState.AddModelError("Message", "too short");
        var submission = new FeedbackSubmission { Type = FeedbackType.Bug, Message = "short" };

        var result = await controller.Index(submission, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task Index_Post_Valid_CallsStore_AndSetsTempData()
    {
        var controller = BuildController(out var store);
        controller.HttpContext.Request.Headers["Referer"] = "https://decksync.test/deck/99";
        controller.HttpContext.Request.Headers["User-Agent"] = "MyAgent/1.0";
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.33");

        var submission = new FeedbackSubmission
        {
            Type = FeedbackType.Suggestion,
            Message = "A real suggestion with enough chars.",
            Email = "u@example.com",
        };

        var result = await controller.Index(submission, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, store.AddCalls);
        Assert.Equal(FeedbackType.Suggestion, store.LastSubmission!.Type);
        Assert.Equal("https://decksync.test/deck/99", store.LastContext!.PageUrl);
        Assert.Equal("MyAgent/1.0", store.LastContext!.UserAgent);
        Assert.Equal("192.0.2.33", store.LastContext!.Ip);
        Assert.Equal("test-version", store.LastContext!.AppVersion);
        Assert.True(controller.TempData.ContainsKey("FeedbackSuccess"));
    }

    private static FeedbackController BuildController(out FakeFeedbackStore store)
    {
        store = new FakeFeedbackStore();
        var controller = new FeedbackController(store, new FakeVersionService("test-version"))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
        };
        return controller;
    }

    private sealed class FakeFeedbackStore : IFeedbackStore
    {
        public int AddCalls { get; private set; }
        public FeedbackSubmission? LastSubmission { get; private set; }
        public FeedbackRequestContext? LastContext { get; private set; }

        public Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            LastSubmission = submission;
            LastContext = context;
            return Task.FromResult(42L);
        }

        public Task<FeedbackItem?> GetAsync(long id, CancellationToken ct = default) => Task.FromResult<FeedbackItem?>(null);
        public Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FeedbackItem>>(Array.Empty<FeedbackItem>());
        public Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<FeedbackStatus, int>>(new Dictionary<FeedbackStatus, int>());
        public Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(long id, CancellationToken ct = default) => Task.CompletedTask;
        public string HashIp(string? ip) => ip ?? string.Empty;
    }

    private sealed class FakeVersionService : DeckFlow.Web.Services.IVersionService
    {
        private readonly string _version;
        public FakeVersionService(string v) { _version = v; }
        public string Version => _version;
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
```

- [ ] **Step 2: Verify `IVersionService.Version` member name**

Run: `grep -n "interface IVersionService" -A 10 DeckFlow.Web/Services/IVersionService.cs`
If the interface member is not named `Version`, update `FakeVersionService` and `FeedbackController` in the next step to match the actual member (most likely `Version` or `GetVersion()`).

- [ ] **Step 3: Write `FeedbackController.cs`**

```csharp
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeckFlow.Web.Controllers;

public sealed class FeedbackController : Controller
{
    private readonly IFeedbackStore _store;
    private readonly IVersionService _versionService;

    public FeedbackController(IFeedbackStore store, IVersionService versionService)
    {
        _store = store;
        _versionService = versionService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new FeedbackSubmission());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("feedback-submit")]
    public async Task<IActionResult> Index(FeedbackSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (!string.IsNullOrEmpty(submission.Website))
        {
            // Honeypot tripped — pretend success.
            TempData["FeedbackSuccess"] = true;
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View(submission);
        }

        var context = new FeedbackRequestContext(
            Ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: Request.Headers.UserAgent.ToString(),
            PageUrl: Request.Headers.Referer.ToString(),
            AppVersion: _versionService.Version);

        await _store.AddAsync(submission, context, cancellationToken);

        TempData["FeedbackSuccess"] = true;
        return RedirectToAction(nameof(Index));
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~FeedbackControllerTests`
Expected: all 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Controllers/FeedbackController.cs DeckFlow.Web.Tests/FeedbackControllerTests.cs
git commit -m "Add FeedbackController with honeypot and validation"
```

---

## Task 5: Add Feedback form view

**Files:**
- Create: `DeckFlow.Web/Views/Feedback/Index.cshtml`
- Modify: `DeckFlow.Web/wwwroot/css/site-common.css`

- [ ] **Step 1: Write `Views/Feedback/Index.cshtml`**

```html
@model DeckFlow.Web.Models.FeedbackSubmission
@{
    ViewData["Title"] = "Feedback";
    var success = TempData["FeedbackSuccess"] as bool? ?? false;
}

<section class="feedback-page">
    <div class="feedback-panel" style="background: var(--panel); border: 1px solid var(--line);">
        <h1>Send feedback</h1>
        <p>Report a bug, suggest a feature, or just share a thought. Email is optional.</p>

        @if (success)
        {
            <div class="feedback-banner feedback-banner--success" role="status">
                Thanks — your feedback was received.
            </div>
        }

        <form method="post" asp-action="Index" asp-antiforgery="true" class="feedback-form" novalidate>
            <div class="feedback-field">
                <label asp-for="Type">Type</label>
                <select asp-for="Type" asp-items="Html.GetEnumSelectList<DeckFlow.Web.Models.FeedbackType>()"></select>
                <span asp-validation-for="Type" class="feedback-error"></span>
            </div>

            <div class="feedback-field">
                <label asp-for="Message">Message</label>
                <textarea asp-for="Message" rows="8" maxlength="4000" minlength="10" required></textarea>
                <span asp-validation-for="Message" class="feedback-error"></span>
            </div>

            <div class="feedback-field">
                <label asp-for="Email">Email (optional)</label>
                <input asp-for="Email" type="email" maxlength="200" autocomplete="email" />
                <span asp-validation-for="Email" class="feedback-error"></span>
            </div>

            <div class="feedback-honeypot" aria-hidden="true">
                <label for="Website">Website</label>
                <input type="text" name="Website" id="Website" tabindex="-1" autocomplete="off" />
            </div>

            <button type="submit" class="feedback-submit">Send</button>
        </form>
    </div>
</section>
```

- [ ] **Step 2: Add styles to `site-common.css`**

Append:

```css
/* Feedback page */
.feedback-page { max-width: 720px; margin: 2rem auto; padding: 0 1rem; }
.feedback-panel { padding: 1.5rem; border-radius: 8px; }
.feedback-form { display: flex; flex-direction: column; gap: 1rem; margin-top: 1rem; }
.feedback-field { display: flex; flex-direction: column; gap: 0.25rem; }
.feedback-field label { font-weight: 600; }
.feedback-field input, .feedback-field select, .feedback-field textarea {
    background: var(--panel);
    color: var(--ink, inherit);
    border: 1px solid var(--line);
    border-radius: 4px;
    padding: 0.5rem;
    font: inherit;
}
.feedback-submit {
    align-self: flex-start;
    background: var(--accent-strong, #3a7);
    color: #fff;
    border: none;
    padding: 0.6rem 1.1rem;
    border-radius: 4px;
    cursor: pointer;
    font-weight: 600;
}
.feedback-submit:hover { filter: brightness(1.1); }
.feedback-error { color: var(--accent-strong, #c55); font-size: 0.875rem; }
.feedback-banner { padding: 0.75rem 1rem; border-radius: 4px; margin-bottom: 1rem; }
.feedback-banner--success { background: rgba(76, 175, 80, 0.15); border: 1px solid rgba(76, 175, 80, 0.4); }
.feedback-honeypot { position: absolute; left: -9999px; width: 1px; height: 1px; overflow: hidden; }
```

- [ ] **Step 3: Build**

Run: `dotnet build DeckFlow.Web/DeckFlow.Web.csproj`
Expected: succeeds.

- [ ] **Step 4: Manual smoke test**

Run: `dotnet run --project DeckFlow.Web`, open `http://localhost:5000/Feedback`, submit a valid entry, confirm success banner appears after redirect.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Views/Feedback/Index.cshtml DeckFlow.Web/wwwroot/css/site-common.css
git commit -m "Add feedback form view and styles"
```

---

## Task 6: Register FeedbackStore and rate limiter in Program.cs

**Files:**
- Modify: `DeckFlow.Web/Program.cs`

- [ ] **Step 1: Add using directives at the top of `Program.cs`**

Add (after existing usings):

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DeckFlow.Web.Infrastructure;
```

- [ ] **Step 2: Register `IFeedbackStore` as singleton**

Find the line `builder.Services.AddSingleton<IVersionService, VersionService>();` and add on the next line:

```csharp
builder.Services.AddSingleton<IFeedbackStore, FeedbackStore>();
```

- [ ] **Step 3: Register the rate limiter policy**

Before `builder.Services.AddEndpointsApiExplorer();` add:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("feedback-submit", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

- [ ] **Step 4: Activate the rate limiter middleware**

Find the `var app = builder.Build();` line. After it, locate the pipeline area (before `app.UseRouting()` if present, or before `app.MapControllerRoute(...)`). Add:

```csharp
app.UseRateLimiter();
```

If `app.UseRouting()` exists, place `app.UseRateLimiter()` after `UseRouting()` and before `UseEndpoints`/`MapControllerRoute`.

- [ ] **Step 5: Wire BasicAuthMiddleware to /Admin**

Immediately after `app.UseRateLimiter();` add:

```csharp
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/Admin"),
    branch => branch.UseMiddleware<BasicAuthMiddleware>("DeckFlow Admin"));
```

- [ ] **Step 6: Build + run**

Run: `dotnet build DeckFlow.Web/DeckFlow.Web.csproj`
Expected: succeeds.

- [ ] **Step 7: Commit**

```bash
git add DeckFlow.Web/Program.cs
git commit -m "Wire FeedbackStore, rate limiter, and admin middleware"
```

---

## Task 7: Build AdminFeedbackController list + detail (TDD)

**Files:**
- Create: `DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs`
- Create: `DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs`

- [ ] **Step 1: Write failing tests `AdminFeedbackControllerTests.cs`**

```csharp
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class AdminFeedbackControllerTests
{
    [Fact]
    public async Task Index_RendersItemsFromStore_WithFilters()
    {
        var store = new FakeStore();
        store.Items.Add(NewItem(1, FeedbackStatus.New, FeedbackType.Bug));
        var controller = Build(store);

        var result = await controller.Index(FeedbackStatus.New, null, 1);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<AdminFeedbackListViewModel>(view.Model);
        Assert.Single(vm.Items);
        Assert.Equal(FeedbackStatus.New, vm.StatusFilter);
    }

    [Fact]
    public async Task Detail_UnknownId_ReturnsNotFound()
    {
        var controller = Build(new FakeStore());
        var result = await controller.Detail(999);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_Known_ReturnsView()
    {
        var store = new FakeStore();
        store.Items.Add(NewItem(7, FeedbackStatus.Read, FeedbackType.Comment));
        var controller = Build(store);
        var result = await controller.Detail(7);
        var view = Assert.IsType<ViewResult>(result);
        var item = Assert.IsType<FeedbackItem>(view.Model);
        Assert.Equal(7, item.Id);
    }

    [Fact]
    public async Task Action_MarkRead_UpdatesStatus_AndRedirects()
    {
        var store = new FakeStore();
        store.Items.Add(NewItem(3, FeedbackStatus.New, FeedbackType.Bug));
        var controller = Build(store);
        var result = await controller.Action(3, "markRead");
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Single(store.StatusUpdates);
        Assert.Equal((3L, FeedbackStatus.Read), store.StatusUpdates[0]);
    }

    [Fact]
    public async Task Action_Archive_UpdatesStatus()
    {
        var store = new FakeStore();
        store.Items.Add(NewItem(4, FeedbackStatus.New, FeedbackType.Bug));
        var controller = Build(store);
        await controller.Action(4, "archive");
        Assert.Equal((4L, FeedbackStatus.Archived), store.StatusUpdates[0]);
    }

    [Fact]
    public async Task Action_Delete_CallsDelete()
    {
        var store = new FakeStore();
        store.Items.Add(NewItem(5, FeedbackStatus.Read, FeedbackType.Bug));
        var controller = Build(store);
        await controller.Action(5, "delete");
        Assert.Contains(5L, store.Deletes);
    }

    [Fact]
    public async Task Action_UnknownAction_Returns400()
    {
        var controller = Build(new FakeStore());
        var result = await controller.Action(1, "bogus");
        Assert.IsType<BadRequestResult>(result);
    }

    private static AdminFeedbackController Build(IFeedbackStore store)
    {
        return new AdminFeedbackController(store)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider()),
        };
    }

    private static FeedbackItem NewItem(long id, FeedbackStatus status, FeedbackType type) =>
        new(id, DateTime.UtcNow, type, "msg with enough chars", null, null, null, null, null, status);

    private sealed class FakeStore : IFeedbackStore
    {
        public List<FeedbackItem> Items { get; } = new();
        public List<(long Id, FeedbackStatus Status)> StatusUpdates { get; } = new();
        public List<long> Deletes { get; } = new();

        public Task<long> AddAsync(FeedbackSubmission s, FeedbackRequestContext c, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<FeedbackItem?> GetAsync(long id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
        public Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken ct = default)
        {
            var filtered = Items.AsEnumerable();
            if (query.Status.HasValue) filtered = filtered.Where(i => i.Status == query.Status.Value);
            if (query.Type.HasValue) filtered = filtered.Where(i => i.Type == query.Type.Value);
            return Task.FromResult<IReadOnlyList<FeedbackItem>>(filtered.ToList());
        }
        public Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(i => (!status.HasValue || i.Status == status.Value) && (!type.HasValue || i.Type == type.Value)));
        public Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken ct = default)
        {
            var map = new Dictionary<FeedbackStatus, int>
            {
                [FeedbackStatus.New] = Items.Count(i => i.Status == FeedbackStatus.New),
                [FeedbackStatus.Read] = Items.Count(i => i.Status == FeedbackStatus.Read),
                [FeedbackStatus.Archived] = Items.Count(i => i.Status == FeedbackStatus.Archived),
            };
            return Task.FromResult<IReadOnlyDictionary<FeedbackStatus, int>>(map);
        }
        public Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken ct = default)
        {
            StatusUpdates.Add((id, status));
            return Task.CompletedTask;
        }
        public Task DeleteAsync(long id, CancellationToken ct = default)
        {
            Deletes.Add(id);
            return Task.CompletedTask;
        }
        public string HashIp(string? ip) => ip ?? "";
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
```

- [ ] **Step 2: Write `AdminFeedbackController.cs`**

```csharp
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

public sealed class AdminFeedbackListViewModel
{
    public IReadOnlyList<FeedbackItem> Items { get; init; } = Array.Empty<FeedbackItem>();
    public FeedbackStatus? StatusFilter { get; init; }
    public FeedbackType? TypeFilter { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int TotalCount { get; init; }
    public IReadOnlyDictionary<FeedbackStatus, int> CountsByStatus { get; init; } =
        new Dictionary<FeedbackStatus, int>();
    public int TotalPages => (int)Math.Ceiling((double)Math.Max(TotalCount, 1) / Math.Max(PageSize, 1));
}

[Route("Admin/Feedback")]
public sealed class AdminFeedbackController : Controller
{
    private readonly IFeedbackStore _store;

    public AdminFeedbackController(IFeedbackStore store)
    {
        _store = store;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(FeedbackStatus? status = FeedbackStatus.New, FeedbackType? type = null, int page = 1)
    {
        page = Math.Max(page, 1);
        const int pageSize = 50;
        var query = new FeedbackListQuery { Status = status, Type = type, Page = page, PageSize = pageSize };
        var items = await _store.ListAsync(query);
        var total = await _store.CountAsync(status, type);
        var counts = await _store.CountsByStatusAsync();

        var vm = new AdminFeedbackListViewModel
        {
            Items = items,
            StatusFilter = status,
            TypeFilter = type,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            CountsByStatus = counts,
        };
        return View(vm);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var item = await _store.GetAsync(id);
        if (item is null) return NotFound();
        return View(item);
    }

    [HttpPost("{id:long}/{action}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Action(long id, string action)
    {
        switch (action?.ToLowerInvariant())
        {
            case "markread":
                await _store.UpdateStatusAsync(id, FeedbackStatus.Read);
                break;
            case "archive":
                await _store.UpdateStatusAsync(id, FeedbackStatus.Archived);
                break;
            case "delete":
                await _store.DeleteAsync(id);
                break;
            default:
                return BadRequest();
        }
        TempData["AdminFeedbackAction"] = $"{action} applied to #{id}";
        return RedirectToAction(nameof(Index));
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~AdminFeedbackControllerTests`
Expected: all 7 tests PASS.

- [ ] **Step 4: Commit**

```bash
git add DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs DeckFlow.Web.Tests/AdminFeedbackControllerTests.cs
git commit -m "Add AdminFeedbackController with list, detail, and actions"
```

---

## Task 8: Build admin list and detail views

**Files:**
- Create: `DeckFlow.Web/Views/Admin/Feedback/Index.cshtml`
- Create: `DeckFlow.Web/Views/Admin/Feedback/Detail.cshtml`
- Modify: `DeckFlow.Web/wwwroot/css/site-common.css`

- [ ] **Step 1: Write `Views/Admin/Feedback/Index.cshtml`**

```html
@model DeckFlow.Web.Controllers.Admin.AdminFeedbackListViewModel
@using DeckFlow.Web.Models
@{
    ViewData["Title"] = "Admin — Feedback";
    var actionMessage = TempData["AdminFeedbackAction"] as string;
}

<section class="admin-feedback">
    <h1>Feedback</h1>

    @if (!string.IsNullOrEmpty(actionMessage))
    {
        <div class="feedback-banner feedback-banner--success">@actionMessage</div>
    }

    <div class="admin-feedback-filters">
        @{
            FeedbackStatus?[] statuses = new FeedbackStatus?[] { FeedbackStatus.New, FeedbackStatus.Read, FeedbackStatus.Archived, null };
        }
        @foreach (var s in statuses)
        {
            var key = s.HasValue ? s.Value : (FeedbackStatus?)null;
            var label = s.HasValue ? s.Value.ToString() : "All";
            var count = s.HasValue ? Model.CountsByStatus.GetValueOrDefault(s.Value, 0) : Model.CountsByStatus.Values.Sum();
            var active = (Model.StatusFilter == s) ? "active" : "";
            <a class="admin-feedback-filter @active" asp-action="Index" asp-route-status="@(s?.ToString())" asp-route-type="@Model.TypeFilter">
                @label (@count)
            </a>
        }

        <form method="get" asp-action="Index" class="admin-feedback-type">
            <input type="hidden" name="status" value="@Model.StatusFilter" />
            <label for="typeSelect">Type:</label>
            <select id="typeSelect" name="type" onchange="this.form.submit()">
                <option value="">All</option>
                @foreach (var t in Enum.GetValues<FeedbackType>())
                {
                    var selected = Model.TypeFilter == t ? "selected" : "";
                    <option value="@t" selected="@(Model.TypeFilter == t)">@t</option>
                }
            </select>
        </form>
    </div>

    @if (Model.Items.Count == 0)
    {
        <p class="admin-feedback-empty">No feedback in this view.</p>
    }
    else
    {
        <table class="admin-feedback-table">
            <thead>
                <tr>
                    <th>Created (UTC)</th>
                    <th>Type</th>
                    <th>Message</th>
                    <th>Email</th>
                    <th>Status</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
            @foreach (var item in Model.Items)
            {
                var preview = item.Message.Length > 80 ? item.Message.Substring(0, 80) + "…" : item.Message;
                <tr>
                    <td><time datetime="@item.CreatedUtc.ToString("o")" title="@item.CreatedUtc.ToLocalTime()">@item.CreatedUtc.ToString("yyyy-MM-dd HH:mm")</time></td>
                    <td><span class="type-badge type-@item.Type.ToString().ToLower()">@item.Type</span></td>
                    <td>@preview</td>
                    <td>@item.Email</td>
                    <td>@item.Status</td>
                    <td>
                        <a asp-action="Detail" asp-route-id="@item.Id">View</a>
                        @if (item.Status != FeedbackStatus.Archived)
                        {
                            <form method="post" asp-action="Action" asp-route-id="@item.Id" asp-route-action="archive" style="display:inline">
                                @Html.AntiForgeryToken()
                                <button type="submit">Archive</button>
                            </form>
                        }
                    </td>
                </tr>
            }
            </tbody>
        </table>

        <nav class="admin-feedback-pagination">
            @if (Model.Page > 1)
            {
                <a asp-action="Index" asp-route-status="@(Model.StatusFilter?.ToString())" asp-route-type="@Model.TypeFilter" asp-route-page="@(Model.Page - 1)">Prev</a>
            }
            <span>Page @Model.Page of @Model.TotalPages</span>
            @if (Model.Page < Model.TotalPages)
            {
                <a asp-action="Index" asp-route-status="@(Model.StatusFilter?.ToString())" asp-route-type="@Model.TypeFilter" asp-route-page="@(Model.Page + 1)">Next</a>
            }
        </nav>
    }
</section>
```

- [ ] **Step 2: Write `Views/Admin/Feedback/Detail.cshtml`**

```html
@model DeckFlow.Web.Models.FeedbackItem
@{
    ViewData["Title"] = $"Feedback #{Model.Id}";
}

<section class="admin-feedback-detail" style="background: var(--panel); border: 1px solid var(--line); padding: 1.5rem; border-radius: 8px; max-width: 800px; margin: 2rem auto;">
    <p><a asp-action="Index">← Back to list</a></p>

    <h1>Feedback #@Model.Id</h1>
    <dl class="detail-grid">
        <dt>Created (UTC)</dt><dd>@Model.CreatedUtc.ToString("yyyy-MM-dd HH:mm:ss")</dd>
        <dt>Type</dt><dd>@Model.Type</dd>
        <dt>Status</dt><dd>@Model.Status</dd>
        <dt>Email</dt><dd>@(Model.Email ?? "(none)")</dd>
        <dt>Page URL</dt><dd>@(Model.PageUrl ?? "(none)")</dd>
        <dt>User agent</dt><dd>@(Model.UserAgent ?? "(none)")</dd>
        <dt>IP hash</dt><dd><code>@(Model.IpHash ?? "(none)")</code></dd>
        <dt>App version</dt><dd>@(Model.AppVersion ?? "(none)")</dd>
    </dl>

    <h2>Message</h2>
    <pre class="detail-message">@Model.Message</pre>

    <div class="detail-actions">
        @if (Model.Status == DeckFlow.Web.Models.FeedbackStatus.New)
        {
            <form method="post" asp-action="Action" asp-route-id="@Model.Id" asp-route-action="markRead" style="display:inline">
                @Html.AntiForgeryToken()
                <button type="submit">Mark Read</button>
            </form>
        }
        @if (Model.Status != DeckFlow.Web.Models.FeedbackStatus.Archived)
        {
            <form method="post" asp-action="Action" asp-route-id="@Model.Id" asp-route-action="archive" style="display:inline">
                @Html.AntiForgeryToken()
                <button type="submit">Archive</button>
            </form>
        }
        <form method="post" asp-action="Action" asp-route-id="@Model.Id" asp-route-action="delete" style="display:inline"
              onsubmit="return confirm('Delete feedback #@Model.Id permanently?');">
            @Html.AntiForgeryToken()
            <button type="submit" class="danger">Delete</button>
        </form>
    </div>
</section>
```

- [ ] **Step 3: Append styles to `site-common.css`**

```css
/* Admin feedback */
.admin-feedback { max-width: 1100px; margin: 2rem auto; padding: 0 1rem; }
.admin-feedback-filters { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 1rem; }
.admin-feedback-filter {
    padding: 0.3rem 0.75rem;
    border: 1px solid var(--line);
    border-radius: 4px;
    text-decoration: none;
    color: var(--accent-strong, inherit);
}
.admin-feedback-filter.active { background: var(--accent-strong); color: #fff; }
.admin-feedback-table { width: 100%; border-collapse: collapse; }
.admin-feedback-table th, .admin-feedback-table td { padding: 0.5rem; border-bottom: 1px solid var(--line); text-align: left; vertical-align: top; }
.admin-feedback-pagination { margin-top: 1rem; display: flex; gap: 0.75rem; align-items: center; }
.admin-feedback-empty { margin: 2rem 0; font-style: italic; }
.type-badge { display: inline-block; padding: 0.15rem 0.5rem; border-radius: 4px; font-size: 0.8rem; background: var(--panel); border: 1px solid var(--line); }
.detail-grid { display: grid; grid-template-columns: max-content 1fr; gap: 0.4rem 1rem; }
.detail-grid dt { font-weight: 600; }
.detail-message { white-space: pre-wrap; word-wrap: break-word; background: rgba(0,0,0,0.05); padding: 1rem; border-radius: 4px; }
.detail-actions { margin-top: 1.5rem; display: flex; gap: 0.5rem; flex-wrap: wrap; }
.detail-actions button.danger { background: #c33; color: #fff; border: none; padding: 0.4rem 0.8rem; border-radius: 4px; cursor: pointer; }
```

- [ ] **Step 4: Manual smoke test**

Set `FEEDBACK_ADMIN_USER=admin FEEDBACK_ADMIN_PASSWORD=dev`, run app, submit one feedback item on `/Feedback`, then browse to `/Admin/Feedback`. Browser should prompt for credentials, list should show the entry, detail page should show all fields and actions.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Views/Admin/Feedback/Index.cshtml DeckFlow.Web/Views/Admin/Feedback/Detail.cshtml DeckFlow.Web/wwwroot/css/site-common.css
git commit -m "Add admin feedback list and detail views"
```

---

## Task 9: Add footer link and Help page card

**Files:**
- Modify: `DeckFlow.Web/Views/Shared/_Layout.cshtml`
- Modify: `DeckFlow.Web/Views/Help/Index.cshtml`

- [ ] **Step 1: Inspect the current footer in `_Layout.cshtml`**

Run: `grep -n "page-footer\|</footer>\|About" DeckFlow.Web/Views/Shared/_Layout.cshtml`
Identify the existing footer block (look for `<footer class="page-footer">`).

- [ ] **Step 2: Add feedback link to the footer**

Inside the existing `<footer class="page-footer">` element, add this anchor next to the existing About link (match the surrounding markup — if links are in an unordered list, wrap it in `<li>`; if separated by middots, mirror that pattern):

```html
<a asp-controller="Feedback" asp-action="Index">Feedback</a>
```

Use the same separator pattern used for About/GitHub links.

- [ ] **Step 3: Add feedback card to Help index**

Inspect `Views/Help/Index.cshtml` (it should render a grid of help topics). At the end of the grid container, add a card following the same markup used for other cards. If topics are rendered as `<a class="help-card">...</a>`:

```html
<a class="help-card help-card--feedback" asp-controller="Feedback" asp-action="Index">
    <h2>Found a bug or have an idea?</h2>
    <p>Send feedback directly to the developer — bugs, suggestions, or just say hi.</p>
    <span class="help-card-arrow">Send feedback →</span>
</a>
```

If the current markup differs (e.g., `<div class="help-card">`), mirror the exact pattern already in the file.

- [ ] **Step 4: Build + manual smoke test**

Run: `dotnet build DeckFlow.Web/DeckFlow.Web.csproj`
Expected: succeeds.
Start app and verify:
- Footer shows "Feedback" link on every page.
- `/Help` shows the new card and clicking it navigates to `/Feedback`.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Views/Shared/_Layout.cshtml DeckFlow.Web/Views/Help/Index.cshtml
git commit -m "Link feedback from footer and Help hub"
```

---

## Task 10: Run full test suite and update README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Run the entire test suite**

Run: `dotnet test`
Expected: all tests pass (previous 242 plus new FeedbackStore, BasicAuthMiddleware, FeedbackController, AdminFeedbackController tests). If the sandbox blocks `dotnet test` per recent memory, fall back to the existing custom reflection-based runner script with `-m:1` on `dotnet build`.

- [ ] **Step 2: Update `README.md`**

Add a new section after the existing Help section (or in the most natural section) titled `User Feedback`, with wording along these lines:

```markdown
## User Feedback

A public **Feedback** form is linked in the site footer (`/Feedback`). Submissions are stored in a SQLite database (`feedback.db`) at `$MTG_DATA_DIR/feedback.db` (falls back to `./artifacts/feedback.db` in development).

An admin page at `/Admin/Feedback` displays submissions with filters for status and type, and lets you mark items Read, Archive, or Delete them.

### Admin configuration

Set these environment variables (via `fly secrets set ...` on Fly.io or the Render env var UI):

- `FEEDBACK_ADMIN_USER` — basic auth username for `/Admin/Feedback`.
- `FEEDBACK_ADMIN_PASSWORD` — basic auth password.
- `FEEDBACK_IP_SALT` (optional) — salt for hashing submitter IPs. If unset, a random 32-byte salt is generated on first run and persisted alongside `feedback.db`.

If `FEEDBACK_ADMIN_USER` or `FEEDBACK_ADMIN_PASSWORD` are not set, `/Admin/Feedback` returns **503 Service Unavailable**. The public `/Feedback` form continues to accept submissions.

Public submissions are rate-limited to 5 per hour per IP.
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "Document feedback form and admin configuration"
```

---

## Self-Review Results

1. **Spec coverage** — every spec section maps to a task:
   - Architecture → Tasks 1–9.
   - Data Model → Task 2 (schema in `EnsureSchemaAsync`).
   - Submission Flow → Tasks 4, 5, 6 (honeypot, validation, rate limiter, PRG).
   - Admin Flow → Tasks 3, 6, 7, 8 (middleware, wiring, controller, views).
   - UI Placement → Task 9 (footer + Help card) and Task 5 (form page theming).
   - Configuration → Task 10 (README env var docs).
   - Testing → Tests inline in Tasks 2, 3, 4, 7.
   - Security Notes → enforced across Tasks 2 (IP hashing, truncation), 3 (constant-time compare, fail closed), 4 (antiforgery, honeypot silence), 7 (antiforgery on actions).

2. **Placeholder scan** — no TBD/TODO left. Step 2 of Task 4 is a sanity-check lookup for an existing interface member, not a placeholder.

3. **Type consistency** — `IFeedbackStore`, `FeedbackRequestContext`, `FeedbackListQuery`, `AdminFeedbackListViewModel` signatures used in later tasks match their definitions in earlier tasks. Controller method names (`Index`, `Detail`, `Action`) are consistent between controller code, tests, and views.

