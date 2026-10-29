namespace QuietHome.Core.Inputs;

/// <summary>
/// Whether the device on an input is on, as the Home page shows it in the line under the
/// input's name. <see cref="InputPowers.Of"/> works it out from an <see cref="InputSource"/>;
/// <see cref="InputCard"/> turns it into text and <see cref="HomeController"/> previews no
/// input without a picture. See docs/13-input-state.md for where the TV reports it.
/// </summary>
public enum InputPower
{
    /// <summary>The TV did not say (Live TV, or a value not seen before); the card shows no state.</summary>
    Unknown,

    /// <summary>The device sends a signal.</summary>
    On,

    /// <summary>No signal, and the device answers HDMI-CEC on its port: it is in standby (rest mode).</summary>
    Standby,

    /// <summary>No signal, and nothing on the port answers HDMI-CEC: off, asleep or not sending.</summary>
    NoSignal,
}

/// <summary>
/// Works out <see cref="InputPower"/> from what the TV reports about an input. Core logic
/// with no TV calls: the source list's <see cref="InputSource.PowerState"/> says whether a
/// signal arrives, and <see cref="InputSource.CecDevice"/>, filled from the CEC port, tells
/// a device in standby from one that is simply off.
/// </summary>
public static class InputPowers
{
    /// <summary>
    /// Returns the state of <paramref name="source"/>: <c>powerState</c> 1 is On; 2 is Standby
    /// when a CEC device is on the port and NoSignal otherwise. Live TV and any other value are
    /// Unknown. The signal decides first: a waking console sends a picture several seconds
    /// before its CEC status says it is on.
    /// </summary>
    public static InputPower Of(InputSource source) => source.SourceType == 0 ? InputPower.Unknown : source.PowerState switch
    {
        1 => InputPower.On,
        2 => source.CecDevice ? InputPower.Standby : InputPower.NoSignal,
        _ => InputPower.Unknown,
    };

    /// <summary>Returns the words shown for <paramref name="power"/>, or null for <see cref="InputPower.Unknown"/>.</summary>
    public static string? Text(InputPower power) => power switch
    {
        InputPower.On => "On",
        InputPower.Standby => "Standby",
        InputPower.NoSignal => "No signal",
        _ => null,
    };
}
