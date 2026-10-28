using System;
using System.Collections.Generic;
using System.Globalization;
using QuietHome.Core.Actions;
using QuietHome.Core.Catalog;
using QuietHome.Core.Diagnostics;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
using QuietHome.Core.Platform.Demo;
using QuietHome.Core.Screens;
using QuietHome.Core.Settings;
using QuietHome.Core.Theme;

namespace QuietHome.Core;

/// <summary>
/// Quiet Home's behaviour, without the TV: it holds the state (page, focus, inputs, catalog,
/// settings, theme, demo mode), reacts to the app's life cycle and the remote, acts through
/// the platform ports and shows the result through <see cref="IHomeView"/>. The TV host
/// (launcher/tizen/QuietHomeApplication.cs) creates one, forwards Tizen's events to it and
/// does nothing else. Errors become a notice on screen instead of an exception.
/// </summary>
public sealed class HomeController
{
    static readonly int processId = Environment.ProcessId;

    readonly HomePorts tv;
    readonly IHomeView view;
    readonly HomeConfig config;
    readonly Navigator navigator = new();
    readonly KeyRepeatFilter keys = new();
    readonly QuickSettingsState settings = new();
    readonly DemoInputSources demoInputs;
    readonly DemoClock demoClock = new();
    DemoQuickSettings demoSettings = new();
    DemoThemeStore demoTheme = new();

    List<LibraryApp> library = new();
    List<LibraryApp> ownLibrary = new();
    List<LibraryApp>? publicLibrary;
    string libraryError = "";
    List<InputSource> inputSources = new();
    string inputError = "";
    // The input list is read once per event; a rebuild reads it only on arriving at Home without a fresh reading.
    bool inputsFresh;
    Page? builtPage;
    HomeScreen? screen;
    bool demo;
    bool dark = true;
    string themeError = "";
    int lastSource;
    PreviewState preview;

    /// <summary>
    /// Creates the controller and subscribes to the TV's input changes and preview failures.
    /// Nothing is shown until <see cref="Start"/>.
    /// </summary>
    public HomeController(HomePorts tv, IHomeView view, HomeConfig config)
    {
        this.tv = tv;
        this.view = view;
        this.config = config;
        demoInputs = new DemoInputSources(tv.Inputs, config.DemoInputs);
        tv.Inputs.Changed += SourcesChanged;
        tv.Preview.Failed += PreviewFailed;
        view.ArtworkLoaded += ArtworkLoaded;
    }

    /// <summary>The navigation state, for tests.</summary>
    public Navigator Navigator => navigator;

    /// <summary>The screen shown last, or null before <see cref="Start"/>.</summary>
    public HomeScreen? Screen => screen;

    /// <summary>True while demo mode (<c>quiet_demo=1</c>) is on.</summary>
    public bool Demo => demo;

    // Demo mode puts the core's demo ports in front of the TV's on every access.
    IInputSources Inputs => demo ? demoInputs : tv.Inputs;

    IQuickSettings QuickSettings => demo ? demoSettings : tv.QuickSettings;

    IThemeStore ThemeStore => demo ? demoTheme : tv.ThemeStore;

    IClock Clock => demo ? demoClock : tv.Clock;

    /// <summary>
    /// Starts the app: loads the owner's catalog and theme, starts loading artwork, shows
    /// the Home page with the input on screen focused and mutes the input's sound.
    /// </summary>
    public void Start()
    {
        try
        {
            library = AppCatalog.Load(config.CatalogPath);
        }
        catch (Exception ex)
        {
            libraryError = ex.Message;
        }
        ownLibrary = library;
        LoadTheme();
        view.Preload(library);
        RefreshSource();
        navigator.Selected = SourceIndex();
        Rebuild();
        SilenceInput();
        WriteState("created");
    }

    /// <summary>
    /// The app came back to the foreground: mutes the input's sound, rereads the input on
    /// screen and the inputs, and the settings on the Settings page; on the Home page the
    /// input preview comes back.
    /// </summary>
    public void Resume()
    {
        SilenceInput();
        RefreshSource();
        RefreshInputCards();
        if (builtPage != null && navigator.Page == Page.Settings)
        {
            RefreshSettings();
            Render(RenderMode.Refresh);
        }
        else if (builtPage == Page.Home)
        {
            Render(RenderMode.Refresh);
        }
        WriteState("resumed");
    }

    /// <summary>The app went to the background: an adjustment in progress ends, and the TV gets its picture and sound back.</summary>
    public void Pause()
    {
        navigator.Adjusting = Adjustment.None;
        ReleaseScreen(true);
        WriteState("paused");
    }

    /// <summary>
    /// The window got the focus back. Samsung Home's source editor opens over this window
    /// without pausing it, so the inputs are reread here.
    /// </summary>
    public void FocusGained()
    {
        RefreshInputCards();
        WriteState("focus_gained");
    }

    /// <summary>The TV reported a change in its inputs: rereads them and redraws the Home page if they changed.</summary>
    public void SourcesChanged()
    {
        RefreshInputCards();
        WriteState("sources_changed");
    }

    /// <summary>
    /// The TV launched the app again with <paramref name="options"/> (Home key, QuickLaunch,
    /// README screenshots): applies demo mode and theme, mutes the input's sound, then shows
    /// the asked page, focused on its first toolbar button when the options ask for it.
    /// </summary>
    public void AppControl(LaunchOptions options)
    {
        bool changed = ApplyLaunchOptions(options);
        SilenceInput();
        if (options.Page == Page.Settings)
        {
            OpenSettings("settings_request");
        }
        else if (options.Page == Page.Apps)
        {
            navigator.Page = Page.Apps;
            navigator.Zone = Zone.FirstRow;
            navigator.Selected = 0;
            navigator.LibraryPage = 0;
            Rebuild();
        }
        else
        {
            Home("home_request", changed);
        }
        if (options.FocusToolbar && screen != null)
        {
            navigator.Zone = Zone.Toolbar;
            navigator.Selected = screen.Rows.Row(-1)[0];
            Render(RenderMode.Refresh);
            WriteState("toolbar_request");
        }
    }

    /// <summary>
    /// Handles one key press: <paramref name="key"/> is what it means, <paramref name="name"/>
    /// the TV's key name and <paramref name="milliseconds"/> a monotonic time, used to drop
    /// held-key repeats. Errors are shown as a notice.
    /// </summary>
    public void Key(RemoteKey key, string name, long milliseconds)
    {
        if (!keys.Accept(name, milliseconds))
            return;
        try
        {
            switch (navigator.HandleKey(key, screen?.Rows ?? new SlotRows(new List<int>()), library.Count))
            {
                case KeyOutcome.GoHome:
                    Home("home_key");
                    break;
                case KeyOutcome.OpenSettings:
                    OpenSettings("settings_key");
                    break;
                case KeyOutcome.LeaveApps:
                    Home("apps_back");
                    break;
                case KeyOutcome.AdjustDown:
                    Adjust(-1);
                    break;
                case KeyOutcome.AdjustUp:
                    Adjust(1);
                    break;
                case KeyOutcome.Refocus:
                    Render(RenderMode.AnimateFocus);
                    break;
                case KeyOutcome.Rebuild:
                    // Arriving at Home through the tabs: the input on screen is the card shown wide.
                    if (navigator.Page == Page.Home && navigator.Zone == Zone.Tabs)
                        navigator.Selected = SourceIndex();
                    Rebuild();
                    break;
                case KeyOutcome.Activate:
                    Activate();
                    break;
                case KeyOutcome.ReturnToInput:
                    SelectSource(lastSource);
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>Updates the clock on screen; the host calls it every 30 seconds while the app is in front.</summary>
    public void Tick()
    {
        if (builtPage != null)
            view.ShowClock(ClockNow());
    }

    /// <summary>The app is ending: gives the TV its picture and sound back and stops listening to it.</summary>
    public void Terminate()
    {
        ReleaseScreen(true);
        tv.Inputs.Changed -= SourcesChanged;
        tv.Preview.Failed -= PreviewFailed;
        view.ArtworkLoaded -= ArtworkLoaded;
    }

    /// <summary>
    /// Applies demo mode and theme from a launch: <c>quiet_demo=1</c> enters demo mode and
    /// any other launch leaves it; <c>quiet_theme</c> lasts until then and is not saved.
    /// Returns whether the page must be rebuilt.
    /// </summary>
    bool ApplyLaunchOptions(LaunchOptions options)
    {
        bool changed = options.Demo != demo;
        if (changed)
        {
            demo = options.Demo;
            navigator.LibraryPage = 0;
            if (demo)
            {
                demoSettings = new DemoQuickSettings();
                demoTheme = new DemoThemeStore();
                if (publicLibrary == null)
                {
                    try
                    {
                        publicLibrary = AppCatalog.Load(config.PublicCatalogPath);
                    }
                    catch (Exception)
                    {
                        publicLibrary = ownLibrary;
                    }
                }
            }
            library = demo ? publicLibrary! : ownLibrary;
        }
        if (options.Dark is bool wantDark)
        {
            if (dark != wantDark)
            {
                dark = wantDark;
                changed = true;
            }
        }
        else if (changed && !demo)
        {
            LoadTheme();
        }
        return changed;
    }

    /// <summary>
    /// Shows the Home page with the input on screen focused, after rereading the inputs.
    /// The page is redrawn only when something changed or <paramref name="rebuild"/> asks
    /// for it (another theme or demo mode). Records <paramref name="activity"/>.
    /// </summary>
    void Home(string activity, bool rebuild = false)
    {
        RefreshSource();
        bool inputsChanged = RefreshInputSources();
        if (builtPage == null)
            return;
        bool unchanged = !rebuild && !inputsChanged && navigator.Page == Page.Home && navigator.Zone == Zone.FirstRow
            && navigator.Adjusting == Adjustment.None && navigator.Selected == SourceIndex();
        navigator.Page = Page.Home;
        navigator.Zone = Zone.FirstRow;
        navigator.Adjusting = Adjustment.None;
        navigator.Selected = SourceIndex();
        if (!unchanged)
            Rebuild();
        WriteState(activity);
    }

    /// <summary>Shows the Settings page with its first tile focused and records <paramref name="activity"/>.</summary>
    void OpenSettings(string activity)
    {
        navigator.Page = Page.Settings;
        navigator.Zone = Zone.FirstRow;
        navigator.Selected = 0;
        navigator.Adjusting = Adjustment.None;
        Rebuild();
        WriteState(activity);
    }

    /// <summary>Remembers the input on screen, if the TV reports one.</summary>
    void RefreshSource()
    {
        var current = Inputs.Current();
        if (current != null)
            lastSource = current.Value;
    }

    /// <summary>Returns the slot of the input on screen among the input cards (the page's first slots); 0 in demo mode or when it is not connected.</summary>
    int SourceIndex() => demo ? 0 : Math.Max(0, inputSources.FindIndex(s => s.SourceType == lastSource));

    /// <summary>Rereads the connected inputs; returns whether they changed.</summary>
    bool RefreshInputSources()
    {
        string before = InputList.Fingerprint(inputSources);
        var reading = Inputs.Read();
        inputSources = reading.Inputs;
        inputError = reading.Error;
        inputsFresh = true;
        return before != InputList.Fingerprint(inputSources);
    }

    /// <summary>On the Home page (not while adjusting), rereads the inputs and redraws the page if they changed.</summary>
    void RefreshInputCards()
    {
        if (builtPage != null && navigator.Page == Page.Home && navigator.Adjusting == Adjustment.None && RefreshInputSources())
            Rebuild();
    }

    /// <summary>Reads the stored theme: dark unless "light" is stored.</summary>
    void LoadTheme() => dark = ThemeSetting.IsDark(ThemeStore.Read());

    /// <summary>Switches between dark and light, saves the choice and redraws; a failed save is shown and recorded.</summary>
    void ToggleTheme()
    {
        dark = !dark;
        themeError = "";
        try
        {
            ThemeStore.Write(ThemeSetting.Text(dark));
        }
        catch (Exception ex)
        {
            themeError = ex.Message;
        }
        Rebuild();
        if (themeError != "")
            ShowError("Theme changed for now; it could not be saved.");
        WriteState("theme_changed");
    }

    /// <summary>Returns the clock text for now.</summary>
    string ClockNow() => ClockText.Format(Clock.Now, CultureInfo.CurrentCulture);

    /// <summary>Returns the screen for the current state.</summary>
    HomeScreen BuildScreen() => ScreenBuilder.Build(new ScreenState(
        navigator.Page, navigator.Zone, navigator.Selected, navigator.LibraryPage, navigator.Adjusting,
        dark, inputSources, library, settings, ClockNow()));

    /// <summary>
    /// Draws the current page from scratch. Arriving at Home without a fresh reading reads the
    /// inputs first; the Apps page is kept within the catalog; the Settings page rereads the
    /// settings. The focus is then moved off anything the new page lacks.
    /// </summary>
    void Rebuild()
    {
        if (navigator.Page == Page.Home && builtPage != Page.Home && !inputsFresh)
            RefreshInputSources();
        if (navigator.Page == Page.Apps && library.Count > 0)
            navigator.LibraryPage = Math.Clamp(navigator.LibraryPage, 0, CatalogGrid.PageCount(library.Count) - 1);
        if (navigator.Page == Page.Settings)
            RefreshSettings();
        navigator.SettleAfterBuild(BuildScreen().Rows);
        screen = BuildScreen();
        view.Render(screen, RenderMode.Rebuild);
        builtPage = navigator.Page;
        inputsFresh = false;
        UpdatePreview();
        WriteState("page_ready");
    }

    /// <summary>Shows the current state on the page already drawn, in <paramref name="mode"/>.</summary>
    void Render(RenderMode mode)
    {
        screen = BuildScreen();
        view.Render(screen, mode);
        UpdatePreview();
        WriteState("focus");
    }

    /// <summary>
    /// Asks for the live picture of the input card shown wide (focused, or the one Down
    /// would focus from the Home tab), or puts the preview away when there is none to
    /// preview. Only the card of the input on screen gets one: its
    /// picture is already there, so the TV changes no source, HDR or game mode for it, while
    /// previewing another input switched those modes on every focus move. Live TV gets none
    /// (without an antenna the TV covers the screen with its notice), nor does demo mode,
    /// nor any card after the TV refused a preview.
    /// </summary>
    void UpdatePreview()
    {
        if (preview == PreviewState.Unsupported)
            return;
        int slot = navigator.Selected;
        bool previewable = !demo && builtPage == Page.Home && navigator.Page == Page.Home
            && (navigator.Zone == Zone.FirstRow || navigator.Zone == Zone.Tabs)
            && navigator.Adjusting == Adjustment.None && slot >= 0 && slot < inputSources.Count
            && inputSources[slot].SourceType != 0 && inputSources[slot].SourceType == lastSource;
        if (!previewable)
        {
            HidePreview();
            return;
        }
        preview = PreviewState.On;
        try
        {
            tv.Preview.Show(inputSources[slot].SourceType, slot);
        }
        catch (Exception)
        {
            PreviewFailed();
        }
    }

    /// <summary>Puts a preview that was asked for away, giving the TV its full picture back.</summary>
    void HidePreview()
    {
        if (preview != PreviewState.On)
            return;
        preview = PreviewState.Off;
        try
        {
            tv.Preview.Hide();
        }
        catch (Exception)
        {
            // The port restores what it can; nothing else to do here.
        }
    }

    /// <summary>
    /// Quiet Home gives the screen up: any preview ends at once and the TV gets its
    /// full-screen picture back, and with <paramref name="sound"/> the input's sound too.
    /// </summary>
    void ReleaseScreen(bool sound)
    {
        if (preview == PreviewState.On)
            preview = PreviewState.Off;
        try
        {
            tv.Preview.Release(sound);
        }
        catch (Exception)
        {
            // The port restores what it can.
        }
    }

    /// <summary>
    /// Mutes the input's sound while Quiet Home is in front, as Samsung's home does; it comes
    /// back when Quiet Home gives the screen up. Demo mode leaves the TV's sound alone.
    /// </summary>
    void SilenceInput()
    {
        if (demo)
            return;
        try
        {
            tv.Preview.Silence();
        }
        catch (Exception)
        {
            // The sound simply stays on.
        }
    }

    /// <summary>The TV refused a preview: no more are asked for this session; records it.</summary>
    void PreviewFailed()
    {
        preview = PreviewState.Unsupported;
        WriteState("preview_failed");
    }

    /// <summary>Performs the action of the focused element; any old notice is cleared first.</summary>
    void Activate()
    {
        if (screen == null || navigator.Selected < 0 || navigator.Selected >= screen.Cards.Count)
            return;
        var action = screen.Cards[navigator.Selected].Action;
        view.ShowNotice("");
        switch (action)
        {
            case HomeAction.SwitchInput input:
                SelectSource(input.SourceType);
                return;
            case HomeAction.ShowPage show:
                navigator.Page = show.Page;
                navigator.Selected = show.Page == Page.Home ? SourceIndex() : 0;
                navigator.Zone = Zone.FirstRow;
                Rebuild();
                return;
            case HomeAction.LaunchApp app:
                Launch(new LaunchRequest(app.AppId));
                return;
            case HomeAction.OpenSamsungSettings menu:
                Launch(LaunchRequest.SamsungSettings(menu.MenuItem));
                return;
            case HomeAction.OpenSourceEditor:
                Launch(LaunchRequest.SourceEditor);
                return;
            case HomeAction.BeginAdjust adjust:
                RefreshSettings();
                if (adjust.Target == Adjustment.Brightness && !settings.CanEdit("brightness"))
                {
                    ShowError("Brightness is unavailable for the current input.");
                    return;
                }
                navigator.Adjusting = adjust.Target;
                Render(RenderMode.AnimateFocus);
                return;
            case HomeAction.SwitchGameMode:
                RefreshSettings();
                var mode = settings.NextGameModeValue();
                if (mode != null)
                    SetSetting(settings.GameModeId, mode);
                return;
            case HomeAction.CycleSetting cycle:
                RefreshSettings();
                var next = settings.NextCycleValue(cycle.Id);
                if (next != null)
                    SetSetting(cycle.Id, next);
                return;
            case HomeAction.ToggleTheme:
                ToggleTheme();
                return;
        }
    }

    /// <summary>Rereads every quick setting.</summary>
    void RefreshSettings() => settings.Refresh(QuickSettings);

    /// <summary>
    /// Writes <paramref name="value"/> to setting <paramref name="id"/>, rereads and shows the
    /// settings, and checks that the TV took the value. Records the outcome as
    /// "setting:id:value:verified".
    /// </summary>
    void SetSetting(string id, string value)
    {
        if (!settings.CanEdit(id))
        {
            ShowError("This setting is unavailable for the current input.");
            return;
        }
        bool written = false;
        try
        {
            written = QuickSettings.WriteItem(id, value);
        }
        catch (Exception)
        {
            // Treated like a refusal below.
        }
        RefreshSettings();
        Render(RenderMode.AnimateFocus);
        bool verified = written && settings.Value(id) == value;
        if (!verified)
            ShowError("The TV could not apply this setting. Try Advanced settings.");
        WriteState("setting:" + id + ":" + value + ":" + verified);
    }

    /// <summary>Changes the setting being adjusted one step down (-1) or up (+1).</summary>
    void Adjust(int delta)
    {
        if (navigator.Adjusting == Adjustment.Volume)
            ChangeVolume(delta);
        else
            ChangeBrightness(delta);
    }

    /// <summary>Moves the brightness one step within the TV's range.</summary>
    void ChangeBrightness(int delta)
    {
        var next = settings.NextBrightness(delta);
        if (next != null)
            SetSetting("brightness", next);
    }

    /// <summary>Moves the master volume one step within 0..100 and shows it; a failure points to the remote's volume keys.</summary>
    void ChangeVolume(int delta)
    {
        var volume = QuickSettings.GetVolume();
        bool changed = volume != null && QuickSettings.SetVolume(QuickSettingsState.NextVolume(volume.Value, delta));
        if (!changed)
            ShowError("Use the remote's volume buttons.");
        RefreshSettings();
        Render(RenderMode.AnimateFocus);
        WriteState("volume_changed");
    }

    /// <summary>
    /// Switches the TV to input <paramref name="source"/> and opens its picture; a failure is
    /// shown and recorded. The TV gets its full-screen picture and sound back first.
    /// </summary>
    void SelectSource(int source)
    {
        ReleaseScreen(true);
        int result = Inputs.Switch(source);
        if (result < 0)
        {
            ShowError("Input could not be switched. Open Advanced settings to check the connection.");
            WriteState("source_failed:" + result);
            return;
        }
        lastSource = source;
        Launch(LaunchRequest.TvViewer);
    }

    /// <summary>
    /// Opens another app and records the result; failures are shown. The TV gets its picture
    /// back first; the input's sound only when Quiet Home goes to the background, so it does
    /// not play while the app starts.
    /// </summary>
    void Launch(LaunchRequest request)
    {
        ReleaseScreen(false);
        var result = tv.Launcher.Launch(request);
        if (!result.Created)
        {
            ShowError("Could not open the app.");
            return;
        }
        WriteState(request.Activity(result.Code));
        if (result.Code != 0)
            ShowError("Could not open this screen. Try Advanced settings.");
    }

    /// <summary>Shows <paramref name="text"/> as the notice.</summary>
    void ShowError(string text) => view.ShowNotice(text);

    /// <summary>An artwork image finished loading: records it.</summary>
    void ArtworkLoaded() => WriteState("artwork_ready");

    /// <summary>Writes the diagnostics file with <paramref name="activity"/> as what just happened. Never throws.</summary>
    void WriteState(string activity)
    {
        try
        {
            var stats = view.Stats;
            var cards = screen?.Cards;
            int selected = navigator.Selected;
            tv.Diagnostics.Write(new StateReport
            {
                Page = navigator.Page,
                Selected = selected,
                Zone = navigator.Zone,
                Activity = activity,
                Key = keys.LastKey,
                FontLoaded = stats.FontLoaded,
                Dark = dark,
                ThemeError = themeError,
                Demo = demo,
                BuildMilliseconds = stats.BuildMilliseconds,
                LibraryPage = navigator.LibraryPage,
                LibraryCount = library.Count,
                LibraryError = libraryError,
                Inputs = string.Join(",", inputSources.ConvertAll(s => s.SourceType.ToString())),
                InputError = inputError,
                SourceCallback = tv.Inputs.WatchStatus,
                SelectedName = cards != null && selected >= 0 && selected < cards.Count ? cards[selected].Name : "",
                ArtworkCached = stats.ArtworkCached,
                ArtworkReady = stats.ArtworkReady,
                ArtworkPending = stats.ArtworkPending,
                ArtworkViewsCreated = stats.ArtworkViewsCreated,
                PageArtworkCold = stats.PageArtworkCold,
                ProcessId = processId,
                Editing = navigator.Adjusting,
                Volume = settings.Volume,
                Brightness = settings.Value("brightness"),
                GameMode = settings.Value(settings.GameModeId),
                SleepTimer = settings.Value("sleeptimer"),
                Preview = preview,
            }.Format(DateTime.UtcNow));
        }
        catch (Exception)
        {
            // Diagnostics must never stop the app.
        }
    }
}
