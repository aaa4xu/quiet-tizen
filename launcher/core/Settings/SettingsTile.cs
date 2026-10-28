using System.Collections.Generic;
using QuietHome.Core.Actions;
using QuietHome.Core.Navigation;

namespace QuietHome.Core.Settings;

/// <summary>
/// One tile of the Settings page: its text, icon, place in the 4 × 2 grid and what Select
/// does. <see cref="All"/> is the page's full table; the TV host draws the tiles from it in
/// that order and asks <see cref="ValueText"/> for the big line on each tile.
/// </summary>
public sealed class SettingsTile
{
    /// <summary>Stable name of the tile, for example "volume" or "advanced".</summary>
    public string Id { get; }

    /// <summary>Title at the top of the tile.</summary>
    public string Title { get; }

    /// <summary>Small hint at the bottom of the tile, also shown while the tile has focus.</summary>
    public string Hint { get; }

    /// <summary>Icon name; the artwork file is <c>quick-{Icon}.svg</c>.</summary>
    public string Icon { get; }

    /// <summary>Column in the grid, 0..3 from the left.</summary>
    public int Column { get; }

    /// <summary>Row in the grid: 0 on top, 1 below.</summary>
    public int Row { get; }

    /// <summary>What Select on this tile does.</summary>
    public HomeAction Action { get; }

    /// <summary>Creates a tile; the page's tiles are listed in <see cref="All"/>.</summary>
    SettingsTile(string id, string title, string hint, string icon, int column, int row, HomeAction action)
    {
        Id = id;
        Title = title;
        Hint = hint;
        Icon = icon;
        Column = column;
        Row = row;
        Action = action;
    }

    /// <summary>The Settings page's tiles, top row first, each row from left to right.</summary>
    public static IReadOnlyList<SettingsTile> All { get; } = new[]
    {
        new SettingsTile("volume", "Volume", "Select · Left / Right to adjust", "volume", 0, 0, new HomeAction.BeginAdjust(Adjustment.Volume)),
        new SettingsTile("brightness", "Brightness", "Select · Left / Right to adjust", "brightness", 1, 0, new HomeAction.BeginAdjust(Adjustment.Brightness)),
        new SettingsTile("game-mode", "Game Mode", "Select to change the mode", "game", 2, 0, new HomeAction.SwitchGameMode()),
        new SettingsTile("sleeptimer", "Sleep Timer", "Select to change the timer", "sleep", 3, 0, new HomeAction.CycleSetting("sleeptimer")),
        new SettingsTile("theme", "Theme", "Select to switch dark or light", "theme", 0, 1, new HomeAction.ToggleTheme()),
        new SettingsTile("controllers", "Controllers", "DualSense · hold PS + Create", "controller", 1, 1, new HomeAction.OpenSamsungSettings("add-bluetooth-keyboard")),
        new SettingsTile("network", "Network", "Wi-Fi and connection setup", "network", 2, 1, new HomeAction.OpenSamsungSettings("networksettings")),
        new SettingsTile("advanced", "Advanced", "Open Samsung's full settings", "settings", 3, 1, new HomeAction.OpenSamsungSettings(null)),
    };

    /// <summary>
    /// Returns the big line on the tile: the current value of its setting, or a fixed label
    /// for tiles that open other screens. In demo mode <paramref name="settings"/> holds the
    /// demo values (<see cref="Platform.Demo.DemoQuickSettings"/>), so screenshots never
    /// depend on the TV's state.
    /// </summary>
    public string? ValueText(QuickSettingsState settings, bool dark) => Id switch
    {
        "volume" => settings.Volume < 0 ? QuickSettingsState.Unavailable : settings.Volume.ToString(),
        "brightness" => settings.Name("brightness"),
        "game-mode" => settings.Name(settings.GameModeId),
        "sleeptimer" => settings.Name("sleeptimer"),
        "theme" => dark ? "Dark" : "Light",
        "controllers" => "Pair controller",
        "network" => "Connections",
        _ => "More settings",
    };
}
