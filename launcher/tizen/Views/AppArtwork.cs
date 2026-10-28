using System;
using QuietHome.Core.Catalog;
using QuietHome.Core.Theme;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// Fills an app card, on the Apps page or in the dock, with what the catalog asks for: the
/// cached artwork, the app's name on placeholder artwork, or the cross of the utility card.
/// </summary>
static class AppArtwork
{
    /// <summary>
    /// Adds <paramref name="app"/>'s content to <paramref name="card"/> of
    /// <paramref name="w"/> × <paramref name="h"/>. Returns true when its artwork image was
    /// not loaded yet, for the diagnostics file's <c>page_artwork_cold=</c>.
    /// </summary>
    public static bool Fill(View card, LibraryApp app, int w, int h, ArtworkCache artwork, ViewPalette palette)
    {
        bool cold = !app.Utility && artwork.AttachTo(card, app, w, h);
        // Placeholders carry no text (the TV's SVG renderer draws none), so the name goes on top.
        if (app.Asset.StartsWith("placeholders/"))
        {
            var ink = ColorMath.DarkText(Rgba.FromHex(app.Background)) ? ViewPalette.ToColor(ColorMath.PlaceholderInk) : ViewPalette.White();
            var name = Scene.Label(app.Name, new Box(16, 0, w - 32, h), (float)Math.Round(h * .17), ink, "Semibold");
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.MultiLine = true;
            card.Add(name);
        }
        // The utility card has no artwork; its cross is drawn in ink, so it follows the theme.
        if (app.Utility)
        {
            card.Add(new View { Position2D = new Position2D(w / 2 - 28, h / 2 - 2), Size2D = new Size2D(56, 4), BackgroundColor = palette.Ink });
            card.Add(new View { Position2D = new Position2D(w / 2 - 2, h / 2 - 28), Size2D = new Size2D(4, 56), BackgroundColor = palette.Ink });
        }
        return cold;
    }
}
