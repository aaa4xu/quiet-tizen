namespace QuietHome.Core.Navigation;

/// <summary>
/// The three pages of Quiet Home, in the order of the navigation tabs. The numbers are the
/// tab positions and also what the diagnostics file writes as <c>page=</c>.
/// </summary>
public enum Page
{
    /// <summary>Inputs and the app dock (tab "Inputs", title "Home").</summary>
    Home = 0,

    /// <summary>The full app catalog in a grid (tab "Apps", title "Your Apps").</summary>
    Apps = 1,

    /// <summary>Quick settings tiles (tab "Settings", title "Quick Settings").</summary>
    Settings = 2,
}
