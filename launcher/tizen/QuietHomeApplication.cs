using System;
using System.Threading;
using QuietHome.Core;
using QuietHome.Core.Inputs;
using QuietHome.TizenHost;
using QuietHome.TizenHost.Platform;
using QuietHome.TizenHost.Views;
using Tizen.Applications;
using Tizen.NUI;

/// <summary>
/// The Tizen app that the TV starts in place of the container package's own app. It only
/// wires things up: it creates the Tizen ports, the NUI view and the core's
/// <see cref="HomeController"/>, and forwards the app's life cycle, window and key events
/// to the controller. Everything Quiet Home decides lives in the controller (launcher/core).
/// </summary>
public sealed class QuietHomeApplication : NUIApplication
{
    const string DemoIcons = "/usr/apps/com.samsung.tv.theme-resource/shared/res/VD_ICONS/i_icon_device_m_";

    // The inputs demo mode shows; their icons are the TV's own files.
    static readonly InputSource[] demoInputs =
    {
        new() { ConnectionState = true, SourceType = 0, IconPath = DemoIcons + "tv.svg" },
        new() { ConnectionState = true, SourceType = 14, EditName = "Game console", IconPath = DemoIcons + "gamepad.svg" },
        new() { ConnectionState = true, SourceType = 15, IconPath = DemoIcons + "hdmi2.svg" },
    };

    TizenInputSources? inputs;
    TizenInputPreview? preview;
    HomeController? controller;
    Tizen.NUI.Timer? clockTimer;

    /// <summary>Starts Quiet Home: creates the ports, view and controller, subscribes to the window and shows the Home page.</summary>
    protected override void OnCreate()
    {
        base.OnCreate();
        // The container's res/quiet-home/ (on the QE65S95F /opt/usr/apps/<container>/res/quiet-home/).
        string root = System.IO.Path.Combine(DirectoryInfo.Resource, "quiet-home") + "/";
        inputs = new TizenInputSources();
        var view = new HomeView(root);
        preview = new TizenInputPreview(view);
        var ports = new HomePorts(inputs, new TizenQuickSettings(), new FileThemeStore(DirectoryInfo.Data), new SystemClock(), new TizenSystemLauncher(), new StateFile(), preview);
        var home = new HomeController(ports, view, new HomeConfig(root + "apps.json", root + "apps.public.json", demoInputs));
        controller = home;
        Window.Default.Title = "Quiet Home";
        Window.Default.KeyEvent += OnKey;
        Window.Default.FocusChanged += (s, e) =>
        {
            if (e.FocusGained)
                home.FocusGained();
        };
        inputs.Watch(SynchronizationContext.Current);
        home.Start();
        clockTimer = new Tizen.NUI.Timer(30000);
        clockTimer.Tick += (s, e) =>
        {
            home.Tick();
            return true;
        };
        clockTimer.Start();
    }

    /// <summary>Back in front: restarts the clock and lets the controller reread the TV.</summary>
    protected override void OnResume()
    {
        base.OnResume();
        clockTimer?.Start();
        controller?.Resume();
    }

    /// <summary>In the background: stops the clock.</summary>
    protected override void OnPause()
    {
        base.OnPause();
        controller?.Pause();
        clockTimer?.Stop();
    }

    /// <summary>Launched again (Home key, QuickLaunch, screenshot recipe): passes the launch extras to the controller.</summary>
    protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
    {
        base.OnAppControlReceived(e);
        var extra = e.ReceivedAppControl.ExtraData;
        var options = LaunchOptions.Parse(key => extra.TryGet(key, out string value) ? value : null);
        clockTimer?.Start();
        controller?.AppControl(options);
    }

    /// <summary>Ending: stops the controller, the TV's input callback and the input preview.</summary>
    protected override void OnTerminate()
    {
        controller?.Terminate();
        inputs?.Dispose();
        preview?.Dispose();
        base.OnTerminate();
    }

    /// <summary>Passes each key press (not releases) to the controller with its meaning and a monotonic time.</summary>
    void OnKey(object? sender, Window.KeyEventArgs e)
    {
        if (e.Key == null || e.Key.State != Key.StateType.Down)
            return;
        string name = e.Key.KeyPressedName;
        controller?.Key(KeyNames.ToRemoteKey(name), name, Environment.TickCount64);
    }

    /// <summary>Runs the app.</summary>
    public static void Main(string[] args)
    {
        using var app = new QuietHomeApplication();
        app.Run(args);
    }
}
