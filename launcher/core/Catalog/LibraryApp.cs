namespace QuietHome.Core.Catalog;

/// <summary>
/// One entry of the app catalog, <c>launcher/assets/apps.json</c> (or the owner's
/// <c>apps.local.json</c>). Each entry is a card on the Apps page; the first five that are
/// not <see cref="Utility"/> also form the dock on the Home page. This is plain data:
/// <see cref="AppCatalog.Load"/> creates and checks the entries, and the TV host
/// (<c>launcher/tizen/Home.cs</c>) draws them and launches <see cref="Id"/> on Select.
/// </summary>
public sealed class LibraryApp
{
    /// <summary>Name shown under the card.</summary>
    public string Name { get; set; } = "";

    /// <summary>Tizen app ID that Select launches.</summary>
    public string Id { get; set; } = "";

    /// <summary>Artwork path relative to the catalog's folder; never absolute and never with "..".</summary>
    public string Asset { get; set; } = "";

    /// <summary>Text shown at the bottom of the screen while the card has focus.</summary>
    public string? Description { get; set; }

    /// <summary>How the artwork fills the card: "cover", "contain" or "contain-full".</summary>
    public string Fit { get; set; } = "cover";

    /// <summary>Card color behind the artwork, as "#RRGGBB".</summary>
    public string Background { get; set; } = "#FFFFFF";

    /// <summary>Artwork width in pixels; with <see cref="Height"/> it gives the aspect ratio.</summary>
    public int Width { get; set; }

    /// <summary>Artwork height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Optional part of the artwork to show: x, y, width, height as fractions of the image (0..1).</summary>
    public float[]? Crop { get; set; }

    /// <summary>
    /// Marks a helper card such as "Get more apps": it is not counted as an app and
    /// never goes to the dock. In the dark theme it is drawn in theme colors instead of
    /// <see cref="Background"/>.
    /// </summary>
    public bool Utility { get; set; }
}
