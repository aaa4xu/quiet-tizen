using System;
using System.Collections.Generic;
using System.Globalization;
using QuietHome.Core.Platform;

namespace QuietHome.Core.Settings;

/// <summary>
/// The latest values of the TV settings that the Settings page shows and changes: volume,
/// brightness, game mode and sleep timer. <see cref="HomeController"/> fills it with <see cref="Refresh"/>
/// from an <see cref="IQuickSettings"/> each time the page is shown or a setting is changed; this class holds the
/// values and the rules for the next value (for example, the sleep timer's next step),
/// so those rules can be tested without the TV. Setting IDs are Samsung's own
/// ("brightness", "game-mode", "sleeptimer"); <see cref="TvSettingIds"/> lists them all.
/// </summary>
public sealed class QuickSettingsState
{
    readonly Dictionary<string, string> values = new();
    readonly Dictionary<string, string?> names = new();
    readonly Dictionary<string, bool> enabled = new();

    /// <summary>Number of sleep timer choices on this TV, including off (0).</summary>
    public const int SleepTimerChoices = 5;

    /// <summary>
    /// Samsung's IDs for game mode, preferred first. The S95F menu shows the first one; it
    /// reports Off "0", On "1" and Auto "2". The second is the older on/off item, which is
    /// empty while Auto is set; it is kept for TVs that do not have the first one.
    /// </summary>
    public static IReadOnlyList<string> GameModeIds { get; } = new[] { "game-mode-setting-game-mode-auto", "game-mode" };

    /// <summary>The settings <see cref="Refresh"/> reads from Samsung's settings API (through <see cref="IQuickSettings"/>) every time.</summary>
    public static IReadOnlyList<string> TvSettingIds { get; } = new[] { "brightness", GameModeIds[0], GameModeIds[1], "sleeptimer" };

    /// <summary>Text shown for a setting the TV did not report.</summary>
    public const string Unavailable = "Unavailable";

    /// <summary>Master volume 0..100, or -1 when the TV did not report it.</summary>
    public int Volume { get; set; }

    /// <summary>Lowest brightness value the TV accepts.</summary>
    public double BrightnessMin { get; private set; }

    /// <summary>Highest brightness value the TV accepts.</summary>
    public double BrightnessMax { get; private set; } = 50;

    /// <summary>How much one Left or Right press changes the brightness.</summary>
    public double BrightnessStep { get; private set; } = 1;

    /// <summary>
    /// Stores what the TV reported for setting <paramref name="id"/>: its raw value and the
    /// display name of that value (for example value "1" with name "On"). A missing or empty
    /// value means the TV has no such setting right now, and the setting is forgotten.
    /// </summary>
    public void Record(string id, string? name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            values[id] = value;
            names[id] = name;
        }
        else
        {
            values.Remove(id);
            names.Remove(id);
        }
    }

    /// <summary>
    /// Rereads everything the Settings page shows from <paramref name="tv"/>: the volume
    /// (-1 when not reported), each of <see cref="TvSettingIds"/> and the brightness range.
    /// A menu item whose reading throws is forgotten; a range that cannot be read keeps the
    /// previous one.
    /// </summary>
    public void Refresh(IQuickSettings tv)
    {
        Volume = tv.GetVolume() ?? -1;
        foreach (var id in TvSettingIds)
        {
            try
            {
                var item = tv.ReadItem(id);
                Record(id, item.Name, item.Value);
                SetEnabled(id, item.Enabled);
            }
            catch (Exception)
            {
                Forget(id);
            }
        }
        try
        {
            var range = tv.ReadRange("brightness");
            SetBrightnessRange(range != null, range?.Min ?? 0, range?.Max ?? 0, range?.Step ?? 0);
        }
        catch (Exception)
        {
            // Keep the range read last time.
        }
    }

    /// <summary>Stores whether the TV lets the owner change setting <paramref name="id"/> for the current input.</summary>
    public void SetEnabled(string id, bool isEnabled) => enabled[id] = isEnabled;

    /// <summary>Forgets setting <paramref name="id"/> entirely, for when reading it failed: no value and not changeable.</summary>
    public void Forget(string id)
    {
        values.Remove(id);
        names.Remove(id);
        enabled[id] = false;
    }

    /// <summary>
    /// Stores the brightness range the TV reported. When the TV gave none
    /// (<paramref name="reported"/> is false) or a step that is not positive, the range
    /// falls back to 0..50 in steps of 1.
    /// </summary>
    public void SetBrightnessRange(bool reported, double min, double max, double step)
    {
        if (!reported || step <= 0)
        {
            (min, max, step) = (0, 50, 1);
        }
        BrightnessMin = min;
        BrightnessMax = max;
        BrightnessStep = step;
    }

    /// <summary>Returns the raw value of setting <paramref name="id"/>, or "" when the TV did not report it.</summary>
    public string Value(string id) => values.TryGetValue(id, out var v) ? v : "";

    /// <summary>Returns the display name of the value of setting <paramref name="id"/>, or <see cref="Unavailable"/>.</summary>
    public string? Name(string id) => names.TryGetValue(id, out var v) ? v : Unavailable;

    /// <summary>Returns true when setting <paramref name="id"/> has a value and the TV allows changing it.</summary>
    public bool CanEdit(string id) => enabled.TryGetValue(id, out var isEnabled) && isEnabled && values.ContainsKey(id);

    /// <summary>Returns the volume one step up or down from <paramref name="current"/>, kept within 0..100.</summary>
    public static int NextVolume(int current, int delta) => Math.Max(0, Math.Min(100, current + delta));

    /// <summary>
    /// Returns the brightness one step down (<paramref name="delta"/> = -1) or up (+1), kept
    /// within the TV's range, as the text the TV expects. Returns null when the current
    /// brightness is not a number, so nothing should be changed.
    /// </summary>
    public string? NextBrightness(int delta)
    {
        if (!double.TryParse(Value("brightness"), NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
            return null;
        return Math.Max(BrightnessMin, Math.Min(BrightnessMax, n + delta * BrightnessStep)).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The game mode item the Settings page shows and changes: the first of
    /// <see cref="GameModeIds"/> that has a value, or the older on/off item when none has.
    /// </summary>
    public string GameModeId => values.ContainsKey(GameModeIds[0]) ? GameModeIds[0] : GameModeIds[1];

    /// <summary>
    /// Returns the game mode value after one Select on its tile. The item with Auto steps
    /// Auto "2" → On "1" → Off "0" → Auto, and returns null for any other value, so nothing
    /// is changed. The older on/off item toggles: "0" when it is on ("1") now, otherwise "1".
    /// </summary>
    public string? NextGameModeValue()
    {
        string value = Value(GameModeId);
        if (GameModeId != GameModeIds[0])
            return value == "1" ? "0" : "1";
        return value switch
        {
            "2" => "1",
            "1" => "0",
            "0" => "2",
            _ => null,
        };
    }

    /// <summary>
    /// Returns the value of setting <paramref name="id"/> after one Select on a tile that
    /// steps through choices. Only the sleep timer does this: its five choices are 0..4 and
    /// the last wraps to 0 (off). Returns null when the current value is not a number.
    /// </summary>
    public string? NextCycleValue(string id) => int.TryParse(Value(id), out int n) ? ((n + 1) % SleepTimerChoices).ToString() : null;
}
