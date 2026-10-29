using System;
using QuietHome.Core.Navigation;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the device state under the input cards (On, Standby, No signal), played
/// on <see cref="HomeHarness"/>: the host's poll and the TV's CEC notice update the line, the
/// poll touches nothing away from Home, and an input without a signal gets no preview. The
/// console (14) is on screen and has no HDMI port in its icon, so its line is the state alone.
/// </summary>
public sealed class InputPowerScenarios : IDisposable
{
    readonly HomeHarness home = new();

    /// <summary>Puts the console in rest mode: no signal, with a CEC device on its port.</summary>
    public InputPowerScenarios()
    {
        home.Tv.Inputs[1].PowerState = 2;
        home.Tv.CecInputs.Add(14);
    }

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary>Returns the line under the console's card on the last screen.</summary>
    string ConsoleLine() => ((HomePageModel)home.Screen.Content).Inputs[1].Text;

    /// <summary>A console in rest mode reads "Standby"; when it starts sending a picture, the next poll shows "On".</summary>
    [Fact]
    public void PollShowsTheConsoleTurningOn()
    {
        home.Open();
        Assert.Equal("Standby", ConsoleLine());
        home.Tv.Inputs[1].PowerState = 1;
        home.Controller.PollInputs();
        Assert.Equal("On", ConsoleLine());
        Assert.Contains("\nactivity=input_power\n", home.Tv.State);
        Assert.Contains("\ninput_states=0:Unknown,14:On\n", home.Tv.State);
    }

    /// <summary>A poll that finds nothing new neither redraws the page nor writes the diagnostics file.</summary>
    [Fact]
    public void QuietPollChangesNothing()
    {
        home.Open();
        int draws = home.View.Modes.Count;
        string state = home.Tv.State;
        home.Controller.PollInputs();
        Assert.Equal(draws, home.View.Modes.Count);
        Assert.Equal(state, home.Tv.State);
    }

    /// <summary>Away from the Home page the poll does not even read the inputs.</summary>
    [Fact]
    public void PollWaitsForHome()
    {
        home.Open();
        home.Launch(("quiet_page", "settings"));
        int reads = home.Tv.Reads;
        home.Controller.PollInputs();
        Assert.Equal(reads, home.Tv.Reads);
    }

    /// <summary>
    /// Without a CEC device the same signal loss reads "No signal"; the TV's CEC notice brings
    /// the device in, and the line turns to "Standby" without a poll.
    /// </summary>
    [Fact]
    public void CecNoticeTellsStandbyFromNoSignal()
    {
        home.Tv.CecInputs.Clear();
        home.Open();
        Assert.Equal("No signal", ConsoleLine());
        home.Tv.CecInputs.Add(14);
        home.Tv.RaiseCecChanged();
        Assert.Equal("Standby", ConsoleLine());
        Assert.Contains("\nactivity=cec_changed\n", home.Tv.State);
    }

    /// <summary>
    /// The input on screen gets no preview while its device sends nothing, so its card keeps the
    /// icon instead of black; once a picture arrives, the poll brings the preview.
    /// </summary>
    [Fact]
    public void NoPreviewWithoutSignal()
    {
        home.Open();
        Assert.Equal("Console", home.Focused);
        Assert.Equal("Standby", ConsoleLine());
        Assert.Null(home.Tv.Previewing);
        home.Tv.Inputs[1].PowerState = 1;
        home.Controller.PollInputs();
        Assert.Equal(14, home.Tv.Previewing);
        home.Press(RemoteKey.Right);
        home.Press(RemoteKey.Left);
        Assert.Equal(14, home.Tv.Previewing);
    }

    /// <summary>Demo mode ignores the TV's CEC devices: the sample inputs show only their own state.</summary>
    [Fact]
    public void DemoModeHasNoCecDevices()
    {
        home.Open();
        int reads = home.Tv.CecReads;
        home.Launch(("quiet_demo", "1"));
        Assert.Equal(reads, home.Tv.CecReads);
        Assert.Contains("\ninput_states=0:Unknown\n", home.Tv.State);
    }
}
