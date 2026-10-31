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

    /// <summary>
    /// Checks that the TV's viewer got the picture back right before the last input switch,
    /// with the sound still muted.
    /// </summary>
    void AssertReleasedBeforeLastSwitch()
    {
        int switched = home.Tv.Events.FindLastIndex(e => e.StartsWith("switch"));
        Assert.True(switched > 1);
        Assert.Equal("release", home.Tv.Events[switched - 2]);
        Assert.Equal("viewer", home.Tv.Events[switched - 1]);
    }

    /// <summary>
    /// With the console on screen, the owner opens HDMI 3 from its card: the sound stays muted
    /// through the switch and comes back only once Quiet Home is in the background, so the
    /// console is not heard before HDMI 3 shows.
    /// </summary>
    [Fact]
    public void AnotherInputOpensWithoutTheSoundOfTheInputBefore()
    {
        home.Open();
        home.Press(RemoteKey.Right);
        Assert.Equal("HDMI 3", home.Focused);
        int selected = home.Tv.Events.Count;
        home.Press(RemoteKey.Select);
        var opening = home.Tv.Events.GetRange(selected, home.Tv.Events.Count - selected);
        Assert.Equal(new[] { "release", "viewer", "switch 15", "release", "launch" }, opening);
        home.Controller.Pause();
        Assert.Equal("release+sound", home.Tv.Events[^1]);
    }

    /// <summary>
    /// While the port waits for the viewer before the switch, a notice that redraws the page
    /// asks for no preview (its hole would open again); the input opens once the port goes on.
    /// </summary>
    [Fact]
    public void NoPreviewWhileTheInputWaitsForTheViewer()
    {
        home.Open();
        home.Tv.HoldViewer = true;
        home.Press(RemoteKey.Select);
        Assert.Empty(home.Tv.Switched);
        int handed = home.Tv.Events.Count;
        home.Tv.RaiseChanged();
        home.Controller.PollInputs();
        Assert.DoesNotContain(home.Tv.Events.GetRange(handed, home.Tv.Events.Count - handed), e => e.StartsWith("preview"));
        home.Tv.PendingOpen!();
        Assert.Equal(14, home.Tv.Switched[^1]);
        Assert.Equal("launch", home.Tv.Events[^1]);
    }

    /// <summary>When the input cannot be switched, Quiet Home stays in front with the error and previews again.</summary>
    [Fact]
    public void FailedSwitchKeepsThePreview()
    {
        home.Open();
        home.Tv.SwitchResult = -1;
        home.Press(RemoteKey.Select);
        Assert.Contains("source_failed:-1", home.Tv.State);
        home.Press(RemoteKey.Right, RemoteKey.Left);
        Assert.Equal(14, home.Tv.Previewing);
    }

    /// <summary>
    /// Once an input is opening, no preview is asked for until Quiet Home is in front again,
    /// even when the TV's notices redraw the page: the console waking up as the TV switches to
    /// it, before and after Quiet Home goes to the background. A preview then would take the
    /// TV's picture back to Quiet Home's hidden window and the opened input would stay black.
    /// </summary>
    [Fact]
    public void NoPreviewWhileAnInputOpens()
    {
        home.Tv.CurrentSource = 15;
        home.Tv.Inputs[1].PowerState = 2;
        home.Tv.CecInputs.Add(14);
        home.Open();
        home.Press(RemoteKey.Left, RemoteKey.Select);
        Assert.Equal(14, home.Tv.Switched[^1]);
        int launched = home.Tv.Events.Count;
        home.Tv.Inputs[1].PowerState = 1;
        home.Tv.RaiseCecChanged();
        home.Controller.PollInputs();
        home.Controller.Pause();
        home.Tv.Inputs[2].PowerState = 2;
        home.Tv.RaiseChanged();
        Assert.Equal("On", ((HomePageModel)home.Screen.Content).Inputs[1].Text);
        Assert.DoesNotContain(home.Tv.Events.GetRange(launched, home.Tv.Events.Count - launched), e => e.StartsWith("preview"));
        home.Controller.Resume();
        Assert.Equal(14, home.Tv.Previewing);
    }

    /// <summary>
    /// When the TV switches the input by itself while Quiet Home is in front (a console's
    /// One Touch Play), the focus on the input cards moves to the new input's card, which
    /// gets the preview; the card focused before must not show the new input's picture.
    /// </summary>
    [Fact]
    public void FocusFollowsAnInputTheTvSwitchesTo()
    {
        home.Tv.CurrentSource = 15;
        home.Open();
        Assert.Equal("HDMI 3", home.Focused);
        Assert.Equal(15, home.Tv.Previewing);
        home.Tv.CurrentSource = 14;
        home.Controller.PollInputs();
        Assert.Equal("Console", home.Focused);
        Assert.Equal(14, home.Tv.Previewing);
        Assert.Equal(1, home.Tv.PreviewSlot);
        Assert.Contains("\nactivity=source_followed\n", home.Tv.State);
    }

    /// <summary>
    /// Away from the input cards (on the dock) the focus stays where it is when the TV
    /// switches the input; the input row then leads to the new input's card.
    /// </summary>
    [Fact]
    public void DockKeepsItsFocusWhenTheTvSwitchesTheInput()
    {
        home.Tv.CurrentSource = 15;
        home.Open();
        home.Press(RemoteKey.Down);
        string? onDock = home.Focused;
        home.Tv.CurrentSource = 14;
        home.Tv.RaiseChanged();
        Assert.Equal(onDock, home.Focused);
        Assert.Null(home.Tv.Previewing);
        home.Launch();
        Assert.Equal("Console", home.Focused);
        Assert.Equal(14, home.Tv.Previewing);
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
