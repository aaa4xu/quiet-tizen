namespace QuietHome.Core.Platform;

/// <summary>
/// One menu item as <see cref="IQuickSettings.ReadItem"/> reports it.
/// <see cref="Settings.QuickSettingsState"/> stores it for the Settings page.
/// </summary>
/// <param name="Name">Display name of the value in the TV's language, for example "Auto"; may be null.</param>
/// <param name="Value">Raw value, for example "2"; null or empty when the TV has no value for the item now.</param>
/// <param name="Enabled">True when the TV allows changing the item for the current input.</param>
public readonly record struct SettingReading(string? Name, string? Value, bool Enabled);
