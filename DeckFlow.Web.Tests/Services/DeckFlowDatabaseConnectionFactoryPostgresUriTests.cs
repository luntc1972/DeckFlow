using DeckFlow.Web.Services;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

public sealed class DeckFlowDatabaseConnectionFactoryPostgresUriTests
{
    [Theory]
    [InlineData("postgresql://u:p@host/db", "host", "u", "p", "db", 5432)]
    [InlineData("postgresql://u:p@host:5433/db", "host", "u", "p", "db", 5433)]
    [InlineData("postgres://u:p@host/db", "host", "u", "p", "db", 5432)]
    public void NormalizePostgresConnectionString_ParsesBasicUriForms(
        string raw,
        string expectedHost,
        string expectedUsername,
        string expectedPassword,
        string expectedDatabase,
        int expectedPort)
    {
        var normalized = DeckFlowDatabaseConnectionFactory.NormalizePostgresConnectionString(raw);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(expectedHost, builder.Host);
        Assert.Equal(expectedUsername, builder.Username);
        Assert.Equal(expectedPassword, builder.Password);
        Assert.Equal(expectedDatabase, builder.Database);
        Assert.Equal(expectedPort, builder.Port);
    }

    [Fact]
    public void NormalizePostgresConnectionString_DecodesUrlEncodedPassword()
    {
        var normalized = DeckFlowDatabaseConnectionFactory.NormalizePostgresConnectionString("postgresql://u:p%40w0rd%21@host/db");
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("p@w0rd!", builder.Password);
    }

    [Fact]
    public void NormalizePostgresConnectionString_MapsSslModeQueryParameter()
    {
        var normalized = DeckFlowDatabaseConnectionFactory.NormalizePostgresConnectionString("postgresql://u:p@host/db?sslmode=require");
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void NormalizePostgresConnectionString_LeavesKeyValueConnectionStringsUnchanged()
    {
        var raw = "Host=h;Username=u;Password=p;Database=d";

        var normalized = DeckFlowDatabaseConnectionFactory.NormalizePostgresConnectionString(raw);

        Assert.Equal(raw, normalized);
    }

    [Fact]
    public void NormalizePostgresConnectionString_IgnoresUnknownQueryParameters()
    {
        var normalized = DeckFlowDatabaseConnectionFactory.NormalizePostgresConnectionString("postgresql://u:p@host/db?application_name=foo");
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("host", builder.Host);
        Assert.Equal("u", builder.Username);
        Assert.Equal("p", builder.Password);
        Assert.Equal("db", builder.Database);
    }
}
