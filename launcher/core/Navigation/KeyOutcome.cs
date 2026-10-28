namespace QuietHome.Core.Navigation;

/// <summary>
/// What the TV host must do after <see cref="Navigator.HandleKey"/> has updated the focus.
/// The navigator only changes its own state; drawing, launching and switching inputs are
/// the host's work, and this value tells it which one.
/// </summary>
public enum KeyOutcome
{
    /// <summary>Nothing to do.</summary>
    Ignored,

    /// <summary>Focus moved within the page: animate the focus to the new card.</summary>
    Refocus,

    /// <summary>The page or the Apps page number changed: draw the page again.</summary>
    Rebuild,

    /// <summary>Perform the focused card's <see cref="Actions.HomeAction"/>.</summary>
    Activate,

    /// <summary>The Home key: show the Home page with focus on the current input.</summary>
    GoHome,

    /// <summary>Back on the Apps page: return to the Home page.</summary>
    LeaveApps,

    /// <summary>The settings key: show the Settings page.</summary>
    OpenSettings,

    /// <summary>Back elsewhere: leave Quiet Home for the input the TV was showing.</summary>
    ReturnToInput,

    /// <summary>Lower the setting being adjusted by one step.</summary>
    AdjustDown,

    /// <summary>Raise the setting being adjusted by one step.</summary>
    AdjustUp,
}
