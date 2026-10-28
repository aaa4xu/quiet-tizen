using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Screens;
using QuietHome.Core.Tests.Screens;

namespace QuietHome.Core.Tests.Fakes;

/// <summary>
/// One Quiet Home for a test: a <see cref="HomeController"/> over a <see cref="FakeTv"/> and
/// a <see cref="FakeView"/>, with its catalogs written to a temporary folder. It plays the
/// part of launcher/tizen/QuietHomeApplication.cs (start, launches, key presses) and offers
/// short ways to read what is on screen. The controller and scenario tests use it.
/// </summary>
sealed class HomeHarness : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "quiet-home-" + Guid.NewGuid().ToString("N"));
    HomeController? controller;
    long now;

    /// <summary>
    /// Prepares a TV with Live TV (0) and HDMI 1 "Console" (14) connected and HDMI 1 on
    /// screen, a catalog of <paramref name="ownApps"/> apps named "Own 1"… and a public catalog
    /// of two. Change <see cref="Tv"/> before the first use of <see cref="Controller"/>.
    /// </summary>
    public HomeHarness(int ownApps = 3)
    {
        // The TV formats the clock with AM/PM; the computer running the tests may not.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(folder);
        WriteCatalog("apps.json", "Own", ownApps);
        WriteCatalog("apps.public.json", "Public", 2);
        Tv.Inputs = ScreenBuilderTests.TwoInputs();
        Tv.CurrentSource = 14;
    }

    /// <summary>The TV.</summary>
    public FakeTv Tv { get; } = new();

    /// <summary>The view.</summary>
    public FakeView View { get; } = new();

    /// <summary>The controller, created on first use so tests can set up the TV first; demo mode shows Live TV only.</summary>
    public HomeController Controller => controller ??= new HomeController(Tv.Ports, View, new HomeConfig(
        Path.Combine(folder, "apps.json"), Path.Combine(folder, "apps.public.json"),
        new[] { new InputSource { ConnectionState = true, SourceType = 0 } }));

    /// <summary>The last screen shown.</summary>
    public HomeScreen Screen => View.Last;

    /// <summary>The name of the focused card, or null when the focus is on the tabs.</summary>
    public string? Focused => Screen.Cards.FirstOrDefault(c => c.Focused)?.Name;

    /// <summary>The names on the input cards of the last screen.</summary>
    public IEnumerable<string> InputCards => ((HomePageModel)Screen.Content).Inputs.Select(i => i.Name);

    /// <summary>The Settings page's tiles on the last screen.</summary>
    public IReadOnlyList<SettingsTileModel> Tiles => ((SettingsPageModel)Screen.Content).Tiles;

    /// <summary>
    /// Opens Quiet Home the way the TV does: the app starts, then receives the launch
    /// request, which moves the focus to the input on screen.
    /// </summary>
    public void Open()
    {
        Controller.Start();
        Controller.AppControl(Options());
    }

    /// <summary>Sends a launch request with the given extras, as the Home key, QuickLaunch or the screenshot recipe would.</summary>
    public void Launch(params (string Key, string Value)[] extras) => Controller.AppControl(Options(extras));

    /// <summary>Presses each key in turn, a second apart so the repeat filter lets them all through.</summary>
    public void Press(params RemoteKey[] keys)
    {
        foreach (var key in keys)
        {
            now += 1000;
            Controller.Key(key, key.ToString(), now);
        }
    }

    /// <summary>Presses <paramref name="key"/> <paramref name="milliseconds"/> after the previous press.</summary>
    public void PressAfter(RemoteKey key, long milliseconds)
    {
        now += milliseconds;
        Controller.Key(key, key.ToString(), now);
    }

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => Directory.Delete(folder, true);

    /// <summary>Returns launch options made of the given extras.</summary>
    public static LaunchOptions Options(params (string Key, string Value)[] extras) =>
        LaunchOptions.Parse(key => extras.FirstOrDefault(e => e.Key == key).Value);

    /// <summary>Writes a catalog of <paramref name="count"/> apps named "<paramref name="name"/> 1"… into the test folder.</summary>
    void WriteCatalog(string file, string name, int count)
    {
        var apps = Enumerable.Range(1, count).Select(i =>
            "{\"Name\":\"" + name + " " + i + "\",\"Id\":\"" + name.ToLowerInvariant() + "." + i + "\",\"Asset\":\"a.svg\",\"Width\":100,\"Height\":50}");
        File.WriteAllText(Path.Combine(folder, file), "[" + string.Join(",", apps) + "]");
    }
}
