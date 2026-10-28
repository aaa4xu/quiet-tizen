using System;
using QuietHome.Core.Navigation;

namespace QuietHome.Core.Diagnostics;

/// <summary>
/// The content of the diagnostics file <c>/tmp/quiet-home-state.txt</c>: one
/// <c>key=value</c> line per value after a timestamp. Tools on the computer read it over SDB
/// (tools/screens.py, tools/screen_compare.py, recipes), so the keys and their order must not
/// change. The TV host fills one after every change and writes <see cref="Format"/> through
/// <see cref="Platform.IDiagnostics"/>.
/// </summary>
public sealed class StateReport
{
    /// <summary>Line <c>page=</c>: the page on screen, as a number (0 Home, 1 Apps, 2 Settings).</summary>
    public Page Page { get; set; }

    /// <summary>Line <c>selected=</c>: the focused slot.</summary>
    public int Selected { get; set; }

    /// <summary>Line <c>area=</c>: the zone with focus, as a number (0 tabs, 1 first row, 2 second row, 3 toolbar).</summary>
    public Zone Zone { get; set; }

    /// <summary>Line <c>activity=</c>: what just happened, for example "page_ready" or "setting:sleeptimer:1:True".</summary>
    public string Activity { get; set; } = "";

    /// <summary>Line <c>key=</c>: the TV's name of the last accepted key.</summary>
    public string Key { get; set; } = "";

    /// <summary>Line <c>font_loaded=</c>: whether the bundled fonts were registered.</summary>
    public bool FontLoaded { get; set; }

    /// <summary>Line <c>theme=</c>: true writes "dark", false "light".</summary>
    public bool Dark { get; set; }

    /// <summary>Line <c>theme_error=</c>: why the theme could not be saved, or "".</summary>
    public string ThemeError { get; set; } = "";

    /// <summary>Line <c>demo=</c>: whether demo mode is on.</summary>
    public bool Demo { get; set; }

    /// <summary>Line <c>build_ms=</c>: how long the last page took to build, in milliseconds.</summary>
    public long BuildMilliseconds { get; set; }

    /// <summary>Line <c>library_page=</c>: the open Apps page, 0-based.</summary>
    public int LibraryPage { get; set; }

    /// <summary>Line <c>library_count=</c>: the number of catalog entries.</summary>
    public int LibraryCount { get; set; }

    /// <summary>Line <c>library_error=</c>: why the catalog could not be loaded, or "".</summary>
    public string LibraryError { get; set; } = "";

    /// <summary>Line <c>inputs=</c>: the source types of the input cards, comma-separated.</summary>
    public string Inputs { get; set; } = "";

    /// <summary>Line <c>input_error=</c>: why the input list could not be read, or "".</summary>
    public string InputError { get; set; } = "";

    /// <summary>Line <c>source_callback=</c>: see <see cref="Platform.IInputSources.WatchStatus"/>.</summary>
    public int SourceCallback { get; set; }

    /// <summary>Line <c>selected_name=</c>: the name of the focused slot, or "".</summary>
    public string SelectedName { get; set; } = "";

    /// <summary>Line <c>artwork_cached=</c>: how many artwork images exist.</summary>
    public int ArtworkCached { get; set; }

    /// <summary>Line <c>artwork_ready=</c>: how many of them finished loading.</summary>
    public int ArtworkReady { get; set; }

    /// <summary>Line <c>artwork_pending=</c>: the images still loading or failed, with their status.</summary>
    public string ArtworkPending { get; set; } = "";

    /// <summary>Line <c>artwork_views_created=</c>: how many image views were ever created.</summary>
    public int ArtworkViewsCreated { get; set; }

    /// <summary>Line <c>page_artwork_cold=</c>: how many images on the last built page were not loaded yet.</summary>
    public int PageArtworkCold { get; set; }

    /// <summary>Line <c>process_id=</c>: the app's process ID.</summary>
    public int ProcessId { get; set; }

    /// <summary>Line <c>editing=</c>: the setting being adjusted, see <see cref="AdjustmentNames.Name"/>.</summary>
    public Adjustment Editing { get; set; }

    /// <summary>Line <c>volume=</c>: the master volume, -1 when unknown.</summary>
    public int Volume { get; set; }

    /// <summary>Line <c>brightness=</c>: the raw brightness value, or "".</summary>
    public string Brightness { get; set; } = "";

    /// <summary>Line <c>game-mode=</c>: the raw value of the game mode item on the Settings page, or "".</summary>
    public string GameMode { get; set; } = "";

    /// <summary>Line <c>sleeptimer=</c>: the raw sleep timer value, or "".</summary>
    public string SleepTimer { get; set; } = "";

    /// <summary>
    /// Returns the file's text: <paramref name="utcNow"/> in round-trip format, then every
    /// line in the fixed order, each ending with a newline. Booleans read "True" / "False".
    /// </summary>
    public string Format(DateTime utcNow) =>
        utcNow.ToString("o")
        + "\npage=" + (int)Page
        + "\nselected=" + Selected
        + "\narea=" + (int)Zone
        + "\nactivity=" + Activity
        + "\nkey=" + Key
        + "\nfont_loaded=" + FontLoaded
        + "\nstyle=free-my-tv"
        + "\ntheme=" + (Dark ? "dark" : "light")
        + "\ntheme_error=" + ThemeError
        + "\ndemo=" + Demo
        + "\nbuild_ms=" + BuildMilliseconds
        + "\nlibrary_page=" + LibraryPage
        + "\nlibrary_count=" + LibraryCount
        + "\nlibrary_error=" + LibraryError
        + "\ninputs=" + Inputs
        + "\ninput_error=" + InputError
        + "\nsource_callback=" + SourceCallback
        + "\nselected_name=" + SelectedName
        + "\nartwork_cached=" + ArtworkCached
        + "\nartwork_ready=" + ArtworkReady
        + "\nartwork_pending=" + ArtworkPending
        + "\nartwork_views_created=" + ArtworkViewsCreated
        + "\npage_artwork_cold=" + PageArtworkCold
        + "\nprocess_id=" + ProcessId
        + "\nediting=" + Editing.Name()
        + "\nvolume=" + Volume
        + "\nbrightness=" + Brightness
        + "\ngame-mode=" + GameMode
        + "\nsleeptimer=" + SleepTimer
        + "\n";
}
