using QuietHome.Core.Theme;
using Tizen.NUI;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// The colors and themed artwork of one theme as NUI values. <see cref="HomeView"/> creates
/// one on every rebuild from the core's <see cref="ThemeColors"/>; the page views draw with it.
/// </summary>
sealed class ViewPalette
{
    /// <summary>Creates the palette of the dark (<paramref name="dark"/>) or light theme.</summary>
    public ViewPalette(bool dark)
    {
        Dark = dark;
        var colors = ThemeColors.For(dark);
        Ink = ToColor(colors.Ink);
        Muted = ToColor(colors.Muted);
        Accent = ToColor(colors.Accent);
        TabFocus = ToColor(colors.TabFocus);
        Window = ToColor(colors.Window);
        InputCard = ToColor(colors.InputCard);
    }

    /// <summary>True for the dark theme.</summary>
    public bool Dark { get; }

    /// <summary>Main text and icon color, and the focus ring around cards.</summary>
    public Color Ink { get; }

    /// <summary>Secondary text color.</summary>
    public Color Muted { get; }

    /// <summary>Notice text.</summary>
    public Color Accent { get; }

    /// <summary>Highlight behind the focused tab.</summary>
    public Color TabFocus { get; }

    /// <summary>Window background behind the background artwork.</summary>
    public Color Window { get; }

    /// <summary>Background of the input cards.</summary>
    public Color InputCard { get; }

    /// <summary>Returns the artwork file for this theme: dark artwork files start with "dark-".</summary>
    public string Themed(string asset) => Dark ? "dark-" + asset : asset;

    /// <summary>Returns white, for text on colored tiles.</summary>
    public static Color White() => new(1, 1, 1, 1);

    /// <summary>Returns full transparency.</summary>
    public static Color Clear() => new(0, 0, 0, 0);

    /// <summary>Returns the soft shadow under app cards.</summary>
    public static Color AppShadow() => new(.12f, .15f, .25f, .08f);

    /// <summary>Returns the background of the utility card in the dark theme.</summary>
    public static Color DarkUtilityCard() => new(.16f, .18f, .24f, 1);

    /// <summary>Returns the hint color on Settings tiles.</summary>
    public static Color TileHint() => new(.95f, .96f, 1, 1);

    /// <summary>Returns the faint white border of cards.</summary>
    public static Color CardBorder() => new(1, 1, 1, .28f);

    /// <summary>Converts a core color to NUI.</summary>
    public static Color ToColor(Rgba color) => new(color.R, color.G, color.B, color.A);

    /// <summary>Returns the NUI color of a "#RRGGBB" card color (white when invalid).</summary>
    public static Color FromHex(string hex) => ToColor(Rgba.FromHex(hex));
}
