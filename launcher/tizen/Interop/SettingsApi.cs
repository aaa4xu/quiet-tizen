using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// Samsung's private settings library (<c>libsettings-api.so</c>). It reads and writes the
/// items of Samsung's settings menu by their menu IDs ("brightness", "sleeptimer"). Only
/// <see cref="Platform.TizenQuickSettings"/> calls it.
/// </summary>
static class SettingsApi
{
    const string Library = "libsettings-api.so";

    /// <summary>
    /// Reads menu item <paramref name="id"/>: its raw value and the display name of that
    /// value in the TV's language. Returns false when the item cannot be read.
    /// </summary>
    [DllImport(Library, EntryPoint = "CSharpGetItemValue")]
    public static extern bool GetItemValue(string id, out string? name, out string? value);

    /// <summary>Writes the raw value of menu item <paramref name="id"/>; returns false on failure.</summary>
    [DllImport(Library, EntryPoint = "CSharpSetItemValue")]
    public static extern bool SetItemValue(string id, string value);

    /// <summary>Reads whether menu item <paramref name="id"/> can be changed for the current input; returns false on failure.</summary>
    [DllImport(Library, EntryPoint = "CSharpGetItemEnable")]
    public static extern bool GetItemEnable(string id, out bool enabled);

    /// <summary>Reads the range and step of numeric menu item <paramref name="id"/>; returns false when the TV has none.</summary>
    [DllImport(Library, EntryPoint = "CSharpGetItemMinMaxStepValue")]
    public static extern bool GetItemMinMaxStepValue(string id, out double min, out double max, out double step);
}
