using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// The TV's sound manager (<c>libcapi-media-sound-manager-tv</c>), for the master volume on
/// the Settings page. Only <see cref="Platform.TizenQuickSettings"/> calls it.
/// </summary>
static class SoundManager
{
    const string Library = "libcapi-media-sound-manager-tv.so.0";

    /// <summary>Reads the master volume, 0..100; returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "sound_manager_get_master_volume")]
    public static extern int GetMasterVolume(out int value);

    /// <summary>Sets the master volume, 0..100; returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "sound_manager_set_master_volume")]
    public static extern int SetMasterVolume(int value);
}
