using System.Collections.Generic;
using QuietHome.Core.Navigation;
using Xunit;

namespace QuietHome.Core.Tests;

/// <summary>Tests of <see cref="LaunchOptions"/>, read from the extras of a launch request.</summary>
public class LaunchOptionsTests
{
    /// <summary>Reads options from a dictionary of extras, as the TV host reads them from a request.</summary>
    static LaunchOptions Parse(Dictionary<string, string> extras) =>
        LaunchOptions.Parse(key => extras.TryGetValue(key, out var value) ? value : null);

    /// <summary>A plain launch: no demo, no theme override, the Home page.</summary>
    [Fact]
    public void PlainLaunch()
    {
        var options = Parse(new());
        Assert.False(options.Demo);
        Assert.Null(options.Dark);
        Assert.Equal(Page.Home, options.Page);
    }

    /// <summary>The README screenshot recipe's request: demo mode, a theme and a page.</summary>
    [Fact]
    public void ScreenshotRequest()
    {
        var options = Parse(new() { ["quiet_demo"] = "1", ["quiet_theme"] = "light", ["quiet_page"] = "settings" });
        Assert.True(options.Demo);
        Assert.False(options.Dark);
        Assert.Equal(Page.Settings, options.Page);
        Assert.Equal(Page.Apps, Parse(new() { ["quiet_page"] = "apps" }).Page);
        Assert.True(Parse(new() { ["quiet_theme"] = "dark" }).Dark);
    }

    /// <summary>Unknown values are ignored: only "1" turns demo on, and an unknown theme or page changes nothing.</summary>
    [Fact]
    public void UnknownValues()
    {
        var options = Parse(new() { ["quiet_demo"] = "true", ["quiet_theme"] = "blue", ["quiet_page"] = "home" });
        Assert.False(options.Demo);
        Assert.Null(options.Dark);
        Assert.Equal(Page.Home, options.Page);
    }
}
