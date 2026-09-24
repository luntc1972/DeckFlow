using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class MechanicLookupViewRenderTests
{
    [Fact]
    public async Task EmptyState_RendersExpandedIntakeWithoutSummary()
    {
        string html = await RenderAsync(new MechanicLookupViewModel());

        Assert.Contains("<div class=\"cutlab-intake cutlab-intake--empty\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cutlab-intake-summary", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-cut-lab-intake-summary", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResultState_RendersCollapsedIntakeWithEdit()
    {
        string html = await RenderAsync(new MechanicLookupViewModel
        {
            Request = new MechanicLookupRequest { MechanicName = "Prowess" },
            MechanicName = "Prowess",
            RulesText = "Whenever you cast a noncreature spell...",
        });

        Assert.Contains("<details class=\"cutlab-intake\" data-cut-lab-intake-summary>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<details class=\"cutlab-intake\" data-cut-lab-intake-summary open", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"cutlab-intake-summary__commander\">Prowess</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"cutlab-intake-summary__change\">Edit</span>", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(MechanicLookupViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider => serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(serviceProvider => serviceProvider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckLookupController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })), new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "MechanicLookup", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'MechanicLookup' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, viewResult.View!, viewData, new TempDataDictionary(httpContext, new StubTempDataProvider()), writer, new HtmlHelperOptions());
        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var contentRoot = AppContext.BaseDirectory;
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(DeckLookupController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
