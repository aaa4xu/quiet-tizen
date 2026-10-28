using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// One entry of the app catalog, <c>launcher/assets/apps.json</c> (or the owner's
/// <c>apps.local.json</c>). Each entry is a card on the Apps page; the first five that are
/// not <see cref="Utility"/> also form the dock on the Home page. This is plain data:
/// <see cref="AppLibrary.Load"/> creates and checks the entries, and the TV host
/// (<c>launcher/tizen/Home.cs</c>) draws them and launches <see cref="Id"/> on Select.
/// </summary>
public sealed class LibraryApp
{
    /// <summary>Name shown under the card.</summary>
    public string Name { get; set; }
    /// <summary>Tizen app ID that Select launches.</summary>
    public string Id { get; set; }
    /// <summary>Artwork path relative to the catalog's folder; never absolute and never with "..".</summary>
    public string Asset { get; set; }
    /// <summary>Text shown at the bottom of the screen while the card has focus.</summary>
    public string Description { get; set; }
    /// <summary>How the artwork fills the card: "cover", "contain" or "contain-full".</summary>
    public string Fit { get; set; } = "cover";
    /// <summary>Card color behind the artwork, as "#RRGGBB".</summary>
    public string Background { get; set; } = "#FFFFFF";
    /// <summary>Artwork width in pixels; with <see cref="Height"/> it gives the aspect ratio.</summary>
    public int Width { get; set; }
    /// <summary>Artwork height in pixels.</summary>
    public int Height { get; set; }
    /// <summary>Optional part of the artwork to show: x, y, width, height as fractions of the image (0..1).</summary>
    public float[] Crop { get; set; }
    /// <summary>
    /// Marks a helper card such as "Get more apps": it is not counted as an app and
    /// never goes to the dock. In the dark theme it is drawn in theme colors instead of
    /// <see cref="Background"/>.
    /// </summary>
    public bool Utility { get; set; }
}

/// <summary>
/// Loads the app catalog and does the Apps page arithmetic: how the catalog splits into
/// pages of <see cref="Columns"/> × <see cref="Rows"/> cards and where focus goes when a
/// direction key is pressed. It knows nothing about drawing; the TV host asks it for numbers
/// (catalog indexes, page counts) and draws the cards itself.
/// </summary>
public static class AppLibrary
{
    /// <summary>Apps page grid: cards per row, rows per page and cards per page.</summary>
    public const int Columns = 5, Rows = 3, PageSize = Columns * Rows;

    /// <summary>
    /// Reads the catalog from a JSON file and checks every entry. Throws
    /// <see cref="InvalidDataException"/> when the catalog is empty, an entry lacks a name,
    /// ID, artwork or size, two entries share an ID, the artwork path leaves the catalog's
    /// folder, or a crop is outside the image. The caller shows the error instead of the cards.
    /// </summary>
    public static List<LibraryApp> Load(string path)
    {
        var apps = JsonSerializer.Deserialize<List<LibraryApp>>(File.ReadAllText(path));
        if (apps == null || apps.Count == 0)
            throw new InvalidDataException("App library is empty.");
        var ids = new HashSet<string>();
        foreach (var app in apps)
        {
            if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.Id) || !ids.Add(app.Id)
                || string.IsNullOrWhiteSpace(app.Asset) || Path.IsPathRooted(app.Asset) || app.Asset.Contains("..")
                || app.Width <= 0 || app.Height <= 0)
                throw new InvalidDataException("Invalid app library entry.");
            var crop = app.Crop;
            if (crop != null && (crop.Length != 4 || !Array.TrueForAll(crop, float.IsFinite)
                || crop[0] < 0 || crop[1] < 0 || crop[2] <= 0 || crop[3] <= 0
                || crop[0] + crop[2] > 1 || crop[1] + crop[3] > 1))
                throw new InvalidDataException("Invalid artwork crop.");
        }
        return apps;
    }

    /// <summary>Number of Apps pages needed for <paramref name="count"/> apps (0 for an empty catalog).</summary>
    public static int PageCount(int count) => (count + PageSize - 1) / PageSize;

    /// <summary>Number of cards on page <paramref name="page"/> (0-based): a full page, the remainder on the last page, or 0 past the end.</summary>
    public static int VisibleCount(int count, int page) => Math.Max(0, Math.Min(PageSize, count - page * PageSize));

    /// <summary>
    /// Works out where focus goes on the Apps page when a direction key is pressed.
    /// <paramref name="absolute"/> is the focused card's index in the whole catalog and
    /// <paramref name="key"/> is "Left", "Right", "Up" or "Down". Returns the new catalog
    /// index, which may be on the previous or next page, or -1 when focus leaves the grid
    /// for the navigation tabs (Up from the first row of the first page, or an empty catalog).
    /// Any other key keeps the focus where it is.
    /// </summary>
    public static int Move(int count, int absolute, string key)
    {
        if (count <= 0)
            return -1;
        absolute = Math.Clamp(absolute, 0, count - 1);
        int page = absolute / PageSize, local = absolute % PageSize, col = local % Columns, row = local / Columns;
        int pageStart = page * PageSize, visible = VisibleCount(count, page);
        if (key == "Left")
            return Math.Max(0, absolute - 1);
        if (key == "Right")
            return Math.Min(count - 1, absolute + 1);
        if (key == "Up")
        {
            // Up from the top row goes to the same column on the last row of the previous page.
            if (row > 0)
                return absolute - Columns;
            if (page == 0)
                return -1;
            return pageStart - Columns + col;
        }
        if (key == "Down")
        {
            // Down moves one row down; when the row below is shorter, focus lands on its last card.
            // From the bottom row it goes to the same column on the next page (or that page's
            // last card); on the last page it stays put.
            if ((row + 1) * Columns < visible)
                return pageStart + Math.Min(local + Columns, visible - 1);
            int next = pageStart + PageSize;
            return next < count ? Math.Min(next + col, count - 1) : absolute;
        }
        return absolute;
    }
}
