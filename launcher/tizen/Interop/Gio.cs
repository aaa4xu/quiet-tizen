using System;
using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// The few GLib/GIO calls Quiet Home needs to talk D-Bus (<c>libgio-2.0.so.0</c>,
/// <c>libglib-2.0.so.0</c>, which ship with the TV). Only <see cref="Platform.TizenCecDevices"/>
/// calls it. Method arguments are built from single values with <c>g_variant_new_tuple</c>,
/// because the variadic <c>g_variant_new</c> cannot be declared safely here.
/// </summary>
static class Gio
{
    const string GioLibrary = "libgio-2.0.so.0", GLibLibrary = "libglib-2.0.so.0";

    /// <summary>G_BUS_TYPE_SYSTEM: the system bus, where the TV's services are.</summary>
    public const int SystemBus = 1;

    /// <summary>
    /// GDBusSignalCallback. On the QE65S95F it ran on Quiet Home's UI thread, because the
    /// app's main loop runs GLib's default context.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SignalCallback(IntPtr connection, IntPtr sender, IntPtr path, IntPtr iface, IntPtr signal, IntPtr parameters, IntPtr data);

    /// <summary>Returns the process's shared connection to <paramref name="busType"/>, or zero with <paramref name="error"/> set.</summary>
    [DllImport(GioLibrary, EntryPoint = "g_bus_get_sync")]
    public static extern IntPtr BusGet(int busType, IntPtr cancellable, out IntPtr error);

    /// <summary>
    /// Calls a method and waits at most <paramref name="timeoutMs"/>; returns the reply tuple,
    /// which the caller unrefs, or zero with <paramref name="error"/> set. Takes over a floating
    /// <paramref name="parameters"/>.
    /// </summary>
    [DllImport(GioLibrary, EntryPoint = "g_dbus_connection_call_sync")]
    public static extern IntPtr CallSync(IntPtr connection, string busName, string path, string iface, string method,
        IntPtr parameters, IntPtr replyType, int flags, int timeoutMs, IntPtr cancellable, out IntPtr error);

    /// <summary>Starts delivering matching signals to <paramref name="callback"/>; returns the subscription id, 0 on failure.</summary>
    [DllImport(GioLibrary, EntryPoint = "g_dbus_connection_signal_subscribe")]
    public static extern uint SignalSubscribe(IntPtr connection, string? sender, string? iface, string? signal,
        string? path, string? arg0, int flags, SignalCallback callback, IntPtr data, IntPtr freeData);

    /// <summary>Ends the subscription <paramref name="id"/>.</summary>
    [DllImport(GioLibrary, EntryPoint = "g_dbus_connection_signal_unsubscribe")]
    public static extern void SignalUnsubscribe(IntPtr connection, uint id);

    /// <summary>Returns a new floating int32 value.</summary>
    [DllImport(GLibLibrary, EntryPoint = "g_variant_new_int32")]
    public static extern IntPtr NewInt32(int value);

    /// <summary>Returns a new floating tuple holding <paramref name="children"/>, which it takes over.</summary>
    [DllImport(GLibLibrary, EntryPoint = "g_variant_new_tuple")]
    public static extern IntPtr NewTuple(IntPtr[] children, UIntPtr count);

    /// <summary>Returns child <paramref name="index"/> of a tuple, with a reference the caller drops.</summary>
    [DllImport(GLibLibrary, EntryPoint = "g_variant_get_child_value")]
    public static extern IntPtr ChildValue(IntPtr value, UIntPtr index);

    /// <summary>Returns the number in an int32 value.</summary>
    [DllImport(GLibLibrary, EntryPoint = "g_variant_get_int32")]
    public static extern int GetInt32(IntPtr value);

    /// <summary>Drops a reference to a value.</summary>
    [DllImport(GLibLibrary, EntryPoint = "g_variant_unref")]
    public static extern void Unref(IntPtr value);

    /// <summary>Frees an error.</summary>
    [DllImport(GLibLibrary, EntryPoint = "g_error_free")]
    public static extern void ErrorFree(IntPtr error);
}
