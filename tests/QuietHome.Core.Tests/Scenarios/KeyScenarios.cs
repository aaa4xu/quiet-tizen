using System;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the remote's special keys, played on <see cref="HomeHarness"/>: Back and
/// Exit on each page, the Home and Settings keys, and the filter that drops held-key repeats.
/// </summary>
public sealed class KeyScenarios : IDisposable
{
    readonly HomeHarness home = new();

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary>Back on Home returns to the input on screen, whichever card is focused.</summary>
    [Fact]
    public void BackOnHomeReturnsToTheInput()
    {
        home.Open();
        home.Press(RemoteKey.Down, RemoteKey.Back);
        Assert.Equal(new[] { 14 }, home.Tv.Switched);
        Assert.Equal(new[] { LaunchRequest.TvViewer }, home.Tv.Launched);
    }

    /// <summary>Back returns to the input chosen last in Quiet Home.</summary>
    [Fact]
    public void BackReturnsToTheLastChosenInput()
    {
        home.Open();
        home.Press(RemoteKey.Left, RemoteKey.Select);
        home.Launch();
        Assert.Equal("Live TV", home.Focused);
        home.Press(RemoteKey.Back);
        Assert.Equal(new[] { 0, 0 }, home.Tv.Switched);
    }

    /// <summary>Back and Exit on Settings return to the input too.</summary>
    [Fact]
    public void BackOnSettingsReturnsToTheInput()
    {
        home.Open();
        home.Launch(("quiet_page", "settings"));
        home.Press(RemoteKey.Back, RemoteKey.Exit);
        Assert.Equal(new[] { 14, 14 }, home.Tv.Switched);
    }

    /// <summary>Back and Exit on the Apps page show Home with the input on screen focused, without leaving Quiet Home.</summary>
    [Fact]
    public void BackOnAppsShowsHome()
    {
        home.Open();
        foreach (var key in new[] { RemoteKey.Back, RemoteKey.Exit })
        {
            home.Launch(("quiet_page", "apps"));
            home.Press(key);
            Assert.IsType<HomePageModel>(home.Screen.Content);
            Assert.Equal("Console", home.Focused);
        }
        Assert.Empty(home.Tv.Switched);
        Assert.Contains("\nactivity=apps_back\n", home.Tv.State);
    }

    /// <summary>Back, Exit and Select during an adjustment only end it.</summary>
    [Fact]
    public void BackDuringAnAdjustmentEndsIt()
    {
        home.Open();
        home.Launch(("quiet_page", "settings"));
        foreach (var key in new[] { RemoteKey.Back, RemoteKey.Exit, RemoteKey.Select })
        {
            home.Press(RemoteKey.Select);
            Assert.Equal(Adjustment.Volume, home.Controller.Navigator.Adjusting);
            Assert.Equal("Left / Right to adjust volume · Select or Back to finish", home.Screen.Chrome.Detail);
            home.Press(key);
            Assert.Equal(Adjustment.None, home.Controller.Navigator.Adjusting);
            Assert.Equal("Volume", home.Focused);
        }
        Assert.Empty(home.Tv.Switched);
    }

    /// <summary>The Home key shows Home with the input on screen focused, from any page and during an adjustment.</summary>
    [Fact]
    public void HomeKeyShowsHome()
    {
        home.Open();
        home.Launch(("quiet_page", "apps"));
        home.Press(RemoteKey.Right, RemoteKey.Home);
        Assert.Equal("Console", home.Focused);
        Assert.Contains("\nactivity=home_key\n", home.Tv.State);
        home.Launch(("quiet_page", "settings"));
        home.Press(RemoteKey.Select, RemoteKey.Home);
        Assert.Equal(Adjustment.None, home.Controller.Navigator.Adjusting);
        Assert.IsType<HomePageModel>(home.Screen.Content);
    }

    /// <summary>The Settings key opens Settings with Volume focused, from any page.</summary>
    [Fact]
    public void SettingsKeyOpensSettings()
    {
        home.Open();
        home.Press(RemoteKey.Settings);
        Assert.IsType<SettingsPageModel>(home.Screen.Content);
        Assert.Equal("Volume", home.Focused);
        Assert.Contains("\nactivity=settings_key\n", home.Tv.State);
        home.Press(RemoteKey.Right, RemoteKey.Settings);
        Assert.Equal("Volume", home.Focused);
    }

    /// <summary>A key repeated within 180 ms is dropped; another key, or the same one later, goes through.</summary>
    [Fact]
    public void HeldKeyRepeatsAreDropped()
    {
        home.Open();
        home.PressAfter(RemoteKey.Right, 1000);
        Assert.Equal("Live TV", home.Focused);
        home.PressAfter(RemoteKey.Right, 50);
        Assert.Equal("Live TV", home.Focused);
        home.PressAfter(RemoteKey.Left, 50);
        Assert.Equal("Console", home.Focused);
        home.PressAfter(RemoteKey.Left, KeyRepeatFilter.MinimumIntervalMs);
        Assert.Equal("Live TV", home.Focused);
    }
}
