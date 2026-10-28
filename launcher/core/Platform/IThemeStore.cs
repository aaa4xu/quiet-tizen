namespace QuietHome.Core.Platform;

/// <summary>
/// Port to where the owner's theme choice is kept between runs. The TV host implements it
/// with a file in the app container's data directory
/// (launcher/tizen/Platform/FileThemeStore.cs); demo mode uses
/// <see cref="Demo.DemoThemeStore"/>, which keeps it in memory.
/// <see cref="Theme.ThemeSetting"/> turns the stored text into the theme.
/// </summary>
public interface IThemeStore
{
    /// <summary>Returns the stored text, or null when nothing is stored or it cannot be read.</summary>
    string? Read();

    /// <summary>Stores <paramref name="text"/>; throws when it cannot be saved, with a message for the diagnostics file.</summary>
    void Write(string text);
}
