namespace QuietHome.Core.Navigation;

/// <summary>
/// The part of the page that has focus. Every page has the same four zones from top to
/// bottom: toolbar buttons (top right), navigation tabs, the first row of cards and the
/// second row. The numbers are what the diagnostics file writes as <c>area=</c>.
/// </summary>
public enum Zone
{
    /// <summary>The Inputs / Apps / Settings tabs.</summary>
    Tabs = 0,

    /// <summary>
    /// Cards in row 0: the inputs on Home, the whole app grid on Apps (all of its rows),
    /// the top row of tiles on Settings.
    /// </summary>
    FirstRow = 1,

    /// <summary>Cards in row 1: the dock on Home, the bottom row of tiles on Settings. Apps has none.</summary>
    SecondRow = 2,

    /// <summary>The round toolbar buttons in the top right corner (row -1).</summary>
    Toolbar = 3,
}
