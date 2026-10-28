using QuietHome.Core.Platform;
using QuietHome.TizenHost.Interop;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// Opens other apps with Tizen's app control API (<see cref="AppControlRequest"/>).
/// Implements the core's <see cref="ISystemLauncher"/> port for the Quiet Home app. The extras
/// that Samsung's own apps expect live only here: Samsung's settings get the screen to open
/// and its fast-start flags, Samsung Home gets the Source key's entry point.
/// </summary>
sealed class TizenSystemLauncher : ISystemLauncher
{
    /// <summary>Builds and sends one launch request; returns whether it was created and the first error code.</summary>
    public LaunchResult Launch(LaunchRequest request)
    {
        using var control = new AppControlRequest();
        if (!control.Created)
            return new LaunchResult(false, control.Result);
        control.SetAppId(request.AppId);
        control.SetOperation(AppControlRequest.DefaultOperation);
        if (request.AppId == LaunchRequest.SamsungSettingsAppId)
        {
            if (request.MenuItem != null)
            {
                control.AddExtra("menuitem", request.MenuItem);
                control.AddExtra("launchoption", "pressed");
            }
            control.AddExtra("__K_EX_FORCE_BOOST", "enable");
            control.AddExtra("__K_SPLASH_SCREEN", "LA_UI_VR_TRANSPARENT");
        }
        // The Source key's entry point opens Samsung Home on its source list.
        if (request.AppId == LaunchRequest.SourceEditorAppId)
            control.AddExtra("entry_point", "source_key");
        control.Send();
        return new LaunchResult(true, control.Result);
    }
}
