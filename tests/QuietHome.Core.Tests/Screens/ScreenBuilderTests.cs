using System.Collections.Generic;
using System.Linq;
using QuietHome.Core.Actions;
using QuietHome.Core.Catalog;
using QuietHome.Core.Inputs;
using QuietHome.Core.Navigation;
using QuietHome.Core.Screens;
using QuietHome.Core.Settings;
using Xunit;

namespace QuietHome.Core.Tests.Screens;

/// <summary>
/// Tests of <see cref="ScreenBuilder"/>: which cards each page has in which slot order, which
/// one is focused, and the texts around them.
/// </summary>
public class ScreenBuilderTests
{
    /// <summary>Returns <paramref name="count"/> catalog apps named "App 1"…, with a utility card at <paramref name="utilityAt"/> (or none for -1).</summary>
    internal static List<LibraryApp> Apps(int count, int utilityAt = -1) =>
        Enumerable.Range(1, count).Select(i => new LibraryApp
        {
            Name = "App " + i,
            Id = "app." + i,
            Description = "Text " + i,
            Utility = i - 1 == utilityAt,
        }).ToList();

    /// <summary>Returns two connected inputs: Live TV (0) and HDMI 1 named "Console" (14).</summary>
    internal static List<InputSource> TwoInputs() => new()
    {
        new() { ConnectionState = true, SourceType = 0 },
        new() { ConnectionState = true, SourceType = 14, EditName = "Console" },
    };

    /// <summary>Returns a state on <paramref name="page"/> with focus in <paramref name="zone"/> at slot <paramref name="selected"/>.</summary>
    static ScreenState State(Page page, Zone zone, int selected, IReadOnlyList<LibraryApp> library, int libraryPage = 0, Adjustment adjusting = Adjustment.None) =>
        new(page, zone, selected, libraryPage, adjusting, true, TwoInputs(), library, new QuickSettingsState(), "9:41 AM");

    /// <summary>The Home page lists the inputs, the dock's first five real apps and "Your Apps", then the toolbar with "Edit inputs" first.</summary>
    [Fact]
    public void HomeSlotOrder()
    {
        var screen = ScreenBuilder.Build(State(Page.Home, Zone.FirstRow, 1, Apps(7, utilityAt: 2)));
        var cards = screen.Cards;
        Assert.Equal(new[] { "Live TV", "Console", "App 1", "App 2", "App 4", "App 5", "App 6", "Your Apps", "Edit inputs", "Apps", "Inputs", "Settings" }, cards.Select(c => c.Name));
        Assert.Equal(new[] { 0, 0, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1 }, cards.Select(c => c.Row));
        Assert.Equal(new HomeAction.SwitchInput(14), cards[1].Action);
        Assert.Equal(new HomeAction.ShowPage(Page.Apps), cards[7].Action);
        Assert.Equal(new[] { 1 }, cards.Select((c, i) => (c, i)).Where(x => x.c.Focused).Select(x => x.i));
        Assert.Equal("Home", screen.Chrome.Title);
        Assert.Equal("Back to input", screen.Chrome.Hint);
        Assert.Equal("", screen.Chrome.Detail);
        Assert.True(screen.Chrome.ClockVisible);
        Assert.False(screen.Chrome.TabsFocused);
    }

    /// <summary>On the dock the detail line names the app; on the toolbar the button's name replaces the clock.</summary>
    [Fact]
    public void DockAndToolbarTexts()
    {
        var dock = ScreenBuilder.Build(State(Page.Home, Zone.SecondRow, 3, Apps(3)));
        Assert.Equal("App 2", dock.Chrome.Detail);
        var toolbar = ScreenBuilder.Build(State(Page.Home, Zone.Toolbar, 7, Apps(3)));
        Assert.Equal("Apps", toolbar.Chrome.ToolbarHint);
        Assert.False(toolbar.Chrome.ClockVisible);
        Assert.Equal("", toolbar.Chrome.Detail);
    }

    /// <summary>On the tabs nothing in the page is focused and the current tab is highlighted.</summary>
    [Fact]
    public void TabsFocusNoCard()
    {
        var screen = ScreenBuilder.Build(State(Page.Settings, Zone.Tabs, 0, Apps(3)));
        Assert.True(screen.Chrome.TabsFocused);
        Assert.DoesNotContain(screen.Cards, c => c.Focused);
    }

    /// <summary>The Apps page shows one page of the grid, counts only real apps and tells how to reach the previous page.</summary>
    [Fact]
    public void AppsSecondPage()
    {
        var screen = ScreenBuilder.Build(State(Page.Apps, Zone.FirstRow, 2, Apps(20, utilityAt: 19), libraryPage: 1));
        var page = Assert.IsType<AppsPageModel>(screen.Content);
        Assert.Equal(new[] { "App 16", "App 17", "App 18", "App 19", "App 20" }, page.Apps.Select(a => a.Name));
        Assert.Equal("19 apps", page.CountText);
        Assert.Equal("2 / 2", page.PagesText);
        Assert.False(page.LoadFailed);
        Assert.Equal("Up for previous apps · Back to Home", screen.Chrome.Hint);
        Assert.Equal("App 18 · Text 18", screen.Chrome.Detail);
        Assert.Equal("Your Apps", screen.Chrome.Title);
    }

    /// <summary>The hint on the Apps page depends on the number of pages and the open one.</summary>
    [Fact]
    public void AppsHints()
    {
        Assert.Equal("Select to open · Back to Home", ScreenBuilder.Build(State(Page.Apps, Zone.FirstRow, 0, Apps(3))).Chrome.Hint);
        Assert.Equal("Down for more apps · Back to Home", ScreenBuilder.Build(State(Page.Apps, Zone.FirstRow, 0, Apps(16))).Chrome.Hint);
        var single = Assert.IsType<AppsPageModel>(ScreenBuilder.Build(State(Page.Apps, Zone.FirstRow, 0, Apps(3))).Content);
        Assert.Null(single.PagesText);
    }

    /// <summary>An empty catalog makes the Apps page report the failed load.</summary>
    [Fact]
    public void EmptyCatalog()
    {
        var page = Assert.IsType<AppsPageModel>(ScreenBuilder.Build(State(Page.Apps, Zone.Tabs, 0, new List<LibraryApp>())).Content);
        Assert.True(page.LoadFailed);
        Assert.Empty(page.Apps);
    }

    /// <summary>The Settings page has its eight tiles with values, a fixed detail line, and the adjusting hint while adjusting.</summary>
    [Fact]
    public void SettingsTiles()
    {
        var screen = ScreenBuilder.Build(State(Page.Settings, Zone.FirstRow, 0, Apps(3)));
        var page = Assert.IsType<SettingsPageModel>(screen.Content);
        Assert.Equal(SettingsTile.All.Select(t => t.Title), page.Tiles.Select(t => t.Name));
        Assert.Equal("Dark", page.Tiles[4].Value);
        Assert.Equal("Your everyday controls", screen.Chrome.Detail);
        Assert.Equal(new[] { "Apps", "Inputs", "Settings" }, screen.Chrome.Toolbar.Select(b => b.Name));
        var adjusting = ScreenBuilder.Build(State(Page.Settings, Zone.FirstRow, 0, Apps(3), adjusting: Adjustment.Volume));
        Assert.Equal("Left / Right to adjust volume · Select or Back to finish", adjusting.Chrome.Detail);
    }
}
