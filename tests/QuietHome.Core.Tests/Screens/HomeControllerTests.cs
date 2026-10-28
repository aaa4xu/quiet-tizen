using System;
using System.Collections.Generic;
using System.Linq;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Screens;

/// <summary>
/// Tests of how <see cref="HomeController"/> works rather than what the owner sees: which
/// render mode it asks for, how often it reads the TV and what the diagnostics file says.
/// What the owner sees and does is in the Scenarios folder.
/// </summary>
public sealed class HomeControllerTests : IDisposable
{
    readonly HomeHarness home = new();

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary>
    /// Start draws the Home page once, after reading the inputs once. The first card is
    /// focused: the input on screen is looked up before the first reading, as before the
    /// refactoring; the Home request that follows a start moves the focus to it.
    /// </summary>
    [Fact]
    public void StartShowsHome()
    {
        home.Controller.Start();
        Assert.Equal(new[] { RenderMode.Rebuild }, home.View.Modes);
        Assert.Equal(1, home.Tv.Reads);
        Assert.Equal(0, home.Controller.Navigator.Selected);
        Assert.Equal(new[] { "Own 1", "Own 2", "Own 3" }, home.View.Preloaded.Select(a => a.Name));
        Assert.Contains("\nactivity=created\n", home.Tv.State);
        Assert.Contains("\nselected_name=Live TV\n", home.Tv.State);
        Assert.Contains("\nbuild_ms=7\n", home.Tv.State);
        Assert.Contains("\ninputs=0,14\n", home.Tv.State);
    }

    /// <summary>A Home request focuses the input on screen; a second one with nothing changed reads the inputs once and draws nothing.</summary>
    [Fact]
    public void HomeRequestWithoutChanges()
    {
        home.Open();
        Assert.Equal(1, home.Controller.Navigator.Selected);
        Assert.Equal(2, home.View.Modes.Count);
        Assert.Equal(2, home.Tv.Reads);
        home.Launch();
        Assert.Equal(2, home.View.Modes.Count);
        Assert.Equal(3, home.Tv.Reads);
        Assert.Contains("\nactivity=home_request\n", home.Tv.State);
    }

    /// <summary>A change notice redraws the Home page only when the inputs really changed; away from Home it reads nothing.</summary>
    [Fact]
    public void SourcesChangedRedrawsHome()
    {
        home.Controller.Start();
        home.Tv.RaiseChanged();
        Assert.Single(home.View.Modes);
        home.Tv.Inputs = new List<InputSource> { new() { ConnectionState = true, SourceType = 0 } };
        home.Tv.RaiseChanged();
        Assert.Equal(2, home.View.Modes.Count);
        Assert.Equal(new[] { "Live TV" }, home.InputCards);
        home.Launch(("quiet_page", "settings"));
        int reads = home.Tv.Reads;
        home.Tv.RaiseChanged();
        Assert.Equal(reads, home.Tv.Reads);
    }

    /// <summary>Moving within a row animates the focus; moving to another tab rebuilds that page, reading the inputs only on arriving at Home.</summary>
    [Fact]
    public void KeysMoveFocusAndPages()
    {
        home.Controller.Start();
        home.Press(RemoteKey.Right);
        Assert.Equal(RenderMode.AnimateFocus, home.View.Modes[^1]);
        Assert.Equal(1, home.Controller.Navigator.Selected);
        home.Press(RemoteKey.Up, RemoteKey.Right);
        Assert.Equal(RenderMode.Rebuild, home.View.Modes[^1]);
        Assert.IsType<AppsPageModel>(home.Screen.Content);
        int reads = home.Tv.Reads;
        home.Press(RemoteKey.Left);
        Assert.IsType<HomePageModel>(home.Screen.Content);
        Assert.Equal(reads + 1, home.Tv.Reads);
    }

    /// <summary>The clock ticks only after the first page; artwork events update the diagnostics file.</summary>
    [Fact]
    public void ClockAndArtwork()
    {
        home.Controller.Tick();
        Assert.Null(home.View.Clock);
        home.Controller.Start();
        home.Controller.Tick();
        Assert.Equal("6:05 PM", home.View.Clock);
        home.View.RaiseArtworkLoaded();
        Assert.Contains("\nactivity=artwork_ready\n", home.Tv.State);
    }

    /// <summary>Pausing ends an adjustment; resuming on the Settings page rereads and refreshes it without a rebuild.</summary>
    [Fact]
    public void PauseAndResume()
    {
        home.Controller.Start();
        home.Launch(("quiet_page", "settings"));
        home.Press(RemoteKey.Select);
        Assert.Equal(Adjustment.Volume, home.Controller.Navigator.Adjusting);
        home.Controller.Pause();
        Assert.Equal(Adjustment.None, home.Controller.Navigator.Adjusting);
        home.Tv.Volume = 33;
        home.Controller.Resume();
        Assert.Equal(RenderMode.Refresh, home.View.Modes[^1]);
        Assert.Equal("33", home.Tiles[0].Value);
    }

    /// <summary>After Terminate the controller no longer listens to the TV's input changes.</summary>
    [Fact]
    public void TerminateStopsListening()
    {
        home.Open();
        home.Controller.Terminate();
        int reads = home.Tv.Reads;
        home.Tv.RaiseChanged();
        Assert.Equal(reads, home.Tv.Reads);
    }
}
