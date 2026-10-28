using System.Collections.Generic;
using QuietHome.Core.Settings;

namespace QuietHome.Core.Platform.Demo;

/// <summary>
/// The quick settings in demo mode (<c>quiet_demo=1</c>): fixed sample values (volume 20,
/// brightness 45, game mode Off, sleep timer Off) kept in memory. Changing them on the
/// Settings page works as on the TV but changes nothing on the TV. The TV host creates a
/// new one each time demo mode starts, so every demo starts from the same values.
/// </summary>
public sealed class DemoQuickSettings : IQuickSettings
{
    int volume = 20;
    readonly Dictionary<string, string> values = new()
    {
        ["brightness"] = "45",
        [QuickSettingsState.GameModeIds[0]] = "0",
        ["sleeptimer"] = "0",
    };

    /// <summary>Returns the demo volume.</summary>
    public int? GetVolume() => volume;

    /// <summary>Changes the demo volume; always succeeds.</summary>
    public bool SetVolume(int volume)
    {
        this.volume = volume;
        return true;
    }

    /// <summary>Returns the demo value of <paramref name="id"/> with its display name, changeable; an item the demo does not have reads as empty and not changeable.</summary>
    public SettingReading ReadItem(string id) =>
        values.TryGetValue(id, out var value) ? new SettingReading(NameOf(id, value), value, true) : new SettingReading(null, null, false);

    /// <summary>Returns 0..50 in steps of 1 for brightness and null for anything else.</summary>
    public SettingRange? ReadRange(string id) => id == "brightness" ? new SettingRange(0, 50, 1) : null;

    /// <summary>Changes the demo value of <paramref name="id"/>; returns false for an item the demo does not have.</summary>
    public bool WriteItem(string id, string value)
    {
        if (!values.ContainsKey(id))
            return false;
        values[id] = value;
        return true;
    }

    /// <summary>
    /// Returns the display name of a demo value, in English: Off / On / Auto for game mode,
    /// Off or minutes for the sleep timer, the number itself for brightness.
    /// </summary>
    static string NameOf(string id, string value)
    {
        if (id == QuickSettingsState.GameModeIds[0])
            return value switch { "1" => "On", "2" => "Auto", _ => "Off" };
        if (id == "sleeptimer")
            return int.TryParse(value, out int choice) && choice > 0 ? choice * 30 + " min" : "Off";
        return value;
    }
}
