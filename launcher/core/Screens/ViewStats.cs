namespace QuietHome.Core.Screens;

/// <summary>
/// Numbers only the view knows, which <see cref="HomeController"/> writes to the diagnostics
/// file (<see cref="Diagnostics.StateReport"/>).
/// </summary>
/// <param name="FontLoaded">Whether the bundled fonts were registered.</param>
/// <param name="BuildMilliseconds">How long building the last page's elements took (<c>build_ms=</c>).</param>
/// <param name="ArtworkCached">How many artwork images exist.</param>
/// <param name="ArtworkReady">How many of them finished loading.</param>
/// <param name="ArtworkPending">The images still loading or failed, with their status.</param>
/// <param name="ArtworkViewsCreated">How many image views were ever created.</param>
/// <param name="PageArtworkCold">How many images on the last built page were not loaded yet.</param>
public readonly record struct ViewStats(
    bool FontLoaded,
    long BuildMilliseconds,
    int ArtworkCached,
    int ArtworkReady,
    string ArtworkPending,
    int ArtworkViewsCreated,
    int PageArtworkCold);
