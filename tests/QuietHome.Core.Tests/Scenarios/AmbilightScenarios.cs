using QuietHome.Core.Navigation;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the Home toolbar's Ambilight button, played on <see cref="HomeHarness"/>:
/// the button starts and stops the packaged sender and shows its state, a missing program
/// hides the button, and a failing start or stop is reported. The button is the last toolbar
/// card on the Home page, right of "Settings".
/// </summary>
public sealed class AmbilightScenarios : System.IDisposable
{
    readonly HomeHarness home = new();

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary>Up twice puts the focus on the toolbar's first button; four Rights reach Ambilight, the last one.</summary>
    void FocusAmbilight()
    {
        home.Open();
        home.Press(RemoteKey.Up, RemoteKey.Up);
        home.Press(RemoteKey.Right, RemoteKey.Right, RemoteKey.Right, RemoteKey.Right);
    }

    /// <summary>Select starts the sender, the label says on and the diagnostics record it; Select again stops it.</summary>
    [Fact]
    public void AmbilightButtonStartsAndStops()
    {
        FocusAmbilight();
        Assert.Equal("Ambilight off", home.Focused);
        home.Press(RemoteKey.Select);
        Assert.Equal(new[] { "restore", "start" }, home.Tv.AmbilightEvents);
        Assert.Equal("Ambilight on", home.Focused);
        Assert.Contains("\nambilight=on\n", home.Tv.State);
        home.Press(RemoteKey.Select);
        Assert.Equal(new[] { "restore", "start", "stop" }, home.Tv.AmbilightEvents);
        Assert.Equal("Ambilight off", home.Focused);
        Assert.Contains("\nambilight=off\n", home.Tv.State);
    }

    /// <summary>A saved "on" preference starts the sender when Quiet Home starts.</summary>
    [Fact]
    public void SavedOnPreferenceRestores()
    {
        home.Tv.AmbilightWantsOn = true;
        home.Open();
        Assert.True(home.Tv.AmbilightOn);
        Assert.Contains(home.Screen.Chrome.Toolbar, b => b.Name == "Ambilight on");
        Assert.Contains("\nambilight=on\n", home.Tv.State);
    }

    /// <summary>Without the packaged program the button is left out and the diagnostics say unavailable.</summary>
    [Fact]
    public void MissingProgramHidesTheButton()
    {
        home.Tv.AmbilightAvailable = false;
        home.Open();
        Assert.DoesNotContain(home.Screen.Chrome.Toolbar, b => b.Name.StartsWith("Ambilight"));
        Assert.Contains("\nambilight=unavailable\n", home.Tv.State);
    }

    /// <summary>quiet_ambilight=on and off start and stop the sender from a launch request, without the toolbar.</summary>
    [Fact]
    public void LaunchExtraStartsAndStops()
    {
        home.Open();
        home.Launch(("quiet_ambilight", "on"));
        Assert.True(home.Tv.AmbilightOn);
        Assert.Contains(home.Screen.Chrome.Toolbar, b => b.Name == "Ambilight on");
        home.Launch(("quiet_ambilight", "off"));
        Assert.False(home.Tv.AmbilightOn);
        Assert.Contains(home.Screen.Chrome.Toolbar, b => b.Name == "Ambilight off");
    }

    /// <summary>A failed start or stop is shown as a notice and leaves the label unchanged.</summary>
    [Fact]
    public void FailedToggleIsReported()
    {
        home.Tv.AmbilightStartFails = true;
        FocusAmbilight();
        home.Press(RemoteKey.Select);
        Assert.Equal("Ambilight could not be started.", home.View.Notice);
        Assert.Equal("Ambilight off", home.Focused);
        Assert.Contains("\nambilight=off\n", home.Tv.State);
        home.Tv.AmbilightStartFails = false;
        home.Press(RemoteKey.Select);
        Assert.Equal("Ambilight on", home.Focused);
        home.Tv.AmbilightStopFails = true;
        home.Press(RemoteKey.Select);
        Assert.Equal("Ambilight could not be stopped.", home.View.Notice);
        Assert.Equal("Ambilight on", home.Focused);
        Assert.Contains("\nambilight=on\n", home.Tv.State);
    }
}
