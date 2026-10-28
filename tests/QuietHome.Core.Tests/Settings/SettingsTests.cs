using System.Linq;
using QuietHome.Core.Actions;
using QuietHome.Core.Navigation;
using QuietHome.Core.Settings;
using Xunit;

namespace QuietHome.Core.Tests.Settings;

/// <summary>
/// Tests of the Settings page: the tile table (<see cref="SettingsTile"/>) and the value
/// rules in <see cref="QuickSettingsState"/>.
/// </summary>
public class SettingsTests
{
    /// <summary>The page has eight tiles in a 4 × 2 grid, listed row by row, each with its action.</summary>
    [Fact]
    public void TileTable()
    {
        var tiles = SettingsTile.All;
        Assert.Equal(new[] { "volume", "brightness", "game-mode", "sleeptimer", "theme", "controllers", "network", "advanced" }, tiles.Select(t => t.Id));
        Assert.Equal(new[] { 0, 1, 2, 3, 0, 1, 2, 3 }, tiles.Select(t => t.Column));
        Assert.Equal(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }, tiles.Select(t => t.Row));
        Assert.Equal(new HomeAction.BeginAdjust(Adjustment.Brightness), tiles[1].Action);
        Assert.Equal(new HomeAction.CycleSetting("sleeptimer"), tiles[3].Action);
        Assert.Equal(new HomeAction.OpenSamsungSettings("networksettings"), tiles[6].Action);
        Assert.Equal(new HomeAction.OpenSamsungSettings(null), tiles[7].Action);
    }

    /// <summary>Each tile shows its own setting's value, found by the tile's ID; unknown settings show "Unavailable".</summary>
    [Fact]
    public void ValuesFollowTheTv()
    {
        var state = new QuickSettingsState { Volume = 17 };
        state.Record("game-mode", "On", "1");
        string?[] values = SettingsTile.All.Select(t => t.ValueText(state, dark: false, demo: false)).ToArray();
        Assert.Equal(new[] { "17", "Unavailable", "On", "Unavailable", "Light", "Pair controller", "Connections", "More settings" }, values);
        state.Volume = -1;
        Assert.Equal("Unavailable", SettingsTile.All[0].ValueText(state, dark: true, demo: false));
    }

    /// <summary>Demo mode shows fixed sample values whatever the TV reports.</summary>
    [Fact]
    public void DemoValues()
    {
        var state = new QuickSettingsState { Volume = 3 };
        state.Record("brightness", "12", "12");
        string?[] values = SettingsTile.All.Take(5).Select(t => t.ValueText(state, dark: true, demo: true)).ToArray();
        Assert.Equal(new[] { "20", "45", "Off", "Off", "Dark" }, values);
    }

    /// <summary>A setting can be changed only when the TV reported a value and allows changes.</summary>
    [Fact]
    public void CanEditNeedsValueAndPermission()
    {
        var state = new QuickSettingsState();
        state.SetEnabled("brightness", true);
        Assert.False(state.CanEdit("brightness"));
        state.Record("brightness", "30", "30");
        Assert.True(state.CanEdit("brightness"));
        state.Record("brightness", null, "");
        Assert.False(state.CanEdit("brightness"));
        Assert.Equal("", state.Value("brightness"));
        state.Record("brightness", "30", "30");
        state.Forget("brightness");
        Assert.False(state.CanEdit("brightness"));
    }

    /// <summary>Volume stays within 0..100.</summary>
    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(50, 1, 51)]
    [InlineData(100, 1, 100)]
    public void VolumeSteps(int current, int delta, int expected)
    {
        Assert.Equal(expected, QuickSettingsState.NextVolume(current, delta));
    }

    /// <summary>Brightness moves by the TV's step within the TV's range, and uses 0..50 by 1 when the TV reports no usable range.</summary>
    [Fact]
    public void BrightnessSteps()
    {
        var state = new QuickSettingsState();
        state.Record("brightness", "40", "40");
        state.SetBrightnessRange(true, 10, 42, 5);
        Assert.Equal("42", state.NextBrightness(1));
        Assert.Equal("35", state.NextBrightness(-1));
        state.SetBrightnessRange(true, 10, 42, 0);
        Assert.Equal((0d, 50d, 1d), (state.BrightnessMin, state.BrightnessMax, state.BrightnessStep));
        state.SetBrightnessRange(false, 10, 42, 5);
        Assert.Equal("41", state.NextBrightness(1));
        state.Record("brightness", "High", "High");
        Assert.Null(state.NextBrightness(1));
    }

    /// <summary>Game mode toggles between "0" and "1"; anything but "1" counts as off.</summary>
    [Theory]
    [InlineData("1", "0")]
    [InlineData("0", "1")]
    [InlineData("2", "1")]
    public void GameModeToggles(string current, string expected)
    {
        var state = new QuickSettingsState();
        state.Record("game-mode", "x", current);
        Assert.Equal(expected, state.NextToggleValue("game-mode"));
    }

    /// <summary>The sleep timer steps through its five choices and wraps to off; a non-number changes nothing.</summary>
    [Theory]
    [InlineData("0", "1")]
    [InlineData("3", "4")]
    [InlineData("4", "0")]
    [InlineData("off", null)]
    public void SleepTimerCycles(string current, string? expected)
    {
        var state = new QuickSettingsState();
        state.Record("sleeptimer", "x", current);
        Assert.Equal(expected, state.NextCycleValue("sleeptimer"));
    }
}
