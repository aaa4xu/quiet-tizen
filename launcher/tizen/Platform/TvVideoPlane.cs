using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Tizen.NUI;
using Tizen.TV.Broadcast.TVService;
using Tizen.TV.Broadcast.TVService.API.Interface;
using Tizen.TV.Broadcast.TVService.Types;
using Tizen.TV.NUI;
using QuietHome.TizenHost.Views;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// The TV's main video plane through Samsung's private TV service
/// (Tizen.TV.Broadcast.TVService and Tizen.TV.NUI from the TV's framework directory): ties
/// the plane, which shows the input on screen, to Quiet Home's window, places it in a
/// rectangle and puts it back to full screen, and mutes and unmutes the input's sound. It never changes
/// the TV's source: previewing another input switched the TV's HDR and game mode. It passes
/// on the TV's notices of the plane's actual size, because the TV may reset the size by
/// itself. Only <see cref="TizenInputPreview"/> uses it, and only inside try blocks, because
/// these assemblies and the internal platform privilege may be missing on other TVs. The
/// call order follows what the QE65S95F showed; see docs/12-hdmi-preview.md.
/// </summary>
sealed class TvVideoPlane : IDisposable
{
    // The TV service's success code; anything else means the TV did not take the call.
    const int Done = 1;
    const string LogPath = "/tmp/quiet-preview.txt";

    // Longest wait for the TV to confirm that the plane is untied from the window.
    const int UntieWaitMs = 500;

    static readonly object logLock = new();

    readonly IVideoControl video;
    readonly IAVControl sound;
    readonly ISourceControl source;
    readonly ISignalSubscriber signals;
    // Set by the TV's notice that the plane's window changed.
    readonly ManualResetEventSlim windowChanged = new();
    // The UI thread, where the TV's notices are passed on; they arrive on another thread.
    readonly SynchronizationContext? ui = SynchronizationContext.Current;
    bool tied;

    /// <summary>
    /// Opens the controls of the TV's main picture and subscribes to the TV's notices of its
    /// size and window. Create it on the UI thread.
    /// </summary>
    public TvVideoPlane()
    {
        var api = TVServiceAPI.Instance();
        video = api.CreateVideoControl(Profile.Main, 0);
        sound = api.CreateAVControl(Profile.Main, 0);
        source = api.CreateSourceControl(Profile.Main, 0);
        signals = api.CreateSignalSubscriber(OnSignal);
        Try(() => signals.Subscribe(SignalType.SignalUpdateSizeInfo), "Subscribe SignalUpdateSizeInfo");
        Try(() => signals.Subscribe(SignalType.SignalXWindowChanged), "Subscribe SignalXWindowChanged");
    }

    /// <summary>
    /// Returns whether the TV is playing the input on screen. After another app, for example,
    /// it is not, and the plane would show black; any failure counts as not playing.
    /// </summary>
    public bool Playing()
    {
        bool active = false;
        try
        {
            int result = source.IsActive(ref active);
            Log($"IsActive -> {result} {active}");
            return result == Done && active;
        }
        catch (Exception ex)
        {
            Log("IsActive !! " + ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Raised on the UI thread with the plane's rectangle (window pixels) whenever the TV
    /// reports it: after every <see cref="Hold"/>, and when the TV resets it itself. A TV that
    /// does not send the notice never raises it.
    /// </summary>
    public event Action<Box>? SizeReported;

    /// <summary>
    /// Puts the input on screen into <paramref name="rect"/> (window pixels), tying the
    /// plane to the window first if it is not. Returns whether it was tied just now, after
    /// which the TV may still reset the size. Throws when the TV refuses a call.
    /// </summary>
    public bool Show(Box rect)
    {
        bool tying = !tied;
        if (tying)
        {
            // Ties the plane to the window and gives the window's video area the rectangle too.
            // The TV service takes height before width.
            uint window = (uint)TVWindow.GetResourceID(Window.Default);
            Check(video.SetXWindowSize(window, rect.X, rect.Y, rect.H, rect.W), "SetXWindowSize");
            tied = true;
        }
        Hold(rect);
        return tying;
    }

    /// <summary>Puts the plane back into <paramref name="rect"/>. Throws when the TV refuses it.</summary>
    public void Hold(Box rect) =>
        Check(video.SetSize(rect.X, rect.Y, rect.H, rect.W), $"SetSize {rect.X},{rect.Y},{rect.W},{rect.H}");

    /// <summary>
    /// Ends the preview: the plane goes back to full screen and is untied from the window.
    /// Both steps run even when one fails: the TV keeps the plane's size otherwise, even
    /// after an input switch. With <paramref name="confirm"/> it waits (at most
    /// <see cref="UntieWaitMs"/>) for the TV's notice that the window changed, so the TV's
    /// viewer, started right after, ties the plane to itself only once the untie is done;
    /// without the wait the input opened black.
    /// </summary>
    public void Reset(bool confirm)
    {
        Try(() => video.SetSize(0, 0, Layout.ScreenHeight, Layout.ScreenWidth), "SetSize full");
        windowChanged.Reset();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Try(() => video.SetXWindow(0), "SetXWindow 0");
        if (confirm)
            Log($"untie confirmed {windowChanged.Wait(UntieWaitMs)} after {watch.ElapsedMilliseconds} ms");
        tied = false;
    }

    /// <summary>
    /// Mutes the input's sound under the home screen, as Samsung's home does. The TV answers
    /// 0 when it is already muted, which is only recorded.
    /// </summary>
    public void Silence() => Try(() => sound.MuteAudio(1), "MuteAudio 1");

    /// <summary>Turns the input's sound back on; the TV's viewer does not do it by itself.</summary>
    public void Unsilence() => Try(() => sound.MuteAudio(0), "MuteAudio 0");

    /// <summary>Stops the notices and releases the TV service's controls.</summary>
    public void Dispose()
    {
        Try(() => signals.Unsubscribe(SignalType.SignalUpdateSizeInfo), "Unsubscribe SignalUpdateSizeInfo");
        Try(() => signals.Unsubscribe(SignalType.SignalXWindowChanged), "Unsubscribe SignalXWindowChanged");
        video.Dispose();
        sound.Dispose();
        source.Dispose();
        windowChanged.Dispose();
    }

    /// <summary>
    /// Receives a notice from the TV on its own thread and returns 0 (handled). A window
    /// notice releases a waiting <see cref="Reset"/>. A size notice carries x, y, width and
    /// height as four 16-bit numbers, lowest first; it is passed on to the UI thread.
    /// </summary>
    int OnSignal(SignalType type, Profile profile, ushort screen, SignalData data, object userData)
    {
        if (profile != Profile.Main)
            return 0;
        if (type == SignalType.SignalXWindowChanged)
        {
            Log($"window changed {data.ll}");
            windowChanged.Set();
            return 0;
        }
        if (type != SignalType.SignalUpdateSizeInfo)
            return 0;
        ulong value = data.ll;
        var box = new Box((ushort)value, (ushort)(value >> 16), (ushort)(value >> 32), (ushort)(value >> 48));
        Log($"size {box.X},{box.Y},{box.W},{box.H}");
        ui?.Post(_ => SizeReported?.Invoke(box), null);
        return 0;
    }

    /// <summary>Logs call <paramref name="name"/> and throws when its <paramref name="result"/> is not the success code.</summary>
    static void Check(int result, string name)
    {
        Log(name + " -> " + result);
        if (result != Done)
            throw new InvalidOperationException(name + " returned " + result);
    }

    /// <summary>Runs restoring call <paramref name="name"/>, logs it and ignores its failure.</summary>
    static void Try(Func<int> call, string name)
    {
        try
        {
            Log(name + " -> " + call());
        }
        catch (Exception ex)
        {
            // Restoring goes on with the next step.
            Log(name + " !! " + ex.GetType().Name);
        }
    }

    /// <summary>
    /// Appends one line with the time to /tmp/quiet-preview.txt, the record of what the TV
    /// service answered; the file starts over past 64 KiB. Never throws.
    /// </summary>
    static void Log(string line)
    {
        try
        {
            lock (logLock)
            {
                var file = new FileInfo(LogPath);
                if (file.Exists && file.Length > 64 * 1024)
                    file.Delete();
                File.AppendAllText(LogPath, DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + line + "\n");
            }
        }
        catch (Exception)
        {
            // The record is only for diagnosis.
        }
    }
}
