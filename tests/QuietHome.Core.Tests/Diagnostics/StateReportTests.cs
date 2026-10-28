using System;
using System.Linq;
using QuietHome.Core.Diagnostics;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
using Xunit;

namespace QuietHome.Core.Tests.Diagnostics;

/// <summary>
/// Tests of <see cref="StateReport"/>, the diagnostics file that tools on the computer parse.
/// Its keys and their order must stay as they were before the refactoring.
/// </summary>
public class StateReportTests
{
    /// <summary>The file has a timestamp and then the keys in the order the tools expect.</summary>
    [Fact]
    public void KeysInFixedOrder()
    {
        string[] lines = new StateReport().Format(new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc)).Split('\n');
        Assert.Equal("2026-10-28T12:00:00.0000000Z", lines[0]);
        Assert.Equal("", lines[^1]);
        Assert.Equal(new[]
        {
            "page", "selected", "area", "activity", "key", "font_loaded", "style", "theme", "theme_error", "demo",
            "build_ms", "library_page", "library_count", "library_error", "inputs", "input_error", "source_callback",
            "selected_name", "artwork_cached", "artwork_ready", "artwork_pending", "artwork_views_created",
            "page_artwork_cold", "process_id", "editing", "volume", "brightness", "game-mode", "sleeptimer", "preview",
        }, lines[1..^1].Select(l => l.Split('=')[0]));
    }

    /// <summary>Pages and zones are written as numbers, booleans as True / False, the theme, adjustment and preview by name.</summary>
    [Fact]
    public void ValueFormats()
    {
        var report = new StateReport
        {
            Page = Page.Settings,
            Zone = Zone.Toolbar,
            Activity = "setting:sleeptimer:1:True",
            FontLoaded = true,
            Dark = false,
            Demo = false,
            Editing = Adjustment.Brightness,
            Volume = -1,
            GameMode = "2",
            Preview = PreviewState.Unsupported,
        };
        string text = report.Format(DateTime.UnixEpoch);
        Assert.Contains("\npage=2\n", text);
        Assert.Contains("\narea=3\n", text);
        Assert.Contains("\nactivity=setting:sleeptimer:1:True\n", text);
        Assert.Contains("\nfont_loaded=True\n", text);
        Assert.Contains("\nstyle=free-my-tv\n", text);
        Assert.Contains("\ntheme=light\n", text);
        Assert.Contains("\ndemo=False\n", text);
        Assert.Contains("\nediting=brightness\n", text);
        Assert.Contains("\nvolume=-1\n", text);
        Assert.Contains("\ngame-mode=2\n", text);
        Assert.Contains("\npreview=unsupported\n", text);
    }
}
