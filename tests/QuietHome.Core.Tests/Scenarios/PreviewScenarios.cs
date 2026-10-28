using System;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the input preview (<see cref="Platform.IInputPreview"/>): which focus
/// shows the live picture of the input on screen in its card, when it is put away and when
/// the input's sound is muted, played with the remote on <see cref="HomeHarness"/>. They
/// check what was asked of the TV, not the picture.
/// </summary>
public sealed class PreviewScenarios : IDisposable
{
    readonly HomeHarness home = new();

    /// <summary>Adds a third input, "HDMI 3" (15), after Live TV and the console.</summary>
    public PreviewScenarios() =>
        home.Tv.Inputs.Add(new InputSource { ConnectionState = true, SourceType = 15, IconPath = "/icons/i_icon_device_m_hdmi3.svg" });

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary>
    /// Only the card of the input on screen gets the preview: another HDMI card and Live TV
    /// get none, so the preview is put away there and comes back on the card.
    /// </summary>
    [Fact]
    public void OnlyTheInputOnScreenIsPreviewed()
    {
        home.Open();
        Assert.Equal("Console", home.Focused);
        Assert.Equal(14, home.Tv.Previewing);
        Assert.Equal(1, home.Tv.PreviewSlot);
        Assert.Contains("\npreview=on\n", home.Tv.State);
        home.Press(RemoteKey.Right);
        Assert.Equal("HDMI 3", home.Focused);
        Assert.Null(home.Tv.Previewing);
        Assert.Contains("\npreview=off\n", home.Tv.State);
        home.Press(RemoteKey.Left);
        Assert.Equal(14, home.Tv.Previewing);
        home.Press(RemoteKey.Left);
        Assert.Equal("Live TV", home.Focused);
        Assert.Null(home.Tv.Previewing);
        Assert.DoesNotContain("preview 15", home.Tv.Events);
    }

    /// <summary>When the TV shows another input after Quiet Home comes back, that input's card gets the preview.</summary>
    [Fact]
    public void PreviewFollowsTheInputOnScreen()
    {
        home.Open();
        home.Controller.Pause();
        home.Tv.CurrentSource = 15;
        home.Controller.Resume();
        home.Launch();
        Assert.Equal("HDMI 3", home.Focused);
        Assert.Equal(15, home.Tv.Previewing);
        Assert.Equal(2, home.Tv.PreviewSlot);
    }

    /// <summary>Quiet Home mutes the input's sound whenever it comes to the front, outside demo mode, and never turns it back on.</summary>
    [Fact]
    public void HomeScreenMutesTheInput()
    {
        home.Open();
        Assert.Contains("silence", home.Tv.Events);
        home.Controller.Pause();
        int before = home.Tv.Events.FindAll(e => e == "silence").Count;
        home.Controller.Resume();
        Assert.True(home.Tv.Events.FindAll(e => e == "silence").Count > before);
        home.Launch(("quiet_demo", "1"));
        int inDemo = home.Tv.Events.FindAll(e => e == "silence").Count;
        home.Controller.Pause();
        home.Controller.Resume();
        Assert.Equal(inDemo, home.Tv.Events.FindAll(e => e == "silence").Count);
    }

    /// <summary>
    /// Moving to the dock, the toolbar or another page puts the preview away; coming back to
    /// the card shows it again. On the Home tab the card stays wide with its preview.
    /// </summary>
    [Fact]
    public void LeavingTheInputsPutsThePreviewAway()
    {
        home.Open();
        home.Press(RemoteKey.Down);
        Assert.Null(home.Tv.Previewing);
        home.Press(RemoteKey.Up, RemoteKey.Right);
        Assert.Equal("Console", home.Focused);
        Assert.Equal(14, home.Tv.Previewing);
        home.Press(RemoteKey.Up);
        Assert.Null(home.Focused);
        Assert.Equal(14, home.Tv.Previewing);
        home.Press(RemoteKey.Up);
        Assert.Null(home.Tv.Previewing);
        home.Launch(("quiet_page", "settings"));
        Assert.Null(home.Tv.Previewing);
        home.Launch();
        Assert.Equal(14, home.Tv.Previewing);
    }

    /// <summary>
    /// Opening an input or app, or going to the background, gives the TV its picture and
    /// sound back first, ending the preview.
    /// </summary>
    [Fact]
    public void PreviewIsPutAwayBeforeTheTvChangesThePicture()
    {
        home.Open();
        home.Press(RemoteKey.Select);
        AssertReleasedBeforeLastSwitch();
        Assert.Equal("launch", home.Tv.Events[^1]);
        Assert.Contains("\npreview=off\n", home.Tv.State);
        home.Controller.Pause();
        home.Controller.Resume();
        Assert.Equal(14, home.Tv.Previewing);
        home.Controller.Pause();
        Assert.Null(home.Tv.Previewing);
        Assert.Equal("release+sound", home.Tv.Events[^1]);
        home.Controller.Resume();
        home.Press(RemoteKey.Back);
        AssertReleasedBeforeLastSwitch();
    }

    /// <summary>Checks that the TV got its picture and sound back right before the last input switch.</summary>
    void AssertReleasedBeforeLastSwitch()
    {
        int switched = home.Tv.Events.FindLastIndex(e => e.StartsWith("switch"));
        Assert.True(switched > 0);
        Assert.Equal("release+sound", home.Tv.Events[switched - 1]);
    }

    /// <summary>Opening an app gives the picture back but keeps the sound muted until Quiet Home goes to the background.</summary>
    [Fact]
    public void AppStartsWithoutTheInputsSound()
    {
        home.Open();
        home.Press(RemoteKey.Down, RemoteKey.Select);
        Assert.Equal(new[] { "release", "launch" }, home.Tv.Events.GetRange(home.Tv.Events.Count - 2, 2));
        home.Controller.Pause();
        Assert.Equal("release+sound", home.Tv.Events[^1]);
    }

    /// <summary>
    /// After Quiet Home opened an app the card of the input on screen keeps its icon (the TV
    /// plays no picture then); opening the input brings the preview back.
    /// </summary>
    [Fact]
    public void NoPreviewAfterAnAppUntilTheInputIsOpened()
    {
        home.Open();
        home.Press(RemoteKey.Down, RemoteKey.Select);
        home.Controller.Pause();
        home.Controller.Resume();
        home.Launch();
        Assert.Equal("Console", home.Focused);
        Assert.True(((HomePageModel)home.Screen.Content).Inputs[1].Expanded);
        Assert.Null(home.Tv.Previewing);
        home.Press(RemoteKey.Select);
        home.Controller.Pause();
        home.Controller.Resume();
        home.Launch();
        Assert.Equal(14, home.Tv.Previewing);
    }

    /// <summary>
    /// Arriving at Home through the tabs, the card of the input on screen is wide with its
    /// preview while the focus is on the tab, and Down focuses that card.
    /// </summary>
    [Fact]
    public void HomeTabShowsTheInputOnScreen()
    {
        home.Open();
        home.Launch(("quiet_page", "settings"));
        home.Press(RemoteKey.Up);
        Assert.Null(home.Focused);
        home.Press(RemoteKey.Left, RemoteKey.Left);
        var inputs = ((HomePageModel)home.Screen.Content).Inputs;
        Assert.True(inputs[1].Expanded);
        Assert.False(inputs[0].Expanded);
        Assert.Equal(14, home.Tv.Previewing);
        home.Press(RemoteKey.Down);
        Assert.Equal("Console", home.Focused);
        Assert.Equal(14, home.Tv.Previewing);
    }

    /// <summary>After the TV refuses a preview, none is asked for again in this session and the state says so.</summary>
    [Fact]
    public void RefusedPreviewIsNotRetried()
    {
        home.Tv.PreviewFails = true;
        home.Open();
        Assert.Contains("\npreview=unsupported\n", home.Tv.State);
        int asked = home.Tv.Events.FindAll(e => e.StartsWith("preview")).Count;
        home.Press(RemoteKey.Right, RemoteKey.Left);
        Assert.Equal("Console", home.Focused);
        Assert.Equal(asked, home.Tv.Events.FindAll(e => e.StartsWith("preview")).Count);
        Assert.Contains("\npreview=unsupported\n", home.Tv.State);
    }

    /// <summary>Demo mode never asks for a preview, so README screenshots show the cards as drawn.</summary>
    [Fact]
    public void DemoModeHasNoPreview()
    {
        home.Open();
        home.Launch(("quiet_demo", "1"));
        Assert.Null(home.Tv.Previewing);
        home.Press(RemoteKey.Right);
        Assert.Null(home.Tv.Previewing);
        home.Launch();
        Assert.Equal(14, home.Tv.Previewing);
    }
}
