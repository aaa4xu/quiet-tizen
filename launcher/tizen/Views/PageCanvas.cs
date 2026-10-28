using System.Collections.Generic;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// What a page view draws with during one rebuild: the scene, the theme, the artwork files
/// and the slot list it appends its cards to. <see cref="HomeView"/> creates one per rebuild
/// and reads <see cref="ColdArtwork"/> afterwards.
/// </summary>
sealed class PageCanvas
{
    /// <summary>Creates the canvas for one rebuild.</summary>
    public PageCanvas(Scene scene, ViewPalette palette, string root, ArtworkCache artwork, List<SlotView> slots)
    {
        Scene = scene;
        Palette = palette;
        Root = root;
        Artwork = artwork;
        Slots = slots;
    }

    /// <summary>Where the page's views go.</summary>
    public Scene Scene { get; }

    /// <summary>The theme's colors.</summary>
    public ViewPalette Palette { get; }

    /// <summary>Folder of Quiet Home's artwork files, ending in "/".</summary>
    public string Root { get; }

    /// <summary>The app artwork images.</summary>
    public ArtworkCache Artwork { get; }

    /// <summary>The page's slots so far, in the order of the screen model's cards.</summary>
    public List<SlotView> Slots { get; }

    /// <summary>How many app images on this page were not loaded yet.</summary>
    public int ColdArtwork { get; set; }
}
