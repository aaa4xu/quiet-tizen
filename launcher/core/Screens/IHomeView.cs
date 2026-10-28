using System;
using System.Collections.Generic;
using QuietHome.Core.Catalog;

namespace QuietHome.Core.Screens;

/// <summary>
/// What draws Quiet Home. <see cref="HomeController"/> calls it with a complete
/// <see cref="HomeScreen"/> after every change; the TV implementation draws with NUI
/// (launcher/tizen/Views/HomeView.cs) and tests use a recording stand-in. The view keeps no
/// state of its own beyond what it needs to draw: artwork, the built elements and timings.
/// </summary>
public interface IHomeView
{
    /// <summary>Raised when an artwork image finished loading (or failed), so the diagnostics file can be updated.</summary>
    event Action? ArtworkLoaded;

    /// <summary>Numbers for the diagnostics file: fonts, page build time and artwork counts.</summary>
    ViewStats Stats { get; }

    /// <summary>Starts loading the artwork of <paramref name="library"/> at start, so the Home and Apps pages appear with it.</summary>
    void Preload(IReadOnlyList<LibraryApp> library);

    /// <summary>
    /// Shows <paramref name="screen"/>. <see cref="RenderMode.Rebuild"/> draws the page from
    /// scratch; the other modes update the focus and values of the page already drawn, which
    /// must have the same elements.
    /// </summary>
    void Render(HomeScreen screen, RenderMode mode);

    /// <summary>Shows a message above the bottom lines, or clears it with "". A rebuilt page starts without one.</summary>
    void ShowNotice(string text);

    /// <summary>Changes the clock text of the page on screen.</summary>
    void ShowClock(string text);
}
