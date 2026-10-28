using QuietHome.Core.Actions;
using QuietHome.Core.Catalog;
using QuietHome.Core.Navigation;
using QuietHome.Core.Settings;

namespace QuietHome.Core.Screens;

/// <summary>
/// One focusable element on screen: a card, a tile or a toolbar button. The
/// <see cref="ScreenBuilder"/> creates them in slot order (the order <see cref="Navigator"/>
/// counts in), the TV view draws each kind in its own way, and the controller performs
/// <see cref="Action"/> when the owner presses Select on it.
/// </summary>
/// <param name="Row">Row in the page's slot rows: 0 and 1 for the rows of cards, -1 for the toolbar (see <see cref="SlotRows"/>).</param>
/// <param name="Name">Name shown for the element and written to <c>selected_name=</c> in the diagnostics file.</param>
/// <param name="Description">Second line shown for the element, or null.</param>
/// <param name="Action">What Select on it does.</param>
public abstract record CardModel(int Row, string Name, string? Description, HomeAction Action)
{
    /// <summary>True when this element has the focus.</summary>
    public bool Focused { get; init; }
}

/// <summary>An input card in the top row of the Home page; Select switches to the input.</summary>
/// <param name="Index">Position in the row, 0 on the left.</param>
/// <param name="Name">The input's name (see <see cref="Inputs.InputCard"/>).</param>
/// <param name="Text">The line under the name.</param>
/// <param name="IconPath">The TV's icon for the input, or null or blank for none.</param>
/// <param name="SourceType">The TV's number for the input.</param>
public sealed record InputCardModel(int Index, string Name, string Text, string? IconPath, int SourceType)
    : CardModel(0, Name, Text, new HomeAction.SwitchInput(SourceType));

/// <summary>
/// A card in the dock at the bottom of the Home page: one of the catalog's first apps, or
/// the last card, "Your Apps", which opens the Apps page.
/// </summary>
/// <param name="Index">Position in the dock, 0 on the left.</param>
/// <param name="App">The app, or null for the "Your Apps" card.</param>
/// <param name="Name">The app's name, or "Your Apps".</param>
public sealed record DockCardModel(int Index, LibraryApp? App, string Name)
    : CardModel(1, Name, Name, App != null ? new HomeAction.LaunchApp(App.Id) : new HomeAction.ShowPage(Page.Apps));

/// <summary>An app card in the grid of the Apps page; Select launches the app.</summary>
/// <param name="Index">Position on the current Apps page, row by row (see <see cref="CatalogGrid"/>).</param>
/// <param name="App">The catalog entry.</param>
public sealed record AppCardModel(int Index, LibraryApp App)
    : CardModel(Index / CatalogGrid.Columns, App.Name, App.Description, new HomeAction.LaunchApp(App.Id));

/// <summary>A tile of the Settings page with its current value.</summary>
/// <param name="Tile">The tile's fixed text, icon, place and action.</param>
/// <param name="Value">The big line on the tile (see <see cref="SettingsTile.ValueText"/>).</param>
public sealed record SettingsTileModel(SettingsTile Tile, string? Value)
    : CardModel(Tile.Row, Tile.Title, Tile.Hint, Tile.Action);

/// <summary>A round button in the toolbar at the top right of every page.</summary>
/// <param name="Position">Place in the toolbar: -1 for "Edit inputs" (Home page only), then 0, 1, 2 from left to right.</param>
/// <param name="Name">The button's name, shown next to it while it has focus.</param>
/// <param name="Icon">Icon name; the artwork file is <c>symbol-{Icon}.png</c>.</param>
/// <param name="Action">What Select on it does.</param>
public sealed record ToolbarButtonModel(int Position, string Name, string Icon, HomeAction Action)
    : CardModel(-1, Name, Name, Action);
