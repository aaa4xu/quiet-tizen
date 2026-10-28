using System;
using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// Samsung's private input library (<c>libsource-api.so</c>): the list of inputs, switching
/// to one and a callback when the list changes. Only <see cref="Platform.TizenInputSources"/>
/// calls it. What the functions do on the QE65S95F is described in docs/11-source-api.md.
/// </summary>
static class SourceApi
{
    const string Library = "libsource-api.so";

    /// <summary>
    /// The callback <see cref="RegisterSourceListChanged"/> takes. The TV calls it on its own
    /// thread with <c>what</c> = 0 and the <c>data</c> passed at registration.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SourceListChanged(int what, IntPtr data);

    /// <summary>Switches the TV to input <paramref name="sourceType"/>; returns a negative number on failure.</summary>
    [DllImport(Library, EntryPoint = "connect_source")]
    public static extern int ConnectSource(int sourceType);

    /// <summary>
    /// Returns the input list as a JSON string allocated with malloc, or zero. The caller
    /// frees it with <see cref="Libc.Free"/>.
    /// </summary>
    [DllImport(Library, EntryPoint = "get_source_list")]
    public static extern IntPtr GetSourceList();

    /// <summary>
    /// Starts calling <paramref name="callback"/> when the input list changes; returns 0 on
    /// success. The delegate must stay referenced while it is registered.
    /// </summary>
    [DllImport(Library, EntryPoint = "register_sourcelist_changed_callback")]
    public static extern int RegisterSourceListChanged(SourceListChanged callback, IntPtr data);

    /// <summary>Stops the callback registered with the same <paramref name="data"/>.</summary>
    [DllImport(Library, EntryPoint = "unregister_sourcelist_changed_callback")]
    public static extern int UnregisterSourceListChanged(IntPtr data);
}
