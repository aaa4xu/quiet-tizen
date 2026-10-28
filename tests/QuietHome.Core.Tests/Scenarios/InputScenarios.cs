using System;
using System.Collections.Generic;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Fakes;
using QuietHome.Core.Tests.Screens;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the inputs on the Home page, played on <see cref="HomeHarness"/>: which
/// input is focused on opening, what happens when inputs are plugged in or out, and what
/// Select on an input does.
/// </summary>
public sealed class InputScenarios : IDisposable
{
    readonly HomeHarness home = new();

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary>Returns the two usual inputs plus HDMI 2 named "Soundbar" (15).</summary>
    static List<InputSource> ThreeInputs()
    {
        var inputs = ScreenBuilderTests.TwoInputs();
        inputs.Add(new() { ConnectionState = true, SourceType = 15, EditName = "Soundbar" });
        return inputs;
    }

    /// <summary>Opening Quiet Home focuses the input on screen.</summary>
    [Fact]
    public void OpeningFocusesTheInputOnScreen()
    {
        home.Open();
        Assert.IsType<HomePageModel>(home.Screen.Content);
        Assert.Equal("Console", home.Focused);
    }

    /// <summary>When the input on screen is not among the connected ones, the first input is focused.</summary>
    [Fact]
    public void OpeningOnAnUnlistedInputFocusesTheFirst()
    {
        home.Tv.CurrentSource = 16;
        home.Open();
        Assert.Equal("Live TV", home.Focused);
    }

    /// <summary>A newly plugged-in input appears on the Home page at once.</summary>
    [Fact]
    public void PluggedInInputAppearsOnHome()
    {
        home.Open();
        home.Tv.Inputs = ThreeInputs();
        home.Tv.RaiseChanged();
        Assert.Equal(new[] { "Live TV", "Console", "Soundbar" }, home.InputCards);
        Assert.Equal("Console", home.Focused);
    }

    /// <summary>An input plugged in while another page is open leaves that page alone and shows up on returning Home.</summary>
    [Fact]
    public void PluggedInInputWaitsForHome()
    {
        home.Open();
        home.Launch(("quiet_page", "settings"));
        int draws = home.View.Modes.Count;
        home.Tv.Inputs = ThreeInputs();
        home.Tv.RaiseChanged();
        Assert.Equal(draws, home.View.Modes.Count);
        Assert.IsType<SettingsPageModel>(home.Screen.Content);
        home.Press(RemoteKey.Home);
        Assert.Equal(new[] { "Live TV", "Console", "Soundbar" }, home.InputCards);
    }

    /// <summary>An input change during a volume adjustment neither redraws the page nor ends the adjustment.</summary>
    [Fact]
    public void InputChangeKeepsAnAdjustment()
    {
        home.Open();
        home.Launch(("quiet_page", "settings"));
        home.Press(RemoteKey.Select);
        int draws = home.View.Modes.Count;
        home.Tv.Inputs = ThreeInputs();
        home.Tv.RaiseChanged();
        Assert.Equal(draws, home.View.Modes.Count);
        Assert.Equal(Adjustment.Volume, home.Controller.Navigator.Adjusting);
        home.Press(RemoteKey.Right);
        Assert.Equal(16, home.Tv.Volume);
    }

    /// <summary>Select on an input switches the TV to it and opens the picture.</summary>
    [Fact]
    public void SelectingAnInputOpensItsPicture()
    {
        home.Open();
        home.Press(RemoteKey.Left, RemoteKey.Select);
        Assert.Equal(new[] { 0 }, home.Tv.Switched);
        Assert.Equal(new[] { LaunchRequest.TvViewer }, home.Tv.Launched);
        Assert.Equal("", home.View.Notice);
        Assert.Contains("\nactivity=launch:org.tizen.tv-viewer:0\n", home.Tv.State);
    }

    /// <summary>A switch the TV refuses shows a notice and opens nothing.</summary>
    [Fact]
    public void FailedSwitchShowsANotice()
    {
        home.Tv.SwitchResult = -1;
        home.Open();
        home.Press(RemoteKey.Select);
        Assert.Equal(new[] { 14 }, home.Tv.Switched);
        Assert.Empty(home.Tv.Launched);
        Assert.Equal("Input could not be switched. Open Advanced settings to check the connection.", home.View.Notice);
        Assert.Contains("\nactivity=source_failed:-1\n", home.Tv.State);
    }

    /// <summary>"Edit inputs", the first toolbar button on Home, opens Samsung's source editor.</summary>
    [Fact]
    public void EditInputsOpensTheSourceEditor()
    {
        home.Open();
        home.Press(RemoteKey.Up, RemoteKey.Up);
        Assert.Equal("Edit inputs", home.Focused);
        home.Press(RemoteKey.Select);
        Assert.Equal(new[] { LaunchRequest.SourceEditor }, home.Tv.Launched);
    }
}
