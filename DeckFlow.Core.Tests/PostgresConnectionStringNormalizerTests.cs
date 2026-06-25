using DeckFlow.Core.Storage;
using Npgsql;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class PostgresConnectionStringNormalizerTests
{
    [Fact]
    public void Normalize_PostgresUrl_ConvertsToKeyValueWithHostPortDatabase()
    {
        var result = PostgresConnectionStringNormalizer.Normalize("postgres://u:p@host:5433/dbname");
        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal("host", builder.Host);
        Assert.Equal(5433, builder.Port);
        Assert.Equal("dbname", builder.Database);
        Assert.Equal("u", builder.Username);
        Assert.Equal("p", builder.Password);
    }

    [Fact]
    public void Normalize_UrlWithoutPort_Defaults5432()
    {
        var result = PostgresConnectionStringNormalizer.Normalize("postgres://u:p@host/dbname");
        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal(5432, builder.Port);
    }

    [Fact]
    public void Normalize_UrlEncodedUserInfo_Unescapes()
    {
        var result = PostgresConnectionStringNormalizer.Normalize("postgres://u:p%40ss@host:5433/dbname");
        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal("u", builder.Username);
        Assert.Equal("p@ss", builder.Password);
    }

    [Fact]
    public void Normalize_SslmodeQuery_MapsToSslMode()
    {
        var result = PostgresConnectionStringNormalizer.Normalize("postgres://u:p@host:5433/dbname?sslmode=require");
        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void Normalize_UrlWithoutSslmode_DefaultsToRequire()
    {
        // Render rejects non-SSL connections; a URL string that omits sslmode must still connect.
        var result = PostgresConnectionStringNormalizer.Normalize("postgres://u:p@host:5433/dbname");
        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void Normalize_ExplicitSslmode_OverridesRequireDefault()
    {
        // An explicit ?sslmode= must still win over the Require default.
        var result = PostgresConnectionStringNormalizer.Normalize("postgres://u:p@host:5433/dbname?sslmode=disable");
        var builder = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal(SslMode.Disable, builder.SslMode);
    }

    [Fact]
    public void Normalize_Url_DefaultsTrustServerCertificate()
    {
        // Render's managed-PG endpoint trips Npgsql Require chain validation; trust is defaulted on.
        var result = PostgresConnectionStringNormalizer.Normalize("postgres://u:p@host:5433/dbname");
        var builder = new NpgsqlConnectionStringBuilder(result);

#pragma warning disable CS0618 // Asserting the no-op key is still emitted for Render string parity.
        Assert.True(builder.TrustServerCertificate);
#pragma warning restore CS0618
    }

    [Fact]
    public void Normalize_AlreadyKeyValue_ReturnedUnchanged()
    {
        const string input = "Host=h;Username=u;Password=p";

        var result = PostgresConnectionStringNormalizer.Normalize(input);

        Assert.Equal(input, result);
    }
}
