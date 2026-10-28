using System;
using System.Collections.Generic;
using System.Linq;
using QuietHome.Core.Actions;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
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
        Assert.Equal(new HomeAction.SwitchGameMode(), tiles[2].Action);
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
        string?[] values = SettingsTile.All.Select(t => t.ValueText(state, dark: false)).ToArray();
        Assert.Equal(new[] { "17", "Unavailable", "On", "Unavailable", "Light", "Pair controller", "Connections", "More settings" }, values);
        state.Volume = -1;
        Assert.Equal("Unavailable", SettingsTile.All[0].ValueText(state, dark: true));
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

    /// <summary>
    /// The Game Mode tile shows the TV's item that knows Auto when the TV reports it, even
    /// while the older on/off item is empty (as it is in Auto), and falls back to the older
    /// item on a TV without the new one.
    /// </summary>
    [Fact]
    public void GameModePrefersTheItemWithAuto()
    {
        var state = new QuickSettingsState();
        var tile = SettingsTile.All[2];
        state.Record("game-mode", "On", "1");
        Assert.Equal("game-mode", state.GameModeId);
        Assert.Equal("On", tile.ValueText(state, dark: true));
        state.Record("game-mode", "----", "");
        state.Record("game-mode-setting-game-mode-auto", "Auto", "2");
        Assert.Equal("game-mode-setting-game-mode-auto", state.GameModeId);
        Assert.Equal("Auto", tile.ValueText(state, dark: true));
    }

    /// <summary>Select steps game mode Auto → On → Off → Auto; an unknown value changes nothing.</summary>
    [Theory]
    [InlineData("2", "1")]
    [InlineData("1", "0")]
    [InlineData("0", "2")]
    [InlineData("3", null)]
    public void GameModeCycles(string current, string? expected)
    {
        var state = new QuickSettingsState();
        state.Record("game-mode-setting-game-mode-auto", "x", current);
        Assert.Equal(expected, state.NextGameModeValue());
    }

    /// <summary>On a TV with only the older on/off item, game mode toggles between "0" and "1"; anything but "1" counts as off.</summary>
    [Theory]
    [InlineData("1", "0")]
    [InlineData("0", "1")]
    [InlineData("2", "1")]
    public void GameModeTogglesWithoutAuto(string current, string expected)
    {
        var state = new QuickSettingsState();
        state.Record("game-mode", "x", current);
        Assert.Equal(expected, state.NextGameModeValue());
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

    /// <summary>
    /// Refresh reads the volume, every menu item and the brightness range from the TV: an
    /// unreported volume is -1, a missing value is forgotten and an item whose reading
    /// throws is not changeable.
    /// </summary>
    [Fact]
    public void RefreshReadsTheTv()
    {
        var tv = new FakeSettings();
        tv.Items["brightness"] = new SettingReading("30", "30", true);
        tv.Items["sleeptimer"] = new SettingReading("Off", "0", false);
        tv.Items["game-mode"] = new SettingReading("----", "", true);
        tv.Range = new SettingRange(0, 100, 2);
        var state = new QuickSettingsState();
        state.Record(QuickSettingsState.GameModeIds[0], "On", "1");
        state.SetEnabled(QuickSettingsState.GameModeIds[0], true);
        state.Refresh(tv);
        Assert.Equal(-1, state.Volume);
        Assert.True(state.CanEdit("brightness"));
        Assert.Equal("Off", state.Name("sleeptimer"));
        Assert.False(state.CanEdit("sleeptimer"));
        Assert.Equal("", state.Value("game-mode"));
        Assert.Equal("", state.Value(QuickSettingsState.GameModeIds[0]));
        Assert.False(state.CanEdit(QuickSettingsState.GameModeIds[0]));
        Assert.Equal((0d, 100d, 2d), (state.BrightnessMin, state.BrightnessMax, state.BrightnessStep));
        tv.Volume = 12;
        tv.Range = null;
        state.Refresh(tv);
        Assert.Equal(12, state.Volume);
        Assert.Equal((0d, 50d, 1d), (state.BrightnessMin, state.BrightnessMax, state.BrightnessStep));
        tv.RangeThrows = true;
        tv.Range = new SettingRange(0, 100, 2);
        state.Refresh(tv);
        Assert.Equal((0d, 50d, 1d), (state.BrightnessMin, state.BrightnessMax, state.BrightnessStep));
    }

    /// <summary>
    /// Stand-in for the TV's settings in these tests: items not listed in
    /// <see cref="Items"/> throw when read, as when Samsung's library fails.
    /// </summary>
    sealed class FakeSettings : IQuickSettings
    {
        /// <summary>The items the TV reports, by menu ID.</summary>
        public Dictionary<string, SettingReading> Items { get; } = new();

        /// <summary>The volume the TV reports, or null.</summary>
        public int? Volume { get; set; }

        /// <summary>The brightness range the TV reports, or null.</summary>
        public SettingRange? Range { get; set; }

        /// <summary>When true, reading the range throws.</summary>
        public bool RangeThrows { get; set; }

        /// <summary>Returns <see cref="Volume"/>.</summary>
        public int? GetVolume() => Volume;

        /// <summary>Not used in these tests; refuses.</summary>
        public bool SetVolume(int volume) => false;

        /// <summary>Returns the listed item, or throws for any other ID.</summary>
        public SettingReading ReadItem(string id) => Items.TryGetValue(id, out var item) ? item : throw new InvalidOperationException("no item");

        /// <summary>Returns <see cref="Range"/>, or throws when <see cref="RangeThrows"/> is set.</summary>
        public SettingRange? ReadRange(string id) => RangeThrows ? throw new InvalidOperationException("no range") : Range;

        /// <summary>Not used in these tests; refuses.</summary>
        public bool WriteItem(string id, string value) => false;
    }
}
