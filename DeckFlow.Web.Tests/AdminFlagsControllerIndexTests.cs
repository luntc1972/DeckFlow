using System.Collections.Generic;
using System.Linq;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="AdminFlagsController.Index"/> filtering: tool.* flags are
/// administered on /Admin/Tools, so the Flags console must exclude them from its list.
/// </summary>
public sealed class AdminFlagsControllerIndexTests
{
    [Fact]
    public void Index_ExcludesToolPrefixedFlags()
    {
        var cache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["tool.knowledge-base.enabled"] = true,
            ["tool.bracket.enabled"] = false,
            ["Tool.case-insensitive.enabled"] = true,
            ["service.scryfall-tagger.enabled"] = true,
            ["analysis.manabase.accuracy"] = true,
        });
        var controller = Build(cache);

        var view = Assert.IsType<ViewResult>(controller.Index());
        var vm = Assert.IsType<AdminFlagsListViewModel>(view.Model);
        var keys = vm.Flags.Select(f => f.Key).ToArray();

        Assert.DoesNotContain(keys, k => k.StartsWith("tool.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vm.Groups, group => group.Prefix.StartsWith("tool.", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("service.scryfall-tagger.enabled", keys);
        Assert.Contains("analysis.manabase.accuracy", keys);
    }

    [Fact]
    public void Index_KeepsNonToolFlagsSortedOrdinal()
    {
        var cache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["service.b"] = true,
            ["analysis.a"] = false,
            ["tool.x.enabled"] = true,
        });
        var controller = Build(cache);

        var view = Assert.IsType<ViewResult>(controller.Index());
        var vm = Assert.IsType<AdminFlagsListViewModel>(view.Model);

        Assert.Equal(new[] { "analysis.a", "service.b" }, vm.Flags.Select(f => f.Key).ToArray());
    }

    [Fact]
    public void Index_DerivesGroupsFromListedRowsOnly()
    {
        var cache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["tool.x.enabled"] = true,
            ["tool.y.flag"] = false,
            ["service.a.enabled"] = true,
            ["service.b.enabled"] = true,
            ["analysis.m.x"] = true,
            ["analysis.m.y"] = true,
            ["analysis.z"] = true,
        });

        var view = Assert.IsType<ViewResult>(Build(cache).Index());
        var vm = Assert.IsType<AdminFlagsListViewModel>(view.Model);

        Assert.Equal(new[] { new FlagFilterGroup("analysis.", "Analysis", 3), new FlagFilterGroup("service.", "Background services", 2) }, vm.Groups);
        Assert.DoesNotContain(vm.Groups, group => group.Prefix.StartsWith("tool.", StringComparison.Ordinal));
        Assert.Equal(new[] { "analysis.m.x", "analysis.m.y", "analysis.z", "service.a.enabled", "service.b.enabled" }, vm.Flags.Select(flag => flag.Key));
    }

    private static AdminFlagsController Build(FakeFeatureFlagCache cache)
    {
        var controller = new AdminFlagsController(new FakeFeatureFlagStore(), cache);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
