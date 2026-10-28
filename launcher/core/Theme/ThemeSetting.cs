namespace QuietHome.Core.Theme;

/// <summary>
/// How the theme choice is written to an <see cref="Platform.IThemeStore"/>: "dark" or
/// "light". The TV host reads the theme at start and when demo mode ends, and writes it when
/// the owner presses Select on the Theme tile.
/// </summary>
public static class ThemeSetting
{
    /// <summary>Returns true (dark) unless <paramref name="stored"/> is "light", ignoring surrounding white space; nothing stored also means dark.</summary>
    public static bool IsDark(string? stored) => stored?.Trim() != "light";

    /// <summary>Returns the text to store for the theme: "dark" or "light".</summary>
    public static string Text(bool dark) => dark ? "dark" : "light";
}
