using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Read-only <see cref="IProdContentReader"/> implementation. Builds the production Npgsql
/// connection on-demand from the raw connection string (the same convention as
/// <see cref="ProdStoreFactory"/>) and runs a SINGLE plain <c>SELECT</c> against
/// <c>content_site_index</c> via Dapper. It runs NO <c>EnsureSchemaAsync</c>, NO <c>CREATE</c>/
/// <c>ALTER</c> DDL, and NO information-schema introspection — the production side is structurally
/// read-only (R1). No connection string or exception detail is logged or surfaced here (D-07).
/// </summary>
public sealed class ProdContentReader : IProdContentReader
{
    // Why: matches ContentSiteIndexStore's read column set 1:1 so the materialized rows are identical
    // to GetAllRowsAsync — but with NO EnsureSchemaAsync call (which would run prod DDL). Plain SELECT,
    // no WHERE on any timestamp column, so the F-51-PG-01 timestamptz-vs-text class cannot recur.
    private const string SelectAllSql = """
        SELECT id, source, title, video_url, artifact_path, published_utc, pushed_to_prod_utc,
               indexed_utc, archetype_tags, bracket_tags, card_category_tags, natural_key_type,
               natural_key_value, is_visible, is_hidden, is_evergreen, approval_status,
               body_sha256, seed_managed
          FROM content_site_index;
        """;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Why: built on-demand, never registered live at DI startup — minimizes the always-live
        // surface (D-03). Normalize handles Render's postgresql:// URL form.
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);

        // Why: this reader ONLY ever connects to Render-managed prod Postgres, which requires SSL
        // (28000: SSL/TLS required). Force SslMode=Require UNCONDITIONALLY — regardless of the
        // operator's string form (URL or key-value) or any sslmode it already carries. The previous
        // "leave an explicit Disable as the operator set it" escape hatch was the last plaintext
        // path: a key-value string with Ssl Mode=Disable skipped the override and connected
        // unencrypted, which Render rejects with the EndOfStream-during-Authenticate signature.
        // There is no valid plaintext case here (Render mandates SSL), so honoring Disable only ever
        // breaks the pull. In Npgsql 10, SslMode.Require encrypts WITHOUT validating the server
        // certificate chain (libpq semantics), which is exactly what Render's managed endpoint
        // needs — so TrustServerCertificate is unnecessary (it is a no-op / obsolete in v10).
        var builder = new NpgsqlConnectionStringBuilder(normalized)
        {
            SslMode = SslMode.Require
        };

        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);

        await using var connection = await conn.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(
            new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));

        return rows.Select(ToContentSiteIndexRow).ToList();
    }

    // Why: single plain SELECT, no WHERE on any timestamp column (no F-51-PG-01 exposure), no
    // DDL/EnsureSchema — the feature_flags read-only twin of SelectAllSql (D-04).
    private const string SelectFlagSql = "SELECT enabled FROM feature_flags WHERE key = @key;";

    /// <inheritdoc />
    public async Task<bool> ReadFlagAsync(
        string connectionString,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            // Why: identical connection setup to ReadAllAsync (normalize + force SslMode.Require) so
            // this accessor honors the exact same Render-Postgres connectivity contract.
            var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
            var builder = new NpgsqlConnectionStringBuilder(normalized)
            {
                SslMode = SslMode.Require
            };

            var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);

            await using var connection = await conn.OpenConnectionAsync(cancellationToken);

            var enabled = await connection.QuerySingleOrDefaultAsync<bool?>(
                new CommandDefinition(SelectFlagSql, new { key }, cancellationToken: cancellationToken));

            return enabled ?? false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Why: fail CLOSED (D-04) — a connection/query failure must read exactly like a
            // missing/false row, never propagate as an error a caller could misinterpret. No
            // connection string or exception detail is logged or surfaced here (D-07 convention).
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool?> TryReadFlagAsync(
        string connectionString,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            // Same read-only connection contract as ReadFlagAsync (normalize + force SslMode.Require).
            var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
            var builder = new NpgsqlConnectionStringBuilder(normalized)
            {
                SslMode = SslMode.Require
            };

            var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);

            await using var connection = await conn.OpenConnectionAsync(cancellationToken);

            var enabled = await connection.QuerySingleOrDefaultAsync<bool?>(
                new CommandDefinition(SelectFlagSql, new { key }, cancellationToken: cancellationToken));

            // A missing row / null enabled is a DEFINITIVE OFF (false), NOT indeterminate — only a
            // caught read failure below returns null. This lets the DirectPush publish gate fail SAFE
            // (verify the deployed body) on a read blip rather than immediate-publishing.
            return enabled ?? false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Indeterminate: the read itself failed. No connection string / exception detail surfaced
            // (D-07). The publish-gate caller treats null as "cannot prove OFF → must verify".
            return null;
        }
    }

    // Mirrors ContentSiteIndexStore.ToContentSiteIndexRow exactly: split natural_key_type into
    // YoutubeVideoId vs RssGuid and deserialize the three serialized tag columns.
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
            BodySha256 = row.BodySha256,
            SeedManaged = row.SeedManaged
        };
    }

    // Dapper materialization target — mirrors ContentSiteIndexStore's private ContentSiteIndexRowData.
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
        public bool? SeedManaged { get; init; }
    }
}
