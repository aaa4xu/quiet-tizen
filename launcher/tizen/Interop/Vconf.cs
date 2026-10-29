using System;
using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// Tizen's key-value store of system state (<c>libvconf</c>). Quiet Home reads three keys from
/// it: the input on screen (<see cref="Platform.TizenInputSources"/>), and the window and
/// state of the TV's viewer (<see cref="Platform.TvVideoPlane"/>,
/// <see cref="Platform.TizenInputPreview"/>).
/// </summary>
static class Vconf
{
    /// <summary>The key that holds the source type of the input on screen.</summary>
    public const string SourceTypeKey = "memory/system/source_type";

    /// <summary>
    /// The key where the TV's viewer (org.tizen.tv-viewer) publishes the resource id of its
    /// window, which normally holds the main video plane; found in libtv-viewer-api.
    /// </summary>
    public const string ViewerWindowKey = "db/tv-viewer/window_id";

    /// <summary>
    /// The key where the TV's viewer publishes its app state: "pause" while another window
    /// covers it, "resume_focus" or "resume_unfocus" while it can be seen.
    /// </summary>
    public const string ViewerStateKey = "memory/tv-viewer/viewer_state";

    /// <summary>Reads integer key <paramref name="key"/>; returns 0 on success.</summary>
    [DllImport("libvconf.so.0", EntryPoint = "vconf_get_int")]
    public static extern int GetInt(string key, out int value);

    /// <summary>Returns string key <paramref name="key"/>, or null when it cannot be read.</summary>
    public static string? GetString(string key)
    {
        IntPtr value = GetStr(key);
        if (value == IntPtr.Zero)
            return null;
        try
        {
            return Marshal.PtrToStringUTF8(value);
        }
        finally
        {
            Libc.Free(value);
        }
    }

    /// <summary>Reads string key <paramref name="key"/> into memory the caller frees; zero when it cannot be read.</summary>
    [DllImport("libvconf.so.0", EntryPoint = "vconf_get_str")]
    static extern IntPtr GetStr(string key);
}
