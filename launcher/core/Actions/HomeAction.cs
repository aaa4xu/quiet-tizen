using QuietHome.Core.Navigation;

namespace QuietHome.Core.Actions;

/// <summary>
/// What happens when the owner presses Select on a card, tile or toolbar button. Each card
/// the TV host draws carries one of these; on Select the host performs it (switches the
/// input, launches an app, changes a setting). Keeping the actions as data instead of
/// strings lets the core describe pages and lets tests check which action a card has.
/// </summary>
public abstract record HomeAction
{
    /// <summary>Switch the TV to input <paramref name="SourceType"/> and show its picture.</summary>
    public sealed record SwitchInput(int SourceType) : HomeAction;

    /// <summary>Show one of Quiet Home's pages.</summary>
    public sealed record ShowPage(Page Page) : HomeAction;

    /// <summary>Launch the app with Tizen ID <paramref name="AppId"/>.</summary>
    public sealed record LaunchApp(string AppId) : HomeAction;

    /// <summary>
    /// Open Samsung's own settings app (<c>org.tizen.menu</c>); with
    /// <paramref name="MenuItem"/> it opens straight on that screen, for example
    /// "networksettings".
    /// </summary>
    public sealed record OpenSamsungSettings(string? MenuItem) : HomeAction;

    /// <summary>Open Samsung Home's source list, where inputs can be renamed.</summary>
    public sealed record OpenSourceEditor : HomeAction;

    /// <summary>Start adjusting volume or brightness with Left and Right.</summary>
    public sealed record BeginAdjust(Adjustment Target) : HomeAction;

    /// <summary>Move game mode to its next value (see <see cref="Settings.QuickSettingsState.NextGameModeValue"/>).</summary>
    public sealed record SwitchGameMode : HomeAction;

    /// <summary>Move a TV setting with several choices (sleep timer) to its next choice.</summary>
    public sealed record CycleSetting(string Id) : HomeAction;

    /// <summary>Switch between the dark and the light theme and save the choice.</summary>
    public sealed record ToggleTheme : HomeAction;
}
