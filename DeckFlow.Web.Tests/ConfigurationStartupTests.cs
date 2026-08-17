using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Xunit;

namespace DeckFlow.Web.Tests;

public class ConfigurationStartupTests
{
    [Fact]
    public void App_configuration_json_sources_do_not_reload_on_change()
    {
        var builder = Program.CreateBuilder(Array.Empty<string>());

        Assert.NotEmpty(builder.Configuration.Sources.OfType<JsonConfigurationSource>());
        Assert.All(
            builder.Configuration.Sources.OfType<JsonConfigurationSource>(),
            source => Assert.False(source.ReloadOnChange));
    }

    [Fact]
    public void Environment_configuration_still_overrides_appsettings()
    {
        const string key = "Logging__LogLevel__Default";
        const string environmentValue = "Warning";
        var original = Environment.GetEnvironmentVariable(key);

        try
        {
            Environment.SetEnvironmentVariable(key, environmentValue);
            var builder = Program.CreateBuilder(Array.Empty<string>());
            var fileValues = ((IConfigurationRoot)builder.Configuration).Providers
                .OfType<JsonConfigurationProvider>()
                .Select(provider => provider.TryGet("Logging:LogLevel:Default", out var value) ? value : null)
                .Where(value => value is not null);

            Assert.DoesNotContain(environmentValue, fileValues);
            Assert.Equal(environmentValue, builder.Configuration["Logging:LogLevel:Default"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, original);
        }
    }
}
