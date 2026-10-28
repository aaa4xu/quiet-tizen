using System;
using System.Collections.Generic;
using QuietHome.Core.Catalog;
using QuietHome.Core.Screens;

namespace QuietHome.Core.Tests.Fakes;

/// <summary>
/// A test stand-in for the NUI view (launcher/tizen/Views/HomeView.cs): it implements the
/// core's <see cref="IHomeView"/> by recording what <see cref="HomeController"/> asks it to
/// show, so tests can check the screens, render modes, notices and clock without a TV.
/// Used through <see cref="HomeHarness"/>.
/// </summary>
sealed class FakeView : IHomeView
{
    /// <summary>Raised by <see cref="RaiseArtworkLoaded"/>.</summary>
    public event Action? ArtworkLoaded;

    /// <summary>Fixed numbers; build_ms is 7.</summary>
    public ViewStats Stats => new(true, 7, 0, 0, "", 0, 0);

    /// <summary>The render modes in order.</summary>
    public List<RenderMode> Modes { get; } = new();

    /// <summary>The last screen shown.</summary>
    public HomeScreen Last { get; private set; } = null!;

    /// <summary>The notice on screen.</summary>
    public string Notice { get; private set; } = "";

    /// <summary>The clock text set by a tick, or null.</summary>
    public string? Clock { get; private set; }

    /// <summary>The catalog passed to <see cref="Preload"/>.</summary>
    public List<LibraryApp> Preloaded { get; } = new();

    /// <summary>Records the catalog.</summary>
    public void Preload(IReadOnlyList<LibraryApp> library) => Preloaded.AddRange(library);

    /// <summary>Records the screen and mode; a rebuild clears the notice like the real view.</summary>
    public void Render(HomeScreen screen, RenderMode mode)
    {
        Last = screen;
        Modes.Add(mode);
        if (mode == RenderMode.Rebuild)
            Notice = "";
    }

    /// <summary>Records the notice.</summary>
    public void ShowNotice(string text) => Notice = text;

    /// <summary>Records the clock.</summary>
    public void ShowClock(string text) => Clock = text;

    /// <summary>Simulates an image that finished loading.</summary>
    public void RaiseArtworkLoaded() => ArtworkLoaded?.Invoke();
}
