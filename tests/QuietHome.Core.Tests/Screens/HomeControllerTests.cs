using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuietHome.Core.Catalog;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
using QuietHome.Core.Screens;
using QuietHome.Core.Settings;
using Xunit;

namespace QuietHome.Core.Tests.Screens;

/// <summary>
/// Tests of <see cref="HomeController"/> with a recording view and stand-ins for the TV:
/// what is drawn when, which TV calls happen, and what the diagnostics file says.
/// </summary>
public sealed class HomeControllerTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "quiet-home-" + Guid.NewGuid().ToString("N"));
    readonly FakeTv tv = new();
    readonly FakeView view = new();
    readonly HomeController controller;

    /// <summary>Creates a controller over a three-app catalog, a two-app public catalog and two connected inputs with HDMI 1 on screen.</summary>
    public HomeControllerTests()
    {
        // The TV formats the clock with AM/PM; the computer running the tests may not.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(folder);
        WriteCatalog("apps.json", "Own", 3);
        WriteCatalog("apps.public.json", "Public", 2);
        tv.Inputs = ScreenBuilderTests.TwoInputs();
        tv.CurrentSource = 14;
        var demoInputs = new[] { new InputSource { ConnectionState = true, SourceType = 0 } };
        controller = new HomeController(tv.Ports, view, new HomeConfig(Path.Combine(folder, "apps.json"), Path.Combine(folder, "apps.public.json"), demoInputs));
    }

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => Directory.Delete(folder, true);

    /// <summary>
    /// Start draws the Home page once, after reading the inputs once. The first card is
    /// focused: the input on screen is looked up before the first reading, as before the
    /// refactoring; the Home request that follows a start moves the focus to it.
    /// </summary>
    [Fact]
    public void StartShowsHome()
    {
        controller.Start();
        Assert.Equal(new[] { RenderMode.Rebuild }, view.Modes);
        Assert.Equal(1, tv.Reads);
        Assert.Equal(0, controller.Navigator.Selected);
        Assert.Equal(new[] { "Own 1", "Own 2", "Own 3" }, view.Preloaded.Select(a => a.Name));
        Assert.Contains("\nactivity=created\n", tv.State);
        Assert.Contains("\nselected_name=Live TV\n", tv.State);
        Assert.Contains("\nbuild_ms=7\n", tv.State);
        Assert.Contains("\ninputs=0,14\n", tv.State);
    }

    /// <summary>A Home request focuses the input on screen; a second one with nothing changed reads the inputs once and draws nothing.</summary>
    [Fact]
    public void HomeRequestWithoutChanges()
    {
        controller.Start();
        controller.AppControl(Options());
        Assert.Equal(1, controller.Navigator.Selected);
        Assert.Equal(2, view.Modes.Count);
        Assert.Equal(2, tv.Reads);
        controller.AppControl(Options());
        Assert.Equal(2, view.Modes.Count);
        Assert.Equal(3, tv.Reads);
        Assert.Contains("\nactivity=home_request\n", tv.State);
    }

    /// <summary>A change notice redraws the Home page only when the inputs really changed; away from Home it reads nothing.</summary>
    [Fact]
    public void SourcesChangedRedrawsHome()
    {
        controller.Start();
        tv.RaiseChanged();
        Assert.Single(view.Modes);
        tv.Inputs = new List<InputSource> { new() { ConnectionState = true, SourceType = 0 } };
        tv.RaiseChanged();
        Assert.Equal(2, view.Modes.Count);
        Assert.Equal(new[] { "Live TV" }, InputCards());
        controller.AppControl(Options(("quiet_page", "settings")));
        int reads = tv.Reads;
        tv.RaiseChanged();
        Assert.Equal(reads, tv.Reads);
    }

    /// <summary>Moving within a row animates the focus; moving to another tab rebuilds that page, reading the inputs only on arriving at Home.</summary>
    [Fact]
    public void KeysMoveFocusAndPages()
    {
        controller.Start();
        Key(RemoteKey.Right, 1000);
        Assert.Equal(RenderMode.AnimateFocus, view.Modes[^1]);
        Assert.Equal(1, controller.Navigator.Selected);
        Key(RemoteKey.Up, 2000);
        Key(RemoteKey.Right, 3000);
        Assert.Equal(RenderMode.Rebuild, view.Modes[^1]);
        Assert.IsType<AppsPageModel>(view.Last.Content);
        int reads = tv.Reads;
        Key(RemoteKey.Left, 4000);
        Assert.IsType<HomePageModel>(view.Last.Content);
        Assert.Equal(reads + 1, tv.Reads);
    }

    /// <summary>Keys pressed faster than the repeat filter allows are dropped.</summary>
    [Fact]
    public void HeldKeyRepeatsAreDropped()
    {
        controller.Start();
        Key(RemoteKey.Right, 1000);
        Key(RemoteKey.Right, 1050);
        Assert.Equal(2, view.Modes.Count);
    }

    /// <summary>Select on an input switches the TV to it and opens the picture; a failed switch shows a notice instead.</summary>
    [Fact]
    public void SelectInput()
    {
        controller.Start();
        controller.AppControl(Options());
        Key(RemoteKey.Select, 1000);
        Assert.Equal(new[] { 14 }, tv.Switched);
        Assert.Equal(new[] { LaunchRequest.TvViewer }, tv.Launched);
        Assert.Equal("", view.Notice);
        tv.SwitchResult = -1;
        Key(RemoteKey.Select, 2000);
        Assert.Equal("Input could not be switched. Open Advanced settings to check the connection.", view.Notice);
        Assert.Contains("\nactivity=source_failed:-1\n", tv.State);
    }

    /// <summary>Back returns to the input on screen.</summary>
    [Fact]
    public void BackReturnsToInput()
    {
        controller.Start();
        Key(RemoteKey.Back, 1000);
        Assert.Equal(new[] { 14 }, tv.Switched);
    }

    /// <summary>Select on the Game Mode tile writes the next value, and a value the TV did not take is reported.</summary>
    [Fact]
    public void GameModeTile()
    {
        tv.Items[QuickSettingsState.GameModeIds[0]] = "2";
        controller.Start();
        controller.AppControl(Options(("quiet_page", "settings")));
        Key(RemoteKey.Right, 1000);
        Key(RemoteKey.Right, 2000);
        Key(RemoteKey.Select, 3000);
        Assert.Equal("1", tv.Items[QuickSettingsState.GameModeIds[0]]);
        Assert.Contains("\nactivity=setting:game-mode-setting-game-mode-auto:1:True\n", tv.State);
        tv.RefuseWrites = true;
        Key(RemoteKey.Select, 4000);
        Assert.Equal("The TV could not apply this setting. Try Advanced settings.", view.Notice);
    }

    /// <summary>Demo mode shows the public catalog, sample inputs, sample settings and 9:41, changes nothing on the TV, and ends with the next launch.</summary>
    [Fact]
    public void DemoModeLeavesTheTvAlone()
    {
        controller.Start();
        controller.AppControl(Options(("quiet_demo", "1"), ("quiet_page", "settings")));
        Assert.True(controller.Demo);
        Assert.Equal("9:41 AM", view.Last.Chrome.Clock);
        var tiles = Assert.IsType<SettingsPageModel>(view.Last.Content).Tiles;
        Assert.Equal(new[] { "20", "45", "Off", "Off" }, tiles.Take(4).Select(t => t.Value));
        Key(RemoteKey.Right, 1000);
        Key(RemoteKey.Right, 2000);
        Key(RemoteKey.Select, 3000);
        Assert.Equal("Auto", Assert.IsType<SettingsPageModel>(view.Last.Content).Tiles[2].Value);
        Assert.Empty(tv.Written);
        controller.AppControl(Options(("quiet_demo", "1")));
        Assert.Equal(new[] { "Live TV" }, InputCards());
        Assert.Equal(new[] { "Public 1", "Public 2", "Your Apps" }, ((HomePageModel)view.Last.Content).Dock.Select(d => d.Name));
        controller.AppControl(Options());
        Assert.False(controller.Demo);
        Assert.Equal(new[] { "Live TV", "Console" }, InputCards());
    }

    /// <summary>The Theme tile saves the other theme and rebuilds; a failed save keeps the theme for now and says so.</summary>
    [Fact]
    public void ThemeTile()
    {
        controller.Start();
        controller.AppControl(Options(("quiet_page", "settings")));
        Key(RemoteKey.Down, 1000);
        Key(RemoteKey.Select, 2000);
        Assert.Equal("light", tv.Theme);
        Assert.False(view.Last.Dark);
        tv.ThemeFails = true;
        Key(RemoteKey.Select, 3000);
        Assert.True(view.Last.Dark);
        Assert.Equal("Theme changed for now; it could not be saved.", view.Notice);
        Assert.Contains("\ntheme_error=disk full\n", tv.State);
    }

    /// <summary>A saved light theme is used from the start.</summary>
    [Fact]
    public void StartWithSavedTheme()
    {
        tv.Theme = "light";
        controller.Start();
        Assert.False(view.Last.Dark);
    }

    /// <summary>The clock ticks only after the first page; artwork events update the diagnostics file.</summary>
    [Fact]
    public void ClockAndArtwork()
    {
        controller.Tick();
        Assert.Null(view.Clock);
        controller.Start();
        controller.Tick();
        Assert.Equal("6:05 PM", view.Clock);
        view.RaiseArtworkLoaded();
        Assert.Contains("\nactivity=artwork_ready\n", tv.State);
    }

    /// <summary>Pausing ends an adjustment; resuming on the Settings page rereads and refreshes it without a rebuild.</summary>
    [Fact]
    public void PauseAndResume()
    {
        controller.Start();
        controller.AppControl(Options(("quiet_page", "settings")));
        Key(RemoteKey.Select, 1000);
        Assert.Equal(Adjustment.Volume, controller.Navigator.Adjusting);
        controller.Pause();
        Assert.Equal(Adjustment.None, controller.Navigator.Adjusting);
        tv.Volume = 33;
        controller.Resume();
        Assert.Equal(RenderMode.Refresh, view.Modes[^1]);
        Assert.Equal("33", Assert.IsType<SettingsPageModel>(view.Last.Content).Tiles[0].Value);
    }

    /// <summary>Volume adjusting moves the volume one step per key.</summary>
    [Fact]
    public void VolumeAdjust()
    {
        tv.Volume = 10;
        controller.Start();
        controller.AppControl(Options(("quiet_page", "settings")));
        Key(RemoteKey.Select, 1000);
        Key(RemoteKey.Right, 2000);
        Assert.Equal(11, tv.Volume);
        Assert.Contains("\nactivity=volume_changed\n", tv.State);
    }

    /// <summary>Writes a catalog of <paramref name="count"/> apps named "<paramref name="name"/> 1"… into the test folder.</summary>
    void WriteCatalog(string file, string name, int count)
    {
        var apps = Enumerable.Range(1, count).Select(i =>
            "{\"Name\":\"" + name + " " + i + "\",\"Id\":\"" + name.ToLowerInvariant() + "." + i + "\",\"Asset\":\"a.svg\",\"Width\":100,\"Height\":50}");
        File.WriteAllText(Path.Combine(folder, file), "[" + string.Join(",", apps) + "]");
    }

    /// <summary>Returns launch options made of the given extras.</summary>
    static LaunchOptions Options(params (string Key, string Value)[] extras) =>
        LaunchOptions.Parse(key => extras.FirstOrDefault(e => e.Key == key).Value);

    /// <summary>Presses <paramref name="key"/> at <paramref name="milliseconds"/>.</summary>
    void Key(RemoteKey key, long milliseconds) => controller.Key(key, key.ToString(), milliseconds);

    /// <summary>Returns the names on the input cards of the last screen.</summary>
    IEnumerable<string> InputCards() => ((HomePageModel)view.Last.Content).Inputs.Select(i => i.Name);

    /// <summary>Records what the controller asks the view to show.</summary>
    sealed class FakeView : IHomeView
    {
        /// <summary>Raised by <see cref="RaiseArtworkLoaded"/>.</summary>
        public event Action? ArtworkLoaded;

        /// <summary>Fixed numbers; build_ms is 7.</summary>
        public ViewStats Stats => new(true, 7, 0, 0, "", 0, 0);

        /// <summary>The render modes in order.</summary>
        public List<RenderMode> Modes { get; } = new();

        /// <summary>The last screen shown.</summary>
        public HomeScreen Last { get; private set; } = null!;

        /// <summary>The notice on screen.</summary>
        public string Notice { get; private set; } = "";

        /// <summary>The clock text set by a tick, or null.</summary>
        public string? Clock { get; private set; }

        /// <summary>The catalog passed to <see cref="Preload"/>.</summary>
        public List<LibraryApp> Preloaded { get; } = new();

        /// <summary>Records the catalog.</summary>
        public void Preload(IReadOnlyList<LibraryApp> library) => Preloaded.AddRange(library);

        /// <summary>Records the screen and mode; a rebuild clears the notice like the real view.</summary>
        public void Render(HomeScreen screen, RenderMode mode)
        {
            Last = screen;
            Modes.Add(mode);
            if (mode == RenderMode.Rebuild)
                Notice = "";
        }

        /// <summary>Records the notice.</summary>
        public void ShowNotice(string text) => Notice = text;

        /// <summary>Records the clock.</summary>
        public void ShowClock(string text) => Clock = text;

        /// <summary>Simulates an image that finished loading.</summary>
        public void RaiseArtworkLoaded() => ArtworkLoaded?.Invoke();
    }

    /// <summary>
    /// A TV for the tests: inputs, settings, theme, clock (6:05 PM), launcher and diagnostics
    /// file in memory, implementing every port.
    /// </summary>
    sealed class FakeTv : IInputSources, IQuickSettings, IThemeStore, IClock, ISystemLauncher, IDiagnostics
    {
        /// <summary>Creates the TV with a sleep timer and a brightness item.</summary>
        public FakeTv()
        {
            Items["brightness"] = "30";
            Items["sleeptimer"] = "0";
        }

        /// <summary>The ports bundle over this TV.</summary>
        public HomePorts Ports => new(this, this, this, this, this, this);

        /// <summary>The connected inputs.</summary>
        public List<InputSource> Inputs { get; set; } = new();

        /// <summary>The input on screen.</summary>
        public int CurrentSource { get; set; }

        /// <summary>How many times the input list was read.</summary>
        public int Reads { get; private set; }

        /// <summary>The inputs switched to, in order.</summary>
        public List<int> Switched { get; } = new();

        /// <summary>What <see cref="Switch"/> returns.</summary>
        public int SwitchResult { get; set; }

        /// <summary>The settings menu items by ID.</summary>
        public Dictionary<string, string> Items { get; } = new();

        /// <summary>The writes to menu items, in order.</summary>
        public List<(string Id, string Value)> Written { get; } = new();

        /// <summary>When true, the TV refuses every item write.</summary>
        public bool RefuseWrites { get; set; }

        /// <summary>The master volume.</summary>
        public int Volume { get; set; } = 15;

        /// <summary>The stored theme text, or null.</summary>
        public string? Theme { get; set; }

        /// <summary>When true, saving the theme fails with "disk full".</summary>
        public bool ThemeFails { get; set; }

        /// <summary>The launch requests sent, in order.</summary>
        public List<LaunchRequest> Launched { get; } = new();

        /// <summary>The last diagnostics text.</summary>
        public string State { get; private set; } = "";

        /// <summary>Raised by <see cref="RaiseChanged"/>.</summary>
        public event Action? Changed;

        /// <summary>Subscribed.</summary>
        public int WatchStatus => 0;

        /// <summary>6:05 PM.</summary>
        public DateTime Now => new(2026, 10, 28, 18, 5, 0);

        /// <summary>Simulates the TV's change notice.</summary>
        public void RaiseChanged() => Changed?.Invoke();

        /// <summary>Counts the read and returns a copy of the inputs.</summary>
        public InputReading Read()
        {
            Reads++;
            return new InputReading(new List<InputSource>(Inputs), "");
        }

        /// <summary>Returns the input on screen.</summary>
        public int? Current() => CurrentSource;

        /// <summary>Records the switch and returns <see cref="SwitchResult"/>.</summary>
        public int Switch(int sourceType)
        {
            Switched.Add(sourceType);
            return SwitchResult;
        }

        /// <summary>Returns the volume.</summary>
        public int? GetVolume() => Volume;

        /// <summary>Sets the volume.</summary>
        public bool SetVolume(int volume)
        {
            Volume = volume;
            return true;
        }

        /// <summary>Returns the item with its value as name, changeable; a missing item has no value.</summary>
        public SettingReading ReadItem(string id) =>
            Items.TryGetValue(id, out var value) ? new SettingReading(value, value, true) : new SettingReading(null, null, false);

        /// <summary>Returns 0..50 by 1.</summary>
        public SettingRange? ReadRange(string id) => new SettingRange(0, 50, 1);

        /// <summary>Records and applies the write unless writes are refused.</summary>
        public bool WriteItem(string id, string value)
        {
            Written.Add((id, value));
            if (RefuseWrites)
                return false;
            Items[id] = value;
            return true;
        }

        /// <summary>Returns the stored theme.</summary>
        string? IThemeStore.Read() => Theme;

        /// <summary>Stores the theme, or fails when <see cref="ThemeFails"/> is set.</summary>
        public void Write(string text)
        {
            if (ThemeFails)
                throw new IOException("disk full");
            Theme = text;
        }

        /// <summary>Records the request; it always succeeds.</summary>
        public LaunchResult Launch(LaunchRequest request)
        {
            Launched.Add(request);
            return new LaunchResult(true, 0);
        }

        /// <summary>Keeps the diagnostics text.</summary>
        void IDiagnostics.Write(string text) => State = text;
    }
}
