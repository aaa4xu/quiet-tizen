namespace QuietHome.Core.Platform;

/// <summary>
/// The range of a numeric menu item (brightness) as <see cref="IQuickSettings.ReadRange"/>
/// reports it. <see cref="Settings.QuickSettingsState.SetBrightnessRange"/> keeps it for the
/// Left and Right steps while adjusting.
/// </summary>
/// <param name="Min">Lowest value the TV accepts.</param>
/// <param name="Max">Highest value the TV accepts.</param>
/// <param name="Step">How much one step changes the value.</param>
public readonly record struct SettingRange(double Min, double Max, double Step);
