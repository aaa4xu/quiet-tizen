namespace QuietHome.Core.Theme;

/// <summary>
/// The colors that change between the dark and the light theme. The TV host picks
/// <see cref="Dark"/> or <see cref="Light"/> from the saved theme and paints text, tabs,
/// focus and the window with them. Colors that are the same in both themes (white
/// tile text, shadows) are not here.
/// </summary>
public sealed class ThemeColors
{
    /// <summary>Main text: page titles, card names, the selected tab.</summary>
    public Rgba Ink { get; }

    /// <summary>Secondary text: unselected tabs, the clock, hints.</summary>
    public Rgba Muted { get; }

    /// <summary>Focus ring on app cards and the error line.</summary>
    public Rgba Accent { get; }

    /// <summary>Pill behind the selected tab while the tabs have focus.</summary>
    public Rgba TabFocus { get; }

    /// <summary>Window background behind the page artwork.</summary>
    public Rgba Window { get; }

    /// <summary>Background of the input cards on the Home page.</summary>
    public Rgba InputCard { get; }

    /// <summary>Creates a palette; only <see cref="Dark"/> and <see cref="Light"/> exist.</summary>
    ThemeColors(Rgba ink, Rgba muted, Rgba accent, Rgba tabFocus, Rgba window, Rgba inputCard)
    {
        Ink = ink;
        Muted = muted;
        Accent = accent;
        TabFocus = tabFocus;
        Window = window;
        InputCard = inputCard;
    }

    /// <summary>The default theme.</summary>
    public static ThemeColors Dark { get; } = new(
        ink: new Rgba(0.95f, 0.96f, 0.98f, 1),
        muted: new Rgba(0.62f, 0.65f, 0.72f, 1),
        accent: new Rgba(0.49f, 0.62f, 1, 1),
        tabFocus: new Rgba(1, 1, 1, .14f),
        window: new Rgba(0.05f, 0.06f, 0.08f, 1),
        inputCard: new Rgba(.11f, .13f, .17f, 1));

    /// <summary>The light theme the owner can switch to on the Settings page.</summary>
    public static ThemeColors Light { get; } = new(
        ink: new Rgba(0.114f, 0.114f, 0.122f, 1),
        muted: new Rgba(0.431f, 0.431f, 0.451f, 1),
        accent: new Rgba(0.24f, 0.29f, 0.72f, 1),
        tabFocus: new Rgba(1, 1, 1, .7f),
        window: new Rgba(0.96f, 0.96f, 0.97f, 1),
        inputCard: new Rgba(1, 1, 1, .92f));

    /// <summary>Returns <see cref="Dark"/> when <paramref name="dark"/> is true, otherwise <see cref="Light"/>.</summary>
    public static ThemeColors For(bool dark) => dark ? Dark : Light;
}
