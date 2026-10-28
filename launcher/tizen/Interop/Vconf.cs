using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// Tizen's key-value store of system state (<c>libvconf</c>). Quiet Home reads one key from
/// it, the input on screen (<see cref="Platform.TizenInputSources"/>).
/// </summary>
static class Vconf
{
    /// <summary>The key that holds the source type of the input on screen.</summary>
    public const string SourceTypeKey = "memory/system/source_type";

    /// <summary>Reads integer key <paramref name="key"/>; returns 0 on success.</summary>
    [DllImport("libvconf.so.0", EntryPoint = "vconf_get_int")]
    public static extern int GetInt(string key, out int value);
}
