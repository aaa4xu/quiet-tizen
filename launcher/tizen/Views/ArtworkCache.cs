using System;
using System.Collections.Generic;
using System.Globalization;
using QuietHome.Core.Catalog;
using QuietHome.Core.Theme;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// The app artwork images, created once per app and card size and kept for the life of the
/// app, so moving between pages never decodes them again. While no page shows an image it
/// sits under an invisible view on the window, which keeps its texture alive.
/// <see cref="HomeView"/> owns the cache; the Home and Apps page views borrow images from it
/// through <see cref="AppArtwork"/>.
/// </summary>
sealed class ArtworkCache
{
    /// <summary>One image and the view it currently belongs to.</summary>
    sealed class Entry
    {
        /// <summary>Creates the entry for <paramref name="image"/> parked under <paramref name="parent"/>.</summary>
        public Entry(ImageView image, View parent)
        {
            Image = image;
            Parent = parent;
        }

        /// <summary>The image.</summary>
        public ImageView Image { get; }

        /// <summary>The card showing the image, or the invisible parking view.</summary>
        public View Parent { get; set; }
    }

    readonly string root;
    readonly Dictionary<string, Entry> entries = new();
    View? parking;

    /// <summary>Creates an empty cache for artwork files under <paramref name="root"/>.</summary>
    public ArtworkCache(string root) => this.root = root;

    /// <summary>Raised when an image finished loading or failed.</summary>
    public event Action? Loaded;

    /// <summary>How many images exist.</summary>
    public int Count => entries.Count;

    /// <summary>How many image views were ever created.</summary>
    public int Created { get; private set; }

    /// <summary>How many images finished loading, as of the last <see cref="Recount"/>.</summary>
    public int Ready { get; private set; }

    /// <summary>The images not loaded yet with their status, as of the last <see cref="Recount"/>.</summary>
    public string Pending { get; private set; } = "";

    /// <summary>
    /// Starts loading the artwork of <paramref name="library"/> in the sizes the Apps grid
    /// and the dock use. The first call adds the invisible parking view to the window, under
    /// everything a page draws later.
    /// </summary>
    public void Preload(IReadOnlyList<LibraryApp> library)
    {
        Parking();
        foreach (var app in library)
        {
            if (!app.Utility)
                Get(app, Layout.AppWidth, Layout.AppHeight);
        }
        foreach (var app in AppCatalog.DockApps(new List<LibraryApp>(library)))
            Get(app, Layout.DockWidth, Layout.DockHeight);
    }

    /// <summary>
    /// Moves the image of <paramref name="app"/> at <paramref name="w"/> × <paramref name="h"/>
    /// onto <paramref name="card"/>, creating it if needed. Returns true when the image has
    /// not finished loading yet (a cold image).
    /// </summary>
    public bool AttachTo(View card, LibraryApp app, int w, int h)
    {
        var entry = Get(app, w, h);
        bool cold = entry.Image.LoadingStatus != ImageView.LoadingStatusType.Ready;
        entry.Parent.Remove(entry.Image);
        card.Add(entry.Image);
        entry.Parent = card;
        return cold;
    }

    /// <summary>Moves every image back to the parking view, before the page's cards are destroyed.</summary>
    public void Park()
    {
        var parkingView = Parking();
        foreach (var entry in entries.Values)
        {
            if (entry.Parent != parkingView)
            {
                entry.Parent.Remove(entry.Image);
                parkingView.Add(entry.Image);
                entry.Parent = parkingView;
            }
        }
    }

    /// <summary>Recounts <see cref="Ready"/> and <see cref="Pending"/>; called when an image is created or finishes loading, and after a rebuild.</summary>
    public void Recount()
    {
        int ready = 0;
        var pending = new List<string>();
        foreach (var entry in entries)
        {
            if (entry.Value.Image.LoadingStatus == ImageView.LoadingStatusType.Ready)
                ready++;
            else
                pending.Add(entry.Key + ":" + entry.Value.Image.LoadingStatus);
        }
        Ready = ready;
        Pending = string.Join(";", pending);
    }

    /// <summary>Returns the invisible full-screen view that holds unused images, adding it to the window the first time.</summary>
    View Parking()
    {
        if (parking == null)
        {
            parking = new View { Size2D = new Size2D(Layout.ScreenWidth, Layout.ScreenHeight), Opacity = 0 };
            Window.Default.Add(parking);
        }
        return parking;
    }

    /// <summary>
    /// Returns the image of <paramref name="app"/> at <paramref name="w"/> × <paramref name="h"/>,
    /// creating it on first use. Catalogs can share an app ID with different artwork, so
    /// everything that changes the picture is part of the key.
    /// </summary>
    Entry Get(LibraryApp app, int w, int h)
    {
        string crop = app.Crop == null ? "" : string.Join(",", Array.ConvertAll(app.Crop, c => c.ToString(CultureInfo.InvariantCulture)));
        string key = app.Id + "|" + app.Asset + "|" + app.Background + "|" + app.Fit + "|" + crop + "@" + w + "x" + h;
        if (entries.TryGetValue(key, out var cached))
            return cached;
        var image = CreateImage(app, w, h);
        var entry = new Entry(image, Parking());
        entries.Add(key, entry);
        Created++;
        image.ResourceReady += (s, e) =>
        {
            Recount();
            Loaded?.Invoke();
        };
        entry.Parent.Add(image);
        image.ResourceUrl = root + app.Asset;
        Recount();
        return entry;
    }

    /// <summary>
    /// Returns a new image view for <paramref name="app"/> in a card of <paramref name="w"/> ×
    /// <paramref name="h"/>, sized and placed by the catalog's Fit and Crop (docs/06-artwork.md).
    /// </summary>
    static ImageView CreateImage(LibraryApp app, int w, int h)
    {
        bool placeholder = app.Asset.StartsWith("placeholders/");
        bool cover = app.Fit == "cover", full = cover || app.Fit == "contain-full";
        int iw = full ? w : w - 54, ih = full ? h : h - 40;
        var crop = app.Crop;
        float cropWidth = crop == null ? 1 : crop[2], cropHeight = crop == null ? 1 : crop[3];
        // SVG art keeps its aspect even with ScaleToFill; fit it to the cell and let the card color fill the rest.
        // Placeholders stretch instead (preserveAspectRatio="none").
        if (cover && !placeholder)
        {
            double aspect = app.Width * cropWidth / (app.Height * cropHeight);
            iw = (int)Math.Min(w, Math.Round(h * aspect));
            ih = (int)Math.Min(h, Math.Round(w / aspect));
        }
        float radius = iw == w && ih == h ? Layout.CardRadius : 0;
        var image = new ImageView
        {
            Position2D = new Position2D((w - iw) / 2, (h - ih) / 2),
            Size2D = new Size2D(iw, ih),
            FittingMode = cover ? FittingModeType.ScaleToFill : FittingModeType.ShrinkToFit,
            SamplingMode = SamplingModeType.BoxThenLanczos,
            DesiredWidth = (int)Math.Ceiling(iw * 2 / cropWidth),
            DesiredHeight = (int)Math.Ceiling(ih * 2 / cropHeight),
            ReleasePolicy = ReleasePolicyType.Destroyed,
            CornerRadius = new Vector4(radius, radius, radius, radius),
        };
        // The placeholder's circle is opaque white; tinting it gives the card a tonal accent.
        if (placeholder)
        {
            var background = Rgba.FromHex(app.Background);
            image.Color = ViewPalette.ToColor(ColorMath.Tone(background, ColorMath.DarkText(background) ? -.06 : .06));
        }
        if (crop != null)
            image.PixelArea = new RelativeVector4(crop[0], crop[1], crop[2], crop[3]);
        return image;
    }
}
