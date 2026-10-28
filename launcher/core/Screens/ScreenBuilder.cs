using System.Collections.Generic;
using System.Linq;
using QuietHome.Core.Actions;
using QuietHome.Core.Catalog;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Settings;

namespace QuietHome.Core.Screens;

/// <summary>
/// Turns <see cref="HomeController"/>'s state into the <see cref="HomeScreen"/> to show:
/// which cards each page has, which one is focused and every text on screen. It decides
/// what is shown, not how: positions, sizes and colors belong to the TV view.
/// </summary>
public static class ScreenBuilder
{
    /// <summary>Returns the screen for <paramref name="state"/>.</summary>
    public static HomeScreen Build(ScreenState state)
    {
        var content = state.Page switch
        {
            Page.Home => HomePage(state),
            Page.Apps => AppsPage(state),
            _ => (PageModel)SettingsPage(state),
        };
        var toolbar = Toolbar(state, content.Cards.Count);
        var cards = content.Cards.Concat<CardModel>(toolbar).ToList();
        var focused = state.Selected >= 0 && state.Selected < cards.Count ? cards[state.Selected] : null;
        var chrome = new ChromeModel(
            Title(state.Page),
            state.Page,
            state.Zone == Zone.Tabs,
            state.Clock,
            state.Zone != Zone.Toolbar,
            state.Zone == Zone.Toolbar ? focused?.Name ?? "" : "",
            Detail(state, focused),
            Hint(state),
            toolbar);
        return new HomeScreen(state.Dark, chrome, content);
    }

    /// <summary>Returns the big title of <paramref name="page"/>.</summary>
    static string Title(Page page) => page switch
    {
        Page.Home => "Home",
        Page.Apps => "Your Apps",
        _ => "Quick Settings",
    };

    /// <summary>Returns true when slot <paramref name="index"/> has the focus: focus is off the tabs and on that slot.</summary>
    static bool IsFocused(ScreenState state, int index) => state.Zone != Zone.Tabs && state.Selected == index;

    /// <summary>Returns the Home page: one card per connected input, then the dock's apps and "Your Apps".</summary>
    static HomePageModel HomePage(ScreenState state)
    {
        var inputs = new List<InputCardModel>();
        for (int i = 0; i < state.Inputs.Count; i++)
        {
            var source = state.Inputs[i];
            var text = InputCard.For(source);
            bool expanded = state.Selected == i && (state.Zone == Zone.FirstRow || state.Zone == Zone.Tabs);
            inputs.Add(new InputCardModel(i, text.Name, text.Description, source.IconPath, source.SourceType)
            {
                Focused = IsFocused(state, i),
                Expanded = expanded,
            });
        }
        var apps = AppCatalog.DockApps(state.Library.ToList());
        var dock = new List<DockCardModel>();
        for (int i = 0; i <= apps.Count; i++)
        {
            var app = i < apps.Count ? apps[i] : null;
            dock.Add(new DockCardModel(i, app, app?.Name ?? "Your Apps") { Focused = IsFocused(state, inputs.Count + i) });
        }
        return new HomePageModel(inputs, dock);
    }

    /// <summary>Returns the open page of the Apps grid with the app count and, for a catalog over one page, the page number.</summary>
    static AppsPageModel AppsPage(ScreenState state)
    {
        var library = state.Library;
        int pages = CatalogGrid.PageCount(library.Count);
        var apps = new List<AppCardModel>();
        int start = state.LibraryPage * CatalogGrid.PageSize;
        for (int i = 0; i < CatalogGrid.VisibleCount(library.Count, state.LibraryPage); i++)
            apps.Add(new AppCardModel(i, library[start + i]) { Focused = IsFocused(state, i) });
        string count = library.Count(a => !a.Utility) + " apps";
        string? pageText = pages > 1 ? state.LibraryPage + 1 + " / " + pages : null;
        return new AppsPageModel(apps, library.Count == 0, count, pageText);
    }

    /// <summary>Returns the Settings page's tiles with their current values.</summary>
    static SettingsPageModel SettingsPage(ScreenState state)
    {
        var tiles = SettingsTile.All
            .Select((tile, i) => new SettingsTileModel(tile, tile.ValueText(state.Settings, state.Dark)) { Focused = IsFocused(state, i) })
            .ToList();
        return new SettingsPageModel(tiles);
    }

    /// <summary>
    /// Returns the toolbar: "Edit inputs" on the Home page only (it opens Samsung's source
    /// list, where an input's name and type are edited), then Apps, Inputs and Settings.
    /// Their slots follow the page's <paramref name="firstSlot"/> cards.
    /// </summary>
    static List<ToolbarButtonModel> Toolbar(ScreenState state, int firstSlot)
    {
        var buttons = new List<ToolbarButtonModel>();
        if (state.Page == Page.Home)
            buttons.Add(new ToolbarButtonModel(-1, "Edit inputs", "tv_settings", new HomeAction.OpenSourceEditor()));
        buttons.Add(new ToolbarButtonModel(0, "Apps", "apps", new HomeAction.ShowPage(Page.Apps)));
        buttons.Add(new ToolbarButtonModel(1, "Inputs", "hdmi", new HomeAction.ShowPage(Page.Home)));
        buttons.Add(new ToolbarButtonModel(2, "Settings", "settings", new HomeAction.ShowPage(Page.Settings)));
        return buttons.Select((b, i) => b with { Focused = IsFocused(state, firstSlot + i) }).ToList();
    }

    /// <summary>
    /// Returns the line at the bottom left. While adjusting it explains the keys; on the
    /// Settings page it names the page's purpose; on the Apps grid it names the focused app
    /// and its description; on the dock it names the focused app; elsewhere it is empty.
    /// </summary>
    static string Detail(ScreenState state, CardModel? focused)
    {
        if (state.Adjusting != Adjustment.None)
            return "Left / Right to adjust " + state.Adjusting.Name() + " · Select or Back to finish";
        if (state.Page == Page.Settings)
            return "Your everyday controls";
        if (focused == null)
            return "";
        if (state.Page == Page.Apps && state.Zone == Zone.FirstRow && focused.Row >= 0)
            return focused.Name + " · " + focused.Description;
        return state.Zone == Zone.SecondRow ? focused.Name : "";
    }

    /// <summary>Returns the line at the bottom right: on the Apps page how to reach the other pages, elsewhere "Back to input".</summary>
    static string Hint(ScreenState state)
    {
        if (state.Page != Page.Apps)
            return "Back to input";
        int pages = CatalogGrid.PageCount(state.Library.Count);
        if (pages <= 1)
            return "Select to open · Back to Home";
        return state.LibraryPage + 1 < pages ? "Down for more apps · Back to Home" : "Up for previous apps · Back to Home";
    }
}
