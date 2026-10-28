using System.Linq;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the owner's apps, played on <see cref="HomeHarness"/>: the dock on the
/// Home page, paging through the Apps page and an empty catalog.
/// </summary>
public sealed class CatalogScenarios
{
    /// <summary>A dock card opens its app, and "Your Apps" at the end of the dock opens the Apps page.</summary>
    [Fact]
    public void DockOpensAppsAndTheAppsPage()
    {
        using var home = new HomeHarness();
        home.Open();
        home.Press(RemoteKey.Down, RemoteKey.Select);
        Assert.Equal(new[] { new LaunchRequest("own.1") }, home.Tv.Launched);
        Assert.Contains("\nactivity=launch:own.1:0\n", home.Tv.State);
        home.Press(RemoteKey.Left);
        Assert.Equal("Your Apps", home.Focused);
        home.Press(RemoteKey.Select);
        Assert.IsType<AppsPageModel>(home.Screen.Content);
        Assert.Equal("Own 1", home.Focused);
    }

    /// <summary>Down from the last row opens the next Apps page and Up from the first row goes back; Select opens the app.</summary>
    [Fact]
    public void AppsPagesTurn()
    {
        using var home = new HomeHarness(ownApps: 20);
        home.Open();
        home.Launch(("quiet_page", "apps"));
        home.Press(RemoteKey.Right, RemoteKey.Down, RemoteKey.Down);
        Assert.Equal("Own 12", home.Focused);
        home.Press(RemoteKey.Down);
        Assert.Equal(RenderMode.Rebuild, home.View.Modes[^1]);
        Assert.Equal("2 / 2", ((AppsPageModel)home.Screen.Content).PagesText);
        Assert.Equal("Own 17", home.Focused);
        home.Press(RemoteKey.Up);
        Assert.Equal("1 / 2", ((AppsPageModel)home.Screen.Content).PagesText);
        Assert.Equal("Own 12", home.Focused);
        home.Press(RemoteKey.Select);
        Assert.Equal(new[] { new LaunchRequest("own.12") }, home.Tv.Launched);
    }

    /// <summary>An app that does not open is reported.</summary>
    [Fact]
    public void AppThatDoesNotOpenIsReported()
    {
        using var home = new HomeHarness();
        home.Tv.LaunchResult = new LaunchResult(false, 0);
        home.Open();
        home.Press(RemoteKey.Down, RemoteKey.Select);
        Assert.Equal("Could not open the app.", home.View.Notice);
    }

    /// <summary>
    /// With an empty catalog the Apps page reports it, the focus stays on the tabs, Up and
    /// Down do nothing and the Home dock holds only "Your Apps".
    /// </summary>
    [Fact]
    public void EmptyCatalog()
    {
        using var home = new HomeHarness(ownApps: 0);
        home.Open();
        Assert.Equal(new[] { "Your Apps" }, ((HomePageModel)home.Screen.Content).Dock.Select(d => d.Name));
        home.Launch(("quiet_page", "apps"));
        Assert.True(((AppsPageModel)home.Screen.Content).LoadFailed);
        Assert.Null(home.Focused);
        Assert.Equal(Zone.Tabs, home.Controller.Navigator.Zone);
        home.Press(RemoteKey.Down, RemoteKey.Up);
        Assert.Null(home.Focused);
        Assert.Equal("", home.View.Notice);
        Assert.Contains("\nlibrary_error=App library is empty.\n", home.Tv.State);
    }
}
