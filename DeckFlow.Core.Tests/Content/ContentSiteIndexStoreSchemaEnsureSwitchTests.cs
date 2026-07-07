using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// D-09/D-11 tests for the <c>ensureSchemaEnabled</c> switch on <see cref="ContentSiteIndexStore"/>.
/// The REQUIRED fact drives a prod-mode (switch-OFF) store through the DirectPush read+write paths
/// against a recording connection and asserts ZERO CREATE/ALTER/DROP SQL is issued — attempted-but-failed
/// DDL is caught, not just successful auto-create. A supplemental fact proves no auto-create on a
/// schema-less file as a cheap second signal.
/// </summary>
public sealed class ContentSiteIndexStoreSchemaEnsureSwitchTests : IDisposable
{
    // Word-boundary, case-insensitive DDL detector — matches CREATE/ALTER/DROP as SQL keywords.
    private static readonly Regex DdlPattern =
        new(@"\b(CREATE|ALTER|DROP)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _dbPath;

    public ContentSiteIndexStoreSchemaEnsureSwitchTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-schemaswitch-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task ProdModeStore_OnDirectPushReadAndWrite_IssuesNoDdl()
    {
        // Arrange: pre-create a full schema via a normal (switch-ON) store so the prod-mode store
        // finds an existing table and never needs to auto-create.
        var onStore = new ContentSiteIndexStore(_dbPath);
        await onStore.EnsureSchemaAsync();

        var recorded = new List<string>();
        var connectionString = $"Data Source={_dbPath}";
        var prodStore = new ContentSiteIndexStore(
            RelationalDatabaseConnection.FromSqlitePath(_dbPath),
            ensureSchemaEnabled: false,
            connectionFactoryOverride: async ct =>
            {
                var inner = new SqliteConnection(connectionString);
                await inner.OpenAsync(ct).ConfigureAwait(false);
                return new RecordingDbConnection(inner, recorded);
            });

        // Act: drive the DirectPush read path (diff) and write path (publish).
        await prodStore.GetAllRowsAsync();
        await prodStore.UpsertContentColumnsOnlyBatchAsync(new[] { CreateYoutubeRow("yt-prod-write") });

        // Assert: nothing that ran matched CREATE/ALTER/DROP.
        Assert.NotEmpty(recorded);
        var ddl = recorded.Where(sql => DdlPattern.IsMatch(sql)).ToList();
        Assert.True(ddl.Count == 0, "Prod-mode store issued DDL: " + string.Join(" | ", ddl));
    }

    [Fact]
    public async Task SwitchOffStore_OnSchemalessFile_ThrowsOnReadAndWrite_NoAutoCreate()
    {
        // A prod-mode store pointed at an empty (schema-less) SQLite file must NOT auto-create the table.
        var prodStore = new ContentSiteIndexStore(
            RelationalDatabaseConnection.FromSqlitePath(_dbPath),
            ensureSchemaEnabled: false);

        await Assert.ThrowsAsync<SqliteException>(() => prodStore.GetAllRowsAsync());
        // The batch write path wraps the underlying "no such table" SqliteException per its rollback contract.
        var writeEx = await Assert.ThrowsAsync<ContentSiteIndexBatchUpsertException>(
            () => prodStore.UpsertContentColumnsOnlyBatchAsync(new[] { CreateYoutubeRow("yt-noschema") }));
        Assert.IsType<SqliteException>(writeEx.InnerException);
    }

    [Fact]
    public async Task SwitchOnStore_AutoCreatesSchema_AsToday()
    {
        // Default (switch-ON) behavior is unchanged: the store auto-creates its schema on first use.
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyBatchAsync(new[] { CreateYoutubeRow("yt-autocreate") });

        var rows = await store.GetAllRowsAsync();
        Assert.Single(rows);
    }

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo" },
            BracketTags = new[] { "cEDH" },
            CardCategoryTags = new[] { "win-cons" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null,
            ApprovalStatus = "approved"
        };

    /// <summary>
    /// Recording <see cref="DbConnection"/> decorator: wraps a real connection and records every
    /// executed <c>CommandText</c> into a shared sink so a test can assert what SQL was issued.
    /// </summary>
    private sealed class RecordingDbConnection : DbConnection
    {
        private readonly DbConnection _inner;
        private readonly List<string> _sink;

        public RecordingDbConnection(DbConnection inner, List<string> sink)
        {
            _inner = inner;
            _sink = sink;
        }

        internal DbConnection Inner => _inner;

        [AllowNull]
        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }

        public override string Database => _inner.Database;

        public override string DataSource => _inner.DataSource;

        public override string ServerVersion => _inner.ServerVersion;

        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public override void Close() => _inner.Close();

        public override void Open() => _inner.Open();

        public override Task OpenAsync(CancellationToken cancellationToken) => _inner.OpenAsync(cancellationToken);

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => _inner.BeginTransaction(isolationLevel);

        protected override DbCommand CreateDbCommand()
            => new RecordingDbCommand(_inner.CreateCommand(), _sink, this);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Recording <see cref="DbCommand"/> decorator: records <c>CommandText</c> on every execution
    /// path (sync + async) then delegates to the wrapped command.
    /// </summary>
    private sealed class RecordingDbCommand : DbCommand
    {
        private readonly DbCommand _inner;
        private readonly List<string> _sink;
        private readonly RecordingDbConnection _connection;

        public RecordingDbCommand(DbCommand inner, List<string> sink, RecordingDbConnection connection)
        {
            _inner = inner;
            _sink = sink;
            _connection = connection;
        }

        [AllowNull]
        public override string CommandText
        {
            get => _inner.CommandText;
            set => _inner.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _inner.CommandTimeout;
            set => _inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _inner.CommandType;
            set => _inner.CommandType = value;
        }

        public override bool DesignTimeVisible
        {
            get => _inner.DesignTimeVisible;
            set => _inner.DesignTimeVisible = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _inner.UpdatedRowSource;
            set => _inner.UpdatedRowSource = value;
        }

        protected override DbConnection? DbConnection
        {
            get => _connection;
            // Unwrap: the inner command is bound to the inner connection, not the decorator.
            set => _inner.Connection = value is RecordingDbConnection rc ? rc.Inner : value;
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

        protected override DbTransaction? DbTransaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = value;
        }

        public override void Cancel() => _inner.Cancel();

        public override void Prepare() => _inner.Prepare();

        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        public override int ExecuteNonQuery()
        {
            Record();
            return _inner.ExecuteNonQuery();
        }

        public override object? ExecuteScalar()
        {
            Record();
            return _inner.ExecuteScalar();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Record();
            return _inner.ExecuteReader(behavior);
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            Record();
            return _inner.ExecuteNonQueryAsync(cancellationToken);
        }

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            Record();
            return _inner.ExecuteScalarAsync(cancellationToken);
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            Record();
            return _inner.ExecuteReaderAsync(behavior, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Record() => _sink.Add(_inner.CommandText ?? string.Empty);
    }
}
