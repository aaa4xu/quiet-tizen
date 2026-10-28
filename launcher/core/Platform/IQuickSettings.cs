namespace QuietHome.Core.Platform;

/// <summary>
/// Port to the TV settings that the Settings page shows and changes: the master volume and
/// items of Samsung's settings menu (brightness, game mode, sleep timer) by their menu IDs.
/// The TV host implements it with Samsung's sound and settings libraries
/// (launcher/tizen/Platform/TizenQuickSettings.cs); demo mode uses
/// <see cref="Demo.DemoQuickSettings"/>, which changes nothing on the TV.
/// <see cref="Settings.QuickSettingsState.Refresh"/> reads it into the page's state.
/// </summary>
public interface IQuickSettings
{
    /// <summary>Returns the master volume, 0..100, or null when the TV did not report it.</summary>
    int? GetVolume();

    /// <summary>Sets the master volume, 0..100; returns false when the TV refused.</summary>
    bool SetVolume(int volume);

    /// <summary>
    /// Reads menu item <paramref name="id"/>: its value, the value's display name and whether
    /// it can be changed now. Throws when the TV's library fails.
    /// </summary>
    SettingReading ReadItem(string id);

    /// <summary>
    /// Returns the range of numeric menu item <paramref name="id"/>, or null when the TV
    /// reports none. Throws when the TV's library fails.
    /// </summary>
    SettingRange? ReadRange(string id);

    /// <summary>Writes the value of menu item <paramref name="id"/>; returns false when the TV refused. May throw.</summary>
    bool WriteItem(string id, string value);
}
