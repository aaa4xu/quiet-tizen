using QuietHome.Core.Platform;
using QuietHome.TizenHost.Interop;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// The TV's volume and settings menu items through Samsung's sound manager
/// (<see cref="SoundManager"/>) and settings library (<see cref="SettingsApi"/>). Implements
/// the core's <see cref="IQuickSettings"/> port for the Quiet Home app outside demo mode.
/// Exceptions from the libraries pass through; the core decides what they mean.
/// </summary>
sealed class TizenQuickSettings : IQuickSettings
{
    /// <summary>Returns the master volume, or null when the sound manager reports an error.</summary>
    public int? GetVolume() => SoundManager.GetMasterVolume(out int volume) == 0 ? volume : null;

    /// <summary>Sets the master volume; returns false when the sound manager reports an error.</summary>
    public bool SetVolume(int volume) => SoundManager.SetMasterVolume(volume) == 0;

    /// <summary>Reads the value, its name and whether it can be changed; a failed value read gives no value.</summary>
    public SettingReading ReadItem(string id)
    {
        bool read = SettingsApi.GetItemValue(id, out string? name, out string? value);
        bool enabled = SettingsApi.GetItemEnable(id, out bool isEnabled) && isEnabled;
        return new SettingReading(name, read ? value : null, enabled);
    }

    /// <summary>Returns the item's range, or null when the TV reports none.</summary>
    public SettingRange? ReadRange(string id) =>
        SettingsApi.GetItemMinMaxStepValue(id, out double min, out double max, out double step) ? new SettingRange(min, max, step) : null;

    /// <summary>Writes the item's value; returns the library's answer.</summary>
    public bool WriteItem(string id, string value) => SettingsApi.SetItemValue(id, value);
}
