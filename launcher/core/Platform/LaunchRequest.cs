namespace QuietHome.Core.Platform;

/// <summary>
/// What <see cref="ISystemLauncher"/> should open: an app ID and, for Samsung's settings, the
/// screen inside it. <see cref="HomeController"/> creates one for each card action that leaves Quiet Home and
/// writes <see cref="Activity"/> to the diagnostics file after sending it.
/// </summary>
/// <param name="AppId">Tizen ID of the app to open.</param>
/// <param name="MenuItem">For Samsung's settings, the screen to open, for example "networksettings"; null opens the main menu.</param>
public sealed record LaunchRequest(string AppId, string? MenuItem = null)
{
    /// <summary>Tizen ID of Samsung's settings app.</summary>
    public const string SamsungSettingsAppId = "org.tizen.menu";

    /// <summary>Tizen ID of Samsung Home, whose source list renames inputs.</summary>
    public const string SourceEditorAppId = "com.samsung.tv.csfs";

    /// <summary>Tizen ID of the app that shows the picture of the selected input.</summary>
    public const string TvViewerAppId = "org.tizen.tv-viewer";

    /// <summary>Opens the TV picture, after switching the input.</summary>
    public static LaunchRequest TvViewer { get; } = new(TvViewerAppId);

    /// <summary>Opens Samsung Home's source list, where inputs can be renamed.</summary>
    public static LaunchRequest SourceEditor { get; } = new(SourceEditorAppId);

    /// <summary>Returns the request that opens Samsung's settings, on screen <paramref name="menuItem"/> or on the main menu when it is null.</summary>
    public static LaunchRequest SamsungSettings(string? menuItem) => new(SamsungSettingsAppId, menuItem);

    /// <summary>
    /// Returns the diagnostics activity for this request sent with result
    /// <paramref name="code"/>, for example "launch:org.tizen.menu:networksettings:0".
    /// </summary>
    public string Activity(int code) => "launch:" + AppId + (MenuItem == null ? "" : ":" + MenuItem) + ":" + code;
}
