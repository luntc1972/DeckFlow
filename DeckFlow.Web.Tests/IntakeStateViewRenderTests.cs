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

public sealed class IntakeStateViewRenderTests
{
    [Theory]
    [InlineData("JudgeQuestions")]
    [InlineData("SuggestCategories")]
    public async Task FreshRender_UsesEmptyIntakeState(string viewName)
    {
        object model = viewName == "JudgeQuestions" ? new JudgeQuestionViewModel() : new DeckDiffViewModel();

        string html = await RenderAsync(viewName, model);

        Assert.Contains("<details class=\"cutlab-intake\" data-intake-state=\"empty\" open>", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(string viewName, object model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(provider => provider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(provider => provider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddControllersWithViews().AddApplicationPart(typeof(JudgeQuestionsController).Assembly);

        using var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })), new ActionDescriptor());
        var viewResult = provider.GetRequiredService<IRazorViewEngine>().FindView(actionContext, viewName, isMainPage: false);
        Assert.True(viewResult.Success, $"View '{viewName}' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, viewResult.View!, viewData, new TempDataDictionary(httpContext, new StubTempDataProvider()), writer, new HtmlHelperOptions());
        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(JudgeQuestionsController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = AppContext.BaseDirectory,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = AppContext.BaseDirectory,
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
