using System.Collections.Generic;
using QuietHome.Core.Navigation;

namespace QuietHome.Core.Screens;

/// <summary>
/// Everything around the page content that every page has: the title, the three tabs, the
/// clock, the toolbar and the text lines at the bottom. <see cref="ScreenBuilder"/> fills it;
/// the TV view draws it the same way on every page.
/// </summary>
/// <param name="Title">The big title, for example "Your Apps".</param>
/// <param name="Page">The page on screen; its tab is drawn as the current one.</param>
/// <param name="TabsFocused">True when the focus is on the tabs, so the current tab gets a highlight.</param>
/// <param name="Clock">The clock text.</param>
/// <param name="ClockVisible">False while the toolbar has focus: the toolbar hint takes the clock's place.</param>
/// <param name="ToolbarHint">The focused toolbar button's name, or "".</param>
/// <param name="Detail">The line at the bottom left: the focused element, the page's purpose or the adjusting hint.</param>
/// <param name="Hint">The line at the bottom right: which keys do what on this page.</param>
/// <param name="Toolbar">The toolbar buttons from left to right.</param>
public sealed record ChromeModel(
    string Title,
    Page Page,
    bool TabsFocused,
    string Clock,
    bool ClockVisible,
    string ToolbarHint,
    string Detail,
    string Hint,
    IReadOnlyList<ToolbarButtonModel> Toolbar)
{
    /// <summary>The tab labels, in page order.</summary>
    public static IReadOnlyList<string> TabNames { get; } = new[] { "Inputs", "Apps", "Settings" };
}
