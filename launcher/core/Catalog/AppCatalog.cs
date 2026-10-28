using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuietHome.Core.Catalog;

/// <summary>
/// Loads and checks the app catalog, and picks the apps for the dock on the Home page.
/// <see cref="HomeController"/> loads the owner's catalog at start and the public one for demo mode;
/// <see cref="CatalogGrid"/> then lays the apps out on the Apps page.
/// </summary>
public static class AppCatalog
{
    /// <summary>How many apps the dock on the Home page shows before its "Your Apps" card.</summary>
    public const int DockSize = 5;

    /// <summary>
    /// The values <see cref="LibraryApp.Fit"/> may take: "cover" fills the card, "contain"
    /// fits the artwork inside a margin, "contain-full" fits it to the whole card.
    /// </summary>
    public static IReadOnlyList<string> Fits { get; } = new[] { "cover", "contain", "contain-full" };

    static readonly Regex hexColor = new(@"^#[0-9A-Fa-f]{6}\z");

    /// <summary>
    /// Reads the catalog from a JSON file and checks every entry. Throws
    /// <see cref="InvalidDataException"/> when the catalog is empty, an entry lacks a name,
    /// ID, artwork or size, two entries share an ID, the artwork path leaves the catalog's
    /// folder, the card color is not "#RRGGBB", the fit is not one of <see cref="Fits"/>, or a
    /// crop is outside the image. The caller shows the error instead of the cards.
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
                || app.Width <= 0 || app.Height <= 0
                || app.Background == null || !hexColor.IsMatch(app.Background) || !Fits.Contains(app.Fit))
                throw new InvalidDataException("Invalid app library entry.");
            var crop = app.Crop;
            if (crop != null && (crop.Length != 4 || !Array.TrueForAll(crop, float.IsFinite)
                || crop[0] < 0 || crop[1] < 0 || crop[2] <= 0 || crop[3] <= 0
                || crop[0] + crop[2] > 1 || crop[1] + crop[3] > 1))
                throw new InvalidDataException("Invalid artwork crop.");
        }
        return apps;
    }

    /// <summary>Returns the apps for the Home page dock: the first <see cref="DockSize"/> catalog entries that are not utility cards.</summary>
    public static List<LibraryApp> DockApps(List<LibraryApp> apps)
    {
        var real = apps.FindAll(a => !a.Utility);
        return real.GetRange(0, Math.Min(DockSize, real.Count));
    }
}
