using System.Collections.Generic;
using QuietHome.Core.Catalog;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Settings;

namespace QuietHome.Core.Screens;

/// <summary>
/// The part of <see cref="HomeController"/>'s state that decides what is on screen.
/// The controller passes it to <see cref="ScreenBuilder.Build"/>; tests create it directly.
/// </summary>
/// <param name="Page">The page on screen.</param>
/// <param name="Zone">The zone with focus.</param>
/// <param name="Selected">The focused slot (see <see cref="Navigator.Selected"/>).</param>
/// <param name="LibraryPage">The open Apps page, 0-based.</param>
/// <param name="Adjusting">The setting being adjusted, if any.</param>
/// <param name="Dark">True for the dark theme.</param>
/// <param name="Inputs">The connected inputs.</param>
/// <param name="Library">The catalog in use (the owner's, or the public one in demo mode).</param>
/// <param name="Settings">The latest quick settings values.</param>
/// <param name="Clock">The clock text.</param>
public sealed record ScreenState(
    Page Page,
    Zone Zone,
    int Selected,
    int LibraryPage,
    Adjustment Adjusting,
    bool Dark,
    IReadOnlyList<InputSource> Inputs,
    IReadOnlyList<LibraryApp> Library,
    QuickSettingsState Settings,
    string Clock);
