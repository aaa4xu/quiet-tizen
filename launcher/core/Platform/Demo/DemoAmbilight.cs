namespace QuietHome.Core.Platform.Demo;

/// <summary>
/// Demo mode's ambilight port: it reports the sender as installed and keeps its on/off state
/// in memory, so screenshots show the Home toolbar button without starting the real process
/// or changing the TV. <see cref="HomeController"/> uses it while demo mode is on.
/// </summary>
public sealed class DemoAmbilight : IAmbilight
{
    /// <summary>Demo mode always has the sender available, so the toolbar button is shown.</summary>
    public bool Available => true;

    /// <summary>The in-memory state; demo mode starts with the sender off.</summary>
    public bool IsOn { get; private set; }

    /// <summary>Turns the in-memory state on and reports success.</summary>
    public bool Start()
    {
        IsOn = true;
        return true;
    }

    /// <summary>Turns the in-memory state off and reports success.</summary>
    public bool Stop()
    {
        IsOn = false;
        return true;
    }

    /// <summary>Demo mode starts from its default state, so there is nothing to restore.</summary>
    public void Restore()
    {
    }
}
