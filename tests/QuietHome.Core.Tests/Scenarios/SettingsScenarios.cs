using System;
using QuietHome.Core.Navigation;
using QuietHome.Core.Platform;
using QuietHome.Core.Settings;
using QuietHome.Core.Tests.Fakes;
using Xunit;

namespace QuietHome.Core.Tests.Scenarios;

/// <summary>
/// Scenario tests of the Settings page, played on <see cref="HomeHarness"/>: volume and
/// brightness adjusting, game mode, sleep timer, theme and the tiles that open Samsung's
/// own settings. The Settings page's tiles are, by slot: 0 Volume, 1 Brightness, 2 Game
/// Mode, 3 Sleep Timer on the first row; 4 Theme, 5 Controllers, 6 Network, 7 Advanced on
/// the second.
/// </summary>
public sealed class SettingsScenarios : IDisposable
{
    static readonly string gameModeAuto = QuickSettingsState.GameModeIds[0];

    readonly HomeHarness home = new();

    /// <summary>Deletes the temporary catalogs.</summary>
    public void Dispose() => home.Dispose();

    /// <summary>Opens Quiet Home on the Settings page and presses <paramref name="keys"/>.</summary>
    void OpenSettings(params RemoteKey[] keys)
    {
        home.Open();
        home.Launch(("quiet_page", "settings"));
        home.Press(keys);
    }

    /// <summary>Left and Right move the volume one step and stop at 0 and 100.</summary>
    [Fact]
    public void VolumeStaysWithinItsLimits()
    {
        home.Tv.Volume = 99;
        OpenSettings(RemoteKey.Select, RemoteKey.Right, RemoteKey.Right);
        Assert.Equal(new[] { 100, 100 }, home.Tv.VolumesSet);
        Assert.Equal("100", home.Tiles[0].Value);
        home.Tv.Volume = 1;
        home.Press(RemoteKey.Left, RemoteKey.Left);
        Assert.Equal(new[] { 100, 100, 0, 0 }, home.Tv.VolumesSet);
        Assert.Equal("0", home.Tiles[0].Value);
        Assert.Contains("\nactivity=volume_changed\n", home.Tv.State);
    }

    /// <summary>When the TV does not report the volume, adjusting points to the remote's volume buttons.</summary>
    [Fact]
    public void UnreportedVolumePointsToTheRemote()
    {
        home.Tv.Volume = null;
        OpenSettings();
        Assert.Equal(QuickSettingsState.Unavailable, home.Tiles[0].Value);
        home.Press(RemoteKey.Select, RemoteKey.Right);
        Assert.Equal("Use the remote's volume buttons.", home.View.Notice);
        Assert.Empty(home.Tv.VolumesSet);
    }

    /// <summary>Brightness moves one step within the TV's range and each value is checked.</summary>
    [Fact]
    public void BrightnessAdjusts()
    {
        home.Tv.Items["brightness"] = "49";
        OpenSettings(RemoteKey.Right, RemoteKey.Select, RemoteKey.Right, RemoteKey.Right);
        Assert.Equal(Adjustment.Brightness, home.Controller.Navigator.Adjusting);
        Assert.Equal("50", home.Tiles[1].Value);
        home.Press(RemoteKey.Left);
        Assert.Equal(new[] { ("brightness", "50"), ("brightness", "50"), ("brightness", "49") }, home.Tv.Written);
        Assert.Contains("\nactivity=setting:brightness:49:True\n", home.Tv.State);
        Assert.Equal("", home.View.Notice);
    }

    /// <summary>When the input on screen does not allow brightness changes, the tile says so and nothing is adjusted.</summary>
    [Fact]
    public void LockedBrightnessIsUnavailable()
    {
        home.Tv.Locked.Add("brightness");
        OpenSettings(RemoteKey.Right, RemoteKey.Select);
        Assert.Equal("Brightness is unavailable for the current input.", home.View.Notice);
        Assert.Equal(Adjustment.None, home.Controller.Navigator.Adjusting);
        Assert.Empty(home.Tv.Written);
    }

    /// <summary>Select on Game Mode steps Auto → On → Off → Auto, checking each value.</summary>
    [Fact]
    public void GameModeCycles()
    {
        home.Tv.Items[gameModeAuto] = "2";
        OpenSettings(RemoteKey.Right, RemoteKey.Right);
        Assert.Equal("Game Mode", home.Focused);
        home.Press(RemoteKey.Select, RemoteKey.Select, RemoteKey.Select);
        Assert.Equal(new[] { (gameModeAuto, "1"), (gameModeAuto, "0"), (gameModeAuto, "2") }, home.Tv.Written);
        Assert.Contains("\nactivity=setting:game-mode-setting-game-mode-auto:2:True\n", home.Tv.State);
        Assert.Contains("\ngame-mode=2\n", home.Tv.State);
        Assert.Equal("", home.View.Notice);
    }

    /// <summary>A game mode value the TV refuses, or accepts but does not keep, is reported.</summary>
    [Fact]
    public void GameModeNotTakenIsReported()
    {
        home.Tv.Items[gameModeAuto] = "2";
        home.Tv.IgnoreWritesTo = gameModeAuto;
        OpenSettings(RemoteKey.Right, RemoteKey.Right, RemoteKey.Select);
        Assert.Equal("The TV could not apply this setting. Try Advanced settings.", home.View.Notice);
        Assert.Contains("\nactivity=setting:game-mode-setting-game-mode-auto:1:False\n", home.Tv.State);
        home.Tv.IgnoreWritesTo = null;
        home.Tv.RefuseWrites = true;
        home.Press(RemoteKey.Select);
        Assert.Equal("The TV could not apply this setting. Try Advanced settings.", home.View.Notice);
        Assert.Equal("2", home.Tv.Items[gameModeAuto]);
    }

    /// <summary>On a TV with only the older on/off game mode item, Select toggles it.</summary>
    [Fact]
    public void OlderGameModeToggles()
    {
        home.Tv.Items["game-mode"] = "1";
        OpenSettings(RemoteKey.Right, RemoteKey.Right, RemoteKey.Select, RemoteKey.Select);
        Assert.Equal(new[] { ("game-mode", "0"), ("game-mode", "1") }, home.Tv.Written);
    }

    /// <summary>Select on Sleep Timer steps through its five choices and wraps to off.</summary>
    [Fact]
    public void SleepTimerCycles()
    {
        home.Tv.Items["sleeptimer"] = "3";
        OpenSettings(RemoteKey.Right, RemoteKey.Right, RemoteKey.Right, RemoteKey.Select, RemoteKey.Select);
        Assert.Equal(new[] { ("sleeptimer", "4"), ("sleeptimer", "0") }, home.Tv.Written);
        Assert.Equal("0", home.Tiles[3].Value);
    }

    /// <summary>The Theme tile saves the other theme and redraws; a failed save keeps the theme for now and says so.</summary>
    [Fact]
    public void ThemeTileSavesTheTheme()
    {
        OpenSettings(RemoteKey.Down, RemoteKey.Select);
        Assert.Equal("light", home.Tv.Theme);
        Assert.False(home.Screen.Dark);
        Assert.Equal("Theme", home.Focused);
        home.Tv.ThemeFails = true;
        home.Press(RemoteKey.Select);
        Assert.True(home.Screen.Dark);
        Assert.Equal("light", home.Tv.Theme);
        Assert.Equal("Theme changed for now; it could not be saved.", home.View.Notice);
        Assert.Contains("\ntheme_error=disk full\n", home.Tv.State);
    }

    /// <summary>A saved light theme is used from the start.</summary>
    [Fact]
    public void SavedThemeIsUsedFromTheStart()
    {
        home.Tv.Theme = "light";
        home.Open();
        Assert.False(home.Screen.Dark);
    }

    /// <summary>Controllers, Network and Advanced open Samsung's settings at their menu item.</summary>
    [Fact]
    public void SamsungSettingsTilesOpenTheirMenus()
    {
        OpenSettings(RemoteKey.Down, RemoteKey.Right, RemoteKey.Select, RemoteKey.Right, RemoteKey.Select, RemoteKey.Right, RemoteKey.Select);
        Assert.Equal(new[]
        {
            LaunchRequest.SamsungSettings("add-bluetooth-keyboard"),
            LaunchRequest.SamsungSettings("networksettings"),
            LaunchRequest.SamsungSettings(null),
        }, home.Tv.Launched);
        Assert.Contains("\nactivity=launch:org.tizen.menu:0\n", home.Tv.State);
    }

    /// <summary>A Samsung screen that does not open, or answers with an error, is reported.</summary>
    [Fact]
    public void FailedSamsungScreenIsReported()
    {
        home.Tv.LaunchResult = new LaunchResult(true, -3);
        OpenSettings(RemoteKey.Down, RemoteKey.Left, RemoteKey.Select);
        Assert.Equal("Could not open this screen. Try Advanced settings.", home.View.Notice);
        Assert.Contains("\nactivity=launch:org.tizen.menu:-3\n", home.Tv.State);
        home.Tv.LaunchResult = new LaunchResult(false, 0);
        home.Press(RemoteKey.Select);
        Assert.Equal("Could not open the app.", home.View.Notice);
    }
}
