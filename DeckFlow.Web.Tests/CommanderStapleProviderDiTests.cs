using System.Reflection;
using DeckFlow.Core.Loading;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CommanderStapleProviderDiTests
{
    [Fact]
    public void RealCutLabRegistrations_InjectCommanderStapleProviderIntoBuilder()
    {
        var services = new ServiceCollection();
        services.AddDeckFlowCutLabServices();
        services.AddSingleton<ICommanderStapleProvider>(new CommanderStapleProvider(Path.GetTempFileName()));
        services.AddSingleton<IDeckEntryLoader>(Proxy<IDeckEntryLoader>());
        services.AddSingleton<IScryfallCardResolver>(Proxy<IScryfallCardResolver>());
        services.AddSingleton(sp => new ScryfallReferenceResolver(sp.GetRequiredService<IScryfallCardResolver>(), new ScryfallCollectionCardCache()));
        services.AddSingleton<ICommanderBanListService>(Proxy<ICommanderBanListService>());
        services.AddScoped<ICutLabSimulationService>(_ => Proxy<ICutLabSimulationService>());
        services.AddScoped<ICutLabFloorResolver>(_ => Proxy<ICutLabFloorResolver>());
        services.AddScoped<ICutLabWhatifService>(_ => Proxy<ICutLabWhatifService>());
        services.AddSingleton<IFeatureFlagCache>(Proxy<IFeatureFlagCache>());
        services.AddSingleton<ILogger<CutLabApiController>>(NullLogger<CutLabApiController>.Instance);
        services.AddScoped<ICutLabPageService, CutLabPageService>();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        CutLabAnalysisContextBuilder builder = Assert.IsType<CutLabAnalysisContextBuilder>(scope.ServiceProvider.GetRequiredService<ICutLabAnalysisContextBuilder>());

        Assert.IsType<CommanderStapleProvider>(InjectedProvider(builder));
    }

    private static ICommanderStapleProvider InjectedProvider(CutLabAnalysisContextBuilder builder)
        => Assert.IsAssignableFrom<ICommanderStapleProvider>(builder.GetType()
            .GetField("_commanderStapleProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(builder));

    private static T Proxy<T>() where T : class => DispatchProxy.Create<T, NoOpProxy>();

    private class NoOpProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }
}
