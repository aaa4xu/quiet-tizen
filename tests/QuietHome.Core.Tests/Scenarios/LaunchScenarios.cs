using System;
using System.Linq;
using QuietHome.Core.Navigation;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the launch extras (<c>quiet_page</c>, <c>quiet_focus</c>, <c>quiet_demo</c>,
/// <c>quiet_theme</c>) that QuickLaunch and the README screenshot recipe send, played on
/// <see cref="HomeHarness"/>.
/// </summary>
public sealed class LaunchScenarios : IDisposable
{
    readonly HomeHarness home = new();

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary><c>quiet_page</c> opens the Apps or Settings page with its first card focused; a launch without it shows Home.</summary>
    [Fact]
    public void PageExtraOpensThatPage()
    {
        home.Open();
        home.Launch(("quiet_page", "apps"));
        Assert.IsType<AppsPageModel>(home.Screen.Content);
        Assert.Equal("Own 1", home.Focused);
        home.Launch(("quiet_page", "settings"));
        Assert.Equal("Volume", home.Focused);
        Assert.Contains("\nactivity=settings_request\n", home.Tv.State);
        home.Launch();
        Assert.IsType<HomePageModel>(home.Screen.Content);
        Assert.Equal("Console", home.Focused);
    }

    /// <summary>
    /// <c>quiet_focus=toolbar</c> focuses the page's first toolbar button, which the remote
    /// leaves as usual; the next launch without it focuses the page's cards again.
    /// </summary>
    [Fact]
    public void FocusExtraFocusesTheToolbar()
    {
        home.Open();
        home.Launch(("quiet_focus", "toolbar"));
        Assert.Equal("Edit inputs", home.Focused);
        Assert.Equal("Edit inputs", home.Screen.Chrome.ToolbarHint);
        Assert.Contains("\nactivity=toolbar_request\n", home.Tv.State);
        home.Press(RemoteKey.Right);
        Assert.Equal("Apps", home.Focused);
        home.Press(RemoteKey.Down);
        Assert.Equal("Live TV", home.Focused);
        home.Launch(("quiet_page", "settings"), ("quiet_focus", "toolbar"));
        Assert.Equal("Apps", home.Focused);
        home.Launch();
        Assert.Equal("Console", home.Focused);
    }

    /// <summary>Demo mode shows the public catalog, sample inputs, sample settings and 9:41, changes nothing on the TV, and ends with the next launch.</summary>
    [Fact]
    public void DemoModeLeavesTheTvAlone()
    {
        home.Open();
        home.Launch(("quiet_demo", "1"), ("quiet_page", "settings"));
        Assert.True(home.Controller.Demo);
        Assert.Equal("9:41 AM", home.Screen.Chrome.Clock);
        Assert.Equal(new[] { "20", "45", "Off", "Off" }, home.Tiles.Take(4).Select(t => t.Value));
        home.Press(RemoteKey.Right, RemoteKey.Right, RemoteKey.Select);
        Assert.Equal("Auto", home.Tiles[2].Value);
        home.Press(RemoteKey.Left, RemoteKey.Left, RemoteKey.Down);
        Assert.Equal("Theme", home.Focused);
        home.Press(RemoteKey.Select);
        Assert.False(home.Screen.Dark);
        Assert.Empty(home.Tv.Written);
        Assert.Null(home.Tv.Theme);
        home.Launch(("quiet_demo", "1"));
        Assert.Equal(new[] { "Live TV" }, home.InputCards);
        Assert.Equal(new[] { "Public 1", "Public 2", "Your Apps" }, ((HomePageModel)home.Screen.Content).Dock.Select(d => d.Name));
        home.Launch();
        Assert.False(home.Controller.Demo);
        Assert.True(home.Screen.Dark);
        Assert.Equal(new[] { "Live TV", "Console" }, home.InputCards);
        Assert.Equal(new[] { "Own 1", "Own 2", "Own 3", "Your Apps" }, ((HomePageModel)home.Screen.Content).Dock.Select(d => d.Name));
    }

    /// <summary><c>quiet_theme</c> changes the theme without saving it; leaving demo mode brings back the saved theme.</summary>
    [Fact]
    public void ThemeExtraIsNotSaved()
    {
        home.Open();
        home.Launch(("quiet_theme", "light"));
        Assert.False(home.Screen.Dark);
        Assert.Null(home.Tv.Theme);
        home.Launch(("quiet_demo", "1"), ("quiet_theme", "dark"));
        Assert.True(home.Screen.Dark);
        home.Launch(("quiet_demo", "1"), ("quiet_theme", "light"));
        Assert.False(home.Screen.Dark);
        home.Launch();
        Assert.True(home.Screen.Dark);
        Assert.Null(home.Tv.Theme);
    }
}
