namespace QuietHome.Core.Inputs;

/// <summary>
/// One input as the TV's source list reports it (libsource-api's <c>get_source_list</c>
/// returns a JSON array of these). The TV's input port reads the list, <see cref="InputList.Parse"/>
/// turns it into these objects, and <see cref="InputCard"/> decides what the Home page shows.
/// Property names match the JSON fields, compared without case.
/// </summary>
public sealed class InputSource
{
    /// <summary>True when a device is plugged in; only connected inputs get a card.</summary>
    public bool ConnectionState { get; set; }

    /// <summary>Name the TV detected from the device (HDMI-CEC), URL-encoded; may be empty.</summary>
    public string? DetectedName { get; set; }

    /// <summary>Name the owner gave the input in Samsung's source list, URL-encoded; may be empty.</summary>
    public string? EditName { get; set; }

    /// <summary>Full path of the TV's own icon for the input, for example ".../i_icon_device_m_hdmi3.svg".</summary>
    public string? IconPath { get; set; }

    /// <summary>The TV's number for the input; 0 is the tuner (Live TV). Switching inputs uses this number.</summary>
    public int SourceType { get; set; }

    /// <summary>
    /// 1 while the device sends a signal, 2 while it does not; 0 for Live TV. The TV updates it
    /// as devices turn on and off without a change notice, so the Home page rereads the list.
    /// </summary>
    public int PowerState { get; set; }

    /// <summary>
    /// True when a device answers HDMI-CEC on this input's port. Not part of the source list:
    /// <see cref="HomeController"/> fills it from <see cref="Platform.ICecDevices"/> after each reading.
    /// </summary>
    public bool CecDevice { get; set; }
}
