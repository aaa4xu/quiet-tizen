using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using QuietHome.Core.Inputs;
using QuietHome.Core.Platform;
using QuietHome.TizenHost.Interop;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// The TV's inputs through Samsung's source library (<see cref="SourceApi"/>) and the vconf
/// key of the input on screen. Implements the core's <see cref="IInputSources"/> port for
/// the Quiet Home app; the app creates one at start, calls <see cref="Watch"/> once and
/// disposes it when it terminates. The TV's change callback arrives on the TV's own thread
/// and is posted to the UI thread here, so <see cref="Changed"/> handlers run on the UI thread.
/// </summary>
sealed class TizenInputSources : IInputSources, IDisposable
{
    // Any value works; it identifies this registration when unregistering.
    static readonly IntPtr callbackToken = new(0x5157);

    // Kept in a field so the garbage collector does not free it while the TV holds it.
    SourceApi.SourceListChanged? callback;

    /// <summary>Raised on the UI thread after the TV reports a change in its input list.</summary>
    public event Action? Changed;

    /// <summary>-1 until <see cref="Watch"/> subscribed; see <see cref="IInputSources.WatchStatus"/>.</summary>
    public int WatchStatus { get; private set; } = -1;

    /// <summary>
    /// Subscribes to the TV's input list changes and delivers them through
    /// <paramref name="ui"/>. Without a UI context it does nothing and
    /// <see cref="WatchStatus"/> stays -1.
    /// </summary>
    public void Watch(SynchronizationContext? ui)
    {
        if (ui == null)
            return;
        callback = (what, data) => ui.Post(_ => Changed?.Invoke(), null);
        try
        {
            WatchStatus = SourceApi.RegisterSourceListChanged(callback, callbackToken);
        }
        catch (Exception)
        {
            WatchStatus = -2;
        }
    }

    /// <summary>Reads the connected inputs; the TV's JSON string is freed here. Never throws.</summary>
    public InputReading Read()
    {
        IntPtr list = IntPtr.Zero;
        try
        {
            list = SourceApi.GetSourceList();
            if (list == IntPtr.Zero)
                return new InputReading(new List<InputSource>(), "empty");
            return new InputReading(InputList.Parse(Marshal.PtrToStringAnsi(list) ?? "null"), "");
        }
        catch (Exception ex)
        {
            return new InputReading(new List<InputSource>(), ex.Message);
        }
        finally
        {
            if (list != IntPtr.Zero)
                Libc.Free(list);
        }
    }

    /// <summary>Returns the source type from vconf, or null when the key cannot be read or is negative.</summary>
    public int? Current()
    {
        try
        {
            if (Vconf.GetInt(Vconf.SourceTypeKey, out int sourceType) == 0 && sourceType >= 0)
                return sourceType;
        }
        catch (Exception)
        {
            // No vconf: the caller keeps the input it knew.
        }
        return null;
    }

    /// <summary>Switches the TV's input; returns the library's result, negative on failure.</summary>
    public int Switch(int sourceType) => SourceApi.ConnectSource(sourceType);

    /// <summary>Stops the TV's change callback, if <see cref="Watch"/> subscribed.</summary>
    public void Dispose()
    {
        if (WatchStatus != 0)
            return;
        try
        {
            SourceApi.UnregisterSourceListChanged(callbackToken);
        }
        catch (Exception)
        {
            // The app is terminating anyway.
        }
        WatchStatus = -1;
    }
}
