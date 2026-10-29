using System;
using System.Collections.Generic;
using System.Threading;
using QuietHome.Core.Platform;
using QuietHome.TizenHost.Interop;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// The TV's HDMI-CEC service, <c>org.tizen.tv.hdmicec</c> on the system bus, reached through
/// <see cref="Gio"/>. Implements the core's <see cref="ICecDevices"/> port for the Quiet Home
/// app; the app creates one at start, calls <see cref="Watch"/> once and disposes it when it
/// terminates. The service and its methods are private and were checked only on the
/// QE65S95F (docs/13-input-state.md); on a TV without them every input simply has no CEC device.
/// </summary>
sealed class TizenCecDevices : ICecDevices, IDisposable
{
    const string Service = "org.tizen.tv.hdmicec", ObjectPath = "/org/tizen/tv/hdmicec", Interface = "org.tizen.tv.HdmiCec";

    // HDMI ports asked about; the TV's source type of port n is n + 12 (HDMI1 is 13).
    const int Ports = 4, FirstHdmiSource = 13;

    // A healthy service answers in a few milliseconds; the call blocks the UI thread.
    const int TimeoutMs = 500;

    IntPtr bus;
    uint subscription;
    // Kept in a field so the garbage collector does not free it while GIO holds it.
    Gio.SignalCallback? callback;

    /// <summary>Raised on the UI thread after the service reports that its device list changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// Subscribes to the service's <c>UpdateCecDeviceList</c> signal and delivers it through
    /// <paramref name="ui"/>. Without a UI context or a system bus it does nothing.
    /// </summary>
    public void Watch(SynchronizationContext? ui)
    {
        if (ui == null || Bus() == IntPtr.Zero)
            return;
        callback = (connection, sender, path, iface, signal, parameters, data) => ui.Post(_ => Changed?.Invoke(), null);
        try
        {
            subscription = Gio.SignalSubscribe(bus, Service, Interface, "UpdateCecDeviceList", ObjectPath, null, 0, callback, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception)
        {
            // Without the notice the inputs are still reread on resume and by the poll.
        }
    }

    /// <summary>
    /// Asks <c>IsCECDeviceConnected</c> for HDMI1..4 and returns the source types of the ports
    /// that answered 1; a device in standby still counts. Never throws.
    /// </summary>
    public IReadOnlySet<int> Inputs()
    {
        var inputs = new HashSet<int>();
        if (Bus() == IntPtr.Zero)
            return inputs;
        for (int port = 1; port <= Ports; port++)
        {
            if (Ask("IsCECDeviceConnected", port) == 1)
                inputs.Add(FirstHdmiSource + port - 1);
        }
        return inputs;
    }

    /// <summary>Stops the signal.</summary>
    public void Dispose()
    {
        if (subscription != 0)
            Gio.SignalUnsubscribe(bus, subscription);
        subscription = 0;
    }

    /// <summary>Returns the system bus connection, opening it on first use; zero when there is none.</summary>
    IntPtr Bus()
    {
        if (bus != IntPtr.Zero)
            return bus;
        try
        {
            bus = Gio.BusGet(Gio.SystemBus, IntPtr.Zero, out IntPtr error);
            Free(error);
        }
        catch (Exception)
        {
            // GIO missing: no CEC devices.
        }
        return bus;
    }

    /// <summary>Calls <paramref name="method"/> with one int32 and returns the int32 it answers, or null when the call fails.</summary>
    int? Ask(string method, int argument)
    {
        IntPtr reply = IntPtr.Zero;
        try
        {
            var arguments = Gio.NewTuple(new[] { Gio.NewInt32(argument) }, (UIntPtr)1);
            reply = Gio.CallSync(bus, Service, ObjectPath, Interface, method, arguments, IntPtr.Zero, 0, TimeoutMs, IntPtr.Zero, out IntPtr error);
            Free(error);
            if (reply == IntPtr.Zero)
                return null;
            IntPtr value = Gio.ChildValue(reply, UIntPtr.Zero);
            int result = Gio.GetInt32(value);
            Gio.Unref(value);
            return result;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (reply != IntPtr.Zero)
                Gio.Unref(reply);
        }
    }

    /// <summary>Frees a GIO error if there is one.</summary>
    static void Free(IntPtr error)
    {
        if (error != IntPtr.Zero)
            Gio.ErrorFree(error);
    }
}
